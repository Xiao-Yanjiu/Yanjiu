// SPDX-License-Identifier: GPL-3.0-or-later
using System.Numerics;
using OmniToolbox.Items;
namespace OmniToolbox.Config;
public enum OwnedCountDisplayMode { Brief,Detailed }
public sealed class ItemConfig
{
    public bool ShowShopSourceInTooltip=true;
    public bool ShowDutySourceInTooltip=true;
    public bool ShowConsumableEffectsInTooltip=true;
    public bool ShowBlindBoxProgressInTooltip=true;
    public bool ShowFadedOrchestrionProgressInTooltip=true;
    public bool ShowCollectableValueInTooltip=true;
    public bool ShowMateriaTotalInTooltip=true;
    public bool ShowItemSetContentsInTooltip=true;
    public ItemImagePreviewConfig ImagePreview=new();
    public ItemInventoryLocation InventoryDisplayLocations=ItemInventoryLocation.All;
    public bool ShowOwnedCountInTooltip=true;
    public int OwnedCountLocationColorKey=45;
    public bool UseCustomOwnedCountLocationColor;
    public Vector3 OwnedCountLocationColor=new(1,1,1);
    public OwnedCountDisplayMode OwnedCountDisplayMode=OwnedCountDisplayMode.Detailed;
    public bool ShowPatchVersionInTooltip=true;
    public bool HideCraftRepairSectionInTooltip;
}