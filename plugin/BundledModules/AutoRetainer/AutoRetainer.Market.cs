// SPDX-License-Identifier: GPL-3.0-or-later
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using OmenTools.Extensions;
using OmenTools.Interop.Game.AddonEvent;
namespace Yanjiu.Modules;
public sealed unsafe partial class AutoRetainer
{
 private readonly HashSet<ulong> ownRetainers=new();
 private void StartReprice()
 {
  if(steps.Count>0||!Ready("RetainerList"))return;
  var manager=RetainerManager.Instance();if(manager==null)return;ownRetainers.Clear();
  for(uint i=0;i<manager->GetRetainerCount();i++){var r=manager->GetRetainerBySortedIndex(i);if(r!=null)ownRetainers.Add(r->RetainerId);}
  for(uint i=0;i<manager->GetRetainerCount();i++){
   var r=manager->GetRetainerBySortedIndex(i);if(r==null||r->MarketItemCount==0)continue;var id=r->RetainerId;
   AddRetainerSelection(id);
   Add("打开出售列表",()=>Ready("SelectString")&&AddonSelectStringEvent.Select("出售"));
   for(short slot=0;slot<20;slot++)EnqueueReprice(slot,id);
   Add("关闭出售列表",()=>{if(Ready("RetainerSellList"))Addon("RetainerSellList")->Close(true);return true;});
   Add("返回列表",()=>{if(Ready("RetainerList"))return true;if(Ready("SelectString"))Addon("SelectString")->Callback(-1);return false;});
  }
  deadline=Environment.TickCount64+30000;
 }
 private void EnqueueReprice(short slot,ulong retainerId)
 {
  uint itemId=0;bool hq=false;bool requested=false;uint original=0;long queryAt=0;uint? written=null;long writtenAt=0;ulong listingId=0;uint quantity=0;bool verifying=false;
  Add("读取报价并改价",()=>{
   var manager=InventoryManager.Instance();var retainers=RetainerManager.Instance();if(manager==null||retainers==null)return false;
   var active=retainers->GetActiveRetainer();if(active==null||active->RetainerId!=retainerId)return false;
   var inventory=manager->GetInventoryContainer(InventoryType.RetainerMarket);if(inventory==null||!inventory->IsLoaded)return false;
   if(slot<0||slot>=inventory->Size)return true;
   var item=inventory->GetInventorySlot(slot);if(item==null||item->ItemId==0){if(requested)throw new InvalidOperationException("改价目标已消失，无法确认结果。");return true;}
   if(written.HasValue){
    if(item->ItemId!=itemId||item->Quantity!=quantity||item->Flags.HasFlag(InventoryItem.ItemFlags.HighQuality)!=hq)throw new InvalidOperationException("改价后物品变化，结果无法确认。");
    if(Environment.TickCount64-writtenAt<2000)return false;
    var confirmation=InfoProxyItemSearch.Instance();if(confirmation==null)return false;
    if(!verifying){RequestQuote(confirmation,itemId);verifying=true;return false;}
    var verified=ReadQuote(confirmation,itemId);if(verified==null)return false;
    var entry=verified.SingleOrDefault(x=>x.Id==listingId&&x.Owner==retainerId&&x.Hq==hq&&x.Quantity==quantity);
    if(entry.Id==0||entry.Price!=written.Value)throw new InvalidOperationException("服务器返回的挂单价格未确认成功，停止后续改价。");
    if((uint)manager->GetRetainerMarketPrice(slot)!=written.Value)throw new InvalidOperationException("本地价格与服务器报价不一致，停止后续改价。");
    CancelQuote();return true;
   }
   var proxy=InfoProxyItemSearch.Instance();if(proxy==null)return false;
   if(!requested){if(Environment.TickCount64<queryAt)return false;queryAt=Environment.TickCount64+1500;quantity=(uint)item->Quantity;itemId=item->ItemId;hq=item->Flags.HasFlag(InventoryItem.ItemFlags.HighQuality);original=(uint)manager->GetRetainerMarketPrice(slot);
    RequestQuote(proxy,itemId);requested=true;return false;}
   if(item->ItemId!=itemId||item->Quantity!=quantity||item->Flags.HasFlag(InventoryItem.ItemFlags.HighQuality)!=hq)throw new InvalidOperationException("出售栏物品已变化，停止改价。");
   if(proxy->SearchItemId!=itemId)throw new InvalidOperationException("市场查询被其他操作改变，停止改价。");
   var listings=ReadQuote(proxy,itemId);if(listings==null)return false;
   uint lowest=uint.MaxValue;
   foreach(var listing in listings){
    if(listing.Hq!=hq||listing.Mannequin||ownRetainers.Contains(listing.Owner))continue;
    lowest=Math.Min(lowest,listing.Price);
   }
   if(lowest==uint.MaxValue){CancelQuote();return true;}
   var calculated=PricePolicy.Calculate(lowest,original,config.Undercut,config.MinimumPrice,config.MaxDropPercent);
   if(calculated==null){CancelQuote();return true;}var price=calculated.Value;
   if((uint)manager->GetRetainerMarketPrice(slot)!=original)throw new InvalidOperationException("原价已被其他操作修改，停止改价。");
   if(price!=original){
    quantity=(uint)item->Quantity;
    var matches=listings.Where(x=>x.Owner==retainerId&&x.Hq==hq&&x.Price==original&&x.Quantity==quantity&&!x.Mannequin).ToArray();
    if(matches.Length!=1)throw new InvalidOperationException("无法唯一对应本雇员的市场挂单，未改价并停止。");
    listingId=matches[0].Id;
    var atk=RaptureAtkModule.Instance();if(atk==null)return false;manager->SetRetainerMarketPrice(slot,price);written=price;writtenAt=Environment.TickCount64;atk->AgentUpdateFlag|=RaptureAtkModule.AgentUpdateFlags.RetainerMarketInventoryUpdate;}
   CancelQuote();nextTick=Environment.TickCount64+1500;return !written.HasValue;
  });
 }
}
