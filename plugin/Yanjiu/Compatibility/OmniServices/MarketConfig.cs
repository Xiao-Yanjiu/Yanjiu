// SPDX-License-Identifier: GPL-3.0-or-later
using OmniToolbox.Market;
namespace OmniToolbox.Config;
public sealed class MarketConfig
{
    public bool ShowRegionPriceInTooltip=true,ShowOriginalWorldPriceInTooltip=true;
    public bool ShowRegionRecentPurchaseInTooltip=true,ShowOriginalWorldRecentPurchaseInTooltip=true;
    public bool ShowRegionAverageSalePriceInTooltip=true,ShowDataCenterAverageSalePriceInTooltip=true,ShowWorldAverageSalePriceInTooltip=true;
    public bool ShowRegionDailySaleVelocityInTooltip=true,ShowDataCenterDailySaleVelocityInTooltip=true,ShowWorldDailySaleVelocityInTooltip=true;
    public MarketMetricScope StatisticsScope=MarketMetricScope.Region;
    public bool IsAverageSalePriceVisible(MarketMetricScope scope)=>scope switch{
        MarketMetricScope.Region=>ShowRegionAverageSalePriceInTooltip,MarketMetricScope.DataCenter=>ShowDataCenterAverageSalePriceInTooltip,_=>ShowWorldAverageSalePriceInTooltip};
    public bool IsDailySaleVelocityVisible(MarketMetricScope scope)=>scope switch{
        MarketMetricScope.Region=>ShowRegionDailySaleVelocityInTooltip,MarketMetricScope.DataCenter=>ShowDataCenterDailySaleVelocityInTooltip,_=>ShowWorldDailySaleVelocityInTooltip};
}