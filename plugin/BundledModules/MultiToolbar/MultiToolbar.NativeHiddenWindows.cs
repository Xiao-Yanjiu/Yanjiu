// SPDX-License-Identifier: GPL-3.0-or-later
using KamiToolKit.BaseTypes;
using KamiToolKit.Nodes;
using FFXIVClientStructs.FFXIV.Component.GUI;
using OmenTools;
namespace OmniToolbox.TreePublic;
public sealed partial class ToolbarRenderer
{
    private NativeHiddenWindows? nativeHiddenWindows;
    private string hiddenPickName="";
    private void PumpNativeHiddenWindows()
    {
        if(barID!="main")return;
        try{
            if(hiddenWindowManagerOpen){nativeHiddenWindows??=new(this);if(!nativeHiddenWindows.IsOpen)nativeHiddenWindows.Open();}
            else if(nativeHiddenWindows!=null){nativeHiddenWindows.Dispose();nativeHiddenWindows=null;}
        }catch(Exception error){hiddenWindowManagerOpen=false;DService.Instance().Log.Error(error,"隐藏窗口管理器更新失败");}
    }
    private sealed unsafe class NativeHiddenWindows:NativeAddon
    {
        private readonly ToolbarRenderer owner;
        private ScrollingNode<ResNode>? scroll;
        private readonly List<ResNode> rows=[];
        private TextNode? status;
        private TextButtonNode? pick;
        private string filter="",signature="";
        private bool closed;
        [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
        internal NativeHiddenWindows(ToolbarRenderer owner){this.owner=owner;InternalName="YanjiuToolbarHiddenWindows";Title="隐藏窗口管理";Size=new(640,470);}
        private void Run(System.Action action){if(closed||owner.runtimeLifetime is not {IsStopping:false})return;try{action();owner.SaveNativeConfiguration();signature="";}catch(Exception error){if(status!=null)status.String=error.GetBaseException().Message;}}
        protected override void OnSetup(AtkUnitBase* addon,Span<AtkValue> args)
        {
            closed=false;signature="";rows.Clear();
            new TextInputNode{Position=ContentStartPosition,Size=new(410,28),PlaceholderString="筛选已收纳窗口",OnInputReceived=value=>{filter=value.ToString();signature="";}}.AttachNode(addon);
            pick=new TextButtonNode{Position=ContentStartPosition+new System.Numerics.Vector2(420,0),Size=new(165,28),String="选取窗口",OnClick=()=>Run(()=>{owner.pickingHiddenWindow=!owner.pickingHiddenWindow;owner.hiddenPickName="";})};pick.AttachNode(addon);
            scroll=new ScrollingNode<ResNode>{Position=ContentStartPosition+new System.Numerics.Vector2(0,40),Size=new(590,320),ScrollSpeed=34};scroll.AttachNode(addon);
            status=new TextNode{Position=ContentStartPosition+new System.Numerics.Vector2(0,374),Size=new(590,40),FontSize=13};status.AttachNode(addon);Refresh();
        }
        private void Refresh()
        {
            if(scroll==null)return;
            var names=owner.config.HiddenWindows.Where(name=>name.Contains(filter,StringComparison.OrdinalIgnoreCase)).OrderBy(name=>name,StringComparer.OrdinalIgnoreCase).ToArray();
            string current=string.Join("\n",names.Select(name=>name+":"+owner.revealedWindows.Contains(name)));
            pick!.String=owner.pickingHiddenWindow?"取消选取":"选取窗口";
            status!.String=owner.pickingHiddenWindow?"左键选择窗口，右键或 Esc 取消："+owner.hiddenPickName:"点击名称或显示按钮可临时显示／隐藏；移除会取消收纳。";
            if(signature==current)return;signature=current;float offset=scroll.ScrollBarNode.ScrollPosition;
            foreach(var row in rows)row.Dispose();rows.Clear();
            for(int i=0;i<names.Length;i++){
                string name=names[i];var root=new ResNode{Position=new(0,i*34),Size=new(570,30)};root.AttachNode(scroll.ContentNode);rows.Add(root);
                void Toggle(){owner.nextHiddenWindowRefresh=0;if(!owner.revealedWindows.Add(name))owner.revealedWindows.Remove(name);}
                new TextButtonNode{Size=new(385,28),String=name,TextTooltip=name,OnClick=()=>Run(Toggle)}.AttachNode(root);
                new TextButtonNode{Position=new(395,0),Size=new(80,28),String=owner.revealedWindows.Contains(name)?"隐藏":"显示",OnClick=()=>Run(Toggle)}.AttachNode(root);
                new TextButtonNode{Position=new(485,0),Size=new(75,28),String="移除",OnClick=()=>Run(()=>{owner.config.HiddenWindows.Remove(name);owner.revealedWindows.Remove(name);owner.nextHiddenWindowRefresh=0;})}.AttachNode(root);
            }
            scroll.ContentNode.Height=Math.Max(320,names.Length*34);scroll.RecalculateSizes();scroll.ScrollBarNode.ScrollPosition=Math.Min(offset,scroll.ScrollBarNode.ScrollMaxPosition);
        }
        protected override void OnUpdate(AtkUnitBase* addon)=>Refresh();
        protected override void OnFinalize(AtkUnitBase* addon){closed=true;owner.hiddenWindowManagerOpen=false;owner.pickingHiddenWindow=false;rows.Clear();scroll=null;base.OnFinalize(addon);}
    }
}
