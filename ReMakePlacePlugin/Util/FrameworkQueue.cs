using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;

namespace ReMakePlacePlugin.Util;

public class FrameworkQueue : IDisposable
{
    private static readonly ConcurrentQueue<(Action Action, TaskCompletionSource Tcs)> Queue = new();

    public FrameworkQueue()
    {
        Svc.Framework.Update += OnUpdate;
    }

    public static Task Enqueue(Action action)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Queue.Enqueue((action, tcs));
        return tcs.Task;
    }
    
    private static void OnUpdate(IFramework framework)
    {
        if (!Queue.TryDequeue(out (Action Action, TaskCompletionSource Tcs) item)) return;
        
        try
        {
            item.Action();
            item.Tcs.SetResult();
        }
        catch (Exception e)
        {
            item.Tcs.SetException(e);
        }
    }

    public void Dispose()
    {
        Svc.Framework.Update -= OnUpdate;

        while (Queue.TryDequeue(out var item))
        {
            item.Tcs.SetCanceled();
        }
    }
}