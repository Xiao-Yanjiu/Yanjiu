// SPDX-License-Identifier: GPL-3.0-or-later
using FFXIVClientStructs.FFXIV.Client.Game;
using OmenTools;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
namespace OmniToolbox.TreePublic;
public sealed partial class ToolbarRenderer
{
    private byte[]? inventoryHudSlots;
    private long inventoryHudRefreshAt;
    private float NativeCompactWidgetWidth(MultiToolbarWidgetType type)=>type switch{
        MultiToolbarWidgetType.InventorySpace=>ScaleToolbar(88),
        MultiToolbarWidgetType.QuickCommands=>ScaleToolbar(7*32),_=>0};
    private unsafe void UpdateInventoryHudSlots()
    {
        if(Environment.TickCount64<inventoryHudRefreshAt)return;
        inventoryHudRefreshAt=Environment.TickCount64+500;
        if(!config.Widgets.Any(w=>w.Enabled&&w.Type==MultiToolbarWidgetType.InventorySpace)){inventoryHudSlots=null;return;}
        var manager=InventoryManager.Instance();if(manager==null){inventoryHudSlots=null;return;}
        var slots=new byte[140];
        var items=DService.Instance().Data.GetExcelSheet<Lumina.Excel.Sheets.Item>();
        for(int bag=0;bag<4;bag++){
            var container=manager->GetInventoryContainer((InventoryType)bag);
            if(container==null||!container->IsLoaded||container->Size!=35){inventoryHudSlots=null;return;}
            for(int index=0;index<35;index++){
                var item=container->GetInventorySlot(index);
                if(item==null||item->ItemId==0||item->Quantity==0)continue;
                slots[bag*35+index]=items.TryGetRow(item->ItemId,out var row)&&row.EquipSlotCategory.RowId!=0?(byte)2:(byte)1;
            }
        }
        var orderModule=ItemOrderModule.Instance();
        var sorter=orderModule==null?null:orderModule->InventorySorter;
        if(sorter==null||sorter->InventoryType!=InventoryType.Inventory1||sorter->ItemsPerPage!=35||sorter->Items.Count!=140){inventoryHudSlots=null;return;}
        var order=new int[140];
        for(int display=0;display<140;display++){
            var entry=sorter->Items[display].Value;
            if(entry==null||entry->Page>=4||entry->Slot>=35){inventoryHudSlots=null;return;}
            order[display]=entry->Page*35+entry->Slot;
        }
        inventoryHudSlots=MapInventoryHudSlots(slots,order);
    }
    private static byte[]? MapInventoryHudSlots(byte[] physical,int[] displayOrder)
    {
        if(physical.Length!=140||displayOrder.Length!=140)return null;
        var seen=new bool[140];var result=new byte[140];
        for(int i=0;i<140;i++){
            int slot=displayOrder[i];if((uint)slot>=140||seen[slot])return null;
            seen[slot]=true;result[i]=physical[slot];
        }
        return result;
    }
}
