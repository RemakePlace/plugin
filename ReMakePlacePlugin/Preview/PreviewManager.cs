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
    public bool IsPreviewing { get; private set; }
    
    private CancellationTokenSource _cts = new();
    private ReMakePlacePlugin Plugin { get; init; }

    public PreviewManager(ReMakePlacePlugin plugin)
    {
        Svc.ClientState.ZoneInit += OnZoneInit;
        Plugin = plugin;
    }

    private void OnZoneInit(ZoneInitEventArgs obj)
    {
        IsPreviewing = false;
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

        GameObjectManager* gameObjectManager = GameObjectManager.Instance();
        if (gameObjectManager is null) return;

        // Toggle event npcs
        foreach (Pointer<GameObject> obj in gameObjectManager->Objects.IndexSorted)
        {
            if (obj.IsNull) continue;
            if (obj.Value->GetObjectKind() != ObjectKind.EventNpc) continue;
            obj.Value->SetVisibility((byte)(enabled ? 0 : 1));
        }
        
        ref var arr = ref furnMan->ObjectManager.ObjectArray;
        int max = Math.Min(arr.Objects.Length, arr.ObjectCount);
        foreach (Pointer<HousingFurniture> ptr in furnMan->FurnitureVector)
        {
            if (ptr.IsNull) continue;
            
            int index = ptr.Value->Index;
            if ((uint)index >= (uint)max) continue;
            
            var obj = (HousingObject*)arr.Objects[index].Value;
            if (obj == null) continue;

            if (!Memory.Instance.IsHousingMode() && HousingData.Instance.TryGetFurniture(obj->HousingObjectId.Id, out var furn))
            {
                // Skip Mannequin and Vendors
                if (furn.UsageType is 15 or 3) continue;
            }

            SharedGroupLayoutInstance* group = obj->SharedGroupLayoutInstance;
            if (group == null) continue;
            
            group->SetActive(enabled);
            group->SetColliderActive(true); // cool that this works :D
        }
    }
    
    /// <summary>
    /// Preview all the items in the current housing layout.
    /// </summary>
    public void PreviewLayout()
    {
        SaveLayoutManager.ImportLayout(Plugin.Config.SaveLocation);
        if (!CheckModeForPreview()) return;
        
        if (Memory.Instance.GetCurrentTerritory() == Memory.HousingArea.Indoors)
        {
            var queue = new Queue<HousingItem>(Plugin.InteriorItemList);
            ToggleRealFurniture(false);
            Svc.Framework.RunOnTick(() => PreviewInteriorItems(queue));
        }
        else if (Memory.Instance.GetCurrentTerritory() == Memory.HousingArea.Outdoors)
        {
            Plugin.GetPlotLocation(); // update current plot
            var queue = new Queue<HousingItem>(Plugin.ExteriorItemList);
            Svc.Framework.RunOnTick(() => PreviewExteriorItems(queue));
        }

        IsPreviewing = true;
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
    
    public unsafe bool CheckModeForPreview()
    {
        HousingModule* module = Memory.Instance.HousingModule;
        if (module->IsIndoors())
            return true;

        // Returns false if not in a plot, true if in a plot.
        return !module->IsOutdoors() || (byte)(module->currentTerritory->Plot + 1) != 0;
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
        IsPreviewing = false;
        
        if (!disposing) _cts = new CancellationTokenSource();
    }

    public void Dispose()
    {
        Svc.ClientState.ZoneInit -= OnZoneInit;

        Svc.Framework.Run(() =>
        {
            ToggleRealFurniture(true);
            Clear(true);
        });
    }
}