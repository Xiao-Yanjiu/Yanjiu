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
    private readonly Configuration config;private readonly ModuleManager modules;private readonly MainWindow window;private bool disposed;
    public Plugin(IDalamudPluginInterface pi)
    {
        pi.Create<Services>();Services.ConfigDirectory=pi.GetPluginConfigDirectory();Directory.CreateDirectory(Services.ConfigDirectory);
        config=pi.GetPluginConfig() as Configuration??new Configuration();config.Modules??=new();config.Repositories??=new();
        modules=new ModuleManager(config,new SourceCompiler());
        try{DService.Init(pi);}catch(Exception e){Services.Log.Error(e,"OmenTools initialization failed; helper-dependent modules may be unavailable");}
        window=new MainWindow(config,modules);Services.Windows.AddWindow(window);
        modules.AddBuiltIn(new BuiltIn.PhoneChat());modules.Restore();
        Services.Commands.AddHandler("/yanjiu",new CommandInfo(Command){HelpMessage="打开小烟酒；/yanjiu 模块名 参数"});Services.Commands.AddHandler("/xyj",new CommandInfo(Command){HelpMessage="打开小烟酒"});
        pi.UiBuilder.Draw+=Draw;pi.UiBuilder.OpenConfigUi+=Open;pi.UiBuilder.OpenMainUi+=Open;Services.Framework.Update+=Update;
    }
    private void Command(string command,string args){if(string.IsNullOrWhiteSpace(args)){Open();return;}var parts=args.Split(' ',2);var entry=modules.Entries.FirstOrDefault(e=>e.Instance?.ModuleName.Equals(parts[0],StringComparison.OrdinalIgnoreCase)==true);if(entry?.Instance?.IsEnabled==true)entry.Instance.TryHandleCommand(command,parts.Length>1?parts[1]:"");else Open();}
    private void Open()=>window.IsOpen=true;
    private void Draw(){if(disposed)return;Themes.Selected=config.Theme;Themes.Scale=Math.Clamp(config.Scale,.8f,1.5f);UiFonts.Configure(config);using var font=UiFonts.Get().Push();using var theme=new ThemeScope();Services.Windows.Draw();foreach(var entry in modules.Entries)if(entry.Instance is BuiltIn.PhoneChat phone)phone.DrawPanel();}
    private void Update(IFramework _){if(disposed)return;modules.Pump();try{DailyRoutines.Manager.PlayersManager.Instance().Update();}catch(Exception e){Services.Log.Debug(e,"Targeting adapter update");}}
    public void Dispose(){if(disposed)return;disposed=true;Services.Framework.Update-=Update;Services.PluginInterface.UiBuilder.Draw-=Draw;Services.PluginInterface.UiBuilder.OpenConfigUi-=Open;Services.PluginInterface.UiBuilder.OpenMainUi-=Open;Services.Commands.RemoveHandler("/yanjiu");Services.Commands.RemoveHandler("/xyj");modules.Dispose();window.Dispose();Services.Windows.RemoveAllWindows();try{DService.Uninit();}catch(Exception e){Services.Log.Error(e,"OmenTools cleanup failed");}}
}
