// SPDX-License-Identifier: GPL-3.0-or-later
using System.Globalization;
using System.Numerics;
using KamiToolKit.Nodes;
namespace Yanjiu.UI;
internal sealed unsafe partial class NativeMainWindow
{
    private TextNode? appearanceStatus;
    private void SetupAppearance(ResNode root)
    {
        appearanceStatus=new TextNode{Position=new(0,530),Size=new(730,40),FontSize=12};appearanceStatus.AttachNode(root);
        string[] labels=["主体","面板","阴影","边框","选中状态","强调色","文字"];
        string[] keys=["body","surface","shadow","border","selected","accent","text"];
        var palette=Themes.Current;
        Vector4[] colors=[palette.Background,palette.Surface,palette.Shadow,palette.Border,palette.Selected,palette.Accent,palette.Text];
        for(int i=0;i<keys.Length;i++){
            var key=keys[i];var color=colors[i];
            new TextNode{Position=new(0,180+i*34),Size=new(90,24),String=labels[i],FontSize=14}.AttachNode(root);
            var input=new TextInputNode{Position=new(100,180+i*34),Size=new(430,26),String=$"{color.X:F3}, {color.Y:F3}, {color.Z:F3}, {color.W:F3}"};
            input.OnInputComplete=text=>{
                var parts=text.ToString().Split(',');
                var values=new float[4];
                for(int j=0;j<4;j++)if(parts.Length!=4||!float.TryParse(parts[j],NumberStyles.Float,CultureInfo.InvariantCulture,out values[j])||!float.IsFinite(values[j])||values[j]<0||values[j]>1){appearanceStatus.String="颜色需要四个0到1的数值，以逗号分隔。";return;}
                config.ThemeColors[key]=values;config.Save();Themes.Configure(config);appearanceStatus.String="配色已保存。";
            };input.AttachNode(root);
        }
        AddAppearanceValue(root,"缩放",420,config.Scale,.8f,1.5f,value=>config.Scale=value);
        AddAppearanceValue(root,"字号",454,config.FontSize,16,28,value=>config.FontSize=MathF.Round(value));
        AddAppearanceValue(root,"边框粗细",488,config.BorderWidth,1,4,value=>config.BorderWidth=value);
        for(int i=0;i<UiFonts.Families.Length;i++){
            int family=i;
            new TextButtonNode{Position=new(550,180+i*36),Size=new(170,28),String=UiFonts.Families[i],OnClick=()=>{config.FontFamily=family;config.Save();}}.AttachNode(root);
        }
        new TextButtonNode{Position=new(550,310),Size=new(170,28),String="恢复主题配色",OnClick=()=>{config.ThemeColors.Clear();config.BorderWidth=2.25f;config.Save();Themes.Configure(config);appearanceStatus.String="配色已恢复。重新打开窗口刷新数值。";}}.AttachNode(root);
    }
    private void AddAppearanceValue(ResNode root,string label,int y,float initial,float min,float max,Action<float> assign)
    {
        new TextNode{Position=new(0,y),Size=new(90,24),String=label,FontSize=14}.AttachNode(root);
        var slider=new FloatSliderNode{Position=new(100,y),Size=new(430,24),Min=min,Max=max,Step=.01f,Value=initial};
        slider.OnValueChanged=value=>{assign(value);config.Save();Themes.Configure(config);};slider.AttachNode(root);
    }
}
