using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using static LatihasChocobo.Plugin;

namespace LatihasChocobo;

[SuppressMessage("ReSharper", "InconsistentNaming")]
[SuppressMessage("ReSharper", "InvertIf")]
public class MainWindow() : Window("LatihasChocobo") {
    public override void Draw() {
        if (ClientState.LocalPlayer is null) return;
        if (ImGui.InputInt("按键时长(ms)", ref Configuration.PressMs)) Configuration.Save();
        if (ImGui.Checkbox("自动使用道具", ref Configuration.AutoUseItem)) Configuration.Save();
        ImGui.Separator();
        foreach (var obj in GetEventObjects()) {
            ImGui.Text($"{GetTargetSide(obj)},{(int)Vector3.Distance(ClientState.LocalPlayer.Position, obj.Position)},{obj.DataId},{ObjectType.GetValueOrDefault(obj.DataId, "UNK")}");
        }
        // if (ImGui.Button("Test")) {
        // }
    }
}