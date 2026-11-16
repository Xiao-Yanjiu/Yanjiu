// SPDX-License-Identifier: GPL-3.0-or-later
using KamiToolKit.BaseTypes;
using KamiToolKit.Nodes;
using KamiToolKit.Enums;
using KamiToolKit.Timelines;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Yanjiu.NativeUI;
using OmenTools;
using OmniToolbox.UI;
using System.Globalization;
namespace OmniToolbox.TreePublic;
public sealed partial class MultiToolbar
{
    private ToolbarNativeSettings? nativeSettings;
    public bool TryOpenNativeSettings()
    {
        if(KamiToolKit.KamiToolKitLibrary.PluginInterface is null)return false;
        OpenToolbarNativeSettings();return true;
    }
    private void OpenToolbarNativeSettings()
    {
        if(KamiToolKit.KamiToolKitLibrary.PluginInterface is null)throw new InvalidOperationException("原生界面尚未初始化。");
        nativeSettings??=new ToolbarNativeSettings(Renderer(),()=>{MarkDirty();RequestEnabled(IsEnabled,true);});
        if(!nativeSettings.IsOpen)nativeSettings.Open();
    }
    private sealed unsafe class ToolbarNativeSettings:NativeAddon
    {
        private readonly ToolbarRenderer renderer;
        private readonly System.Action save;
        private readonly NativePanel panel;
        private readonly List<NodeBase> nodes=[];
        private bool rebuild;
        private TextNode? status;
        private KamiToolKit.Addons.ColorPickerAddon? picker;
        private bool picking;
        private NativeToolbarName? rename;
        [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
        internal ToolbarNativeSettings(ToolbarRenderer renderer,System.Action save)
        {
            this.renderer=renderer;this.save=save;panel=renderer.CreateNativeConfigPanel();
            InternalName="YanjiuToolbarNativeSettings";Title="多功能工具栏设置";Size=new(1120,460);
        }
        protected override void OnSetup(AtkUnitBase* addon,Span<AtkValue> arguments)=>Build(addon);
        private T Control<T>(string label) where T:NativeControl=>panel.Controls.OfType<T>().Single(item=>item.Label==label);
        private void Run(System.Action action){try{if(picking||rename?.IsOpen==true)return;action();save();}catch(Exception error){if(status!=null)status.String=error.GetBaseException().Message;}}
        private void Attach(NodeBase node,AtkUnitBase* addon){node.AttachNode(addon);nodes.Add(node);}
        private TextButtonNode Button(AtkUnitBase* addon,string label,float x,float y,float width,System.Action click,bool enabled=true)
        {
            var button=new TextButtonNode{Position=ContentStartPosition+new Vector2(x,y),Size=new(width,28),String=label,IsEnabled=enabled,OnClick=()=>Run(click)};Attach(button,addon);return button;
        }
        private void Build(AtkUnitBase* addon)
        {
            foreach(var node in nodes)node.Dispose();nodes.Clear();
            var bars=Control<NativeSelect>("工具栏");
            float y=42;
            foreach(var option in bars.Options()){
                var selected=option;
                var button=Button(addon,option.Label,0,y,178,()=>{bars.Write(selected.Id);rebuild=true;});
                if(option.Id!="main")button.AddEvent(AtkEventType.MouseClick,(listener,type,param,ev,data)=>{
                    if(data==null||picking||rename?.IsOpen==true)return;
                    if(data->MouseData.ButtonId==0){button.OnClick?.Invoke();return;}
                    if(data->MouseData.ButtonId!=1)return;
                    rename?.Dispose();rename=new NativeToolbarName(selected.Label,value=>{
                        if(!bars.Options().Any(option=>option.Id==selected.Id))throw new InvalidOperationException("此工具栏已删除。");
                        bars.Write(selected.Id);Control<NativeText>("工具栏名称").Write(value);save();rebuild=true;
                    });rename.Open();
                });y+=34;
            }
            Button(addon,"添加",0,y,60,()=>{Control<NativeButton>("添加副工具栏").Click();rebuild=true;});
            Button(addon,"删除副工具栏",68,y,110,()=>{Control<NativeButton>("删除当前副工具栏").Click();rebuild=true;},bars.Read()!="main");
            Attach(new VerticalLineNode{Position=ContentStartPosition+new Vector2(190,38),Size=new(2,330)},addon);
            Button(addon,"导出配置",0,0,100,Control<NativeButton>("导出配置到剪贴板").Click);
            Button(addon,"导入配置",108,0,100,()=>{Control<NativeButton>("从剪贴板导入配置").Click();rebuild=true;});
            Button(addon,"编辑工具栏组件",208,42,156,()=>renderer.OpenNativeSettingsEditor("组件"));
            Button(addon,"编辑插件收纳",372,42,156,()=>renderer.OpenNativeSettingsEditor("插件"));
            Button(addon,"编辑指令收纳",536,42,156,()=>renderer.OpenNativeSettingsEditor("指令"));
            var toggle=Control<NativeToggle>("组件自动展开");
            Attach(new CheckboxNode{Position=ContentStartPosition+new Vector2(208,82),Size=new(176,28),String=toggle.Label,IsChecked=toggle.Read(),OnClick=value=>Run(()=>toggle.Write(value))},addon);
            string[] colors=["背景颜色","文本颜色","强调颜色","边框颜色"];
            for(int i=0;i<colors.Length;i++){
                var color=Control<NativeColor>(colors[i]);
                var node=new ColorSquareTextButtonNode{Position=ContentStartPosition+new Vector2(390+i*172,82),Size=new(164,28),String=color.Label,DefaultColor=color.Read()};
                node.OnClick=()=>Run(()=>{
                    picker?.Dispose();picker=new KamiToolKit.Addons.ColorPickerAddon{InternalName="YanjiuToolbarSettingsColor",Title=color.Label,InitialColor=color.Read(),OnColorConfirmed=value=>{picking=false;Run(()=>color.Write(value));rebuild=true;},OnColorCancelled=()=>picking=false};
                    picking=true;try{picker.Open();}catch{picking=false;throw;}
                });Attach(node,addon);
            }
            string[] numbers=["垂直偏移","工具栏背景透明度","按钮背景透明度","工具栏缩放","图标大小","组件间距"];
            for(int i=0;i<numbers.Length;i++){
                var field=Control<NativeNumber>(numbers[i]);int column=i%4;int row=i/4;
                float x=208+column*218;float top=124+row*60;
                Attach(new TextNode{Position=ContentStartPosition+new Vector2(x,top),Size=new(210,22),FontSize=13,String=field.Label},addon);
                var slider=new FloatSliderNode{
                    Position=ContentStartPosition+new Vector2(x,top+22),Size=new(204,24),
                    Min=(float)field.Minimum,Max=(float)field.Maximum,
                    Step=i is 0 or 4 or 5?1f:.01f,
                    Value=(float)field.Read()
                };
                slider.FloatValueNode.IsVisible=true;
                // Set the initial value before binding; dragging uses the existing debounced persistence.
                slider.OnValueChanged=value=>{
                    if(picking||rename?.IsOpen==true)return;
                    try{field.Write(Math.Clamp(value,field.Minimum,field.Maximum));}
                    catch(Exception error){if(status!=null)status.String=error.GetBaseException().Message;}
                };
                Attach(slider,addon);
            }
            Select(addon,Control<NativeSelect>("位置"),644,184,204);
            Select(addon,Control<NativeSelect>("字体"),862,184,204);
            Text(addon,Control<NativeText>("配装插件命令"),208,262);
            Text(addon,Control<NativeText>("ID 信息插件命令"),208,306);
            status=new TextNode{Position=ContentStartPosition+new Vector2(208,360),Size=new(850,30),FontSize=12};Attach(status,addon);
        }
        private void Select(AtkUnitBase* addon,NativeSelect select,float x,float y,float width)
        {
            var options=select.Options().ToList();
            Attach(new TextNode{Position=ContentStartPosition+new Vector2(x,y),Size=new(width,22),FontSize=13,String=select.Label},addon);
            Attach(new DropDownNode<NativeOption>{Position=ContentStartPosition+new Vector2(x,y+22),Size=new(width,28),GetLabelFunction=option=>option.Label,Options=options,SelectedOption=options.FirstOrDefault(option=>option.Id==select.Read()),OnOptionSelected=option=>Run(()=>select.Write(option.Id))},addon);
        }
        private void Text(AtkUnitBase* addon,NativeText field,float x,float y)
        {
            Attach(new TextInputNode{Position=ContentStartPosition+new Vector2(x,y),Size=new(540,28),String=field.Read(),OnInputComplete=value=>Run(()=>field.Write(value.ToString()))},addon);
            Attach(new TextNode{Position=ContentStartPosition+new Vector2(x+552,y),Size=new(300,28),FontSize=13,String=field.Label+"（选填）"},addon);
        }
        protected override void OnUpdate(AtkUnitBase* addon){if(rebuild&&!picking){rebuild=false;Build(addon);}}
        protected override void OnFinalize(AtkUnitBase* addon){picker?.Dispose();picker=null;rename?.Dispose();rename=null;picking=false;nodes.Clear();base.OnFinalize(addon);}
        public override void Dispose(){picker?.Dispose();picker=null;rename?.Dispose();rename=null;nodes.Clear();base.Dispose();}
    }
}
public sealed partial class ToolbarRenderer
{
    private NativeToolbarEditor? nativeEditor;
    private static async void ObserveNativeEditor(Task operation){try{await operation;}catch(Exception error){DService.Instance().Log.Error(error,"打开原生组件设置失败");}}
    internal void OpenNativeSettingsEditor(string editor)
    {
        if(barID!="main")selectedBarID=barID;
        nativeEditor?.Dispose();nativeEditor=new NativeToolbarEditor(this,editor);nativeEditor.Open();
    }
    private sealed unsafe class NativeToolbarEditor:NativeAddon
    {
        private readonly ToolbarRenderer owner;
        private readonly MultiToolbarBarConfig config;
        private readonly string kind;
        private readonly List<NodeBase> roots=[];
        private bool pending;
        private TextNode? status;
        private bool finalized;
        private readonly List<System.Action> iconLayouts=[];
        private readonly List<System.Action> pluginRefresh=[];
        private long nextPluginIcons;
        private static void FixedLabel(TextNode label,float x,float y)
        {
            label.AddTimeline(new TimelineBuilder().BeginFrameSet(1,200).AddFrame(1,position:new Vector2(x,y),alpha:255).EndFrameSet().Build());
            label.Position=new(x,y);
        }
        private void DecorateOptions(DropDownNode<MultiToolbarWidgetType> dropdown)
        {
            var property=typeof(DropDownNode<MultiToolbarWidgetType>).GetProperty("PopupButtonListNode",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            if(property?.GetValue(dropdown) is not VerticalListNode list)return;
            var decorated=new Dictionary<ListButtonNode,IconImageNode>();
            iconLayouts.Add(()=>{
                if(dropdown.IsCollapsed)return;
                var current=list.GetNodes<ListButtonNode>().ToArray();
                foreach(var old in decorated.Keys.Where(node=>!current.Contains(node)).ToArray())decorated.Remove(old);
                foreach(var button in current){
                    if(!decorated.TryGetValue(button,out var icon)){
                        icon=new IconImageNode{FitTexture=true,Position=new(8,3),Size=new(16,16),IsVisible=false};icon.AttachNode(button);decorated[button]=icon;
                        FixedLabel(button.LabelNode,30,1);button.LabelNode.Width=button.Width-34;
                    }
                    string title=button.String.ToString();var matches=dropdown.Options.Where(type=>WidgetTypeLabel(type)==title).ToArray();
                    uint id=0;
                    if(matches.Length==1){
                        var configured=config.Widgets.FirstOrDefault(widget=>widget.Type==matches[0]&&widget.GameIconID>0);
                        id=configured?.GameIconID??owner.WidgetGameIcon(matches[0]);
                    }
                    // Popup draw flags were propagated before this dynamically created icon existed.
                    icon.AddDrawFlags(DrawFlags.RenderOnTop);
                    if(icon.IconId!=id)icon.IconId=id;icon.Size=new(16,16);icon.FitTexture=true;icon.IsVisible=id>0;
                }
            });
        }
        private readonly List<(IconImageNode Node,Func<uint> Read)> icons=[];
        [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
        internal NativeToolbarEditor(ToolbarRenderer owner,string kind)
        {
            this.owner=owner;this.kind=kind;
            config=owner.CollectionConfig.AuxiliaryBars.FirstOrDefault(bar=>bar.ID==owner.selectedBarID) as MultiToolbarBarConfig ?? owner.config;
            InternalName="YanjiuToolbarEditor_"+owner.barID;Title="编辑"+kind;Size=kind=="组件"?new(1400,680):new(1000,560);
        }
        private void Run(System.Action action,bool rebuild=false)
        {
            if(finalized)return;
            var collection=owner.CollectionConfig;if(!ReferenceEquals(config,collection)&&!collection.AuxiliaryBars.Any(bar=>ReferenceEquals(bar,config))){if(status!=null)status.String="工具栏已移除，请关闭此窗口。";return;}
            try{action();owner.SaveNativeConfiguration();pending|=rebuild;}catch(Exception error){if(status!=null)status.String=error.GetBaseException().Message;}
        }
        private void Button(ResNode parent,string title,float x,float y,float width,System.Action action,bool rebuild=false,bool enabled=true)
        {
            string hint=title switch { "上"=>"上移", "下"=>"下移", "删"=>"删除", _=>title };
            if(width<=24){
                new CircleButtonNode{Icon=title switch {"上"=>CircleButtonIcon.UpArrow,"下"=>CircleButtonIcon.ArrowDown,"删"=>CircleButtonIcon.Cross,_=>CircleButtonIcon.GearCog},Position=new(x,y+3),Size=new(22,22),TextTooltip=hint,IsEnabled=enabled,OnClick=()=>Run(action,rebuild)}.AttachNode(parent);
            }else new TextButtonNode{Position=new(x,y),Size=new(width,26),String=title,TextTooltip=hint,IsEnabled=enabled,OnClick=()=>Run(action,rebuild)}.AttachNode(parent);
        }
        private void Input(ResNode parent,string text,float x,float y,float width,System.Action<string> write,bool live=false)
        {
            var input=new TextInputNode{Position=new(x,y),Size=new(width,26),String=text,OnInputComplete=value=>Run(()=>write(value.ToString()))};
            if(live)input.OnInputReceived=value=>{var text=value.ToString();if(uint.TryParse(text,out _))Run(()=>write(text));};
            input.AttachNode(parent);
        }
        private ScrollingNode<ResNode> Column(AtkUnitBase* addon,string title,float x,float width,float height)
        {
            var root=new ResNode{Position=ContentStartPosition+new Vector2(x,0),Size=new(width,height+40)};root.AttachNode(addon);roots.Add(root);
            new TextNode{Size=new(width,24),FontSize=14,String=title}.AttachNode(root);
            var scroll=new ScrollingNode<ResNode>{Position=new(0,30),Size=new(width,height),ScrollSpeed=34};scroll.AttachNode(root);return scroll;
        }
        protected override void OnSetup(AtkUnitBase* addon,Span<AtkValue> args){finalized=false;Build(addon);}
        private void Build(AtkUnitBase* addon)
        {
            icons.Clear();iconLayouts.Clear();pluginRefresh.Clear();foreach(var root in roots)root.Dispose();roots.Clear();
            if(kind=="组件"){
                for(int column=0;column<3;column++){
                    var side=(MultiToolbarWidgetSide)column;
                    var scroll=Column(addon,new[]{"左侧","中间","右侧"}[column],column*342,332,550);
                    var items=config.Widgets.Where(item=>item.Side==side).ToArray();float y=0;
                    foreach(var item in items){
                        var widget=item;
                        new CheckboxNode{Position=new(0,y),Size=new(30,28),IsChecked=widget.Enabled,OnClick=value=>Run(()=>widget.Enabled=value)}.AttachNode(scroll.ContentNode);
                        var options=SelectableWidgetTypes.ToList();
                        var dropdown=new DropDownNode<MultiToolbarWidgetType>{Position=new(28,y),Size=new(184,28),TextTooltip=WidgetTypeLabel(widget.Type),GetLabelFunction=value=>WidgetTypeLabel(value),Options=options,SelectedOption=widget.Type,OnOptionSelected=value=>Run(()=>widget.Type=value,true)};
                        dropdown.AttachNode(scroll.ContentNode);DecorateOptions(dropdown);
                        uint ReadIcon()=>widget.ShowIcon?(widget.GameIconID>0?widget.GameIconID:owner.WidgetGameIcon(widget.Type)):0;
                        var icon=new IconImageNode{FitTexture=true,IconId=ReadIcon(),Position=new(20,6),Size=new(16,16),IsVisible=ReadIcon()>0};icon.AttachNode(dropdown);icons.Add((icon,ReadIcon));
                        void LayoutIcon(){icon.Position=new(20,6);icon.Size=new(16,16);icon.FitTexture=true;dropdown.LabelNode.X=ReadIcon()>0?40:20;dropdown.LabelNode.Y=0;dropdown.LabelNode.Width=ReadIcon()>0?132:152;}
                        FixedLabel(dropdown.LabelNode,ReadIcon()>0?40:20,0);iconLayouts.Add(LayoutIcon);LayoutIcon();
                        Button(scroll.ContentNode,"上",216,y,22,()=>MoveWidget(widget,-1),true,Array.IndexOf(items,widget)>0);
                        Button(scroll.ContentNode,"下",240,y,22,()=>MoveWidget(widget,1),true,Array.IndexOf(items,widget)<items.Length-1);
                        Button(scroll.ContentNode,"删",288,y,22,()=>config.Widgets.Remove(widget),true);
                        // Keep detailed editing separate from the four-column overview.
                        Button(scroll.ContentNode,"设置",264,y,22,()=>OpenDetail(widget));y+=34;
                    }
                    var types=SelectableWidgetTypes.ToList();MultiToolbarWidgetType addType=MultiToolbarWidgetType.CustomButton;
                    var addDropdown=new DropDownNode<MultiToolbarWidgetType>{Position=new(0,y),Size=new(220,28),GetLabelFunction=value=>WidgetTypeLabel(value),Options=types,SelectedOption=addType,OnOptionSelected=value=>addType=value};addDropdown.AttachNode(scroll.ContentNode);DecorateOptions(addDropdown);FixedLabel(addDropdown.LabelNode,20,0);
                    Button(scroll.ContentNode,"添加",228,y,74,()=>{var added=CreateDefaultWidget(addType);added.Side=side;config.Widgets.Add(added);},true);
                    scroll.ContentNode.Height=Math.Max(550,y+40);scroll.RecalculateSizes();
                }
                var dtr=Column(addon,"服务器信息栏",1026,332,550);float top=0;
                foreach(var entry in DService.Instance().DTRBar.Entries.OrderBy(item=>config.DtrOrder.IndexOf(item.Title)).Reverse()){
                    string title=entry.Title;
                    new CheckboxNode{Position=new(0,top),Size=new(220,28),String=title.Length>22?title[..21]+"…":title,TextTooltip=title,IsChecked=config.CollapsedDtrTitles.Contains(title),OnClick=value=>Run(()=>{
                        if(value){if(!config.CollapsedDtrTitles.Contains(title))config.CollapsedDtrTitles.Add(title);}else config.CollapsedDtrTitles.Remove(title);
                        if(!config.DtrOrder.Contains(title))config.DtrOrder.Add(title);
                    })}.AttachNode(dtr.ContentNode);
                    Button(dtr.ContentNode,"上移",224,top,42,()=>{if(!config.DtrOrder.Contains(title))config.DtrOrder.Add(title);Move(config.DtrOrder,title,1);},true);
                    Button(dtr.ContentNode,"下移",270,top,42,()=>{if(!config.DtrOrder.Contains(title))config.DtrOrder.Add(title);Move(config.DtrOrder,title,-1);},true);top+=34;
                }
                dtr.ContentNode.Height=Math.Max(550,top);dtr.RecalculateSizes();
            }else if(kind=="插件"){
                owner.pluginCacheValid=false;owner.RefreshPluginCache();
                var scroll=Column(addon,"插件收纳",0,940,420);float y=0;
                foreach(var heading in new[]{("原插件名称",206f,350f),("备注名称（留空使用原名）",564f,330f)}){
                    var title=new TextNode{Position=ContentStartPosition+new Vector2(heading.Item2,0),Size=new(heading.Item3,24),FontSize=13,String=heading.Item1};title.AttachNode(addon);roots.Add(title);
                }
                var names=config.SelectedPlugins.Concat(owner.loadedPlugins.Select(plugin=>plugin.InternalName)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                foreach(string name in names){
                    string id=name;bool selected=config.SelectedPlugins.Contains(id);
                    Button(scroll.ContentNode,"上移",0,y,60,()=>Move(config.SelectedPlugins,id,-1),true,selected&&config.SelectedPlugins.IndexOf(id)>0);
                    Button(scroll.ContentNode,"下移",66,y,60,()=>Move(config.SelectedPlugins,id,1),true,selected&&config.SelectedPlugins.IndexOf(id)<config.SelectedPlugins.Count-1);
                    owner.installedPlugins.TryGetValue(id,out var plugin);
                    string original=plugin?.Name??id;
                    new CheckboxNode{Position=new(138,y),Size=new(30,28),IsChecked=selected,OnClick=value=>Run(()=>{
                        if(value){if(!config.SelectedPlugins.Contains(id))config.SelectedPlugins.Add(id);}else config.SelectedPlugins.Remove(id);
                        config.MultiDockPluginsMigrated=true;owner.pluginCacheValid=false;
                    },true)}.AttachNode(scroll.ContentNode);
                    if(plugin!=null){
                        var captured=plugin;var icon=new ImGuiImageNode{FitTexture=true,Position=new(174,y+4),Size=new(20,20),IsVisible=false};icon.AttachNode(scroll.ContentNode);
                        Dalamud.Bindings.ImGui.ImTextureID bound=default;
                        void RefreshPluginIcon(){
                            var texture=PluginIconResolver.GetIcon(captured,20);
                            if(!PluginIconResolver.IsUsableTexture(texture))return;
                            if(bound!=texture.Handle){icon.LoadTexture(texture.CreateWrapSharingLowLevelResource());icon.TextureSize=texture.Size;bound=texture.Handle;}
                            icon.FitTexture=true;icon.Size=new(20,20);icon.IsVisible=true;
                        }
                        pluginRefresh.Add(RefreshPluginIcon);RefreshPluginIcon();
                    }
                    new TextNode{Position=new(206,y),Size=new(350,28),FontSize=13,String=original,TextTooltip=id}.AttachNode(scroll.ContentNode);
                    Input(scroll.ContentNode,config.PluginDisplayNames.GetValueOrDefault(id,""),564,y,330,value=>{
                        if(string.IsNullOrWhiteSpace(value))config.PluginDisplayNames.Remove(id);else config.PluginDisplayNames[id]=value.Trim();
                    });y+=34;
                }
                scroll.ContentNode.Height=Math.Max(420,y);scroll.RecalculateSizes();
            }else{
                var scroll=Column(addon,"",0,940,420);float y=0;
                foreach(var header in new[]{("排序",0f,112f),("启用",118f,32f),("名称",154f,160f),("指令",320f,320f),("图标",646f,100f)}){
                    var title=new TextNode{Position=ContentStartPosition+new Vector2(header.Item2,0),Size=new(header.Item3,24),FontSize=13,String=header.Item1};title.AttachNode(addon);roots.Add(title);
                }
                foreach(var item in config.Commands){
                    var command=item;
                    Button(scroll.ContentNode,"上移",0,y,54,()=>Move(config.Commands,command,-1),true,config.Commands.IndexOf(command)>0);
                    Button(scroll.ContentNode,"下移",58,y,54,()=>Move(config.Commands,command,1),true,config.Commands.IndexOf(command)<config.Commands.Count-1);
                    new CheckboxNode{Position=new(118,y),Size=new(30,28),IsChecked=command.Enabled,OnClick=value=>Run(()=>command.Enabled=value)}.AttachNode(scroll.ContentNode);
                    Input(scroll.ContentNode,command.Name,154,y,160,value=>command.Name=value);
                    Input(scroll.ContentNode,command.Command,320,y,320,value=>command.Command=value);
                    var commandIcon=new IconImageNode{FitTexture=true,IconId=command.GameIconID,Position=new(650,y+6),Size=new(16,16),IsVisible=command.GameIconID>0};commandIcon.AttachNode(scroll.ContentNode);icons.Add((commandIcon,()=>command.GameIconID));
                    Input(scroll.ContentNode,command.GameIconID.ToString(),676,y,70,value=>{
                        if(!uint.TryParse(value,out var id))throw new ArgumentException("图标编号无效。");command.GameIconID=id;command.ShowIcon=true;command.IconFilePath="";command.IconGlyph="";
                    },true);
                    new CircleButtonNode{Icon=CircleButtonIcon.Document,Position=new(752,y+3),Size=new(24,24),TextTooltip="选择游戏图标",OnClick=()=>Run(()=>owner.OpenNativeIconPicker(command))}.AttachNode(scroll.ContentNode);
                    Button(scroll.ContentNode,"设置",782,y,62,()=>OpenDetail(command));
                    Button(scroll.ContentNode,"删除",850,y,62,()=>config.Commands.Remove(command),true);y+=34;
                }
                Button(scroll.ContentNode,"添加指令",0,y,100,()=>config.Commands.Add(CreateDefaultWidget(MultiToolbarWidgetType.CustomButton)),true);
                scroll.ContentNode.Height=Math.Max(420,y+40);scroll.RecalculateSizes();
            }
            status=new TextNode{Position=ContentStartPosition+new Vector2(0,kind=="组件"?600:470),Size=new(940,28),FontSize=12};status.AttachNode(addon);roots.Add(status);
        }
        private void MoveWidget(MultiToolbarWidgetConfig widget,int direction)
        {
            var items=config.Widgets.Where(item=>item.Side==widget.Side).ToList();int index=items.IndexOf(widget);int target=index+direction;
            if(target<0||target>=items.Count)return;int first=config.Widgets.IndexOf(widget),second=config.Widgets.IndexOf(items[target]);
            (config.Widgets[first],config.Widgets[second])=(config.Widgets[second],config.Widgets[first]);
        }
        private static void Move<T>(List<T> items,T item,int direction){int index=items.IndexOf(item),target=index+direction;if(index<0||target<0||target>=items.Count)return;(items[index],items[target])=(items[target],items[index]);}
        private void OpenDetail(MultiToolbarWidgetConfig widget)
        {
            owner.nativeWidgetDetails?.Dispose();owner.nativeWidgetDetails=new NativeWidgetDetails(owner,config,widget);owner.nativeWidgetDetails.Open();
        }
        internal void Invalidate()=>pending=true;
        protected override void OnUpdate(AtkUnitBase* addon){if(pending){pending=false;Build(addon);}
            foreach(var layout in iconLayouts)layout();
            if(Environment.TickCount64>=nextPluginIcons){nextPluginIcons=Environment.TickCount64+1000;foreach(var refresh in pluginRefresh){try{refresh();}catch(Exception error){DService.Instance().Log.Debug(error,"插件图标等待加载");}}}
            foreach(var item in icons){uint id=item.Read();if(item.Node.IconId!=id){item.Node.IconId=id;item.Node.Size=new(16,16);}item.Node.FitTexture=true;item.Node.Size=new(16,16);item.Node.IsVisible=id>0;}
        }
        protected override void OnFinalize(AtkUnitBase* addon){finalized=true;icons.Clear();iconLayouts.Clear();pluginRefresh.Clear();roots.Clear();base.OnFinalize(addon);}
    }
}
