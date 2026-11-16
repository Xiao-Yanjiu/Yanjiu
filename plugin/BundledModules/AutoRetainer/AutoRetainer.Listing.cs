// SPDX-License-Identifier: GPL-3.0-or-later
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using OmenTools.Extensions;
namespace Yanjiu.Modules;
public sealed unsafe partial class AutoRetainer
{
 private bool listingVisible,listingFinished;
 private uint listingItem;private bool listingHq;
 private InventoryType listingType;private short listingSlot;
 private ulong listingOwner;private int listingOriginal;private long listingDeadline;
 private void TickListingPrice()
 {
  if(!Ready("RetainerSell")){CancelQuote();listingVisible=false;listingFinished=false;return;}
  if(!config.AutoPriceOnListing){CancelQuote();listingFinished=true;return;}
  if(listingFinished)return;
  if(listingVisible&&Environment.TickCount64>=listingDeadline){CancelQuote();listingFinished=true;status="报价查询超时，保留原价格。";return;}
  var agent=AgentRetainer.Instance();var manager=InventoryManager.Instance();var retainers=RetainerManager.Instance();
  if(agent==null||manager==null||retainers==null)return;
  var owner=retainers->GetActiveRetainer();if(owner==null)return;
  var dialog=(AddonRetainerSell*)Addon("RetainerSell");if(dialog==null||dialog->AskingPrice==null)return;
  var inventory=manager->GetInventoryContainer(agent->SellItemInventoryType);
  if(inventory==null||!inventory->IsLoaded||agent->SellItemInventorySlot>=inventory->Size)return;
  var item=inventory->GetInventorySlot(agent->SellItemInventorySlot);
  if(item==null||item->ItemId==0)return;
  var proxy=InfoProxyItemSearch.Instance();if(proxy==null)return;
  if(!listingVisible){
   listingVisible=true;listingItem=item->ItemId;listingHq=item->Flags.HasFlag(InventoryItem.ItemFlags.HighQuality);
   listingType=agent->SellItemInventoryType;listingSlot=(short)agent->SellItemInventorySlot;listingOwner=owner->RetainerId;listingOriginal=dialog->AskingPrice->Value;
   ownRetainers.Clear();for(uint i=0;i<retainers->GetRetainerCount();i++){var r=retainers->GetRetainerBySortedIndex(i);if(r!=null)ownRetainers.Add(r->RetainerId);}
   RequestQuote(proxy,listingItem);
   listingDeadline=Environment.TickCount64+15000;status="正在查询上架物品报价，请等待价格填写完成。";return;
  }
  if(owner->RetainerId!=listingOwner||agent->SellItemInventoryType!=listingType||agent->SellItemInventorySlot!=listingSlot||item->ItemId!=listingItem||item->Flags.HasFlag(InventoryItem.ItemFlags.HighQuality)!=listingHq||dialog->AskingPrice->Value!=listingOriginal||proxy->SearchItemId!=listingItem){CancelQuote();listingFinished=true;status="物品或价格已被手动修改，取消自动填写。";return;}
  if(Environment.TickCount64>=listingDeadline){CancelQuote();listingFinished=true;status="报价查询超时，保留原价格。";return;}
  var list=ReadQuote(proxy,listingItem);if(list==null)return;
  uint minimum=uint.MaxValue;
  foreach(var l in list)if(l.Hq==listingHq&&!l.Mannequin&&!ownRetainers.Contains(l.Owner))minimum=Math.Min(minimum,l.Price);
  CancelQuote();
  listingFinished=true;
  var price=PricePolicy.Calculate(minimum,(uint)Math.Max(0,listingOriginal),config.Undercut,config.MinimumPrice,listingType==InventoryType.RetainerMarket?config.MaxDropPercent:100);
  if(price==null){status="没有有效报价或触发价格保护，保留原价。";return;}
  dialog->AskingPrice->InnerSetValue((int)price.Value,true,false);status="已填写上架价格，请在游戏窗口确认上架。";
 }
}
