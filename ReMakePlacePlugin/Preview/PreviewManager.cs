using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Game.ClientState;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.LayoutEngine.Group;
using FFXIVClientStructs.Interop;
using FFXIVClientStructs.STD;

namespace ReMakePlacePlugin.Preview;

public class PreviewManager : IAsyncDisposable
{
    public readonly Dictionary<uint, SharedGroup> Furniture = [];
    private CancellationTokenSource cts = new();
    
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

    public SharedGroup this[uint id] => Furniture[id];
    public bool Remove(SharedGroup sharedGroup) => Remove(sharedGroup.Id, out _);
    public bool Remove(uint id, out SharedGroup? sharedGroup) => Furniture.Remove(id, out sharedGroup);

    public async Task Clear(bool disposing = false)
    {
        await cts.CancelAsync();
        cts.Dispose();

        foreach (var furn in Furniture.Values)
        {
            await Svc.Framework.Run(() =>
            {
                try
                {
                    furn.Dispose();
                }
                catch (Exception e)
                {
                    Svc.Log.Error(e, "Error while disposing Shared Group");
                }
            });
        }
        
        Furniture.Clear();
        
        if (!disposing) cts = new CancellationTokenSource();
    }

    public async ValueTask DisposeAsync()
    {
        Svc.ClientState.ZoneInit -= OnZoneInit;
        await Clear(true);
    }
}