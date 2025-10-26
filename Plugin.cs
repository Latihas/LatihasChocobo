using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using Dalamud.Game;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;

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

    private const uint WM_KEYUP = 0x101;
    private const uint WM_KEYDOWN = 0x100;
    private static IntPtr mwh;
    private static bool isRunning;
    private static readonly Random _random = new();
    internal static readonly Dictionary<uint, string> ObjectType = new() {
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


    private static readonly Dictionary<int, long> PressTime = new();
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
    [PluginService] private static IGameGui GameGui { get; set; } = null!;

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
        if (!ObjectType.ContainsKey(target.DataId)) return Direction.InValid;
        var playerPos = player.Position;
        var targetPos = target.Position;
        var rotation = player.Rotation;
        var distance = Vector3.Distance(playerPos, targetPos);
        var forwardDir = new Vector2(
            (float)Math.Sin(rotation),
            (float)Math.Cos(rotation)
        );
        var toTargetDir = new Vector2(
            targetPos.X - playerPos.X,
            targetPos.Z - playerPos.Z
        );
        var dotProduct = forwardDir.X * toTargetDir.X + forwardDir.Y * toTargetDir.Y;
        var crossProduct = forwardDir.X * toTargetDir.Y - forwardDir.Y * toTargetDir.X;

        var zDiff = targetPos.Y - playerPos.Y;
        if (zDiff < -2 || !(dotProduct > 0)) return Direction.InValid;
        var toTargetNormalized = toTargetDir.LengthSquared() > 0
            ? Vector2.Normalize(toTargetDir)
            : Vector2.Zero;
        var cosTheta = Vector2.Dot(forwardDir, toTargetNormalized);
        cosTheta = Math.Clamp(cosTheta, -1f, 1f);
        var angleDeg = (float)(Math.Acos(cosTheta) * 180 / Math.PI);
        if (distance < 8 && angleDeg < 30)
            return zDiff > 2 ? Direction.FrontUp : Direction.Front;
        if (distance < 15 && angleDeg < 20)
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
        Log.Warning($"WM_KEYDOWN: {code}");
    }

    private static unsafe bool CanUseItem() {
        AtkImageNode* FinalImageNode = null;
        try {
            var _ActionBar = (AtkUnitBase*)GameGui.GetAddonByName("_ActionBar").Address;
            var _ActionBarUldManager = _ActionBar->UldManager;
            for (var i = 0; i < _ActionBarUldManager.NodeListCount; i++) {
                var BaseComponentNode = _ActionBarUldManager.NodeList[i];
                if ((int)BaseComponentNode->Type != 1005) continue;
                var BaseComponentNodeUldManager = BaseComponentNode->GetComponent()->UldManager;
                for (uint j = 0; j < BaseComponentNodeUldManager.NodeListCount; j++) {
                    var TextNode = BaseComponentNodeUldManager.NodeList[j];
                    if (TextNode->Type != NodeType.Text) continue;
                    if (TextNode->GetAsAtkTextNode()->NodeText.ToString() != "1") continue;
                    for (var ix = 0; ix < BaseComponentNodeUldManager.NodeListCount; ix++) {
                        var DragDropComponentNode = BaseComponentNodeUldManager.NodeList[ix];
                        if ((int)DragDropComponentNode->Type != 1002) continue;
                        var DragDropComponentNodeUldManager = DragDropComponentNode->GetComponent()->UldManager;
                        for (var jx = 0; jx < DragDropComponentNodeUldManager.NodeListCount; jx++) {
                            var IconComponentNode = DragDropComponentNodeUldManager.NodeList[jx];
                            if ((int)IconComponentNode->Type != 1001) continue;
                            var IconComponentNodeUldManager = IconComponentNode->GetComponent()->UldManager;
                            for (var k = 0; k < IconComponentNodeUldManager.NodeListCount; k++) {
                                var TmpFinalImageNode = IconComponentNodeUldManager.NodeList[k];
                                if (TmpFinalImageNode->Type != NodeType.Image) continue;
                                FinalImageNode = TmpFinalImageNode->GetAsAtkImageNode();
                                // ImGui.Text($"{FinalImageNode->Type.ToString()},{FinalImageNode->MultiplyRed},{FinalImageNode->MultiplyGreen},{FinalImageNode->MultiplyBlue}");
                                break;
                            }
                        }
                        if (FinalImageNode != null) break;
                    }
                    if (FinalImageNode != null) break;
                }
                if (FinalImageNode != null) break;
            }
            if (FinalImageNode == null) return false;
            var texture = FinalImageNode->PartsList->Parts[FinalImageNode->PartId].UldAsset;
            if (texture->AtkTexture.TextureType != TextureType.Resource) return false;
            return texture->AtkTexture.Resource->TexFileResourceHandle->ResourceHandle.FileName.ToString() != "ui/icon/070000/070101_hr1.tex";
        }
        catch (Exception e) {
            Log.Warning(e.ToString());
        }
        return false;
    }

    private unsafe void Press(IFramework framework) {
        if (!isRunning) return;
        var speedHigh = false;
        try {
            var _RaceChocoboParameter = (AtkUnitBase*)GameGui.GetAddonByName("_RaceChocoboParameter").Address;
            var _RaceChocoboParameterUldManager = _RaceChocoboParameter->UldManager;
            var _RaceChocoboParameterSpeedNode = _RaceChocoboParameterUldManager.NodeList[_RaceChocoboParameterUldManager.NodeListCount - 1];
            speedHigh = _RaceChocoboParameterSpeedNode->IsVisible();
        }
        catch (Exception e) {
            Log.Warning(e.ToString());
        }
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
            SendMessage(mwh, WM_KEYUP, code, 0);
            Log.Warning($"WM_KEYUP: {code}");
        }
        if (!speedHigh) TryPress(87);
        var maxDist = 114514f;
        IGameObject? target = null;
        foreach (var obj in Objects) {
            if (obj.ObjectKind != ObjectKind.EventObj) continue;
            var newdis = Vector3.Distance(ClientState.LocalPlayer!.Position, obj.Position);
            if (newdis < maxDist) {
                target = obj;
                maxDist = newdis;
            }
        }
        if (target == null) return;
        switch (GetTargetSide(target)) {
            case Direction.Left:
                TryPress(65);
                break;
            case Direction.Right:
                TryPress(68);
                break;
            case Direction.FrontUp:
                TryPress(32);
                break;
            case Direction.Front:
            case Direction.InValid:
            default:
                break;
        }
        if (Configuration.AutoUseItem && CanUseItem()) TryPress(49);
    }

    private static bool InRace() => ClientState.TerritoryType is 389 or 390 or 391;

    private static void TerritoryChanged(ushort territory) {
        if (InRace()) isRunning = true;
        else if (isRunning) {
            isRunning = false;
            foreach (var code in PressTime.Keys)
                SendMessage(mwh, WM_KEYUP, code, 0);
        }
    }

    [DllImport("user32.dll")]
    private static extern IntPtr FindWindowEx(IntPtr hWndParent, IntPtr hWndChildAfter, string lpszClass, string lpszWindow);

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

    private void OnCommand() {
        _mainWindow.Toggle();
    }
}