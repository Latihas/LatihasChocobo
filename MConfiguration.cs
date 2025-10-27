using System;
using Dalamud.Configuration;

namespace LatihasChocobo;

[Serializable]
public class MConfiguration : IPluginConfiguration {
    public string CcbColor = "", AutoDutyTerritory = "";
    public bool Enabled, AutoDuty, AutoUseItem = true;
    public int PressMs = 30, AutoDutyWait = 5, CcbMaxStar = 5;
    public float SpeedHighW = 1f;
    public int Version { get; set; }
    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);
}