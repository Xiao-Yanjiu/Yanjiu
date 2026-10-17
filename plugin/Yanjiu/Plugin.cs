// SPDX-License-Identifier: GPL-3.0-or-later
using Dalamud.Plugin;
using Dalamud.Game.Command;
using Dalamud.Plugin.Services;
using OmenTools;
using Yanjiu.Modules;
using Yanjiu.UI;
namespace Yanjiu;
public sealed class Plugin : IDalamudPlugin
{
    private readonly Configuration config;private readonly ModuleManager modules;private readonly MainWindow window;private bool disposed;private long nextCommandHelpRefresh;private readonly HashSet<string> ownedCommands=new(StringComparer.OrdinalIgnoreCase);
    public Plugin(IDalamudPluginInterface pi)
    {
        PluginFiles.Initialize(pi.AssemblyLocation.FullName);pi.Create<Services>();Services.ConfigDirectory=pi.GetPluginConfigDirectory();Directory.CreateDirectory(Services.ConfigDirectory);
        config=pi.GetPluginConfig() as Configuration??new Configuration();config.Modules??=new();config.Repositories??=new();config.ThemeColors??=new();config.RepositoryAutoRefresh??=new();config.HiddenAvailableModules??=new();
        if(config.NativeThemeRevision<2){config.Theme=Themes.NativeGameIndex;config.ThemeColors.Clear();config.BorderWidth=2.25f;config.NativeThemeRevision=2;config.Save();}
        if(config.RemoveRetiredBuiltInModules())config.Save();
        modules=new ModuleManager(config,new SourceCompiler());DailyRoutines.Manager.ModuleManager.Instance().Bind(modules);OmniModuleFactory.Bind(modules);
        try{DService.Init(pi);}catch(Exception e){Services.Log.Error(e,"OmenTools initialization failed; helper-dependent modules may be unavailable");}
        window=new MainWindow(config,modules);Services.Windows.AddWindow(window);
        modules.Restore();
        RegisterCommand("/yanjiu",Command,"打开小烟酒；/yanjiu 模块名 参数；/yanjiu icons 打开图标浏览器");
        RegisterCommand("/xyj",Command,"打开小烟酒，或转发指定模块命令");
        RegisterCommand("/ymusic",(_,args)=>Command("/ymusic","MusicPlayer "+args),"打开或关闭 Yanjiu 音乐播放器悬浮窗（模块须已启用）");
        if(!Services.Commands.Commands.ContainsKey("/omni"))RegisterCommand("/omni",Command,"兼容旧模块宏：/omni 模块名 参数；与 Omni 同时使用时请用 /yanjiu");
        pi.UiBuilder.Draw+=Draw;pi.UiBuilder.OpenConfigUi+=Open;pi.UiBuilder.OpenMainUi+=Open;Services.Framework.Update+=Update;
    }
    private void RegisterCommand(string name,IReadOnlyCommandInfo.HandlerDelegate handler,string help){if(Services.Commands.AddHandler(name,new CommandInfo(handler){HelpMessage=help,ShowInHelp=true,AllowedInMacros=true}))ownedCommands.Add(name);}
    private void Command(string command,string args){
        if(string.IsNullOrWhiteSpace(args)){Open();return;}
        if(args.Trim().Equals("icons",StringComparison.OrdinalIgnoreCase)){OmniModuleFactory.GetIconBrowser().Toggle();return;}
        try{var result=ModuleCommandRouter.Dispatch(modules.Entries,command,args);if(result.Length>0)Services.Chat.Print("[Yanjiu] "+result);}catch(Exception e){Services.Log.Error(e,"Module command failed");Services.Chat.PrintError("[Yanjiu] 模块命令失败："+e.GetBaseException().Message);}
    }
    private void Open()=>window.IsOpen=true;
    private void Draw(){if(disposed)return;Themes.Configure(config);UiFonts.Configure(config);using var font=UiFonts.Get().Push();using var theme=new ThemeScope();Services.Windows.Draw();OmniModuleFactory.Draw();}
    private void Update(IFramework _){if(disposed)return;modules.Pump();if(Environment.TickCount64>=nextCommandHelpRefresh){nextCommandHelpRefresh=Environment.TickCount64+2000;CommandMetadata.EnsureHelp(OmenTools.OmenService.CommandManager.Instance().Commands);}OmniModuleFactory.Tick(_);try{DailyRoutines.Manager.PlayersManager.Instance().Update();}catch(Exception e){Services.Log.Debug(e,"Targeting adapter update");}}
    public void Dispose(){if(disposed)return;disposed=true;Services.Framework.Update-=Update;Services.PluginInterface.UiBuilder.Draw-=Draw;Services.PluginInterface.UiBuilder.OpenConfigUi-=Open;Services.PluginInterface.UiBuilder.OpenMainUi-=Open;foreach(var name in ownedCommands)Services.Commands.RemoveHandler(name);ownedCommands.Clear();modules.Dispose();OmniToolbox.Host.DalamudServices.IsUnloading=true;OmniModuleFactory.Dispose();DailyRoutines.Manager.PlayersManager.Instance().Clear();DRNativeRuntime.Stop();window.Dispose();Services.Windows.RemoveAllWindows();try{DService.Uninit();}catch(Exception e){Services.Log.Error(e,"OmenTools cleanup failed");}}
}






