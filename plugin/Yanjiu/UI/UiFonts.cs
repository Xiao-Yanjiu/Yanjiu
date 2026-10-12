// SPDX-License-Identifier: GPL-3.0-or-later
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Interface.Utility;
namespace Yanjiu.UI;
public static class UiFonts
{
    private static readonly Dictionary<float,IFontHandle> handles=new();
    private static float size=20;private static int family=1;
    public static readonly string[] Families=["卫月默认","微软雅黑","黑体"];
    public static void Configure(Configuration config)
    {
        var next=Math.Clamp(config.FontSize,16,28);var nextFamily=Math.Clamp(config.FontFamily,0,2);
        if(size==next&&family==nextFamily)return;
        Dispose();size=next;family=nextFamily;
    }
    public static IFontHandle Get(float scale=1)
    {
        var pixels=MathF.Round(size*Math.Clamp(scale,.6f,2)*ImGuiHelpers.GlobalScale);
        if(handles.TryGetValue(pixels,out var font))return font;
        int sourceFamily=family;var file=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts),sourceFamily==2?"simhei.ttf":"msyh.ttc");
        ushort[] ranges=[0x20,0xff,0x2000,0x2bff,0x3000,0x30ff,0x3400,0x9fff,0xff00,0xffef,0];
        font=Services.PluginInterface.UiBuilder.FontAtlas.NewDelegateFontHandle(e=>e.OnPreBuild(tk=>{
            var added=sourceFamily!=0&&File.Exists(file)?tk.AddFontFromFile(file,new(){SizePx=pixels,GlyphRanges=ranges,PixelSnapH=true}):tk.AddDalamudDefaultFont(pixels,ranges);
            tk.AddGameSymbol(new(){SizePx=pixels,MergeFont=added,PixelSnapH=true});
        }));
        handles[pixels]=font;return font;
    }
    public static void Dispose(){foreach(var handle in handles.Values)handle.Dispose();handles.Clear();}
}
