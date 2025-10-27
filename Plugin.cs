using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Dalamud.Game;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;
using static LatihasChocobo.Constant;

namespace LatihasChocobo;

[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Local")]
[SuppressMessage("ReSharper", "AutoPropertyCanBeMadeGetOnly.Local")]
[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
public sealed class Plugin : IDalamudPlugin {
    public enum Direction {
        Left,
        Right,
        Front,
        FrontUp,
        InValid
    }

    private static IntPtr mwh;
    private static bool isRunning;
    private static readonly Random _random = new();
    internal static readonly Dictionary<uint, string> GoodObjectType = new() {
        {
            2005024, "黄宝箱"
        }, {
            2005025, "蓝宝箱"
        }, {
            2005038, "蓝加速"
        }, {
            2005041, "绿体力"
        }
    };
    internal static readonly Dictionary<uint, string> BadObjectType = new() {
        {
            2005039, "紫减速"
        }, {
            2005040, "红眩晕"
        }
    };
    private static readonly Dictionary<int, long> PressTime = new();
    internal static bool speedHigh, canUseItem, L, H;
    internal static float HpPercent;
    internal static int RacePercent;
    private readonly MainWindow _mainWindow;
    // ReSharper disable once MemberCanBePrivate.Global
    public readonly WindowSystem WindowSystem = new("LatihasChocobo");

    public Plugin() {
        Configuration = PluginInterface.GetPluginConfig() as MConfiguration ?? new MConfiguration();
        _mainWindow = new MainWindow();
        WindowSystem.AddWindow(_mainWindow);
        var p = new CommandInfo(OnCommand) {
            HelpMessage = "打开主界面"
        };
        CommandManager.AddHandler("/lc", p);
        CommandManager.AddHandler("/latihaschocobo", p);
        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi += OnCommand;
        TryFindGameWindow(out mwh);
        Framework.Update += Press;
        ClientState.TerritoryChanged += TerritoryChanged;
        if (InRace()) isRunning = true;
    }

    private static int PRESS_TIME => Configuration.PressMs * 10000;

    internal static MConfiguration Configuration { get; private set; } = null!;
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] private static IPluginLog Log { get; set; } = null!;
    [PluginService] private static ICommandManager CommandManager { get; set; } = null!;
    [PluginService] private static IFramework Framework { get; set; } = null!;
    [PluginService] private static IObjectTable Objects { get; set; } = null!;
    [PluginService] internal static ISigScanner SigScanner { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IGameGui GameGui { get; private set; } = null!;

    public void Dispose() {
        WindowSystem.RemoveAllWindows();
        CommandManager.RemoveHandler("/lc");
        CommandManager.RemoveHandler("/latihaschocobo");
        ClientState.TerritoryChanged -= TerritoryChanged;
        Framework.Update -= Press;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    public static Direction GetTargetSide(IGameObject target) {
        var player = ClientState.LocalPlayer!;
        if (!BadObjectType.ContainsKey(target.DataId) && !GoodObjectType.ContainsKey(target.DataId)) return Direction.InValid;
        var playerPos = player.Position;
        var targetPos = target.Position;
        var rotation = player.Rotation;
        var distance = Vector3.Distance(playerPos, targetPos);
        var forwardDir = new Vector2((float)Math.Sin(rotation), (float)Math.Cos(rotation));
        var toTargetDir = new Vector2(targetPos.X - playerPos.X, targetPos.Z - playerPos.Z);
        var dotProduct = forwardDir.X * toTargetDir.X + forwardDir.Y * toTargetDir.Y;
        var crossProduct = forwardDir.X * toTargetDir.Y - forwardDir.Y * toTargetDir.X;
        var zDiff = targetPos.Y - playerPos.Y;
        if (zDiff < -4 || !(dotProduct > 0)) return Direction.InValid;
        var toTargetNormalized = toTargetDir.LengthSquared() > 0
            ? Vector2.Normalize(toTargetDir)
            : Vector2.Zero;
        var cosTheta = Vector2.Dot(forwardDir, toTargetNormalized);
        cosTheta = Math.Clamp(cosTheta, -1f, 1f);
        var angleDeg = (float)(Math.Acos(cosTheta) * 180 / Math.PI);
        if (distance < 8 && angleDeg < 20)
            return zDiff > 2 ? Direction.FrontUp : Direction.Front;
        if (distance < 15 && angleDeg < 15)
            return Direction.Front;
        return crossProduct > 0 ? Direction.Right : Direction.Left;
    }

    internal static IGameObject[] GetEventObjects() {
        if (ClientState.LocalPlayer is null) return [];
        return Objects.Where(obj =>
            Vector3.Distance(ClientState.LocalPlayer.Position, obj.Position) < 75
            && obj.ObjectKind == ObjectKind.EventObj
        ).ToArray();
    }

    private static void TryPress(int code, float percent = 1000) {
        if (!PressTime.ContainsKey(code)) PressTime[code] = DateTime.Now.Ticks;
        if (DateTime.Now.Ticks - PressTime[code] <= PRESS_TIME) return;
        PressTime[code] = DateTime.Now.Ticks;
        if (!(percent > 100) && !(_random.NextDouble() * 100 < percent)) return;
        SendMessage(mwh, WM_KEYDOWN, code, 0);
        Log.Info($"WM_KEYDOWN: {code}");
    }

    // internal static unsafe AtkResNode* FirstAtkUnitBaseByType(AtkUnitBase* root, int type) => FirstAtkUnitBaseByType(root->UldManager, type);
    internal static unsafe AtkResNode* FirstAtkUnitBaseByType(AtkResNode* root, int type) {
        var prevNode = root->ChildNode;
        while (prevNode != null) {
            if ((int)prevNode->Type == type) return prevNode;
            prevNode = prevNode->PrevSiblingNode;
        }
        throw new Exception($"Failed to find BaseComponentNode: {type}");
    }

    internal static unsafe AtkResNode* FirstAtkUnitBaseByType(AtkUldManager UldManager, int type) {
        for (var i = 0; i < UldManager.NodeListCount; i++) {
            var Node = UldManager.NodeList[i];
            if ((int)Node->Type == type) return Node;
        }
        throw new Exception($"Failed to find BaseComponentNode: {type}");
    }
    // internal static unsafe AtkResNode* FirstAtkUnitBaseByType(AtkUldManager UldManager, int type) {
    //     // for (var i = 0; i < UldManager.NodeListCount; i++) {
    //     //     var Node = UldManager.NodeList[i];
    //     //     if ((int)Node->Type == type) return Node;
    //     // }
    //     // throw new Exception($"Failed to find BaseComponentNode: {type}");
    //     
    // }

    // internal static unsafe List<AtkResNodeWrapper> AllAtkUnitBaseByType(AtkUnitBase* root, int type) => AllAtkUnitBaseByType(root->UldManager, type);

    internal static unsafe List<AtkResNodeWrapper> AllAtkUnitBaseByType(AtkResNode* root, int type) {
        List<AtkResNodeWrapper> result = [];
        var prevNode = root->ChildNode;
        while (prevNode != null) {
            if ((int)prevNode->Type == type) result.Add(new AtkResNodeWrapper(prevNode));
            prevNode = prevNode->PrevSiblingNode;
        }
        return result;
    }

    internal static unsafe List<AtkResNodeWrapper> AllAtkUnitBaseByType(AtkUnitBase* root, int type) =>
        AllAtkUnitBaseByType(root->UldManager, type);


    internal static unsafe List<AtkResNodeWrapper> AllAtkUnitBaseByType(AtkUldManager UldManager, int type) {
        List<AtkResNodeWrapper> result = [];
        for (var i = 0; i < UldManager.NodeListCount; i++) {
            var Node = UldManager.NodeList[i];
            if ((int)Node->Type == type) result.Add(new AtkResNodeWrapper(Node));
        }
        return result;
    }

    private static unsafe bool CanUseItem() {
        AtkImageNode* FinalImageNode = null;
        try {
            var _ActionBar = (AtkUnitBase*)GameGui.GetAddonByName("_ActionBar").Address;
            foreach (var BaseComponentNodew in AllAtkUnitBaseByType(_ActionBar, 1005)) {
                var BaseComponentNode = BaseComponentNodew.Node;
                var TextNode = FirstAtkUnitBaseByType(BaseComponentNode->GetComponent()->UldManager, (int)NodeType.Text);
                if (TextNode->GetAsAtkTextNode()->NodeText.ToString() != "1") continue;
                var DragDropComponentNode = FirstAtkUnitBaseByType(BaseComponentNode->GetComponent()->UldManager, 1002);
                var IconComponentNode = FirstAtkUnitBaseByType(DragDropComponentNode->GetComponent()->UldManager, 1001);
                var TmpFinalImageNode = FirstAtkUnitBaseByType(IconComponentNode->GetComponent()->UldManager, (int)NodeType.Image);
                FinalImageNode = TmpFinalImageNode->GetAsAtkImageNode();
                break;
            }
            if (FinalImageNode == null) return false;
            var texture = FinalImageNode->PartsList->Parts[FinalImageNode->PartId].UldAsset->AtkTexture;
            if (texture.TextureType != TextureType.Resource) return false;
            return texture.Resource->TexFileResourceHandle->ResourceHandle.FileName.ToString() != "ui/icon/070000/070101_hr1.tex";
        }
        catch (Exception e) {
            Log.Warning(e.ToString());
        }
        return false;
    }

    private static unsafe void Click(AtkComponentButton* target, AtkUnitBase* addon) {
        if (!target->IsEnabled || !target->AtkResNode->IsVisible()) return;
        var btnRes = target->AtkComponentBase.OwnerNode->AtkResNode;
        var evt = btnRes.AtkEventManager.Event;
        addon->ReceiveEvent(evt->State.EventType, (int)evt->Param, btnRes.AtkEventManager.Event);
        var resetEvt = btnRes.AtkEventManager.Event;
        resetEvt->State.StateFlags = AtkEventStateFlags.None;
        addon->ReceiveEvent(resetEvt->State.EventType, (int)resetEvt->Param, btnRes.AtkEventManager.Event);
    }

    private static unsafe void Press(IFramework framework) {
        if (!Configuration.Enabled || !isRunning) return;
        // End
        try {
            var ptr = GameGui.GetAddonByName("RaceChocoboResult");
            if (ptr != IntPtr.Zero) {
                var RaceChocoboResult = (AtkUnitBase*)ptr.Address;
                var ButtonComponentNode = FirstAtkUnitBaseByType(RaceChocoboResult->UldManager, 1001)->GetAsAtkComponentButton();
                Click(ButtonComponentNode, RaceChocoboResult);
            }
        }
        catch (Exception e) {
            Log.Warning(e.ToString());
        }
        // Race
        speedHigh = false;
        try {
            var _RaceChocoboParameter = (AtkUnitBase*)GameGui.GetAddonByName("_RaceChocoboParameter").Address;
            var _RaceChocoboParameterUldManager = _RaceChocoboParameter->UldManager;
            var _RaceChocoboParameterSpeedNode = _RaceChocoboParameterUldManager.NodeList[_RaceChocoboParameterUldManager.NodeListCount - 1]->GetAsAtkImageNode();
            var texture = _RaceChocoboParameterSpeedNode->PartsList->Parts[_RaceChocoboParameterSpeedNode->PartId].UldAsset;
            speedHigh = _RaceChocoboParameterSpeedNode->IsVisible() &&
                        texture->AtkTexture.TextureType == TextureType.Resource &&
                        texture->AtkTexture.Resource->TexFileResourceHandle->ResourceHandle.FileName.ToString() ==
                        "ui/icon/180000/chs/180043_hr1.tex";
            var CounterNode = FirstAtkUnitBaseByType(_RaceChocoboParameter->UldManager, (int)NodeType.Counter)->GetAsAtkCounterNode();
            HpPercent = float.Parse(CounterNode->NodeText.ToString()[..^1]);
        }
        catch (Exception e) {
            Log.Warning(e.ToString());
        }
        try {
            var found = false;
            var _ToDoList = (AtkUnitBase*)GameGui.GetAddonByName("_ToDoList").Address;
            foreach (var BaseComponentNode in AllAtkUnitBaseByType(_ToDoList, 1008)) {
                foreach (var NodeText in AllAtkUnitBaseByType(BaseComponentNode.Node->GetComponent()->UldManager, (int)NodeType.Text)) {
                    var str = NodeText.Node->GetAsAtkTextNode()->NodeText.ToString();
                    if (!str.StartsWith("进度：")) continue;
                    RacePercent = int.Parse(str[3..^1]);
                    found = true;
                    break;
                }
                if (found) break;
            }
        }
        catch (Exception e) {
            Log.Warning(e.ToString());
        }
        L = Configuration.DisableSpeedUpWhenLowHP && HpPercent < 100 - RacePercent;
        H = Configuration.EnableSpeedUpWhenHighHP && HpPercent > 100 - RacePercent;
        var notSpeedHigh = !speedHigh || _random.NextDouble() * 100 < Configuration.SpeedHighW && !L || H;
        foreach (var code in PressTime.Select(p => new {
                         p,
                         code = p.Key
                     })
                     .Select(t => new {
                         t,
                         time = t.p.Value
                     })
                     .Where(t => DateTime.Now.Ticks - t.time > PRESS_TIME)
                     .Select(t => t.t.code)) {
            if (notSpeedHigh && code == KC_W) continue;
            SendMessage(mwh, WM_KEYUP, code, 0);
            Log.Info($"WM_KEYUP: {code}");
        }
        if (notSpeedHigh) TryPress(KC_W);
        var maxDist = 114514f;
        IGameObject? target = null;
        foreach (var obj in Objects) {
            if (obj.ObjectKind != ObjectKind.EventObj) continue;
            var newdis = Vector3.Distance(ClientState.LocalPlayer!.Position, obj.Position);
            if (!(newdis < maxDist)) continue;
            target = obj;
            maxDist = newdis;
        }
        if (target == null) return;
        var isBad = BadObjectType.ContainsKey(target.DataId);
        switch (GetTargetSide(target)) {
            case Direction.Left:
                SendMessage(mwh, WM_KEYUP, isBad ? KC_A : KC_D, 0);
                TryPress(isBad ? KC_D : KC_A);
                break;
            case Direction.Right:
                SendMessage(mwh, WM_KEYUP, isBad ? KC_D : KC_A, 0);
                TryPress(isBad ? KC_A : KC_D);
                break;
            case Direction.FrontUp:
                TryPress(KC_SPACE);
                break;
            case Direction.Front:
                if (isBad) TryPress(KC_SPACE);
                break;
            case Direction.InValid:
            default:
                break;
        }
        canUseItem = CanUseItem();
        if (Configuration.AutoUseItem && canUseItem) TryPress(KC_1);
    }

    private static bool InRace() => ClientState.TerritoryType is 389 or 390 or 391;

    private static void TerritoryChanged(ushort territory) {
        if (InRace())
            Task.Run(async () => {
                await Task.Delay(Configuration.AutoDutyWait * 1000);
                isRunning = true;
            });
        else if (isRunning) {
            isRunning = false;
            foreach (var code in PressTime.Keys)
                SendMessage(mwh, WM_KEYUP, code, 0);
        }
        if (Configuration.AutoDuty && Configuration.AutoDutyTerritory.Split('|').Contains(ClientState.TerritoryType.ToString())) {
            Task.Run(async () => {
                await Task.Delay(Configuration.AutoDutyWait * 1000);
                if (Configuration.Enabled) await Framework.RunOnFrameworkThread(() => ChatBox.SendMessage(RequestDuty));
            });
        }
    }

    [DllImport("user32.dll")]
    private static extern IntPtr FindWindowEx(IntPtr hWndParent, IntPtr hWndChildAfter, string lpszClass, string? lpszWindow);

    [DllImport("user32.dll")]
    private static extern int GetWindowThreadProcessId(IntPtr hWnd, out int lpdwProcessId);

    private static void TryFindGameWindow(out IntPtr hwnd) {
        hwnd = IntPtr.Zero;
        while (true) {
            hwnd = FindWindowEx(IntPtr.Zero, hwnd, "FFXIVGAME", null);
            if (hwnd == IntPtr.Zero) break;
            GetWindowThreadProcessId(hwnd, out var pid);
            if (pid == Environment.ProcessId) break;
        }
    }

    private void OnCommand(string command, string args) => OnCommand();

    private void OnCommand() => _mainWindow.Toggle();

    internal class AtkResNodeWrapper {
        public unsafe readonly AtkResNode* Node;

        public unsafe AtkResNodeWrapper(AtkResNode* node) {
            Node = node;
        }
    }
}