using System;
using Dalamud.Configuration;

namespace LatihasChocobo;

[Serializable]
public class MConfiguration : IPluginConfiguration {
    public string CcbColor = "素雪白", AutoDutyTerritory = "";
    public bool Enabled, AutoDuty, AutoUseItem = true, DisableSpeedUpWhenLowHP = true, EnableSpeedUpWhenHighHP = true, MaxLevelMode;
    public int PressMs = 30, AutoDutyWait = 5, CcbMaxStar = 5;
    public float SpeedHighW = 5f;
    public int Version { get; set; }
    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);
}