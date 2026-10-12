// SPDX-License-Identifier: GPL-3.0-or-later
using System.Numerics;
using Dalamud.Bindings.ImGui;
namespace Yanjiu.UI;
public sealed record Palette(string Name,Vector4 Background,Vector4 Surface,Vector4 Text,Vector4 Accent,bool Dark)
{
    public Vector4 Primary=>Accent;public Vector4 Secondary=>Surface;
    public Vector4? BorderOverride {get;init;}public Vector4? ShadowOverride {get;init;}public Vector4? SelectedOverride {get;init;}
    public Vector4 Border=>BorderOverride??(Dark?new(.36f,.39f,.44f,1):new(.97f,.97f,.92f,1));
    public Vector4 Shadow=>ShadowOverride??new Vector4(0,0,0,Dark?.45f:.18f);
    public Vector4 Selected=>SelectedOverride??Vector4.Lerp(Surface,Accent,.45f);
    public float BorderRadius=>12;public float BorderThickness=>Themes.BorderWidth;public float HighlightStrength=>.25f;
    public Vector4 Error=>new(.88f,.32f,.38f,1);public Vector4 Success=>new(.25f,.65f,.46f,1);public Vector4 Warning=>new(.92f,.66f,.22f,1);
}
public static class Themes
{
    public static readonly Palette[] Available={
        new("石墨",new(.11f,.12f,.13f,1),new(.18f,.20f,.22f,1),new(.91f,.92f,.93f,1),new(.47f,.53f,.58f,1),true),
        new("霁蓝",new(.12f,.13f,.19f,1),new(.19f,.21f,.30f,1),new(.91f,.92f,.96f,1),new(.46f,.50f,.66f,1),true),
        new("青麦",new(.84f,.85f,.73f,1),new(.90f,.90f,.79f,1),new(.19f,.22f,.18f,1),new(.65f,.69f,.51f,1),false),
        new("纸白",new(.94f,.94f,.93f,1),new(.99f,.99f,.98f,1),new(.22f,.24f,.26f,1),new(.60f,.65f,.70f,1),false),
        new("暖灰",new(.17f,.16f,.15f,1),new(.25f,.24f,.22f,1),new(.94f,.92f,.88f,1),new(.59f,.55f,.48f,1),true),
        new("月白",new(.88f,.91f,.93f,1),new(.95f,.97f,.98f,1),new(.20f,.25f,.29f,1),new(.57f,.68f,.72f,1),false)
    };
    public static int Selected {get;set;}
    public static float Scale {get;set;}=1;
    public static float BorderWidth {get;private set;}=2.25f;
    private static IReadOnlyDictionary<string,float[]> colors=new Dictionary<string,float[]>();
    public static void Configure(Configuration config){Selected=config.Theme;Scale=Math.Clamp(config.Scale,.8f,1.5f);BorderWidth=float.IsFinite(config.BorderWidth)?Math.Clamp(config.BorderWidth,1,4):2.25f;colors=config.ThemeColors;}
    public static Palette Current=>Resolve(Available[Math.Clamp(Selected,0,Available.Length-1)],colors);
    public static Palette Resolve(Palette basis,IReadOnlyDictionary<string,float[]> overrides)
    {
        Vector4 Color(string key,Vector4 fallback)=>overrides.TryGetValue(key,out var v)&&v is {Length:4}&&v.All(float.IsFinite)?new(Math.Clamp(v[0],0,1),Math.Clamp(v[1],0,1),Math.Clamp(v[2],0,1),Math.Clamp(v[3],0,1)):fallback;
        return basis with{Background=Color("body",basis.Background),Surface=Color("surface",basis.Surface),Text=Color("text",basis.Text),Accent=Color("accent",basis.Accent),BorderOverride=Color("border",basis.Border),ShadowOverride=Color("shadow",basis.Shadow),SelectedOverride=Color("selected",basis.Selected)};
    }
}
public sealed class ThemeScope : IDisposable
{
    private int colors,vars;private bool disposed;
    public ThemeScope()
    {
        var p=Themes.Current;Color(ImGuiCol.Text,p.Text);Color(ImGuiCol.TextDisabled,new Vector4(p.Text.X,p.Text.Y,p.Text.Z,.6f));
        Color(ImGuiCol.WindowBg,p.Background);Color(ImGuiCol.ChildBg,p.Surface);Color(ImGuiCol.PopupBg,p.Background);
        Color(ImGuiCol.TitleBg,p.Surface);Color(ImGuiCol.TitleBgActive,p.Surface);Color(ImGuiCol.TitleBgCollapsed,p.Background);
        Color(ImGuiCol.ScrollbarBg,p.Background);Color(ImGuiCol.ScrollbarGrab,p.Accent);Color(ImGuiCol.ScrollbarGrabHovered,p.Border);Color(ImGuiCol.ScrollbarGrabActive,p.Accent);
        Color(ImGuiCol.Border,p.Border);Color(ImGuiCol.FrameBg,p.Surface);Color(ImGuiCol.FrameBgHovered,Vector4.Lerp(p.Surface,p.Selected,.5f));Color(ImGuiCol.FrameBgActive,p.Selected);
        Color(ImGuiCol.Button,p.Surface);Color(ImGuiCol.ButtonHovered,Vector4.Lerp(p.Surface,p.Selected,.5f));Color(ImGuiCol.ButtonActive,p.Selected);
        Color(ImGuiCol.Header,p.Surface);Color(ImGuiCol.HeaderHovered,Vector4.Lerp(p.Surface,p.Selected,.5f));Color(ImGuiCol.HeaderActive,p.Selected);
        Color(ImGuiCol.CheckMark,p.Accent);Color(ImGuiCol.SliderGrab,p.Accent);Color(ImGuiCol.SliderGrabActive,p.Accent);Color(ImGuiCol.Separator,p.Border);
        Var(ImGuiStyleVar.WindowRounding,14);Var(ImGuiStyleVar.ChildRounding,12);Var(ImGuiStyleVar.FrameRounding,8);Var(ImGuiStyleVar.PopupRounding,10);Var(ImGuiStyleVar.GrabRounding,7);
        Var(ImGuiStyleVar.FrameBorderSize,p.BorderThickness);Var(ImGuiStyleVar.WindowBorderSize,p.BorderThickness);Var(ImGuiStyleVar.ChildBorderSize,p.BorderThickness);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding,new Vector2(14,12)*Themes.Scale);vars++;
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding,new Vector2(10,7)*Themes.Scale);vars++;
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing,new Vector2(9,8)*Themes.Scale);vars++;
    }
    private void Color(ImGuiCol index,Vector4 color){ImGui.PushStyleColor(index,color);colors++;}
    private void Var(ImGuiStyleVar index,float value){ImGui.PushStyleVar(index,value*Themes.Scale);vars++;}
    public void Dispose(){if(disposed)return;ImGui.PopStyleVar(vars);ImGui.PopStyleColor(colors);disposed=true;}
}
