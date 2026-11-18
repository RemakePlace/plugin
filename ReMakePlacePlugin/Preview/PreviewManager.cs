using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Game.ClientState;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.LayoutEngine.Group;
using FFXIVClientStructs.Interop;
using FFXIVClientStructs.STD;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using ReMakePlacePlugin.Objects;
using ReMakePlacePlugin.Util;
using HousingFurniture = FFXIVClientStructs.FFXIV.Client.Game.HousingFurniture;

namespace ReMakePlacePlugin.Preview;

public class PreviewManager : IAsyncDisposable
{
    public readonly Dictionary<uint, SharedGroup> Furniture = [];
    private CancellationTokenSource _cts = new();
    
    public PreviewManager()
    {
        Svc.ClientState.ZoneInit += OnZoneInit;
    }

    private void OnZoneInit(ZoneInitEventArgs obj)
    {
        throw new NotImplementedException();
    }
    
    /// <summary>
    /// Toggles the current furniture's visibility while retaining player interaction.
    /// </summary>
    /// <param name="enabled">Whether the furniture should be visible</param>
    public static unsafe void ToggleRealFurniture(bool enabled)
    {
        HousingManager* man = HousingManager.Instance();
        if (man == null) return;

        StdVector<Pointer<HousingFurniture>> furnVector = man->GetFurnitureManager()->FurnitureVector;
        Span<Pointer<GameObject>> objArray = man->GetFurnitureManager()->ObjectManager.ObjectArray.Objects;
        foreach (Pointer<HousingFurniture> ptr in furnVector)
        {
            if (ptr.IsNull) continue;
            
            int index = ptr.Value->Index;
            var obj = (HousingObject*)objArray[index].Value;
            if (obj == null) continue;

            SharedGroupLayoutInstance* group = obj->SharedGroupLayoutInstance;
            if (group == null) continue;
            
            group->SetActive(enabled);
            group->SetColliderActive(true); // cool that this works :D
        }
    }

    /// <summary>
    /// Spawns the furniture for the given path.
    /// </summary>
    /// <param name="path">Path of furniture</param>
    /// <param name="position">Position to spawn furniture</param>
    /// <param name="rotation">Rotation of the furniture</param>
    /// <param name="stain">RowId for the stain to use</param>
    /// <param name="materialItem">The Item rowId for the material used (Wallpaper, paintings, etc.)</param>
    public void PreviewItem(string path, Vector3 position, float rotation, byte stain, uint materialItem)
    {
        if (materialItem != 0)
        {
            string materialPath = GetMaterialPath(materialItem);
            if (materialPath != string.Empty)
            {
                Add(new SharedGroup(path)
                {
                    MaterialPath = materialPath,
                    Position = position,
                    Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, rotation),
                    Scale = Vector3.One
                });

                return;
            }
        }
        
        if (stain != 0 && Svc.Data.GetExcelSheet<Stain>().TryGetRow(stain, out var row))
        {
            Add(new SharedGroup(path)
            {
                Color = Utils.StainToVector4(row.Color),
                Position = position,
                Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, rotation),
                Scale = Vector3.One
            });
        }
        else
        {
            Add(new SharedGroup(path)
            {
                Position = position,
                Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, rotation),
                Scale = Vector3.One
            });
        }
    }

    private string GetMaterialPath(uint materialItem)
    {
        if (!Svc.Data.GetExcelSheet<Item>().TryGetRow(materialItem, out var materialRow)) return string.Empty;
        
        // 73 == Interior Wall
        if (materialRow.ItemUICategory.RowId == 73 && materialRow.AdditionalData.TryGetValue<HousingInterior>(out var housingInterior))
        {
            return $"bgcommon/hou/dyna/mat/wl/{housingInterior.Unknown1:0000}/material/rom_wl_2{housingInterior.Unknown1:0000}a.mtrl";
        }
            
        // 95 == Painting
        if (materialRow.ItemUICategory.RowId == 95)
        {
            return $"bgcommon/hou/indoor/pic/ta/{materialRow.AdditionalData.RowId:0000}/material/pic_ta_2{materialRow.AdditionalData.RowId:0000}a.mtrl";
        }

        return string.Empty;
    }

    /// <summary>
    /// Preview a queue of interior housing items
    /// </summary>
    /// <param name="queue"></param>
    public void PreviewInteriorItems(Queue<HousingItem> queue)
    {
        if (queue.Count == 0) return;
        while (queue.TryDequeue(out var item))
        {
            if (HousingData.Instance.TryGetFurnitureByItemId(item.ItemKey, out var furniture))
            {
                ushort model = furniture.ModelKey;
                string itemPath = $"bgcommon/hou/indoor/general/{model:0000}/asset/fun_b0_m{model:0000}.sgb";
            
                var position = item.GetLocation();
                float rotation = item.Rotate;
                byte stain = item.Stain;
                uint materialItem = item.MaterialItemKey;
                
                PreviewItem(itemPath, position, rotation, stain, materialItem);
            }
            else
            {
                Svc.Log.Error($"Cannot identify item: {item.Name}");
            }
        }
    }
    
    /// <summary>
    /// Preview a queue of exterior items. This rotates the items for the plot the player is in.
    /// </summary>
    /// <param name="queue"></param>
    public void PreviewExteriorItems(Queue<HousingItem> queue)
    {
        if (queue.Count == 0) return;
        while (queue.TryDequeue(out var item))
        {
            if (HousingData.Instance.TryGetYardObjectByItemId(item.ItemKey, out var furniture))
            {
                ushort model = furniture.ModelKey;
                string itemPath = $"bgcommon/hou/outdoor/general/{model:0000}/asset/gar_b0_m{model:0000}.sgb";
                
                var rotateVector = Quaternion.CreateFromAxisAngle(Vector3.UnitY, -ReMakePlacePlugin.PlotLocation.rotation);
                var position = Vector3.Transform(item.GetLocation(), rotateVector) + ReMakePlacePlugin.PlotLocation.ToVector();
                float rotation = item.Rotate - ReMakePlacePlugin.PlotLocation.rotation;
                byte stain = item.Stain;
                PreviewItem(itemPath, position, rotation, stain, item.MaterialItemKey);
            }
            else
            {
                Svc.Log.Error($"Cannot identify item: {item.Name}");
            }
        }
    }

    public SharedGroup this[uint id] => Furniture[id];

    public void Add(SharedGroup sharedGroup) => Add(sharedGroup.Id, sharedGroup);
    public void Add(uint id, SharedGroup sharedGroup) => Furniture.Add(id, sharedGroup);
    
    public bool Remove(SharedGroup sharedGroup) => Remove(sharedGroup.Id, out _);
    public bool Remove(uint id, out SharedGroup? sharedGroup) => Furniture.Remove(id, out sharedGroup);

    public async Task Clear(bool disposing = false)
    {
        await _cts.CancelAsync();
        _cts.Dispose();
        
        await Svc.Framework.Run(() =>
        {
            foreach (var furn in Furniture.Values)
            {
                try
                {
                    furn.Dispose();
                }
                catch (Exception e)
                {
                    Svc.Log.Error(e, "Error while disposing Shared Group");
                }
            } 
        });
        
        Furniture.Clear();
        
        if (!disposing) _cts = new CancellationTokenSource();
    }

    public async ValueTask DisposeAsync()
    {
        Svc.ClientState.ZoneInit -= OnZoneInit;
        ToggleRealFurniture(true);
        await Clear(true);
    }
}