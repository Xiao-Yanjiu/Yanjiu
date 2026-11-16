// SPDX-License-Identifier: GPL-3.0-or-later
using System.Numerics;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Nodes;
namespace Yanjiu.UI;
internal sealed unsafe partial class NativeMainWindow
{
    private TextNode? diagnostics;
    private string? nativeDetails;
    private ModuleConfigurationWindow? moduleSettings;
    private NativeBuiltInSettings? builtInSettings;
    private NativePanelWindow? localSettings;
    private int settingsRequest;
    private bool settingsDisposed;
    private void ShowNativeModuleDetails(Yanjiu.Modules.ModuleEntry entry)
    {
        if(settingsDisposed)return;
        int request=++settingsRequest;
        var instance=entry.Instance;
        if(entry.Origin is "本地" or "Yanjiu"&&instance is Yanjiu.Modules.Api1ModuleAdapter api){
            NativeSettingsPreparation.Open(api,()=>{
                if(!settingsDisposed&&request==settingsRequest&&modules.Entries.Contains(entry)&&ReferenceEquals(entry.Instance,instance)&&!entry.Transitioning)ShowNativeModuleDetailsReady(entry);
            });
        }else ShowNativeModuleDetailsReady(entry);
    }
    private void ShowNativeModuleDetailsReady(Yanjiu.Modules.ModuleEntry entry)
    {
        if(!NativeBuiltInSettings.HasConfiguration(entry))return;
        localSettings?.Dispose();localSettings=null;
        builtInSettings?.Dispose();builtInSettings=null;
        if(moduleSettings!=null){Services.Windows.RemoveWindow(moduleSettings);moduleSettings.Dispose();}
        moduleSettings=null;
        if(NativeBuiltInSettings.Supports(entry)){builtInSettings=new NativeBuiltInSettings(entry,modules);builtInSettings.Open();return;}
        if(entry.Origin is "本地" or "Yanjiu"&&entry.Instance is Yanjiu.NativeUI.INativeModuleUI provider){
            if(!entry.Transitioning && provider.TryOpenNativeSettings())return;
            if(provider.CreateNativePanel() is {} panel){
                localSettings=new NativePanelWindow(entry,modules,panel);localSettings.Open();return;
            }
        }
        moduleSettings=new ModuleConfigurationWindow(entry,modules,controller);
        Services.Windows.AddWindow(moduleSettings);
    }
    public override void Dispose(){settingsDisposed=true;settingsRequest++;localSettings?.Dispose();localSettings=null;builtInSettings?.Dispose();builtInSettings=null;if(moduleSettings!=null){Services.Windows.RemoveWindow(moduleSettings);moduleSettings.Dispose();}moduleSettings=null;base.Dispose();}
    private void ShowPage(int index)
    {
        selectedPage=index;nativeDetails=null;
        if(moduleContent!=null)moduleContent.IsVisible=index==1;
        for(int i=0;i<pageRoots.Count;i++)pageRoots[i].IsVisible=i==index;
        nextRefresh=0;
    }
    private void SetupPages(AtkUnitBase* addon)
    {
        for(int i=0;i<4;i++){
            var root=new ResNode{Position=ContentStartPosition+new Vector2(146,0),Size=new(750,580)};
            root.AttachNode(addon);pageRoots.Add(root);
        }
        var home=pageRoots[0];
        new TextNode{Size=new(740,30),String="模块来源与管理",FontSize=18}.AttachNode(home);
        string address="";
        new TextInputNode{Position=new(0,38),Size=new(600,28),PlaceholderString="在线仓库地址或本地 DLL / CS 路径",OnInputReceived=value=>address=value.ToString()}.AttachNode(home);
        new TextButtonNode{Position=new(610,38),Size=new(120,28),String="导入来源",OnClick=()=>{try{importSource(address);}catch(Exception e){Services.Log.Error(e,"Native source import failed");}}}.AttachNode(home);
        SetupSourceRows(home);
        var appearance=pageRoots[2];
        new TextNode{Size=new(730,30),String="界面主题",FontSize=18}.AttachNode(appearance);
        for(int i=0;i<Themes.Available.Length;i++){
            var theme=i;
            new TextButtonNode{Position=new((i%3)*180,45+(i/3)*40),Size=new(170,30),String=Themes.Available[i].Name,OnClick=()=>{config.Theme=theme;config.ThemeColors.Clear();config.Save();Themes.Configure(config);}}.AttachNode(appearance);
        }
        SetupAppearance(appearance);
        diagnostics=new TextNode{Size=new(740,480),FontSize=14};diagnostics.AttachNode(pageRoots[3]);
    }
}

internal static class NativeSettingsPreparation
{
    internal static async void Open(Yanjiu.Modules.Api1ModuleAdapter module,Action ready)
    {
        try{await module.PrepareNativeSettingsAsync();await Services.Framework.RunOnFrameworkThread(ready);}
        catch(Exception error){Services.Log.Error(error,"打开原生模块设置失败");}
    }
}
