// SPDX-License-Identifier: GPL-3.0-or-later
using FFXIVClientStructs.FFXIV.Client.Game;
namespace Yanjiu.Modules;
public sealed unsafe partial class AutoRetainer
{
 private static Dictionary<(uint,uint),long> MergeInventorySnapshot(){
  var inv=InventoryManager.Instance();if(inv==null)throw new InvalidOperationException("无法读取合并物品数据。");
  var totals=new Dictionary<(uint,uint),long>();
  foreach(var type in new[]{InventoryType.Inventory1,InventoryType.Inventory2,InventoryType.Inventory3,InventoryType.Inventory4,
   InventoryType.RetainerPage1,InventoryType.RetainerPage2,InventoryType.RetainerPage3,InventoryType.RetainerPage4,InventoryType.RetainerPage5,InventoryType.RetainerPage6,InventoryType.RetainerPage7}){
   var bag=inv->GetInventoryContainer(type);if(bag==null||!bag->IsLoaded)throw new InvalidOperationException("合并所需背包尚未全部加载，停止操作。");
   for(int i=0;i<bag->Size;i++){var item=bag->GetInventorySlot(i);if(item==null||item->ItemId==0)continue;
    var key=(item->ItemId,(uint)item->Flags);totals[key]=totals.GetValueOrDefault(key)+item->Quantity;
   }
  }
  return totals;
 }
}
