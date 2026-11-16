// SPDX-License-Identifier: GPL-3.0-or-later
using Dalamud.Game.Network.Structures;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using OmenTools;
using OmenTools.Extensions;
namespace Yanjiu.Modules;
public sealed unsafe partial class AutoRetainer
{
 private readonly object quoteLock=new();
 private readonly QuoteReceipt receipt=new();
 private byte requestedSequence,sequenceAfterSend;
 private void CancelQuote(){lock(quoteLock)receipt.Cancel();}
 private void OnOfferings(IMarketBoardCurrentOfferings data){
  var rows=data.ItemListings.Select(x=>new QuoteReceipt.Quote(x.ListingId,x.ItemId,x.IsHq,x.OnMannequin,x.RetainerId,x.PricePerUnit,x.ItemQuantity)).ToArray();
  lock(quoteLock)receipt.Receive(data.RequestId,rows);
 }
 private bool RequestQuote(InfoProxyItemSearch* proxy,uint item){
  // Never hold a managed lock across native calls that may dispatch network callbacks.
  if(proxy->WaitingForListings)throw new InvalidOperationException("已有市场请求进行中，停止本次自动查询。");
  // Match DR cleanup ordering, but only after rejecting an in-flight request.
  proxy->EndRequest();
  requestedSequence=proxy->NextRequestId;
  lock(quoteLock){
   if(!receipt.Begin(requestedSequence,item))throw new InvalidOperationException("报价编号已使用，无法排除旧响应，停止自动定价。");
  }
  proxy->SearchItemId=item;
  if(!proxy->RequestData()){CancelQuote();throw new InvalidOperationException("市场请求未接受，保留原价。");}
  sequenceAfterSend=proxy->NextRequestId;
  return true;
 }
 private QuoteReceipt.Quote[]? ReadQuote(InfoProxyItemSearch* proxy,uint item){
  if(proxy->NextRequestId!=sequenceAfterSend)throw new InvalidOperationException("等待报价期间有新的市场请求插入，停止自动定价。");
  if(proxy->SearchItemId!=item)throw new InvalidOperationException("市场查询目标已改变，停止自动定价。");
  if(proxy->WaitingForListings||!proxy->IsFullyReceived(item))return null;
  lock(quoteLock){if(receipt.Invalid)throw new InvalidOperationException("收到不属于本次请求或内容冲突的报价，已停止自动定价。");return receipt.Read(proxy->CurrentRequestId,item,(int)proxy->EntryCount);}
 }
}
