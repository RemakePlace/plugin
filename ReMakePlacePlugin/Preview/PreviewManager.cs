using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Game.ClientState;
using ECommons;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.LayoutEngine.Group;
using FFXIVClientStructs.Interop;
using Lumina.Excel.Sheets;
using ReMakePlacePlugin.Objects;
using HousingFurniture = FFXIVClientStructs.FFXIV.Client.Game.HousingFurniture;

namespace ReMakePlacePlugin.Preview;

public class PreviewManager : IDisposable
{
    public readonly Dictionary<uint, SharedGroup> Furniture = [];
    private CancellationTokenSource _cts = new();
    
    public PreviewManager()
    {
        Svc.ClientState.ZoneInit += OnZoneInit;
    }

    private void OnZoneInit(ZoneInitEventArgs obj)
    {
        ReMakePlacePlugin.CurrentlyPreviewing = false;
        Task.Run(() => Clear());
    }
    
    /// <summary>
    /// Toggles the current furniture's visibility while retaining player interaction.
    /// </summary>
    /// <param name="enabled">Whether the furniture should be visible</param>
    public static unsafe void ToggleRealFurniture(bool enabled)
    {
        HousingManager* man = HousingManager.Instance();
        if (man is null) return;

        HousingFurnitureManager* furnMan = man->GetFurnitureManager();
        if (furnMan is null) return;
        
        ref var arr = ref furnMan->ObjectManager.ObjectArray;
        int max = Math.Min(arr.Objects.Length, arr.ObjectCount);
        foreach (Pointer<HousingFurniture> ptr in furnMan->FurnitureVector)
        {
            if (ptr.IsNull) continue;
            
            int index = ptr.Value->Index;
            if ((uint)index >= (uint)max) continue;
            
            var obj = (HousingObject*)arr.Objects[index].Value;
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
            ushort materialKey = GetMaterialKey(materialItem);
            if (materialKey != 0)
            {
                Add(new SharedGroup(path, _cts.Token)
                {
                    MaterialKey = materialKey,
                    Position = position,
                    Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, rotation),
                    Scale = Vector3.One
                });

                return;
            }
        }
        
        if (stain != 0 && Svc.Data.GetExcelSheet<Stain>().TryGetRow(stain, out var row))
        {
            Add(new SharedGroup(path, _cts.Token)
            {
                Color = Utils.StainToVector4(row.Color),
                Position = position,
                Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, rotation),
                Scale = Vector3.One
            });

            return;
        }

        Add(new SharedGroup(path, _cts.Token)
        {
            Position = position,
            Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, rotation),
            Scale = Vector3.One
        });
    }

    private static ushort GetMaterialKey(uint materialItem)
    {
        return Svc.Data.GetExcelSheet<VaseFlower>().FirstOr0(p => p.Item.RowId == materialItem).Unknown0;
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

    public void Clear(bool disposing = false)
    {
        _cts.Cancel();
        _cts.Dispose();
        
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
        
        Furniture.Clear();
        
        if (!disposing) _cts = new CancellationTokenSource();
    }

    public void Dispose()
    {
        Svc.ClientState.ZoneInit -= OnZoneInit;
        ToggleRealFurniture(true);
        Clear(true);
    }
}