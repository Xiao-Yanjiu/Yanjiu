// SPDX-License-Identifier: GPL-3.0-or-later
using System.Numerics;
using Dalamud.Utility;
using Lumina.Text.ReadOnly;
using OmniToolbox.Config;
using OmniToolbox.Items;
using OmenTools.OmenService;
namespace OmniToolbox.Tooltips;
public sealed record OwnedItemLocation(ItemInventoryLocation Location,ulong HolderID,string HolderName,ulong Quantity);
public sealed class ItemOwnedCountTooltip(ItemConfig config,PlayerInventoryService inventory):IDisposable
{
    private bool subscribed,disposed;
    private ItemPatchMap? patches;
    public void SetFeatureEnabled(bool enabled){
        ObjectDisposedException.ThrowIf(disposed,this);
        if(enabled&&!subscribed){TooltipManager.Instance().RegItem(OnItemTooltip);subscribed=true;}
        else if(!enabled&&subscribed){TooltipManager.Instance().Unreg(OnItemTooltip);subscribed=false;}
        RefreshSettings();
    }
    public void RefreshSettings(){if(!disposed)TooltipManager.Instance().TriggerItemDetailUpdate();}
    public static IReadOnlyList<OwnedItemLocation> Summarize(IEnumerable<InventorySnapshotItem> items,uint itemID,ItemInventoryLocation mask)=>
        items.Where(i=>i.ItemID==itemID&&(i.Location&mask)!=0)
            .GroupBy(i=>(i.Location,i.HolderID,i.HolderName))
            .Select(g=>new OwnedItemLocation(g.Key.Location,g.Key.HolderID,g.Key.HolderName,g.Aggregate(0UL,(sum,item)=>checked(sum+item.Quantity))))
            .OrderBy(g=>g.Location).ThenBy(g=>g.HolderID).ToArray();
    public static string LocationName(ItemInventoryLocation location)=>location switch{
        ItemInventoryLocation.Inventory=>"背包",ItemInventoryLocation.Armory=>"兵装库",
        ItemInventoryLocation.Equipped=>"装备中",ItemInventoryLocation.Saddlebag=>"陆行鸟鞍囊",
        ItemInventoryLocation.Retainer=>"雇员",ItemInventoryLocation.FreeCompanyChest=>"部队储物柜",
        ItemInventoryLocation.GlamourDresser=>"投影台",ItemInventoryLocation.Armoire=>"收藏柜",
        ItemInventoryLocation.RetainerMarket=>"雇员出售",ItemInventoryLocation.HousingStoreroom=>"住宅仓库",_=>"其他"
    };
    private void OnItemTooltip(ItemKind kind,uint itemID,ref List<TooltipItemModification> modifications){
        if(disposed||!subscribed||kind==ItemKind.EventItem)return;
        if(config.ShowOwnedCountInTooltip){
            var groups=Summarize(inventory.GetItemsSnapshot(),itemID,config.InventoryDisplayLocations);
            using var rented=new RentedSeStringBuilder();var builder=rented.Builder;
            builder.Append("已读取的持有记录：").Append(groups.Aggregate(0UL,(sum,g)=>sum+g.Quantity).ToString(System.Globalization.CultureInfo.InvariantCulture));
            if(config.OwnedCountDisplayMode==OwnedCountDisplayMode.Detailed){
                foreach(var group in groups){
                    builder.Append("\n");
                    bool custom=config.UseCustomOwnedCountLocationColor;
                    if(custom){var color=config.OwnedCountLocationColor;if(!float.IsFinite(color.X)||!float.IsFinite(color.Y)||!float.IsFinite(color.Z))color=Vector3.One;builder.PushColorRgba(new Vector4(Vector3.Clamp(color,Vector3.Zero,Vector3.One),1));}
                    else builder.PushColorType((uint)Math.Clamp(config.OwnedCountLocationColorKey,0,65535));
                    builder.Append(LocationName(group.Location));
                    if(!string.IsNullOrWhiteSpace(group.HolderName))builder.Append(" · ").Append(group.HolderName);
                    if(custom)builder.PopColor();else builder.PopColorType();
                    builder.Append("：").Append(group.Quantity.ToString(System.Globalization.CultureInfo.InvariantCulture));
                }
            }
            builder.Append("\n未读取的位置不计入；关闭容器后显示上次读取记录。");
            modifications.Add(new(){Target=TooltipItemType.OwnedCount,Type=TooltipModificationType.Append,Text=builder.ToReadOnlySeString()});
        }
        if(config.ShowPatchVersionInTooltip){
            var patch=(patches??=new ItemPatchMap()).Get(itemID);
            if(patch>0){using var rented=new RentedSeStringBuilder();rented.Builder.Append("版本 ").Append(ItemPatchMap.Format(patch));modifications.Add(new(){Target=TooltipItemType.UICategory,Type=TooltipModificationType.Append,Text=rented.Builder.ToReadOnlySeString()});}
        }
        if(config.HideCraftRepairSectionInTooltip)
            foreach(var section in new[]{TooltipItemType.MarkerName,TooltipItemType.RepairInfo,TooltipItemType.RepairMaterial,TooltipItemType.QuickRepairCost})
                modifications.Add(new(){Target=section,Type=TooltipModificationType.Contribute,Text=new ReadOnlySeString(Array.Empty<byte>())});
    }
    public void Dispose(){if(disposed)return;if(subscribed){TooltipManager.Instance().Unreg(OnItemTooltip);subscribed=false;TooltipManager.Instance().TriggerItemDetailUpdate();}disposed=true;}
}