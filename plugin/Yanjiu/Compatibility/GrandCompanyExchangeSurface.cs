// SPDX-License-Identifier: GPL-3.0-or-later
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using OmenTools.Extensions;
using OmenTools.Interop.Game.Helpers;
namespace DailyRoutines.Extensions;
public enum GrandCompanyObservation { Pending,Complete,Invalid }
public sealed record GrandCompanyOffer(uint ItemID,uint UnitCost,byte Company,uint RequiredRank,byte RankTab,byte CategoryTab,int Count,uint StackSize,bool Unique);
public sealed record GrandCompanyVisibleRow(int Index,uint ItemID,uint UnitCost,uint RequiredRank,bool CurrencyDialog);
/// <summary>Uses the live addon contract; never casts an unpublished event-handler structure.</summary>
public sealed class GrandCompanyExchangeSurface
{
    private GrandCompanyOffer? offer;
    private int remaining,pending;
    private ulong beforeCount;
    private uint beforeSeals;
    private long deadline,ackDeadline,nextSelection;
    public static int FindOffer(IEnumerable<GrandCompanyVisibleRow> rows,GrandCompanyOffer expected){
        var matches=rows.Where(r=>r.ItemID==expected.ItemID&&r.UnitCost==expected.UnitCost&&r.RequiredRank==expected.RequiredRank).Take(2).ToArray();
        return matches.Length==1?matches[0].Index:-1;
    }
    public static GrandCompanyObservation Observe(ulong beforeItems,ulong afterItems,uint beforeCurrency,uint afterCurrency,int quantity,uint unitCost){
        if(quantity<=0||unitCost==0||afterItems<beforeItems||afterCurrency>beforeCurrency)return GrandCompanyObservation.Invalid;
        ulong expectedCost=(ulong)quantity*unitCost;
        if(expectedCost>beforeCurrency||afterItems-beforeItems>(ulong)quantity||(ulong)beforeCurrency-afterCurrency>expectedCost)return GrandCompanyObservation.Invalid;
        return afterItems-beforeItems==(ulong)quantity&&(ulong)beforeCurrency-afterCurrency==expectedCost?GrandCompanyObservation.Complete:GrandCompanyObservation.Pending;
    }
    public void Begin(GrandCompanyOffer value){
        if(value.ItemID==0||value.UnitCost==0||value.Count<=0||value.StackSize==0)throw new ArgumentException("军票兑换目标无效");
        if(value.Unique&&value.Count>1)throw new InvalidOperationException("独有物品每次只能请求一件");
        offer=value;remaining=value.Count;pending=0;deadline=Environment.TickCount64+120000;nextSelection=0;
    }
    public void Cancel(){offer=null;remaining=0;pending=0;}
    public bool Tick(){try{return TickCore();}catch{Cancel();throw;}}
    private unsafe bool TickCore(){
        if(offer is not {} expected)return true;
        if(Environment.TickCount64>deadline)throw new TimeoutException("军票兑换等待超时");
        var player=PlayerState.Instance();var inventory=InventoryManager.Instance();
        if(player==null||inventory==null||player->GrandCompany!=expected.Company||player->GetGrandCompanyRank()<expected.RequiredRank)
            throw new InvalidOperationException("军票兑换的军团或军衔已变化");
        if(!TryReadBags(expected.ItemID,expected.StackSize,out var count,out var capacity))return false;
        uint seals=inventory->GetCompanySeals(expected.Company);
        if(pending>0){
            var observation=Observe(beforeCount,count,beforeSeals,seals,pending,expected.UnitCost);
            if(observation==GrandCompanyObservation.Complete){
                remaining-=pending;pending=0;
                if(remaining==0){Cancel();return true;}
            }else if(observation==GrandCompanyObservation.Invalid||Environment.TickCount64>ackDeadline)
                throw new InvalidOperationException("军票兑换后的物品或军票变化与请求不一致，已停止后续兑换");
            else return false;
        }
        if(expected.Unique&&count>0)throw new InvalidOperationException("已持有该独有物品");
        if((ulong)remaining>capacity)throw new InvalidOperationException("背包可用容量不足以完成请求的军票兑换");
        if((ulong)remaining*expected.UnitCost>seals)throw new InvalidOperationException("军票余额不足以完成请求");
        var addon=RaptureAtkUnitManager.Instance()->GetAddonByName("GrandCompanyExchange");
        if(addon==null||!addon->IsVisible||addon->AtkValues==null)return false;
        if(Environment.TickCount64<nextSelection)return false;
        if(!SelectTab(addon,37u+expected.RankTab)||!SelectTab(addon,44u+expected.CategoryTab)){nextSelection=Environment.TickCount64+300;return false;}
        var rows=ReadRows(addon);
        int row=FindOffer(rows,expected);
        if(row<0)throw new InvalidOperationException("军票商店当前列表无法唯一匹配目标物品、单价和军衔");
        var live=rows.Single(r=>r.Index==row);
        if(live.CurrencyDialog)throw new InvalidOperationException("该兑换需要额外货币对话框，当前不支持自动提交");
        int batch=Math.Min(99,remaining);if(expected.Unique)batch=1;
        beforeCount=count;beforeSeals=seals;pending=batch;ackDeadline=Environment.TickCount64+10000;
        // Native addon callback contract: purchase, visible row, quantity, flags, optional dialog data.
        addon->Callback(true,0,row,batch,0,true,false,0,0,0);
        return false;
    }
    private static unsafe bool TryReadBags(uint itemID,uint stackSize,out ulong count,out ulong capacity){
        count=0;capacity=0;var inventory=InventoryManager.Instance();if(inventory==null)return false;
        foreach(var type in new[]{InventoryType.Inventory1,InventoryType.Inventory2,InventoryType.Inventory3,InventoryType.Inventory4}){
            var container=inventory->GetInventoryContainer(type);if(container==null||!container->IsLoaded||container->Size is <0 or >4096)return false;
            for(int slot=0;slot<container->Size;slot++){
                var item=container->GetInventorySlot(slot);if(item==null)return false;
                uint id=item->GetBaseItemId();
                if(id==0){capacity+=stackSize;continue;}
                if(id==itemID){uint amount=item->GetQuantity();count+=amount;if((item->Flags&InventoryItem.ItemFlags.HighQuality)==0&&amount<stackSize)capacity+=stackSize-amount;}
            }
        }
        return true;
    }
    private static unsafe bool SelectTab(AtkUnitBase* addon,uint nodeID){
        var node=addon->GetNodeById(nodeID);
        var radio=node==null?null:node->GetAsAtkComponentRadioButton();
        if(node==null||!node->IsVisible()||radio==null||!radio->AtkComponentButton.IsEnabled)throw new InvalidOperationException("军票兑换分类不可用");
        if(radio->IsSelected)return true;
        var owner=radio->AtkComponentButton.AtkComponentBase.OwnerNode;
        var evt=owner==null?null:owner->AtkResNode.AtkEventManager.Event;
        if(evt==null)throw new InvalidOperationException("军票兑换分类没有原生事件");
        addon->ReceiveEvent(evt->State.EventType,(int)evt->Param,evt);
        return false;
    }
    private static unsafe List<GrandCompanyVisibleRow> ReadRows(AtkUnitBase* addon){
        var values=addon->AtkValues;
        if(addon->AtkValuesCount<2)throw new InvalidOperationException("军票商店列表未就绪");
        uint count=Number(values[1]);
        if(count is 0 or >50||addon->AtkValuesCount<467+count)throw new InvalidOperationException("军票商店窗口布局不受支持");
        var rows=new List<GrandCompanyVisibleRow>();
        for(int i=0;i<count;i++){
            var dialog=values[467+i];if(dialog.Type!=AtkValueType.Bool)throw new InvalidOperationException("军票兑换对话框标志无效");
            rows.Add(new(i,Number(values[317+i]),Number(values[67+i]),Number(values[417+i]),dialog.Bool));
        }
        return rows;
    }
    private static uint Number(AtkValue value)=>value.Type switch{
        AtkValueType.UInt=>value.UInt,AtkValueType.Int when value.Int>=0=>(uint)value.Int,
        _=>throw new InvalidOperationException("军票商店数据类型不受支持")
    };
}