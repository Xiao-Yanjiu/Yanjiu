// SPDX-License-Identifier: GPL-3.0-or-later
using KamiToolKit.Nodes;
using KamiToolKit.UiOverlay;
using KamiToolKit.Enums;
using FFXIVClientStructs.FFXIV.Component.GUI;
using OmenTools;
using Lumina.Text.ReadOnly;
using KamiToolKit.Nodes.Simplified;
using Dalamud.Interface;
namespace OmniToolbox.TreePublic;

public sealed partial class ToolbarRenderer
{
    private OverlayController? hudOverlay;
    private bool hudFailed;
    private readonly Dictionary<object,HudEntry> hudEntries=[];
    private sealed record HudSnapshot(Vector2 Position,Vector2 Size,ReadOnlySeString Text,uint Icon,Vector2? Symbol,float FontSize,bool Hovered,bool Pressed,long At,Vector4? Background=null,string? ImagePath=null,bool ReserveIcon=false,MultiToolbarWidgetType? Kind=null,byte[]? Slots=null,int QuickIndex=-1);
    private readonly Dictionary<object,HudSnapshot> hudSnapshots=[];
    private bool DrawNativeHudBackground(Vector2 min,Vector2 max)
    {
        if(hudFailed)return false;
        var color=ToolbarTheme.Background;color.W*=Math.Clamp(config.BarBackgroundOpacity,0,1);
        hudSnapshots["bar-background"]=new(min-ImGui.GetMainViewport().Pos,max-min,"",0,null,16,false,false,Environment.TickCount64,color);
        return hudEntries.TryGetValue("bar-background",out var entry)&&entry.IsAttached;
    }
    private bool DrawNativeHudEntry(MultiToolbarWidgetConfig widget,string text,uint icon,Vector2 size)
    {
        bool showIcon=widget.ShowIcon||IsIconOnlyWidget(widget.Type)||widget.Type==MultiToolbarWidgetType.MarkerControl;
        if(!IsIconOnlyWidget(widget.Type)&&widget.ShowIcon&&TryGetCommandGlyph(widget.IconGlyph??"",out var glyph))text=((char)glyph)+" "+text;
        bool originalIcon=showIcon&&widget.Type==MultiToolbarWidgetType.MarkerControl;
        Vector2? symbol=showIcon&&icon==0?widget.Type switch{
            MultiToolbarWidgetType.Volume=>GetVolumeIcon()==FontAwesomeIcon.VolumeMute?new(84,56):new(56,56),
            MultiToolbarWidgetType.PluginList=>new(168,84),
            MultiToolbarWidgetType.CommandList or MultiToolbarWidgetType.DtrList=>new(0,28),
            MultiToolbarWidgetType.WalkingIndicator=>new(144,112),
            MultiToolbarWidgetType.ToolbarPin=>new(72,136),_=>null}:null;
        var path=showIcon?(ToolbarIcons.GetBuiltInIconPath(icon)??(icon==0&&!IsIconOnlyWidget(widget.Type)?widget.IconFilePath:null)):null;
        bool ready=DrawNativeHudText(widget,text,originalIcon?0:string.IsNullOrWhiteSpace(path)?icon:0,size,originalIcon?null:string.IsNullOrWhiteSpace(path)?symbol:null,path,originalIcon,widget.Type);
        if(ready&&originalIcon){
            float side=Math.Min(size.Y-4,Math.Clamp(ScaleToolbar(20),12,24)+3);
            var position=ImGui.GetItemRectMin()+new Vector2(3,(size.Y-side)/2+(ImGui.IsItemActive()?1:0));
            var glyphText=(widget.Type==MultiToolbarWidgetType.Volume?GetVolumeIcon():FontAwesomeIcon.MapSigns).ToIconString();
            using var font=ImRaii.PushFont(UiBuilder.IconFont);
            var glyphSize=ImGui.CalcTextSize(glyphText)*(side/ImGui.GetFontSize());
            position+=(new Vector2(side)-glyphSize)/2;
            DrawToolbarTextOutline(ImGui.GetWindowDrawList(),glyphText,position,side);
            ImGui.GetWindowDrawList().AddText(ImGui.GetFont(),side,position,ImGui.GetColorU32(ToolbarTheme.Text),glyphText);
        }
        return ready;
    }
    private bool DrawNativeHudText(object key,ReadOnlySeString text,uint icon,Vector2 size,Vector2? symbol=null,string? imagePath=null,bool reserveIcon=false,MultiToolbarWidgetType? kind=null)
    {
        if(hudFailed)return false;
        hudSnapshots[key]=new(ImGui.GetItemRectMin()-ImGui.GetMainViewport().Pos,size,text,icon,symbol,
            Math.Clamp(ScaleToolbar(20),12,24),ImGui.IsItemHovered(),ImGui.IsItemActive(),Environment.TickCount64,ImagePath:imagePath,ReserveIcon:reserveIcon,Kind:kind,Slots:kind==MultiToolbarWidgetType.InventorySpace?inventoryHudSlots:null,QuickIndex:kind==MultiToolbarWidgetType.QuickCommands&&ImGui.IsItemHovered()?QuickCategoryIndex(ImGui.GetMousePos().X,ImGui.GetItemRectMin().X,size.X):-1);
        // Keep the existing hit area and handlers. Native nodes deliberately have no collision.
        return hudEntries.TryGetValue(key,out var entry)&&entry.IsAttached;
    }
    private void PumpNativeHud()
    {
        if(hudFailed)return;
        try{PumpNativeHudCore();}
        catch(Exception error){hudFailed=true;ReleaseNativeHud();DService.Instance().Log.Error(error,"工具栏原生入口显示失败，保留原入口操作");}
    }
    private bool IsPointerOverQuickCommandBar(float x,float y)
    {
        foreach(var state in hudSnapshots.Values){
            if(state.Kind!=MultiToolbarWidgetType.QuickCommands||Environment.TickCount64-state.At>250)continue;
            if(x>=state.Position.X&&x<state.Position.X+state.Size.X&&y>=state.Position.Y&&y<state.Position.Y+state.Size.Y)return true;
        }
        return false;
    }
    private void PumpNativeHudCore()
    {
        if(runtimeLifetime is null||runtimeLifetime.IsStopping)return;
        UpdateInventoryHudSlots();
        foreach(var widget in hudEntries.Keys.Where(w=>w is MultiToolbarWidgetConfig configWidget&&!config.Widgets.Contains(configWidget)||!hudSnapshots.TryGetValue(w,out var state)||Environment.TickCount64-state.At>2000).ToArray()){
            hudOverlay?.RemoveNode(hudEntries[widget]);hudEntries.Remove(widget);hudSnapshots.Remove(widget);
        }
        foreach(var pair in hudSnapshots.ToArray()){
            if(Environment.TickCount64-pair.Value.At>250)continue;
            if(!hudEntries.TryGetValue(pair.Key,out var entry)){
                hudOverlay??=new OverlayController();
                entry=new HudEntry(()=>hudSnapshots.GetValueOrDefault(pair.Key),(pair.Key as MultiToolbarWidgetConfig)?.Type);
                hudOverlay.AddNode(entry);hudEntries.Add(pair.Key,entry);
            }
        }
    }
    private void ReleaseNativeHud()
    {
        hudOverlay?.Dispose();hudOverlay=null;hudEntries.Clear();hudSnapshots.Clear();inventoryHudSlots=null;inventoryHudRefreshAt=0;
    }
    private sealed class HudEntry:OverlayNode
    {
        public override OverlayLayer OverlayLayer=>OverlayLayer.BehindUserInterface;
        private readonly Func<HudSnapshot?> snapshot;
        private readonly IconImageNode icon;
        private readonly ColorImageNode background;
        private readonly SimpleImageNode symbol;
        private readonly ImGuiImageNode fileIcon;
        private Dalamud.Bindings.ImGui.ImTextureID fileTexture;
        private readonly TextNode text;
        private readonly TextNode heavyTextX,heavyTextY;
        private uint iconId;
        private readonly List<ColorImageNode> bagDots=[];
        private readonly List<CircleButtonNode> quickButtons=[];
        private readonly int[] quickAnimations=Enumerable.Repeat(-1,7).ToArray();
        internal unsafe bool IsAttached=>!IsDisposed&&Node!=null&&Node->ParentNode!=null;
        internal HudEntry(Func<HudSnapshot?> read,MultiToolbarWidgetType? kind)
        {
            snapshot=read;
            background=new ColorImageNode{IsVisible=false};background.AttachNode(this);
            icon=new IconImageNode{FitTexture=true,Size=new(20,20),IsVisible=false};icon.AttachNode(this);
            symbol=new SimpleImageNode{TexturePath="ui/uld/CircleButtons.tex",TextureSize=new(28,28),FitTexture=true,IsVisible=false};symbol.AttachNode(this);
            fileIcon=new ImGuiImageNode{FitTexture=true,IsVisible=false};fileIcon.AttachNode(this);
            text=new TextNode{FontSize=16,TextColor=new(1,.98f,.9f,1),TextOutlineColor=new(.12f,.1f,.08f,1)};
            text.AddTextFlags(TextFlags.Edge|TextFlags.Bold|TextFlags.MultiLine);text.AttachNode(this);
            heavyTextX=new TextNode{TextFlags=TextFlags.Bold|TextFlags.MultiLine};heavyTextX.AttachNode(this);
            heavyTextY=new TextNode{TextFlags=TextFlags.Bold|TextFlags.MultiLine};heavyTextY.AttachNode(this);
            if(kind==MultiToolbarWidgetType.InventorySpace)for(int i=0;i<140;i++){
                var dot=new ColorImageNode();dot.AttachNode(this);bagDots.Add(dot);
            }
            if(kind==MultiToolbarWidgetType.QuickCommands)for(int i=0;i<7;i++){
                var quickButton=new CircleButtonNode{Icon=CircleButtonIcon.GearCog};quickButton.AttachNode(this);quickButtons.Add(quickButton);
                quickButton.ImageNode.TexturePath="ui/uld/MainCommand_Icon.tex";
                quickButton.ImageNode.TextureCoordinates=new((i+1)*32,0);quickButton.ImageNode.TextureSize=new(32);quickButton.ImageNode.ImageNodeFlags=0;
                // The existing toolbar hit area routes input; only render/animate
                // this native button, so two input systems cannot both fire it.
                const NodeFlags input=NodeFlags.HasCollision|NodeFlags.RespondToMouse|NodeFlags.Focusable|NodeFlags.EmitsEvents;
                quickButton.RemoveNodeFlags(input);quickButton.CollisionNode.RemoveNodeFlags(input);
            }
        }
        protected override void OnUpdate()
        {
            var state=snapshot();IsVisible=state!=null&&Environment.TickCount64-state.At<=250;
            if(!IsVisible||state==null)return;
            heavyTextX.IsVisible=false;heavyTextY.IsVisible=false;
            Position=state.Position;Size=state.Size;
            background.IsVisible=state.Background!=null;
            if(state.Background is {} color){background.Size=Size;background.Color=color;}
            if(state.Kind==MultiToolbarWidgetType.InventorySpace){
                icon.IsVisible=false;symbol.IsVisible=false;fileIcon.IsVisible=false;text.IsVisible=false;
                float height=Math.Min(state.Size.Y-4,state.FontSize+3),step=height/7,gutter=step;
                float width=4*5*step+3*gutter,left=(state.Size.X-width)/2,top=(state.Size.Y-height)/2;
                for(int i=0;i<bagDots.Count;i++){
                    int page=i/35,index=i%35;var dot=bagDots[i];
                    dot.Position=new(left+page*(5*step+gutter)+(index%5)*step,top+(index/5)*step);
                    dot.Size=new(Math.Max(1,step*.7f));
                    byte value=state.Slots is {Length:140}?state.Slots[i]:(byte)0;
                    dot.Color=value switch{2=>new(.2f,.75f,.88f,1),1=>new(.6f,.85f,.15f,1),_=>new(.2f,.2f,.2f,.9f)};
                }
                return;
            }
            if(quickButtons.Count>0){
                float slotWidth=state.Size.X/7,buttonSize=Math.Min(Math.Min(state.Size.Y-2,slotWidth-1),state.FontSize+8);
                for(int i=0;i<quickButtons.Count;i++){
                var quickButton=quickButtons[i];
                quickButton.Size=new(buttonSize);quickButton.Position=new(i*slotWidth+(slotWidth-buttonSize)/2,(state.Size.Y-buttonSize)/2);
                // Atlas slices must keep their source dimensions. AutoFit samples
                // the complete atlas, not the individual circular icon.
                quickButton.ImageNode.ImageNodeFlags=0;quickButton.ImageNode.TextureCoordinates=new((i+1)*32,0);
                quickButton.ImageNode.TextureSize=new(32);quickButton.ImageNode.Size=new(32);quickButton.ImageNode.Scale=new(buttonSize/32);
                int animation=state.QuickIndex==i?(state.Pressed?3:2):4;
                if(animation!=quickAnimations[i]){quickButton.Timeline?.PlayAnimation(animation);quickAnimations[i]=animation;}
                }
                icon.IsVisible=false;symbol.IsVisible=false;fileIcon.IsVisible=false;text.IsVisible=false;return;
            }
            float side=Math.Min(state.Size.Y-4,state.FontSize+3),gap=state.Icon==0&&state.Symbol==null&&string.IsNullOrWhiteSpace(state.ImagePath)&&!state.ReserveIcon?0:side+5;
            icon.IsVisible=state.Icon!=0;
            if(state.Icon!=0&&iconId!=state.Icon){icon.IconId=state.Icon;iconId=state.Icon;}
            var textureSize=icon.ActualTextureSize;
            if(textureSize.X>0&&textureSize.Y>0){icon.TextureCoordinates=state.Icon==60560?textureSize*.25f:Vector2.Zero;icon.TextureSize=state.Icon==60560?textureSize*.5f:textureSize;}
            float renderedSide=state.Icon==60560?side*2:side;
            icon.FitTexture=true;icon.Size=new(renderedSide,renderedSide);icon.Position=new(state.Text.IsEmpty?(state.Size.X-renderedSide)/2:3,(state.Size.Y-renderedSide)/2+(state.Pressed?1:0));
            symbol.IsVisible=state.Symbol!=null;
            if(state.Symbol is {} coordinates){
                float sourceSide=coordinates.Y>=112?24:28;
                float symbolSide=state.Kind==MultiToolbarWidgetType.Volume?side*1.4f:side;
                symbol.ImageNodeFlags=0;symbol.TextureCoordinates=coordinates;symbol.TextureSize=new(sourceSide);
                symbol.Size=new(sourceSide);symbol.Scale=new(symbolSide/sourceSide);
                symbol.Position=state.Kind==MultiToolbarWidgetType.Volume?new((state.Size.X-symbolSide)/2,(state.Size.Y-symbolSide)/2+(state.Pressed?1:0)):icon.Position;
            }
            fileIcon.IsVisible=false;
            if(!string.IsNullOrWhiteSpace(state.ImagePath)){
                var texture=DService.Instance().Texture.GetFromFile(state.ImagePath).GetWrapOrDefault();
                if(PluginIconResolver.IsUsableTexture(texture)){
                    if(fileTexture!=texture.Handle){fileIcon.LoadTexture(texture.CreateWrapSharingLowLevelResource());fileIcon.TextureSize=texture.Size;fileTexture=texture.Handle;}
                    fileIcon.FitTexture=true;fileIcon.Size=new(side,side);fileIcon.Position=icon.Position;fileIcon.IsVisible=true;
                }
            }
            text.String=state.Text;text.FontSize=(uint)Math.Round(state.FontSize);
            text.Position=new(3+gap,state.Pressed?1:0);text.Size=new(Math.Max(0,state.Size.X-gap-3),state.Size.Y);
            text.TextColor=state.Hovered?new(1,.87f,.57f,1):new(1,.98f,.9f,1);
            // CJK game fonts can ignore the Bold flag. Thicken the fill with
            // native text nodes; keep a single outline to avoid dark halos.
            UpdateHeavyText(heavyTextX,state);UpdateHeavyText(heavyTextY,state);
            heavyTextX.Position=text.Position+new Vector2(.85f,0);
            heavyTextY.Position=text.Position+new Vector2(0,.55f);
        }
        private void UpdateHeavyText(TextNode copy,HudSnapshot state)
        {
            copy.IsVisible=!state.Text.IsEmpty;copy.String=state.Text;copy.FontSize=text.FontSize;
            copy.LineSpacing=text.LineSpacing;copy.Size=text.Size;copy.TextColor=text.TextColor;
        }
    }
}
