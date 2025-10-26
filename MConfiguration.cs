using System;
using Dalamud.Configuration;

namespace LatihasChocobo;

[Serializable]
public class MConfiguration : IPluginConfiguration {
    public bool AutoUseItem = true, AutoDuty = false;
    public int PressMs = 30, AutoDutyTerritory = 144, AutoDutyWait = 5;
    public int Version { get; set; }
    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);
}