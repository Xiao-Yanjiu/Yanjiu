// SPDX-License-Identifier: GPL-3.0-or-later
using System.Numerics;
using Dalamud.Bindings.ImGui;
namespace Yanjiu.UI;

/// <summary>Uses native ImGui interaction/navigation; draws the game's gray bevel.</summary>
public static class ThemeWidgets
{
    public static bool Button(string label,Vector2 size=default)
    {
        if(!Themes.Current.NativeGame)return ImGui.Button(label,size);
        var text=label.Split("##",2,StringSplitOptions.None)[0];
        var ink=ImGui.GetStyle().Colors[(int)ImGuiCol.Text];
        foreach(var color in new[]{ImGuiCol.Text,ImGuiCol.Button,ImGuiCol.ButtonHovered,ImGuiCol.ButtonActive,ImGuiCol.Border,ImGuiCol.BorderShadow})
            ImGui.PushStyleColor(color,Vector4.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize,0);
        bool clicked=ImGui.Button(label,size);
        ImGui.PopStyleVar();
        ImGui.PopStyleColor(6);
        var min=ImGui.GetItemRectMin();var max=ImGui.GetItemRectMax();var height=max.Y-min.Y;
        if(height<=2||max.X-min.X<=2||!ImGui.IsItemVisible())return clicked;
        bool pressed=ImGui.IsItemActive(),hover=ImGui.IsItemHovered();
        var draw=ImGui.GetWindowDrawList();float radius=Math.Min(height/2,(max.X-min.X)/2);
        uint Color(Vector4 c)=>ImGui.GetColorU32(c);
        Vector4 top=pressed?new(.16f,.16f,.16f,1):new(.36f,.36f,.35f,1);
        Vector4 middle=pressed?new(.21f,.21f,.21f,1):new(.21f,.21f,.21f,1);
        Vector4 bottom=pressed?new(.29f,.29f,.29f,1):new(.23f,.23f,.23f,1);
        if(hover&&!pressed)top+=new Vector4(.025f,.025f,.025f,0);
        draw.AddRectFilled(min,max,Color(middle),radius);
        // Rounded scanlines keep the gradient inside the capsule, with no texture assets.
        for(float y=1;y<height-1;y+=1){
            float t=y/height,dy=y<radius?radius-y:y>height-radius?y-(height-radius):0;
            float inset=dy>0?radius-MathF.Sqrt(Math.Max(0,radius*radius-dy*dy)):0;
            var c=t<.5f?Vector4.Lerp(top,middle,t*2):Vector4.Lerp(middle,bottom,(t-.5f)*2);
            draw.AddRectFilled(new(min.X+Math.Max(1,inset),min.Y+y),new(max.X-Math.Max(1,inset),min.Y+y+1),Color(c));
        }
        draw.AddRect(min,max,Color(new(.38f,.37f,.34f,.65f)),radius);
        draw.AddRect(min+Vector2.One,max-Vector2.One,Color(pressed?new(0,0,0,.4f):new(1,1,1,.08f)),Math.Max(0,radius-1));
        if(ImGui.IsItemFocused())draw.AddRect(min-Vector2.One,max+Vector2.One,Color(Themes.Current.Accent),radius+1);
        var textSize=ImGui.CalcTextSize(text);var pos=min+new Vector2(Math.Max(0,(max.X-min.X-textSize.X)/2),(height-textSize.Y)/2);
        draw.PushClipRect(min,max,true);
        draw.AddText(pos+new Vector2(0,1),Color(new(0,0,0,.85f)),text);draw.AddText(pos,Color(ink),text);
        draw.PopClipRect();
        return clicked;
    }
}
