// SPDX-License-Identifier: GPL-3.0-or-later
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using OmenTools;
using OmenTools.Extensions;
using OmenTools.Interop.Game.AddonEvent;
namespace Yanjiu.Modules;
public sealed unsafe partial class AutoRetainer
{
 private void EnqueueWithdraw(){
  uint beforePlayer=0,beforeRetainer=0,amount=0;bool sent=false;
  Add("打开金币管理",()=>Ready("SelectString")&&AddonSelectStringEvent.Select("金币管理"));
  Add("取出金币并核对双方余额",()=>{
   var inv=InventoryManager.Instance();var rm=RetainerManager.Instance();if(inv==null||rm==null)return false;
   var r=rm->GetActiveRetainer();if(r==null)return false;
   if(sent){
    var player=inv->GetGil();
    if(OperationResults.Withdraw(beforePlayer,beforeRetainer,amount,player,r->Gil))return true;
    if(player<beforePlayer||r->Gil>beforeRetainer)throw new InvalidOperationException("金币余额变化与取出方向不符，已停止；请检查游戏金币管理窗口。");
    return false;
   }
   if(!Ready("Bank"))return false;
   var bank=Addon("Bank");if(bank==null||bank->AtkValues==null||bank->AtkValuesCount<=6)throw new InvalidOperationException("金币窗口数据结构不符，停止操作。");
   beforePlayer=inv->GetGil();beforeRetainer=r->Gil;
   if(beforePlayer>=999999999)throw new InvalidOperationException("角色金币已达上限，停止取出金币。");
   amount=Math.Min(beforeRetainer,999999999-beforePlayer);
   if(amount==0){AddonBankEvent.ClickCancel();return true;}
   if(AddonBankEvent.RetainerGilAmount!=(int)beforeRetainer)throw new InvalidOperationException("金币窗口与当前雇员余额不一致，停止取出。");
   AddonBankEvent.SetNumber(amount);AddonBankEvent.ClickConfirm();sent=true;return false;
  });
  Add("关闭已核对的金币窗口",()=>{if(Ready("Bank"))AddonBankEvent.ClickCancel();return !Ready("Bank");});
 }
 private static void CheckVentureResources(uint task,bool checkBag){
  var inv=InventoryManager.Instance();if(inv==null)throw new InvalidOperationException("物品数据尚未就绪。");
  var sheet=DService.Instance().Data.GetExcelSheet<Lumina.Excel.Sheets.RetainerTask>();
  if(!sheet.TryGetRow(task,out var row)||row.VentureCost==0)throw new InvalidOperationException("无法确定探险币需求，停止自动派遣。");
  if(inv->GetInventoryItemCount(21072)<row.VentureCost)throw new InvalidOperationException($"探险币不足，需要 {row.VentureCost} 枚；停止领取与派遣。");
  if(!checkBag)return;
  int empty=0;
  foreach(var type in new[]{InventoryType.Inventory1,InventoryType.Inventory2,InventoryType.Inventory3,InventoryType.Inventory4}){
   var bag=inv->GetInventoryContainer(type);if(bag==null||!bag->IsLoaded)throw new InvalidOperationException("背包数据未加载，停止领取。");
   for(int i=0;i<bag->Size;i++){var slot=bag->GetInventorySlot(i);if(slot!=null&&slot->ItemId==0)empty++;}
  }
  // Conservative: do not guess whether unknown rewards can stack into occupied slots.
  if(empty<2)throw new InvalidOperationException("背包空间不足：请至少留出两个空格后重新运行。");
 }
 private void EnqueueVenture(uint task,uint previousCompletion){
  int venturesBefore=0;
  Add("检查背包与探险币",()=>{
   var rm=RetainerManager.Instance();if(rm==null)return false;var active=rm->GetActiveRetainer();if(active==null)return false;
   if(active->VentureId!=task||active->VentureComplete!=previousCompletion)throw new InvalidOperationException("探险任务已被其他操作更改，停止领取与派遣。");
   CheckVentureResources(task,true);return true;
  });
  Add("确认探险完成",()=>Ready("SelectString")&&AddonSelectStringEvent.Select("结束"));
  Add("收取并重新派遣",()=>{if(!Ready("RetainerTaskResult"))return false;CheckVentureResources(task,true);Addon("RetainerTaskResult")->Callback(14);return true;});
  Add("确认派遣",()=>{
   if(!Ready("RetainerTaskAsk"))return false;CheckVentureResources(task,false);
   venturesBefore=InventoryManager.Instance()->GetInventoryItemCount(21072);
   Addon("RetainerTaskAsk")->Callback(12);return true;
  });
  Add("核对派遣结果与探险币扣除",()=>{
   var rm=RetainerManager.Instance();var inv=InventoryManager.Instance();if(rm==null||inv==null)return false;
   var r=rm->GetActiveRetainer();if(r==null)return false;
   var cost=DService.Instance().Data.GetExcelSheet<Lumina.Excel.Sheets.RetainerTask>().GetRow(task).VentureCost;
   return OperationResults.Venture(task,r->VentureId,previousCompletion,r->VentureComplete,Framework.GetServerTime(),venturesBefore,inv->GetInventoryItemCount(21072),cost,Ready("RetainerTaskAsk")||Ready("RetainerTaskResult"));
  });
 }
}
