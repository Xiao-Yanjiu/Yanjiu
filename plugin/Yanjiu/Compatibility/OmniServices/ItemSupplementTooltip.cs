// SPDX-License-Identifier: GPL-3.0-or-later
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;
using OmniToolbox.Config;
using OmniToolbox.Collections;
using OmniToolbox.Market;
using OmenTools.Interop.Game.Lumina;
using OmenTools.OmenService;
namespace OmniToolbox.Tooltips;
/// <summary>Desynthesis text through the public Omen tooltip modification pipeline.</summary>
public sealed class ItemSupplementTooltip:IDisposable
{
    private bool subscribed,enabled,showText,disposed,previewEnabled,marketEnabled;
    private MarketConfig? marketConfig;
    private MarketPriceService? market;
    public void ConfigureMarket(MarketConfig config,MarketPriceService service){ObjectDisposedException.ThrowIf(disposed,this);marketConfig=config;market=service;}
    public void SetMarketEnabled(bool value){ObjectDisposedException.ThrowIf(disposed,this);if(value&&(marketConfig==null||market==null))throw new InvalidOperationException("物价查询服务未配置");marketEnabled=value;UpdateSubscription();}
    private ItemPreviewSupplement? preview;
    public void ConfigurePreview(ItemConfig config,CollectionSourceService sources){ObjectDisposedException.ThrowIf(disposed,this);preview=new(config,sources);}
    public void SetPreviewEnabled(bool value){ObjectDisposedException.ThrowIf(disposed,this);if(value&&preview==null)throw new InvalidOperationException("物品预览提示数据服务未配置");previewEnabled=value;UpdateSubscription();}
    public void SetDesynthesisEnabled(bool value,bool show){
        ObjectDisposedException.ThrowIf(disposed,this);
        enabled=value;showText=show;
        UpdateSubscription();
    }
    private void UpdateSubscription(){
        bool wanted=marketEnabled||previewEnabled||(enabled&&showText);
        if(wanted&&!subscribed){TooltipManager.Instance().RegItem(OnItemTooltip);subscribed=true;}
        else if(!wanted&&subscribed){TooltipManager.Instance().Unreg(OnItemTooltip);subscribed=false;}
        TooltipManager.Instance().TriggerItemDetailUpdate();
    }
    private unsafe void OnItemTooltip(ItemKind kind,uint itemID,ref List<TooltipItemModification> modifications){
        if(kind==ItemKind.EventItem||!LuminaGetter.TryGetRow<Item>(itemID,out var item))return;
        if(previewEnabled&&preview!=null){
            var text=preview.Describe(item);
            if(!string.IsNullOrEmpty(text)){
                using var supplement=new RentedSeStringBuilder();supplement.Builder.Append(text);
                modifications.Add(new(){Target=TooltipItemType.Description,Type=TooltipModificationType.Append,Text=supplement.Builder.ToReadOnlySeString()});
            }
        }
        if(marketEnabled&&market!=null&&marketConfig!=null&&!item.IsUntradable&&item.ItemSearchCategory.RowId!=0){
            market.Request(itemID);
            var price=market.Get(itemID);var status=market.Status(itemID);
            string marketText=price==null?status:MarketTooltipFormatter.Describe(price,marketConfig,item.CanBeHq,id=>LuminaGetter.TryGetRow<World>(id,out var world)?world.Name.ToString():id.ToString());
            if(price!=null&&!string.IsNullOrEmpty(status))marketText+="\n"+status;
            using var marketBuilder=new RentedSeStringBuilder();marketBuilder.Builder.Append(marketText);
            modifications.Add(new(){Target=TooltipItemType.Description,Type=TooltipModificationType.Append,Text=marketBuilder.Builder.ToReadOnlySeString()});
        }
        if(!enabled||!showText||item.Desynth==0||item.ClassJobRepair.RowId==0)return;
        var ui=UIState.Instance();if(ui==null)return;
        var level=DesynthesisEnhancementState.Calculate(ui->PlayerState.GetDesynthesisLevel(item.ClassJobRepair.RowId),item.LevelItem.RowId);
        using var builder=new RentedSeStringBuilder();
        builder.Builder.Append("分解技能：").PushColorType(DesynthesisEnhancementState.GetColorKey(level.Status))
            .Append(level.Current.ToString("0.##",System.Globalization.CultureInfo.InvariantCulture)).PopColorType()
            .Append(" / 物品品级：").Append(level.Recommended);
        modifications.Add(new(){Target=TooltipItemType.Description,Type=TooltipModificationType.Append,Text=builder.Builder.ToReadOnlySeString()});
    }
    public void Dispose(){if(disposed)return;disposed=true;if(subscribed){TooltipManager.Instance().Unreg(OnItemTooltip);subscribed=false;TooltipManager.Instance().TriggerItemDetailUpdate();}}
}