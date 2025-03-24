using System;
using ReMakePlacePlugin.Gui;

namespace ReMakePlacePlugin
{
    public class PluginUi : IDisposable
    {
        private readonly ReMakePlacePlugin _plugin;
        public ConfigurationWindow ConfigWindow { get; }

        public PluginUi(ReMakePlacePlugin plugin)
        {
            ConfigWindow = new ConfigurationWindow(plugin);

            _plugin = plugin;
            DalamudApi.PluginInterface.UiBuilder.Draw += Draw;
            DalamudApi.PluginInterface.UiBuilder.OpenConfigUi += OnOpenConfigUi;
            DalamudApi.PluginInterface.UiBuilder.OpenMainUi += OnOpenConfigUi;
        }

        private void Draw()
        {
            ConfigWindow.Draw();
        }
        private void OnOpenConfigUi()
        {
            ConfigWindow.Visible = true;
            ConfigWindow.CanUpload = false;
            ConfigWindow.CanImport = false;
        }

        public void Dispose()
        {
            DalamudApi.PluginInterface.UiBuilder.Draw -= Draw;
            DalamudApi.PluginInterface.UiBuilder.OpenConfigUi -= OnOpenConfigUi;
            DalamudApi.PluginInterface.UiBuilder.OpenMainUi -= OnOpenConfigUi;
        }
    }
}