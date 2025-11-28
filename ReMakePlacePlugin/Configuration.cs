using Dalamud.Configuration;
using System;
using System.Collections.Generic;

namespace ReMakePlacePlugin
{
    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 0;

        public bool ShowTooltips = true;
        public bool DrawScreen = false;
        public float DrawDistance = 0;
        public List<int> HiddenScreenItemHistory = new List<int>();
        public List<int> GroupingList = new List<int>();

        public bool Basement = true;
        public bool GroundFloor = true;
        public bool UpperFloor = true;

        public int LoadInterval = 500;

        public string SaveLocation = null;

        public bool AutoConfirmDye = false;
        public bool SelectPreviousDye = false;

        public void Save()
        {
            DalamudApi.PluginInterface.SavePluginConfig(this);
        }

        public void ResetRecord()
        {
            HiddenScreenItemHistory.Clear();
            GroupingList.Clear();
            Save();
        }

    }
}
