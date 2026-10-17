// SPDX-License-Identifier: GPL-3.0-or-later
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.Sheets;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using OmenTools.Info.Game.Data;
using OmniToolbox.Host;
namespace OmniToolbox.Items;
public sealed class PlayerInventoryService:IDisposable
{
    private readonly ObservedInventoryCache cache=new();
    private long specialRevision;
    private InventorySnapshotItem[] cabinetItems=[],dresserItems=[];
    private long nextScan;private ulong player;private ulong company;private bool disposed;
    private IReadOnlyList<InventorySnapshotItem> snapshot=[];private long snapshotRevision=-1;
    public IReadOnlyDictionary<uint,uint> ArmoireRows{get;}
    public PlayerInventoryService(){
        ArmoireRows=DalamudServices.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Cabinet>().Where(r=>r.Item.RowId!=0).GroupBy(r=>r.Item.RowId).ToDictionary(g=>g.Key,g=>g.First().RowId);
    }
    public long GetSnapshotRevision()=>cache.Revision+specialRevision;
    public IReadOnlyList<InventorySnapshotItem> GetItemsSnapshot(){
        if(snapshotRevision!=GetSnapshotRevision()){snapshot=cache.Snapshot(player).Where(item=>item.Location!=ItemInventoryLocation.FreeCompanyChest||item.HolderID==company).Concat(cabinetItems).Concat(dresserItems).ToArray();snapshotRevision=GetSnapshotRevision();}
        return snapshot;
    }
    public void RefreshDisplaySettings(){snapshotRevision=-1;}
    public unsafe void Tick(){
        if(disposed||Environment.TickCount64<nextScan)return;nextScan=Environment.TickCount64+500;
        ulong current=DalamudServices.PlayerState.IsLoaded?DalamudServices.PlayerState.ContentId:0;
        if(current!=player){player=current;cabinetItems=[];dresserItems=[];specialRevision++;snapshotRevision=-1;}
        if(player==0)return;
        var companyInfo=InfoProxyFreeCompany.Instance();ulong currentCompany=companyInfo==null?0:companyInfo->Id;
        if(company!=currentCompany){company=currentCompany;snapshotRevision=-1;}
        ScanSpecialStorage();
        var housing=HousingManager.Instance();ulong house=0;string houseName="";
        if(housing!=null&&housing->CurrentTerritory!=null&&housing->CurrentTerritory->IsLoaded()&&housing->HasHousePermissions()){var id=housing->GetCurrentHouseId();if(id.Id!=0&&id.Id!=ulong.MaxValue){house=id.Id;houseName=$"{id.WorldId} · {id.WardIndex+1}区{id.PlotIndex+1}号 · {id.RoomNumber}室";}}
        var manager=InventoryManager.Instance();if(manager==null)return;
        var retainers=RetainerManager.Instance();var retainer=retainers==null?null:retainers->GetActiveRetainer();
        ulong retainerID=retainer==null?0:retainer->RetainerId;
        string retainerName=retainer==null?"":retainer->NameString;
        foreach(var type in Enum.GetValues<InventoryType>()){
            var location=Classify(type);if(location==ItemInventoryLocation.None)continue;
            bool isRetainer=location is ItemInventoryLocation.Retainer or ItemInventoryLocation.RetainerMarket;
            ulong holder=isRetainer?retainerID:location==ItemInventoryLocation.FreeCompanyChest?company:location==ItemInventoryLocation.HousingStoreroom?house:player;if(holder==0)continue;
            var container=manager->GetInventoryContainer(type);
            if(container==null||!container->IsLoaded||container->Size is <0 or >4096)continue;
            var items=new List<InventorySnapshotItem>();
            for(int index=0;index<container->Size;index++){
                var item=container->GetInventorySlot(index);
                if(item==null||item->GetBaseItemId()==0||item->GetQuantity()==0)continue;
                items.Add(new(item->GetBaseItemId(),item->GetQuantity(),holder,isRetainer?retainerName:location==ItemInventoryLocation.HousingStoreroom?houseName:"",type,(ushort)index,location,item->GetStain(0),item->GetStain(1)));
            }
            if(cache.Update(player,holder,type,items,true))snapshotRevision=-1;
        }
    }
    private unsafe void ScanSpecialStorage(){
        var ui=UIState.Instance();
        if(ui!=null&&ui->Cabinet.State==FFXIVClientStructs.FFXIV.Client.Game.UI.Cabinet.CabinetState.Loaded){
            var found=new List<InventorySnapshotItem>();
            foreach(var entry in ArmoireRows)if(ui->Cabinet.IsItemInCabinet(entry.Value))
                found.Add(new(entry.Key,1,player,"",InventoryType.Invalid,0,ItemInventoryLocation.Armoire,0,0));
            ReplaceSpecial(ref cabinetItems,found.ToArray());
        }
        var mirage=MirageManager.Instance();
        if(mirage!=null&&mirage->PrismBoxLoaded){
            var found=new List<InventorySnapshotItem>();
            for(int index=0;index<mirage->PrismBoxItemIds.Length;index++){
                uint id=ItemUtil.GetBaseId(mirage->PrismBoxItemIds[index]).ItemId;if(id==0)continue;
                found.Add(new(id,1,player,"",InventoryType.Invalid,(ushort)index,ItemInventoryLocation.GlamourDresser,mirage->PrismBoxStain0Ids[index],mirage->PrismBoxStain1Ids[index]));
            }
            ReplaceSpecial(ref dresserItems,found.ToArray());
        }
    }
    private void ReplaceSpecial(ref InventorySnapshotItem[] target,InventorySnapshotItem[] observed){
        // Invalid marks records read from their native storage manager, not InventoryManager containers.
        if(target.SequenceEqual(observed))return;
        target=observed;specialRevision++;snapshotRevision=-1;
    }
    public static ItemInventoryLocation Classify(InventoryType type){
        if(Inventories.Player.Contains(type))return ItemInventoryLocation.Inventory;
        if(type==InventoryType.EquippedItems)return ItemInventoryLocation.Equipped;
        if(Inventories.Armory.Contains(type))return ItemInventoryLocation.Armory;
        if(type==InventoryType.RetainerMarket)return ItemInventoryLocation.RetainerMarket;
        if(Inventories.RetainerAll.Contains(type))return ItemInventoryLocation.Retainer;
        var name=type.ToString();
        if(name.StartsWith("SaddleBag",StringComparison.Ordinal)||name.StartsWith("PremiumSaddleBag",StringComparison.Ordinal))return ItemInventoryLocation.Saddlebag;
        if(name.StartsWith("Housing",StringComparison.Ordinal)&&name.Contains("Storeroom",StringComparison.Ordinal))return ItemInventoryLocation.HousingStoreroom;
        if(name.StartsWith("FreeCompanyPage",StringComparison.Ordinal))return ItemInventoryLocation.FreeCompanyChest;
        return ItemInventoryLocation.None;
    }
    public void Dispose(){if(disposed)return;disposed=true;cache.Clear();cabinetItems=[];dresserItems=[];snapshot=[];}
}
