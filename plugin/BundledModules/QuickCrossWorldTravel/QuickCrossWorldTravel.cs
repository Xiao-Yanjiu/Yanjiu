using OmniToolbox.UI;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Interface.Utility.Raii;
using Lumina.Excel.Sheets;
using OmniToolbox.Common.Module.Abstractions;
using OmniToolbox.Common.Module.Enums;
using OmniToolbox.Common.Module.Models;
using OmniToolbox.Host;
using OmenTools;
using TreeTools.Lifecycle;
namespace Yanjiu.Modules;

[ModuleApiVersion(1)]
public sealed partial class QuickCrossWorldTravel : OmniModuleBase, Yanjiu.NativeUI.INativeModuleUI
{
    public sealed class Settings { public bool ShowDtr = true; }
    private Settings config = new();
    private IDtrBarEntry? entry;
    private FeatureLifetime? lifetime;
    private bool visible;
    private volatile bool busy;
    private volatile string status = string.Empty;
    private uint selectedWorld;
    private uint pendingTravel;
    private enum TravelStage { Idle, WaitingLogout, WaitingTitle, SettlingTitle, Request, BetweenLegs, OpenCharacters, SelectWorld, SelectCharacter, WaitingLogin }
    private TravelStage stage;
    private long deadline;
    private (int Current, int Target, ulong Content, bool ReturnHome, string Name) snapshot;
    private Task<Exception>? travelRequest;
    private readonly Queue<(int Current, int Target, bool Back)> travelLegs = new();
    private long nextSubmission;
    private static long Now => Environment.TickCount64;
    private void Finish(string message)
    {
        status = message; stage = TravelStage.Idle; busy = false;
        pendingTravel = 0; snapshot = default; travelRequest = null; travelLegs.Clear();
    }
    private bool TitleReady()
    {
        var title = DService.Instance().GameGUI.GetAddonByName("_TitleMenu", 1);
        return !DService.Instance().ClientState.IsLoggedIn && !title.IsNull && title.IsVisible;
    }
    private readonly List<(uint Id, string Name, string Dc)> worlds = [];
    public override ModuleInfo Info { get; } = new() { Category = ModuleCategory.Interface };
    public override ModuleLocalization Localization { get; } = ModuleLocalization.Create(new Dictionary<string,string> {
        ["Title"] = "快捷跨界传送", ["QuickCrossWorldTravelTitle"] = "快捷跨界传送",
        ["Description"] = "按大区选择服务器，通过 DCTravelerX 跨界传送；提供可被工具栏收纳的 DTR 入口。",
        ["QuickCrossWorldTravelDescription"] = "按大区选择服务器，通过 DCTravelerX 跨界传送；提供可被工具栏收纳的 DTR 入口。" });
    public override bool HasSettings => true;
    protected override void OnStart(FeatureLifetime activation)
    {
        lifetime = activation;
        nativeWindow=new NativeTravelWindow(this);
        activation.OnStopping(()=>{nativeWindow?.Dispose();nativeWindow=null;});
        LoadWorlds();
        entry = DService.Instance().DTRBar.Get("Yanjiu.QuickCrossWorldTravel");
        entry.Text = "跨界传送"; entry.Tooltip = "点击打开快捷跨界传送（DCTravelerX）";
        entry.OnClick = _ => visible = !visible; entry.Shown = config.ShowDtr;
        activation.OnStopping(() => {  entry?.Remove(); entry = null; visible = false; lifetime = null; });

        activation.OnStopping(() => { DalamudServices.Framework.Update -= UpdateTravel; pendingTravel = 0; Finish("传送准备已取消。"); });
        DalamudServices.Framework.Update += UpdateTravel;
    }
    public override bool TryHandleCommand(string arguments)
    {
        if (!IsEnabled) return false;
        if (arguments.Trim() is "" or "open") { visible = true; return true; }
        if (arguments.Trim() == "close") { visible = false; return true; }
        return false;
    }
    public override bool DrawSettings()
    {
        ImGui.TextWrapped("需要安装并配置 DCTravelerX。DTR 条目名称：Yanjiu.QuickCrossWorldTravel，可在多功能工具栏中收纳。");
        var changed = ImGui.Checkbox("显示服务器信息栏入口", ref config.ShowDtr);
        if (entry != null) entry.Shown = config.ShowDtr;
        using (ImRaii.Disabled(!IsEnabled)) if (ImGui.Button("打开传送面板")) visible = true;
        ImGui.TextDisabled("版本：1.3.0 · 标题界面等待流程");
        ImGui.TextDisabled("命令：/yanjiu QuickCrossWorldTravel open");
        return changed;
    }
    private void LoadWorlds()
    {
        try {
            var loaded = OmenTools.Info.Lumina.Sheets.CNWorlds.Values
                .Select(w => (w.RowId, w.Name.ToString(), w.DataCenter.Value.Name.ToString())).ToList();
            worlds.Clear(); worlds.AddRange(loaded);
            status = worlds.Count == 0 ? "国服服务器表返回空列表。" : $"已读取 {worlds.Count} 个国服服务器。";
        } catch (Exception ex) { status = "服务器表读取失败：" + ex.GetBaseException().Message; }
    }
    private static bool IsReady()
    {
        try { return DalamudServices.PluginInterface.GetIpcSubscriber<bool>("DCTravelerX.IsValid").InvokeFunc(); }
        catch { return false; }
    }
    private void UpdateTravel(Dalamud.Plugin.Services.IFramework framework)
    {
        if (lifetime == null || lifetime.IsStopping) return;
        if(rebuildNativeWindow){
            rebuildNativeWindow=false;var previous=nativeWindow;nativeWindow=null;previous?.Dispose();nativeWindow=new NativeTravelWindow(this);
        }
        if(entry!=null)entry.Shown=config.ShowDtr;
        if(visible){if(nativeWindow is {IsOpen:false})nativeWindow.Open();}
        else if(nativeWindow is {IsOpen:true})nativeWindow.Close();
        try
        {
            if (pendingTravel != 0 && !busy)
            {
                var target = pendingTravel; pendingTravel = 0;
                StartTravel(target);
                return;
            }
            if (stage == TravelStage.Idle) return;
            if (stage == TravelStage.Request)
            {
                if (travelRequest is { IsCompleted: true })
                {
                    var error = travelRequest.GetAwaiter().GetResult();
                    if (error != null)
                    {
                        if (error.Message.Contains("10339325") || error.Message.Contains("频繁")) nextSubmission = Now + 60000;
                        Finish("传送失败：" + error.Message);
                    }
                    else if (travelLegs.Count > 0)
                    {
                        stage = TravelStage.BetweenLegs; nextSubmission = Now + 60000; deadline = Now + 90000; nextLoginAction = Now + 2000; interLegCloseSent = false;
                        status = "已返回原始大区，等待 60 秒后前往目标大区；期间保持暂停自动登录。";
                    }
                    else { stage = TravelStage.OpenCharacters; deadline = Now + 30000; nextLoginAction = Now + 2000; status = "旅行成功，正在登录原角色…"; }
                }
                else if (Now >= deadline)
                    status = "DCTravelerX 尚未结束，请在其界面检查进度；为避免重复请求，暂不允许再次传送。";
                return;
            }
            if (stage is TravelStage.OpenCharacters or TravelStage.SelectWorld or TravelStage.SelectCharacter or TravelStage.WaitingLogin)
            {
                ContinueLogin();
                return;
            }
            if (Now >= deadline) { Finish("等待退出或标题界面超时，传送已取消。请重新选择目标。"); return; }
            switch (stage)
            {
                case TravelStage.BetweenLegs:
                    if (DService.Instance().ClientState.IsLoggedIn)
                    {
                        Finish("返回原服后检测到其他功能已登录角色，第二段已停止；请关闭其他自动登录后重新选择目标。");
                        return;
                    }
                    if (Now < nextLoginAction) return;
                    if (TitleReady())
                    {
                        stage = TravelStage.SettlingTitle;
                        titleStableSince = Now;
                        break;
                    }
                    var characterList = DService.Instance().GameGUI.GetAddonByName("_CharaSelectListMenu", 1);
                    if (!interLegCloseSent && !characterList.IsNull && characterList.IsVisible)
                    {
                        OmenTools.Interop.Game.AgentEvent.AgentLobbyEvent.CloseCharacterSelect();
                        interLegCloseSent = true;
                        status = "已返回原服，正在回到标题界面，等待提交间隔后继续第二段旅行。";
                    }
                    break;
                case TravelStage.WaitingLogout:
                    ConfirmRequestedLogout();
                    if (!DService.Instance().ClientState.IsLoggedIn)
                    {
                        stage = TravelStage.WaitingTitle; deadline = Now + 30000;
                        status = "等待标题界面就绪…";
                    }
                    break;
                case TravelStage.WaitingTitle:
                    if (DService.Instance().ClientState.IsLoggedIn) { Finish("检测到重新登录，已取消传送。"); return; }
                    if (TitleReady()) { stage = TravelStage.SettlingTitle; deadline = Math.Max(Now + 32000, nextSubmission + 30000); titleStableSince = Now; }
                    break;
                case TravelStage.SettlingTitle:
                    if (DService.Instance().ClientState.IsLoggedIn) { Finish("检测到重新登录，已取消传送。"); return; }
                    if (!TitleReady()) { stage = TravelStage.WaitingTitle; break; }
                    if (Now - titleStableSince < 2000 || Now < nextSubmission) return;
                    if (!IsReady()) { Finish("DCTravelerX 已不可用，取消传送。"); return; }
                    // Only managed identity values survive logout. Never retain player pointers.
                    stage = TravelStage.Request; deadline = Now + 180000;
                    status = "标题界面已就绪，正在交由 DCTravelerX 处理。";
                    var leg = travelLegs.Dequeue();
                    nextSubmission = Now + 60000;
                    travelRequest = DalamudServices.PluginInterface.GetIpcSubscriber<int,int,ulong,bool,string,Task<Exception>>("DCTravelerX.Travel")
                        .InvokeFunc(leg.Current, leg.Target, snapshot.Content, leg.Back, snapshot.Name);
                    if (travelRequest == null) Finish("DCTravelerX 未返回有效请求。");
                    break;
            }
        }
        catch (Exception ex) { Finish("传送失败：" + ex.GetBaseException().Message); }
    }
    private bool interLegCloseSent;
    private long nextLoginAction;
    private unsafe void ContinueLogin()
    {
        if (DService.Instance().ClientState.IsLoggedIn)
        {
            var player = DService.Instance().ObjectTable.LocalPlayer;
            if (player == null) return;
            Finish(player.CurrentWorld.RowId == snapshot.Target && DalamudServices.PlayerState.ContentId == snapshot.Content
                ? "旅行成功，已登录原角色。" : "已登录，但角色或服务器与旅行目标不符，请检查。");
            return;
        }
        if (Now >= deadline) { Finish("旅行已成功，但自动登录等待超时，请手动登录；不会重新发起旅行。"); return; }
        if (Now < nextLoginAction) return;
        nextLoginAction = Now + 1000;
        var lobby = FFXIVClientStructs.FFXIV.Client.UI.Agent.AgentLobby.Instance();
        if (lobby == null) return;
        if (stage == TravelStage.OpenCharacters)
        {
            var existingList = DService.Instance().GameGUI.GetAddonByName("_CharaSelectListMenu", 1);
            if (existingList.IsNull || !existingList.IsVisible)
            {
                if (!TitleReady()) return;
                OmenTools.Interop.Game.AgentEvent.AgentLobbyEvent.OpenCharacterSelect();
            }
            stage = TravelStage.SelectWorld; deadline = Now + 30000;
        }
        else if (stage == TravelStage.SelectWorld)
        {
            var list = DService.Instance().GameGUI.GetAddonByName("_CharaSelectListMenu", 1);
            if (list.IsNull || !list.IsVisible) return;
            if (lobby->WorldId != snapshot.Target)
            {
                OmenTools.Interop.Game.AgentEvent.AgentLobbyEvent.SelectWorldByID((uint)snapshot.Target);
                return;
            }
            stage = TravelStage.SelectCharacter; deadline = Now + 30000;
        }
        else if (stage == TravelStage.SelectCharacter)
        {
            if (lobby->WorldId != snapshot.Target) { Finish("服务器选择发生变化，已停止自动登录。"); return; }
            var content = snapshot.Content;
            if (OmenTools.Interop.Game.AgentEvent.AgentLobbyEvent.SelectCharacter(entry => entry.ContentId == content))
            {
                stage = TravelStage.WaitingLogin; deadline = Now + 180000;
            }
        }
    }
    private bool logoutConfirmed;
    private unsafe void ConfirmRequestedLogout()
    {
        if (logoutConfirmed || !DService.Instance().ClientState.IsLoggedIn) return;
        var ptr = DService.Instance().GameGUI.GetAddonByName("SelectYesno", 1);
        if (ptr.IsNull || !ptr.IsVisible) return;
        var addon = (FFXIVClientStructs.FFXIV.Component.GUI.AtkUnitBase*)ptr.Address;
        var node = addon->GetTextNodeById(2);
        if (node == null) return;
        var text = node->NodeText.ToString().Replace("\n", "");
        // Confirm only the logout dialog requested by this active travel operation.
        if (!text.Contains("退出") || !text.Contains("标题")) return;
        logoutConfirmed = OmenTools.Interop.Game.AddonEvent.AddonSelectYesnoEvent.ClickYes(text);
    }
    private long titleStableSince;
    private unsafe void StartTravel(uint target)
    {
        var player = DService.Instance().ObjectTable.LocalPlayer;
        if (player == null || busy || target == player.CurrentWorld.RowId || !worlds.Any(w => w.Id == target)) return;
        if (Now < nextSubmission) { status = "提交间隔不足，请等待一分钟后再试。"; return; }
        if (!IsReady() || DalamudServices.PlayerState.ContentId == 0) { status = "角色或 DCTravelerX 尚未就绪。"; return; }
        snapshot = ((int)player.CurrentWorld.RowId, (int)target, DalamudServices.PlayerState.ContentId,
            target == player.HomeWorld.RowId && player.CurrentWorld.RowId != player.HomeWorld.RowId, player.Name.ToString());
        travelLegs.Clear();
        var currentDc = worlds.FirstOrDefault(w => w.Id == player.CurrentWorld.RowId).Dc;
        var homeDc = worlds.FirstOrDefault(w => w.Id == player.HomeWorld.RowId).Dc;
        var targetDc = worlds.FirstOrDefault(w => w.Id == target).Dc;
        foreach (var leg in TravelRoute.Build((int)player.CurrentWorld.RowId, (int)player.HomeWorld.RowId, (int)target, currentDc, homeDc, targetDc))
            travelLegs.Enqueue(leg);
        // Same hand-off used by Omni: suppress the title-triggered login before logout.
        foreach (var channel in new[] { "Yanjiu.DR.Modules.AutoLogin.MarkNextAutoLoginHandled", "DailyRoutines.Modules.AutoLogin.MarkNextAutoLoginHandled" })
        {
            try { DalamudServices.PluginInterface.GetIpcSubscriber<object>(channel).InvokeAction(); }
            catch (Dalamud.Plugin.Ipc.Exceptions.IpcNotReadyError) { }
        }
        logoutConfirmed = false;
        busy = true; stage = TravelStage.WaitingLogout; deadline = Now + 30000;
        status = "正在退出登录；到达标题界面后将自动传送。";
        var ui = FFXIVClientStructs.FFXIV.Client.UI.UIModule.Instance();
        if (ui == null) { Finish("游戏界面尚未就绪。"); return; }
        using var command = new FFXIVClientStructs.FFXIV.Client.System.String.Utf8String();
        command.SetString("/logout");
        ui->ProcessChatBoxEntry(&command);
    }
}

