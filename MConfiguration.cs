using System;
using Dalamud.Configuration;

namespace LatihasChocobo;

[Serializable]
public class MConfiguration : IPluginConfiguration {
    public bool AutoUseItem = true;
    public int PressMs = 30;
    public int Version { get; set; }
    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);
}