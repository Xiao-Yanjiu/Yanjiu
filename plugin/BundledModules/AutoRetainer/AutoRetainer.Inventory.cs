// SPDX-License-Identifier: GPL-3.0-or-later
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using OmenTools.Extensions;
using OmenTools.Interop.Game.AddonEvent;
using OmenTools.Interop.Game.AgentEvent;
namespace Yanjiu.Modules;
public sealed unsafe partial class AutoRetainer
{
 private bool mergeSubmitted,mergeCancelled;
 private void OnMergeClosed(Dalamud.Game.Addon.Lifecycle.AddonEvent type,Dalamud.Game.Addon.Lifecycle.AddonArgTypes.AddonArgs args){
  if(merging&&(args.AddonName=="RetainerItemTransferProgress"||!mergeSubmitted))mergeCancelled=true;
 }
 private void StartInventory(bool crystals)
 {
  if(steps.Count>0||!Ready("RetainerList"))return;
  var manager=RetainerManager.Instance();if(manager==null)return;
  for(uint i=0;i<manager->GetRetainerCount();i++){
   var r=manager->GetRetainerBySortedIndex(i);if(r==null||(!crystals&&r->ItemCount==0))continue;var id=r->RetainerId;
   AddRetainerSelection(id);
   if(crystals){
    Add("打开出售列表",()=>Ready("SelectString")&&AddonSelectStringEvent.Select("出售"));
    for(short slot=0;slot<18;slot++)EnqueueCrystal(slot,id);
    Add("关闭出售列表",()=>{if(Ready("RetainerSellList"))Addon("RetainerSellList")->Close(true);return true;});
   }else{
    Add("打开道具管理",()=>Ready("SelectString")&&AddonSelectStringEvent.Select("道具管理"));
    Dictionary<(uint,uint),long>? beforeMerge=null;
    Add("合并同类道具",()=>{var agents=AgentModule.Instance();if(agents==null)return false;var a=agents->GetAgentByInternalId(AgentId.Retainer);if(a==null||!a->IsAgentActive())return false;beforeMerge=MergeInventorySnapshot();merging=true;mergeSubmitted=false;mergeCancelled=false;noMergeItems=false;AgentId.Retainer.SendEvent(0,0);return true;});
    bool submitted=false,progressSeen=false;long missingSince=0;
    Add("等待合并完成",()=>{
     if(noMergeItems){merging=false;return true;}
     if(!submitted&&Ready("RetainerItemTransferList")){mergeSubmitted=true;submitted=true;Addon("RetainerItemTransferList")->Callback(1);return false;}
     if(Ready("RetainerItemTransferProgress")){progressSeen=true;missingSince=0;var a=Addon("RetainerItemTransferProgress");if(a->AtkValues==null||a->AtkValuesCount<=2||(int)a->AtkValues[2].Type!=7||!float.IsFinite(a->AtkValues[2].Float)||a->AtkValues[2].Float<1)return false;merging=false;a->Callback(-2);if(Ready("RetainerItemTransferProgress"))Addon("RetainerItemTransferProgress")->Close(true);return true;}
     if(submitted&&!Ready("RetainerItemTransferList")&&!Ready("RetainerItemTransferProgress")){
      if(missingSince==0)missingSince=Environment.TickCount64;
      if(Environment.TickCount64-missingSince>3000)throw new InvalidOperationException(progressSeen?"合并进度窗口被关闭，未确认完成，已停止。":"合并已取消或未启动，已停止后续任务。");
     }else missingSince=0;
     return false;
    });
    Add("核对合并前后物品总量",()=>{
     if(beforeMerge==null)throw new InvalidOperationException("缺少合并前快照，停止确认。");
     var after=MergeInventorySnapshot();
     return beforeMerge.Count==after.Count&&beforeMerge.All(x=>after.TryGetValue(x.Key,out var count)&&count==x.Value);
    });
    Add("关闭道具管理",()=>{var agents=AgentModule.Instance();if(agents==null)return false;var a=agents->GetAgentByInternalId(AgentId.Retainer);if(a==null)return false;
     var units=FFXIVClientStructs.FFXIV.Client.UI.RaptureAtkUnitManager.Instance();if(units==null)return false;
     var inventoryAgent=agents->GetAgentByInternalId(AgentId.Inventory);
     if(inventoryAgent!=null&&inventoryAgent->IsAgentActive()){
      var playerWindow=units->GetAddonById((ushort)inventoryAgent->GetAddonId());if(playerWindow!=null)playerWindow->Callback(-1);
     }
     var retainerWindow=units->GetAddonById((ushort)a->GetAddonId());if(retainerWindow!=null)retainerWindow->Close(true);
     AgentId.Retainer.SendEvent(0,-1);return true;});
   }
   Add("返回雇员列表",()=>{if(Ready("RetainerList"))return true;if(Ready("SelectString"))Addon("SelectString")->Callback(-1);return false;});
  }
  deadline=Environment.TickCount64+30000;
 }
 private void EnqueueCrystal(short slot,ulong owner)
 {
  bool sent=false;uint original=0,itemId=0,quantity=0;ushort destination=0;
  Add("上架超出保留数量的水晶",()=>{
   var m=InventoryManager.Instance();var rm=RetainerManager.Instance();if(m==null||rm==null)return false;
   var retainer=rm->GetActiveRetainer();if(retainer==null||retainer->RetainerId!=owner)return false;
   var bag=m->GetInventoryContainer(InventoryType.Crystals);var market=m->GetInventoryContainer(InventoryType.RetainerMarket);
   if(bag==null||market==null||!bag->IsLoaded||!market->IsLoaded)return false;
   if(slot<0||slot>=bag->Size){if(sent)throw new InvalidOperationException("水晶槽位变化，无法确认上架结果。");return true;}var item=bag->GetInventorySlot(slot);if(item==null){if(sent)throw new InvalidOperationException("水晶槽位不可读取，无法确认上架结果。");return true;}
   if(sent){if(destination>=market->Size)throw new InvalidOperationException("出售栏容量变化，停止确认上架。");var listed=market->GetInventorySlot(destination);return item->ItemId==itemId&&item->Quantity==original-quantity&&listed!=null&&listed->ItemId==itemId&&listed->Quantity==quantity;}
   if(item->ItemId==0||item->Quantity<=config.CrystalKeep)return true;
   bool found=false;for(ushort j=0;j<market->Size;j++){var target=market->GetInventorySlot(j);if(target!=null&&target->ItemId==0){destination=j;found=true;break;}}
   if(!found)throw new InvalidOperationException("雇员出售栏已满，停止上架水晶。");original=(uint)item->Quantity;itemId=item->ItemId;quantity=original-(uint)config.CrystalKeep;
   m->MoveToRetainerMarket(InventoryType.Crystals,(ushort)slot,InventoryType.RetainerMarket,destination,quantity,(uint)config.CrystalPrice);
   sent=true;return false;
  });
 }
}
