// SPDX-License-Identifier: GPL-3.0-or-later
using System.Globalization;
using OmniToolbox.Config;
namespace OmniToolbox.Market;
public static class MarketTooltipFormatter
{
    public static string Describe(MarketPrice price,MarketConfig config,bool allowHQ,Func<uint,string> worldName){
        var lines=new List<string>{"Universalis 市场数据（众包采集，可能滞后）","均价与日均成交按接口近4日统计"};
        Quality("普通品质",price.NQ);
        if(allowHQ)Quality("高品质",price.HQ);
        lines.Add("查询于 "+price.FetchedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm",CultureInfo.InvariantCulture));
        return string.Join("\n",lines);
        void Quality(string label,IReadOnlyDictionary<MarketMetricScope,MarketScopeData> values){
            if(!values.Values.Any(v=>v.Minimum!=null||v.Recent!=null||v.Average.HasValue||v.DailyQuantity.HasValue))return;
            lines.Add(label+"：");
            foreach(var scope in new[]{MarketMetricScope.Region,MarketMetricScope.DataCenter,MarketMetricScope.World}){
                if(!values.TryGetValue(scope,out var value))continue;
                var parts=new List<string>();
                bool min=scope==MarketMetricScope.Region?config.ShowRegionPriceInTooltip:scope==MarketMetricScope.World&&config.ShowOriginalWorldPriceInTooltip;
                bool recent=scope==MarketMetricScope.Region?config.ShowRegionRecentPurchaseInTooltip:scope==MarketMetricScope.World&&config.ShowOriginalWorldRecentPurchaseInTooltip;
                                if(min){
                    uint sourceWorld=value.Minimum?.WorldID??(scope==MarketMetricScope.World?price.HomeWorldID:0);
                    var stamp=sourceWorld!=0&&price.UploadTimes.TryGetValue(sourceWorld,out var uploaded)?" [采集 "+uploaded.ToLocalTime().ToString("yyyy-MM-dd HH:mm",CultureInfo.InvariantCulture)+"]":"";
                    parts.Add("最低 "+Quote(value.Minimum)+stamp);
                }
                if(recent)parts.Add("最近成交 "+Quote(value.Recent)+(value.Recent?.Timestamp is {} time?" ["+time.ToLocalTime().ToString("MM-dd HH:mm",CultureInfo.InvariantCulture)+"]":""));
                if(config.IsAverageSalePriceVisible(scope))parts.Add(value.Average.HasValue?"均价 "+Number(value.Average)+" 金币":"均价 暂无数据");
                if(config.IsDailySaleVelocityVisible(scope))parts.Add("日均成交 "+Number(value.DailyQuantity));
                if(parts.Count>0)lines.Add((scope==MarketMetricScope.Region?"全区":scope==MarketMetricScope.DataCenter?"大区":"原服")+" · "+string.Join("；",parts));
            }
        }
        string Quote(MarketQuote? value)=>value==null?"暂无数据":Number(value.Price)+" 金币"+(value.WorldID is {} id?"（"+worldName(id)+"）":"");
        static string Number(decimal? value)=>value?.ToString("N2",CultureInfo.InvariantCulture)??"暂无数据";
    }
}