// SPDX-License-Identifier: GPL-3.0-or-later
using KamiToolKit.BaseTypes;
using KamiToolKit.Nodes;
using FFXIVClientStructs.FFXIV.Component.GUI;
namespace OmniToolbox.TreePublic;
public sealed partial class ToolbarRenderer
{
    private NativeWidgetDetails? nativeWidgetDetails;
    private sealed unsafe class NativeWidgetDetails:NativeAddon
    {
        private readonly ToolbarRenderer owner;
        private readonly MultiToolbarBarConfig bar;
        private readonly MultiToolbarWidgetConfig widget;
        private TextNode? status;
        private IconImageNode? preview;
        private bool closed;
        [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
        internal NativeWidgetDetails(ToolbarRenderer owner,MultiToolbarBarConfig bar,MultiToolbarWidgetConfig widget)
        {
            this.owner=owner;this.bar=bar;this.widget=widget;
            InternalName="YanjiuToolbarWidgetDetails_"+owner.barID;Title="组件设置";Size=new(760,600);
        }
        private bool Valid(){var collection=owner.CollectionConfig;return !closed&&(ReferenceEquals(bar,collection)||collection.AuxiliaryBars.Any(item=>ReferenceEquals(item,bar)))&&(bar.Widgets.Contains(widget)||bar.Commands.Contains(widget));}
        private void Run(System.Action action){if(!Valid())return;try{action();owner.SaveNativeConfiguration();owner.nativeEditor?.Invalidate();}catch(Exception error){if(status!=null)status.String=error.GetBaseException().Message;}}
        protected override void OnSetup(AtkUnitBase* addon,Span<AtkValue> args)
        {
            closed=false;
            void Text(string label,int y,Func<string> read,System.Action<string> write){
                new TextNode{Position=ContentStartPosition+new System.Numerics.Vector2(0,y),Size=new(175,28),String=label,FontSize=13}.AttachNode(addon);
                new TextInputNode{Position=ContentStartPosition+new System.Numerics.Vector2(185,y),Size=new(500,28),String=read(),OnInputComplete=value=>Run(()=>write(value.ToString()))}.AttachNode(addon);
            }
            Text("名称",0,()=>widget.Name,value=>widget.Name=value);
            new CheckboxNode{Position=ContentStartPosition+new System.Numerics.Vector2(185,40),Size=new(450,28),String="使用组件默认显示名称",IsChecked=widget.DisplayName is null,OnClick=value=>Run(()=>widget.DisplayName=value?null:"")}.AttachNode(addon);
            Text("显示名称（空白仅图标）",80,()=>widget.DisplayName??"",value=>widget.DisplayName=value);
            Text("左键指令",120,()=>widget.Command,value=>widget.Command=value);
            Text("右键指令",160,()=>widget.RightCommand,value=>widget.RightCommand=value);
            new CheckboxNode{Position=ContentStartPosition+new System.Numerics.Vector2(185,200),Size=new(200,28),String="显示图标",IsChecked=widget.ShowIcon,OnClick=value=>Run(()=>widget.ShowIcon=value)}.AttachNode(addon);
            preview=new IconImageNode{IconId=widget.GameIconID,FitTexture=true,Position=ContentStartPosition+new System.Numerics.Vector2(185,242),Size=new(24,24),IsVisible=widget.GameIconID>0};preview.AttachNode(addon);
            new TextNode{Position=ContentStartPosition+new System.Numerics.Vector2(0,240),Size=new(175,28),String="游戏图标编号",FontSize=13}.AttachNode(addon);
            new TextInputNode{Position=ContentStartPosition+new System.Numerics.Vector2(220,240),Size=new(210,28),String=widget.GameIconID.ToString(),OnInputReceived=value=>{if(uint.TryParse(value.ToString(),out var id))Run(()=>widget.GameIconID=id);}}.AttachNode(addon);
            new TextButtonNode{Position=ContentStartPosition+new System.Numerics.Vector2(445,240),Size=new(140,28),String="选择游戏图标",OnClick=()=>Run(()=>owner.OpenNativeIconPicker(widget))}.AttachNode(addon);
            Text("图标文件路径",280,()=>widget.IconFilePath,value=>widget.IconFilePath=value);
            Text("图标字符",320,()=>widget.IconGlyph,value=>widget.IconGlyph=value);
            Text("单条信息栏名称",360,()=>widget.DtrTitle,value=>widget.DtrTitle=value);
            var options=Enum.GetValues<MultiToolbarWidgetSide>().ToList();
            new TextNode{Position=ContentStartPosition+new System.Numerics.Vector2(0,400),Size=new(175,28),String="组件位置",FontSize=13}.AttachNode(addon);
            new DropDownNode<MultiToolbarWidgetSide>{Position=ContentStartPosition+new System.Numerics.Vector2(185,400),Size=new(220,28),GetLabelFunction=value=>value switch{MultiToolbarWidgetSide.Left=>"左侧",MultiToolbarWidgetSide.Center=>"中间",_=>"右侧"},Options=options,SelectedOption=widget.Side,IsEnabled=bar.Widgets.Contains(widget),OnOptionSelected=value=>Run(()=>widget.Side=value)}.AttachNode(addon);
            status=new TextNode{Position=ContentStartPosition+new System.Numerics.Vector2(0,460),Size=new(690,35),FontSize=12};status.AttachNode(addon);
        }
        protected override void OnUpdate(AtkUnitBase* addon){
            if(!Valid()){if(status!=null)status.String="原组件已移除，请关闭此窗口。";return;}
            if(preview!=null){if(preview.IconId!=widget.GameIconID)preview.IconId=widget.GameIconID;preview.FitTexture=true;preview.Size=new(24,24);preview.IsVisible=widget.GameIconID>0;}
        }
        protected override void OnFinalize(AtkUnitBase* addon){closed=true;preview=null;base.OnFinalize(addon);}
    }
}
