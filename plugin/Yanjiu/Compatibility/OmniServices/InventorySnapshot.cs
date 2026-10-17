// SPDX-License-Identifier: GPL-3.0-or-later
using FFXIVClientStructs.FFXIV.Client.Game;
namespace OmniToolbox.Items;
[Flags]
public enum ItemInventoryLocation
{
    None=0,Inventory=1,Armory=2,Equipped=4,Saddlebag=8,Retainer=16,FreeCompanyChest=32,GlamourDresser=64,Armoire=128,RetainerMarket=256,HousingStoreroom=512,All=1023
}
public sealed record InventorySnapshotItem(uint ItemID,uint Quantity,ulong HolderID,string HolderName,InventoryType ContainerType,ushort SlotIndex,ItemInventoryLocation Location,byte Stain0,byte Stain1);
/// <summary>Observed container data; an unavailable container is never recorded as an empty one.</summary>
public sealed class ObservedInventoryCache
{
    private sealed record Segment(InventorySnapshotItem[] Items,long Sequence);
    private readonly Dictionary<(ulong Player,ulong Holder,InventoryType Type),Segment> segments=[];
    private long sequence;
    public long Revision{get;private set;}
    public int SegmentCount=>segments.Count;
    public bool Update(ulong player,ulong holder,InventoryType type,IEnumerable<InventorySnapshotItem> items,bool observed){
        if(!observed||player==0||holder==0)return false;
        var key=(player,holder,type);var snapshot=items.Take(4097).ToArray();
        if(snapshot.Any(item=>item.HolderID!=holder||item.ContainerType!=type))throw new InvalidDataException("库存记录所属容器不一致");
        if(snapshot.Length>4096)throw new InvalidDataException("库存容器记录超出限制");
        if(segments.TryGetValue(key,out var before)&&before.Items.SequenceEqual(snapshot)){segments[key]=before with{Sequence=++sequence};return false;}
        segments[key]=new(snapshot,++sequence);Revision++;
        while(segments.Count>512||segments.Values.Sum(v=>v.Items.Length)>25000){
            var oldest=segments.MinBy(pair=>pair.Value.Sequence).Key;segments.Remove(oldest);Revision++;
        }
        return true;
    }
    public IReadOnlyList<InventorySnapshotItem> Snapshot(ulong player)=>segments.Where(pair=>pair.Key.Player==player).OrderBy(pair=>pair.Key.Holder).ThenBy(pair=>pair.Key.Type).SelectMany(pair=>pair.Value.Items).ToArray();
    public void Clear(){segments.Clear();Revision++;}
}
