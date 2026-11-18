using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Game.ClientState;
using ECommons.DalamudServices;

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