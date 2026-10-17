// SPDX-License-Identifier: GPL-3.0-or-later
using Lumina.Excel.Sheets;
using OmenTools.Interop.Game.Lumina;
using OmniToolbox.Collections;
namespace OmniToolbox.Items;
/// <summary>Item categories derived from game sheets, without synthetic collection records.</summary>
public sealed class CollectionItemIndex
{
    public IReadOnlyDictionary<uint,CollectionType> Items {get;}
    public CollectionItemIndex(){
        var emotes=LuminaGetter.Get<Emote>().Where(r=>r.RowId!=0&&r.UnlockLink!=0).Select(r=>r.UnlockLink).ToHashSet();
        var hairstyles=HairstyleData.GetPurchasableRows().Where(r=>r.Icon!=131094).ToArray();
        var hairLinks=hairstyles.Select(r=>(uint)r.UnlockLink).ToHashSet();
        var items=new Dictionary<uint,CollectionType>();
        foreach(var hair in hairstyles)if(hair.HintItem.RowId!=0)items.TryAdd(hair.HintItem.RowId,CollectionType.Hairstyle);
        foreach(var item in LuminaGetter.Get<Item>()){
            if(item.RowId==0||!item.ItemAction.IsValid)continue;
            var action=item.ItemAction.Value;
            var type=Classify(action.Action.RowId,(uint)action.Data[0],emotes,hairLinks);
            if(type.HasValue)items.TryAdd(item.RowId,type.Value);
        }
        Items=System.Collections.Frozen.FrozenDictionary.ToFrozenDictionary(items);
    }
    // Contract values verified against Dalamud/Game/UnlockState/ItemActionAction.cs.
    public static CollectionType? Classify(uint action,uint target,IReadOnlySet<uint> emotes,IReadOnlySet<uint> hairstyles)=>action switch{
        853=>CollectionType.Minion,1013=>CollectionType.Barding,1322=>CollectionType.Mount,
        3357=>CollectionType.TripleTriadCard,25183=>CollectionType.Orchestrion,
        29459=>CollectionType.Portrait,20086=>CollectionType.FashionAccessory,37312=>CollectionType.Glasses,
        2633 when hairstyles.Contains(target)=>CollectionType.Hairstyle,
        2633 when emotes.Contains(target)=>CollectionType.Emote,
        _=>null
    };
}