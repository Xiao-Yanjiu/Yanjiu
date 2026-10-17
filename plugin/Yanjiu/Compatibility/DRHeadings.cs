// SPDX-License-Identifier: GPL-3.0-or-later
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
namespace DailyRoutines.Common.Extensions;
public ref struct DRHeading : IDisposable
{
    private bool active;
    public DRHeading(string text,string? help){active=true;ImGui.TextColored(Yanjiu.UI.Themes.Current.Accent,text);if(help!=null)OmenTools.ImGuiOm.ImGuiOm.HelpMarker(help);ImGui.Indent();}
    public void Dispose(){if(!active)return;active=false;ImGui.Unindent();}
}
public static class HeadingExtensions
{
    extension(ImRaii)
    {
        public static DRHeading Heading1(string text,string? help=null)=>new(text,help);
        public static DRHeading Heading2(string text,string? help=null)=>new(text,help);
    }
}
