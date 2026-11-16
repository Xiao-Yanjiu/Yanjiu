// SPDX-License-Identifier: GPL-3.0-or-later
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Yanjiu.Modules;
namespace Yanjiu.UI;
internal sealed class ModuleConfigurationWindow : Window,IDisposable
{
    private readonly ModuleEntry entry;
    private readonly ModuleManager modules;
    private readonly MainWindow controller;
    private ThemeScope? windowTheme;
    private readonly NativeSettingsShell shell=new();
    private float nativeScale=1;
    private bool disposed;
    internal ModuleConfigurationWindow(ModuleEntry module,ModuleManager manager,MainWindow main):base("模块设置###YanjiuModuleConfiguration")
    {
        entry=module;modules=manager;controller=main;
        Flags=ImGuiWindowFlags.NoTitleBar|ImGuiWindowFlags.NoBackground|ImGuiWindowFlags.NoMove|ImGuiWindowFlags.NoResize|ImGuiWindowFlags.NoSavedSettings|ImGuiWindowFlags.NoFocusOnAppearing;
        RespectCloseHotkey=false;DisableWindowSounds=true;
        try{shell.Open();}catch{shell.Dispose();throw;}
        IsOpen=true;
    }
    public override bool DrawConditions()=>!disposed&&shell.TryGetBody(out _,out _,out _);
    // Apply before WindowSystem calls Begin: title bar, resize grips and scrollbars
    // are drawn by Begin, not by the module's Draw callback.
    public override void PreDraw()
    {
        windowTheme=new ThemeScope(Themes.Available[Themes.NativeGameIndex]);
        if(shell.TryGetBody(out var position,out var size,out nativeScale))
        {
            Position=ImGui.GetMainViewport().Pos+position;PositionCondition=ImGuiCond.Always;
            Size=size;SizeCondition=ImGuiCond.Always;
        }
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize,0);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding,Vector2.Zero);
    }
    public override void PostDraw()
    {
        ImGui.PopStyleVar(2);windowTheme?.Dispose();
        windowTheme=null;
    }
    public override void Draw()
    {
        ImGui.SetWindowFontScale(nativeScale);
        if(!modules.Entries.Contains(entry)){ImGui.TextDisabled("模块已移除，请关闭此窗口。");return;}
        try
        {
            if(Api1Runtime.IsReady)Api1Runtime.SyncTheme(Themes.Available[Themes.NativeGameIndex]);
            ImGui.PushID(entry.Path);
            try{ImGui.TextUnformatted(entry.Title);controller.DrawModuleConfiguration(entry);}
            finally{ImGui.PopID();}
        }
        finally{if(Api1Runtime.IsReady)Api1Runtime.SyncTheme();}
    }
    public void Dispose(){if(disposed)return;disposed=true;IsOpen=false;shell.Dispose();}
}
