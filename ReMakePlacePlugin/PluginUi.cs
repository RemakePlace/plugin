using Dalamud.Interface.Windowing;
using ECommons.DalamudServices;
using ReMakePlacePlugin.Gui;
using System;

namespace ReMakePlacePlugin;

public class PluginUi : IDisposable
{
    private readonly ReMakePlacePlugin _plugin;

    public readonly WindowSystem WindowSystem = new("ReMakePlace");
    public ConfigurationWindow ConfigWindow { get; }

    public PluginUi(ReMakePlacePlugin plugin)
    {
        _plugin = plugin;

        ConfigWindow = new ConfigurationWindow(plugin);

        WindowSystem.AddWindow(ConfigWindow);

        Svc.PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        Svc.PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
        Svc.PluginInterface.UiBuilder.OpenMainUi += ToggleConfigUi;
    }

    private void ToggleConfigUi() => ConfigWindow.Toggle();

    public void Dispose()
    {
        Svc.PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        Svc.PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        Svc.PluginInterface.UiBuilder.OpenMainUi -= ToggleConfigUi;

        WindowSystem.RemoveAllWindows();

        ConfigWindow.Dispose();
    }
}