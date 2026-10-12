// SPDX-License-Identifier: GPL-3.0-or-later
using System.Numerics;
using Dalamud.Bindings.ImGui;
namespace Yanjiu.UI;
public sealed record Palette(string Name,Vector4 Background,Vector4 Surface,Vector4 Text,Vector4 Accent,bool Dark)
{
    public Vector4 Primary=>Accent;public Vector4 Secondary=>Surface;
    public Vector4 Border=>Vector4.Lerp(Surface,Accent,.3f);
    public Vector4 Shadow=>new(0,0,0,Dark?.45f:.18f);
    public float BorderRadius=>12;public float BorderThickness=>1;public float HighlightStrength=>.25f;
    public Vector4 Error=>new(.88f,.32f,.38f,1);public Vector4 Success=>new(.25f,.65f,.46f,1);public Vector4 Warning=>new(.92f,.66f,.22f,1);
}
public static class Themes
{
    public static readonly Palette[] Available={
        new("薄荷",new(.94f,.97f,.91f,1),new(.83f,.90f,.77f,1),new(.16f,.23f,.18f,1),new(.30f,.57f,.42f,1),false),
        new("桃夭",new(1,.94f,.94f,1),new(.97f,.84f,.86f,1),new(.28f,.19f,.22f,1),new(.76f,.43f,.51f,1),false),
        new("鸢尾",new(.95f,.94f,.99f,1),new(.85f,.82f,.96f,1),new(.23f,.20f,.33f,1),new(.51f,.43f,.78f,1),false),
        new("石墨",new(.10f,.11f,.14f,1),new(.16f,.18f,.23f,1),new(.90f,.92f,.97f,1),new(.53f,.65f,.95f,1),true),
        new("纸白",new(.98f,.98f,.99f,1),new(.90f,.92f,.96f,1),new(.16f,.19f,.25f,1),new(.31f,.48f,.77f,1),false),
        new("琥珀",new(.16f,.14f,.10f,1),new(.24f,.21f,.15f,1),new(.97f,.92f,.79f,1),new(.90f,.68f,.31f,1),true)
    };
    public static int Selected {get;set;}
    public static float Scale {get;set;}=1;
    public static Palette Current=>Available[Math.Clamp(Selected,0,Available.Length-1)];
}
public sealed class ThemeScope : IDisposable
{
    private int colors,vars;private bool disposed;
    public ThemeScope()
    {
        var p=Themes.Current;Color(ImGuiCol.Text,p.Text);Color(ImGuiCol.TextDisabled,new Vector4(p.Text.X,p.Text.Y,p.Text.Z,.6f));
        Color(ImGuiCol.WindowBg,p.Background);Color(ImGuiCol.ChildBg,p.Surface);Color(ImGuiCol.PopupBg,p.Background);
        Color(ImGuiCol.Border,p.Border);Color(ImGuiCol.FrameBg,p.Surface);Color(ImGuiCol.FrameBgHovered,Vector4.Lerp(p.Surface,p.Accent,.2f));Color(ImGuiCol.FrameBgActive,Vector4.Lerp(p.Surface,p.Accent,.35f));
        Color(ImGuiCol.Button,p.Surface);Color(ImGuiCol.ButtonHovered,Vector4.Lerp(p.Surface,p.Accent,.25f));Color(ImGuiCol.ButtonActive,Vector4.Lerp(p.Surface,p.Accent,.45f));
        Color(ImGuiCol.Header,p.Surface);Color(ImGuiCol.HeaderHovered,Vector4.Lerp(p.Surface,p.Accent,.25f));Color(ImGuiCol.HeaderActive,Vector4.Lerp(p.Surface,p.Accent,.4f));
        Color(ImGuiCol.CheckMark,p.Accent);Color(ImGuiCol.SliderGrab,p.Accent);Color(ImGuiCol.SliderGrabActive,p.Accent);Color(ImGuiCol.Separator,p.Border);
        Var(ImGuiStyleVar.WindowRounding,14);Var(ImGuiStyleVar.ChildRounding,12);Var(ImGuiStyleVar.FrameRounding,8);Var(ImGuiStyleVar.PopupRounding,10);Var(ImGuiStyleVar.GrabRounding,7);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding,new Vector2(14,12)*Themes.Scale);vars++;
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding,new Vector2(10,7)*Themes.Scale);vars++;
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing,new Vector2(9,8)*Themes.Scale);vars++;
    }
    private void Color(ImGuiCol index,Vector4 color){ImGui.PushStyleColor(index,color);colors++;}
    private void Var(ImGuiStyleVar index,float value){ImGui.PushStyleVar(index,value*Themes.Scale);vars++;}
    public void Dispose(){if(disposed)return;ImGui.PopStyleVar(vars);ImGui.PopStyleColor(colors);disposed=true;}
}
