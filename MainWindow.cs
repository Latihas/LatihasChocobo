using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;
using static LatihasChocobo.Plugin;

namespace LatihasChocobo;

[SuppressMessage("ReSharper", "InconsistentNaming")]
[SuppressMessage("ReSharper", "InvertIf")]
public class MainWindow() : Window("Chocobo=>CCB?") {
    private const ImGuiTableFlags ImGuiTableFlag = ImGuiTableFlags.Borders | ImGuiTableFlags.Resizable | ImGuiTableFlags.RowBg;

    private static readonly Vector4 green = new(0, 1, 0, 1),
        red = new(1, 0, 0, 1);

    private static void NewTab(string tabname, Action act) {
        if (ImGui.BeginTabItem(tabname)) {
            act();
            ImGui.EndTabItem();
        }
    }

    private static void NewTable(string[] header, List<string[]> data) {
        if (ImGui.BeginTable("Table", data[0].Length, ImGuiTableFlag)) {
            foreach (var item in header) ImGui.TableSetupColumn(item, ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableHeadersRow();
            foreach (var res in data) {
                ImGui.TableNextRow();
                for (var i = 0; i < res.Length; i++) {
                    ImGui.TableSetColumnIndex(i);
                    ImGui.Text(res[i]);
                }
            }
            ImGui.EndTable();
        }
    }

    public override unsafe void Draw() {
        if (ClientState.LocalPlayer is null) return;
        if (ImGui.BeginTabBar("tab")) {
            NewTab("赛鸟", () => {
                if (ImGui.Checkbox("启用", ref Configuration.Enabled)) Configuration.Save();
                ImGui.Separator();
                if (!Configuration.Enabled) return;
                if (ImGui.Checkbox("进入指定地点自动循环匹配(需要DR)", ref Configuration.AutoDuty)) {
                    Configuration.Save();
                    if (Configuration.AutoDuty) {
                        if (Configuration.AutoDutyTerritory.Split('|').Contains(ClientState.TerritoryType.ToString()))
                            ChatBox.SendMessage("/pdrduty r 随机赛道");
                    }
                }
                if (ImGui.InputText("循环区域(用竖线|分隔)", ref Configuration.AutoDutyTerritory)) Configuration.Save();
                if (ImGui.InputInt("循环间延迟(s)", ref Configuration.AutoDutyWait)) Configuration.Save();
                ImGui.Text($"当前区域: {ClientState.TerritoryType}, 经验: {RaceChocoboManager.Instance()->ExperienceCurrent}/{RaceChocoboManager.Instance()->ExperienceMax}");
                if (ImGui.InputInt("按键时长(ms)", ref Configuration.PressMs)) Configuration.Save();
                if (ImGui.InputFloat("超速也加速概率", ref Configuration.SpeedHighW, 1)) Configuration.Save();
                if (ImGui.Checkbox("自动使用道具", ref Configuration.AutoUseItem)) Configuration.Save();
                ImGui.Separator();
                List<string[]> data = [];
                foreach (var obj in GetEventObjects()) {
                    var name = "UNK";
                    if (BadObjectType.TryGetValue(obj.DataId, out var v1)) name = v1;
                    if (GoodObjectType.TryGetValue(obj.DataId, out var v2)) name = v2;
                    data.Add([
                        GetTargetSide(obj).ToString(), ((int)Vector3.Distance(ClientState.LocalPlayer.Position, obj.Position)).ToString(), obj.DataId.ToString(), name
                    ]);
                }
                NewTable(["状态", "距离", "DataId", "名称"], data);
            });
            NewTab("背包", () => {
                if (ImGui.InputInt("筛选星级(OR)", ref Configuration.CcbMaxStar)) Configuration.Save();
                if (ImGui.InputText("筛选颜色(竖线|分隔)", ref Configuration.CcbColor)) Configuration.Save();
                List<string[]> data = [];
                string? name = null;
                string? color = null;
                try {
                    var ItemDetail = (AtkUnitBase*)GameGui.GetAddonByName("ItemDetail").Address;
                    if (ItemDetail->IsVisible) {
                        foreach (var BaseComponentNodeA in AllAtkUnitBaseByType(ItemDetail, 1005)) {
                            var TextNodeA = AllAtkUnitBaseByType(BaseComponentNodeA.Node->GetComponent()->UldManager, (int)NodeType.Text);
                            foreach (var TextNode in TextNodeA) {
                                var str = TextNode.Node->GetAsAtkTextNode()->NodeText.ToString();
                                if (str.StartsWith("颜色：")) {
                                    color = str;
                                    break;
                                }
                            }
                            if (color != null) break;
                        }
                        foreach (var TextNode in AllAtkUnitBaseByType(ItemDetail, (int)NodeType.Text)) {
                            var str = TextNode.Node->GetAsAtkTextNode()->NodeText.ToString();
                            if (str.Contains("性陆行鸟配种登记书")) {
                                name = str.Substring(str.IndexOf("性陆行鸟配种登记书", StringComparison.Ordinal) - 3, 12);
                                break;
                            }
                        }
                        foreach (var node in AllAtkUnitBaseByType(ItemDetail, (int)NodeType.Res)) {
                            var BaseComponentNodeA = AllAtkUnitBaseByType(node.Node, 1004);
                            if (BaseComponentNodeA.Count != 5) continue;
                            foreach (var BaseComponentNode in BaseComponentNodeA) {
                                var ResNode = FirstAtkUnitBaseByType(BaseComponentNode.Node, (int)NodeType.Res);
                                var ndata = AllAtkUnitBaseByType(ResNode, (int)NodeType.Text);
                                ndata.Reverse();
                                var d = new string[3];
                                foreach (var str in ndata.Select(TextNode => TextNode.Node->GetAsAtkTextNode()->NodeText.ToString())) {
                                    if (str.StartsWith("\u0002H\u0004")) {
                                        var sp = str.Split('\u3000');
                                        d[1] = sp[0].Substring(32, 4);
                                        d[2] = sp[1].Substring(32, 4);
                                    }
                                    else d[0] = str;
                                }
                                data.Add(d);
                            }
                        }
                    }
                }
                catch (Exception) {
                    // ignored
                }
                var isValid = data.Count != 0 && name != null && color != null;
                var maxcount = 0;
                foreach (var obj in data) {
                    foreach (var p in obj) {
                        if (p.IsNullOrEmpty()) {
                            isValid = false;
                            break;
                        }
                        if (p == "\u2605\u2605\u2605\u2605") maxcount++;
                    }
                    if (!isValid) break;
                }
                if (isValid) {
                    var preserve = Configuration.CcbMaxStar <= maxcount || Configuration.CcbColor.Contains(color![3..]);
                    NewTable(["属性", "雄星级", "雌星级"], data);
                    ImGui.Text(name);
                    ImGui.Text($"满星数量: {maxcount}, {color}");
                    ImGui.PushStyleColor(ImGuiCol.Text, preserve ? green : red);
                    ImGui.Text($"建议{(preserve ? "保留" : "舍弃")}");
                    ImGui.PopStyleColor();
                }
                else ImGui.Text("鼠标移动到配种登记书上以查看");
            });
            ImGui.EndTabBar();
        }
    }
}