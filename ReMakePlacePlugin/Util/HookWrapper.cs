using Dalamud.Hooking;
using ECommons.DalamudServices;
using System;

namespace ReMakePlacePlugin;

// based on https://github.com/Caraxi/SimpleTweaksPlugin/blob/main/Helper/HookWrapper.cs
public interface IHookWrapper : IDisposable
{
    public bool IsEnabled { get; }
    public bool IsDisposed { get; }
    public void Enable();
    public void Disable();
}

public class HookWrapper<T> : IHookWrapper where T : Delegate
{
    private bool disposed;

    private readonly Hook<T> wrappedHook;

    public HookWrapper(Hook<T> hook)
    {
        wrappedHook = hook;
    }

    public T Original => wrappedHook.Original;

    public void Enable()
    {
        if (disposed) return;
        wrappedHook?.Enable();
    }

    public void Disable()
    {
        if (disposed) return;
        wrappedHook?.Disable();
    }

    public void Dispose()
    {
        Svc.Log.Info("Disposing of {cdelegate}", typeof(T).Name);
        Disable();
        disposed = true;
        wrappedHook?.Dispose();
    }

    public IntPtr Address => wrappedHook.Address;

    public bool IsEnabled => wrappedHook.IsEnabled;
    public bool IsDisposed => wrappedHook.IsDisposed;
}