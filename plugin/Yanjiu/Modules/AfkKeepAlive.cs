// SPDX-License-Identifier: GPL-3.0-or-later
using System.Runtime.InteropServices;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace Yanjiu.Modules;

/// <summary>A bounded, framework-thread input pulse. No native pointer survives a frame.</summary>
public sealed class AfkKeepAlive : ModuleBase
{
    public override ModuleDescriptor Descriptor=>new("AFK 防掉线","空闲时向当前游戏窗口发送短暂 Ctrl 按键，防止因无操作被踢出。检测到 AntiAfkKick 已运行时自动让行。","小烟酒","System");
    public override bool HasSettings=>true;
    private Settings settings=new();
    private readonly AfkPulse pulse=new();
    private long nextCheck;
    private string status="未启用";
    private nint pulseWindow;
    private bool settingsLoaded;
    public sealed class Settings { public int IdleSeconds {get;set;}=30; }
    private void LoadSettings(){if(settingsLoaded)return;settings=ReadConfiguration<Settings>()??new();settings.IdleSeconds=Math.Clamp(settings.IdleSeconds,30,300);settingsLoaded=true;}
    protected override void OnEnable(){LoadSettings();nextCheck=0;Services.Framework.Update+=Update;status="等待空闲";}
    protected override void OnDisable(){Services.Framework.Update-=Update;pulse.Release(SendKey);status="未启用";}
    public override bool DrawSettings(){
        LoadSettings();int seconds=settings.IdleSeconds;
        ImGui.SetNextItemWidth(180);
        if(ImGui.SliderInt("空闲阈值（秒）",ref seconds,30,300)){settings.IdleSeconds=seconds;WriteConfiguration(settings);}
        ImGui.TextWrapped(IsEnabled?status:"未启用，可先调整设置。");
        ImGui.TextDisabled("无需前置模块；与 AntiAfkKick、Omni 的 AFK 防掉线功能重复。");
        ImGui.TextWrapped("只处理无操作导致的掉线，不处理网络断开、维护或服务器故障。");
        return false;
    }
    private unsafe void Update(IFramework framework){
        try{
            var now=Environment.TickCount64;
            if(pulse.IsPressed){pulse.Advance(now,SendKey);return;}
            if(now<nextCheck)return;nextCheck=now+10_000;
            if(Services.PluginInterface.InstalledPlugins.Any(p=>p.IsLoaded&&p.InternalName.Equals("AntiAfkKick-Dalamud",StringComparison.OrdinalIgnoreCase))){status="AntiAfkKick 正在运行，Yanjiu 已暂停发送按键。";return;}
            if(Services.Objects.LocalPlayer==null){status="等待角色登录";return;}
            var ui=UIModule.Instance();if(ui==null)return;
            var timers=ui->GetInputTimerModule();if(timers==null)return;
            var idle=Math.Max(timers->AfkTimer,Math.Max(timers->ContentInputTimer,timers->InputTimer));
            if(!AfkPulse.ShouldPulse(idle,settings.IdleSeconds)){status="等待空闲";return;}
            // Do not release a real modifier key the player is currently holding.
            if((GetAsyncKeyState(0x11)&0x8000)!=0||(GetAsyncKeyState(0x10)&0x8000)!=0||(GetAsyncKeyState(0x12)&0x8000)!=0){status="等待修饰键松开";return;}
            pulseWindow=FindGameWindow();
            if(pulseWindow==0){status="未找到当前游戏窗口，稍后重试";return;}
            status=pulse.Start(now,SendKey)?"已发送防掉线按键":"游戏窗口未响应，稍后重试";
        }catch(Exception e){nextCheck=Environment.TickCount64+30_000;status="防掉线检查失败，30 秒后重试";Services.Log.Error(e,"Yanjiu AFK check failed");pulse.Release(SendKey);}
    }
    private bool SendKey(bool down){
        if(pulseWindow==0||!IsWindow(pulseWindow))return !down;
        GetWindowThreadProcessId(pulseWindow,out var pid);if(pid!=Environment.ProcessId)return !down;
        // Bound the call so a hung game window cannot block unload indefinitely.
        return SendMessageTimeout(pulseWindow,down?0x100u:0x101u,0xA2,0,2,100,out _)!=0;
    }
    private static nint FindGameWindow(){
        nint found=0;
        EnumWindows((hwnd,_)=>{GetWindowThreadProcessId(hwnd,out var pid);if(pid!=Environment.ProcessId)return true;var name=new System.Text.StringBuilder(64);GetClassName(hwnd,name,name.Capacity);if(name.ToString()!="FFXIVGAME")return true;found=hwnd;return false;},0);
        return found;
    }
    private delegate bool EnumWindow(nint hwnd,nint data);
    [DllImport("user32.dll")]private static extern bool EnumWindows(EnumWindow callback,nint data);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern int GetClassName(nint window,System.Text.StringBuilder name,int length);
    [DllImport("user32.dll")]private static extern uint GetWindowThreadProcessId(nint window,out uint processId);
    [DllImport("user32.dll")]private static extern bool IsWindow(nint window);
    [DllImport("user32.dll")]private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern nint SendMessageTimeout(nint window,uint message,nuint key,nint flags,uint options,uint timeout,out nuint result);
}

public sealed class AfkPulse
{
    public bool IsPressed {get;private set;}
    private long releaseAt;
    public static bool ShouldPulse(float idle,int threshold)=>float.IsFinite(idle)&&idle>=Math.Clamp(threshold,30,300);
    public bool Start(long now,Func<bool,bool> send){if(IsPressed)return false;if(!send(true))return false;IsPressed=true;releaseAt=now+200;return true;}
    public void Advance(long now,Func<bool,bool> send){if(IsPressed&&now>=releaseAt)Release(send);}
    public void Release(Func<bool,bool> send){if(!IsPressed)return;IsPressed=false;send(false);}
}
