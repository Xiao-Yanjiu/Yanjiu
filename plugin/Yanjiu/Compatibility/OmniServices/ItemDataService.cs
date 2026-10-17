// SPDX-License-Identifier: GPL-3.0-or-later
using Lumina.Excel.Sheets;
namespace OmniToolbox.Items;
public static class ItemDataService
{
    public static bool IsCurrency(Item item)=>ItemCategoryMap.IsCurrency(item);
}
