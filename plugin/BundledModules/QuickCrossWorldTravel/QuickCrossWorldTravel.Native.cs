// SPDX-License-Identifier: GPL-3.0-or-later
using System.Numerics;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.BaseTypes;
using KamiToolKit.Nodes;
using OmenTools;
namespace Yanjiu.Modules;
public sealed unsafe partial class QuickCrossWorldTravel
{
    private NativeTravelWindow? nativeWindow;
    private bool rebuildNativeWindow;
    private sealed class NativeTravelWindow : NativeAddon
    {
        private readonly QuickCrossWorldTravel module;
        private readonly List<(uint Id,TextButtonNode Button)> buttons=[];
        private TextNode? information;
        private TextButtonNode? confirm;
        private TextButtonNode? returnHome;
        private TextButtonNode? cancel;
        [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
        public NativeTravelWindow(QuickCrossWorldTravel owner)
        {
            module=owner;InternalName="YanjiuNativeCrossWorld";Title="快捷跨界传送";Size=new(800,530);
        }
        protected override void OnSetup(AtkUnitBase* addon,Span<AtkValue> values)
        {
            buttons.Clear();
            new TextButtonNode{Position=ContentStartPosition,Size=new(180,28),String="重新读取服务器",OnClick=()=>{module.LoadWorlds();module.rebuildNativeWindow=true;}}.AttachNode(addon);
            string[] groups=["豆豆柴","陆行鸟","莫古力","猫小胖"];
            for(int column=0;column<groups.Length;column++){
                var group=groups[column];
                new TextNode{Position=ContentStartPosition+new Vector2(column*190,38),Size=new(180,24),String=group,FontSize=14}.AttachNode(addon);
                int row=0;
                foreach(var world in module.worlds.Where(world=>world.Dc==group)){
                    var target=world.Id;
                    var button=new TextButtonNode{Position=ContentStartPosition+new Vector2(column*190,68+row*34),Size=new(180,28),String=world.Name,OnClick=()=>module.selectedWorld=target};
                    button.AttachNode(addon);buttons.Add((target,button));row++;
                }
            }
            returnHome=new TextButtonNode{Position=ContentStartPosition+new Vector2(0,350),Size=new(260,28),String="返回原始大区 / 原服",OnClick=()=>{var player=DService.Instance().ObjectTable.LocalPlayer;if(player!=null)module.selectedWorld=player.HomeWorld.RowId;}};returnHome.AttachNode(addon);
            confirm=new TextButtonNode{Position=ContentStartPosition+new Vector2(280,350),Size=new(180,28),String="确认传送",OnClick=()=>{if(module.pendingTravel==0&&!module.busy&&IsReady()&&DService.Instance().ObjectTable.LocalPlayer!=null)module.pendingTravel=module.selectedWorld;}};confirm.AttachNode(addon);
            cancel=new TextButtonNode{Position=ContentStartPosition+new Vector2(480,350),Size=new(180,28),String="取消传送准备",OnClick=()=>module.Finish("已取消传送准备。")};cancel.AttachNode(addon);
            new TextButtonNode{Position=ContentStartPosition+new Vector2(670,350),Size=new(90,28),String="清除选择",OnClick=()=>module.selectedWorld=0}.AttachNode(addon);
            information=new TextNode{Position=ContentStartPosition+new Vector2(0,390),Size=new(750,80),FontSize=13,TextFlags=TextFlags.WordWrap|TextFlags.MultiLine};information.AttachNode(addon);
        }
        protected override void OnUpdate(AtkUnitBase* addon)
        {
            var player=DService.Instance().ObjectTable.LocalPlayer;bool ready=IsReady();
            foreach(var row in buttons)row.Button.IsEnabled=ready&&player!=null&&!module.busy&&player.CurrentWorld.RowId!=row.Id;
            if(confirm!=null)confirm.IsEnabled=ready&&player!=null&&!module.busy&&module.selectedWorld!=0&&module.pendingTravel==0;
            if(returnHome!=null)returnHome.IsEnabled=ready&&player!=null&&!module.busy&&player.CurrentWorld.RowId!=player.HomeWorld.RowId;
            if(cancel!=null)cancel.IsEnabled=module.busy&&module.stage is TravelStage.WaitingLogout or TravelStage.WaitingTitle or TravelStage.SettlingTitle;
            if(information!=null)information.String="目标 "+module.worlds.FirstOrDefault(world=>world.Id==module.selectedWorld).Name+"\n"+module.status+(ready?"":"\nDCTravelerX 尚未就绪。");
        }
        protected override void OnFinalize(AtkUnitBase* addon){if(ReferenceEquals(module.nativeWindow,this))module.visible=false;base.OnFinalize(addon);}
    }
}
