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
        if (ImGui.Checkbox("进入指定地点自动循环匹配", ref Configuration.AutoDuty)) {
            Configuration.Save();
            if (Configuration.AutoDuty) {
                if (ClientState.TerritoryType == Configuration.AutoDutyTerritory)
                    ChatBox.SendMessage("/pdrduty r 随机赛道");
            }
        }
        if (ImGui.InputInt("循环区域", ref Configuration.AutoDutyTerritory)) Configuration.Save();
        if (ImGui.InputInt("循环间延迟(s)", ref Configuration.AutoDutyWait)) Configuration.Save();
        ImGui.Text($"当前区域: {ClientState.TerritoryType}");
        if (ImGui.InputInt("按键时长(ms)", ref Configuration.PressMs)) Configuration.Save();
        if (ImGui.Checkbox("自动使用道具", ref Configuration.AutoUseItem)) Configuration.Save();
        ImGui.Separator();
        foreach (var obj in GetEventObjects()) {
            var name = "UNK";
            if (BadObjectType.TryGetValue(obj.DataId, out var v1)) name = v1;
            if (GoodObjectType.TryGetValue(obj.DataId, out var v2)) name = v2;
            ImGui.Text($"{GetTargetSide(obj)},{(int)Vector3.Distance(ClientState.LocalPlayer.Position, obj.Position)},{obj.DataId},{name}");
        }
    }
}