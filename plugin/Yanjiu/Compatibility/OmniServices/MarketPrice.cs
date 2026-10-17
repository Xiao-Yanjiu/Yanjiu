// SPDX-License-Identifier: GPL-3.0-or-later
using System.Text.Json;
namespace OmniToolbox.Market;
public enum MarketMetricScope { Region,DataCenter,World }
public sealed record MarketQuote(decimal Price,uint? WorldID,DateTimeOffset? Timestamp);
public sealed record MarketScopeData(MarketQuote? Minimum,MarketQuote? Recent,decimal? Average,decimal? DailyQuantity);
public sealed record MarketPrice(uint ItemID,uint HomeWorldID,DateTimeOffset FetchedAt,IReadOnlyDictionary<MarketMetricScope,MarketScopeData> NQ,IReadOnlyDictionary<MarketMetricScope,MarketScopeData> HQ,IReadOnlyDictionary<uint,DateTimeOffset> UploadTimes);
public static class UniversalisMarketParser
{
    public static MarketPrice Decode(string json,uint itemID,uint homeWorld,DateTimeOffset fetchedAt){
        if(json.Length>2097152)throw new InvalidDataException("物价响应超出大小限制");
        using var doc=JsonDocument.Parse(json,new JsonDocumentOptions{MaxDepth=32});
        var root=doc.RootElement;
        if(root.TryGetProperty("failedItems",out var failed)&&failed.EnumerateArray().Any(x=>x.GetUInt32()==itemID))throw new InvalidDataException("Universalis 没有该物品的市场数据");
        if(!root.TryGetProperty("results",out var rows)||rows.ValueKind!=JsonValueKind.Array||rows.GetArrayLength()>100)throw new InvalidDataException("物价响应结构无效");
        var matches=rows.EnumerateArray().Where(r=>r.TryGetProperty("itemId",out var id)&&id.GetUInt32()==itemID).Take(2).ToArray();
        if(matches.Length!=1)throw new InvalidDataException("物价响应的物品编号不匹配或重复");
        var result=matches[0];var uploads=new Dictionary<uint,DateTimeOffset>();
        if(result.TryGetProperty("worldUploadTimes",out var uploadRows)&&uploadRows.ValueKind==JsonValueKind.Array){
            if(uploadRows.GetArrayLength()>1000)throw new InvalidDataException("上传记录数量超出限制");
            foreach(var upload in uploadRows.EnumerateArray())uploads[upload.GetProperty("worldId").GetUInt32()]=DateTimeOffset.FromUnixTimeMilliseconds(upload.GetProperty("timestamp").GetInt64());
        }
        return new(itemID,homeWorld,fetchedAt,Quality(result,"nq"),Quality(result,"hq"),new System.Collections.ObjectModel.ReadOnlyDictionary<uint,DateTimeOffset>(uploads));
    }
    private static IReadOnlyDictionary<MarketMetricScope,MarketScopeData> Quality(JsonElement result,string name){
        var quality=Child(result,name);
        var scopes=new Dictionary<MarketMetricScope,MarketScopeData>();
        foreach(var (scope,key) in new[]{(MarketMetricScope.Region,"region"),(MarketMetricScope.DataCenter,"dc"),(MarketMetricScope.World,"world")}){
            var minimum=Quote(Child(Child(quality,"minListing"),key));
            var recent=Quote(Child(Child(quality,"recentPurchase"),key));
            scopes[scope]=new(minimum,recent,Number(Child(Child(quality,"averageSalePrice"),key),"price"),Number(Child(Child(quality,"dailySaleVelocity"),key),"quantity"));
        }
        return new System.Collections.ObjectModel.ReadOnlyDictionary<MarketMetricScope,MarketScopeData>(scopes);
    }
    private static JsonElement Child(JsonElement parent,string name)=>parent.ValueKind==JsonValueKind.Object&&parent.TryGetProperty(name,out var value)?value:default;
    private static decimal? Number(JsonElement parent,string name){
        var value=Child(parent,name);
        if(value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)return null;
        if(value.ValueKind!=JsonValueKind.Number||!value.TryGetDecimal(out var number)||number<0)throw new InvalidDataException("物价统计包含无效数值");
        return number;
    }
    private static MarketQuote? Quote(JsonElement entry){
        var price=Number(entry,"price");if(price==null)return null;
        uint? world=null;DateTimeOffset? time=null;
        var worldValue=Child(entry,"worldId");if(worldValue.ValueKind==JsonValueKind.Number)world=worldValue.GetUInt32();
        var stamp=Child(entry,"timestamp");if(stamp.ValueKind==JsonValueKind.Number)time=DateTimeOffset.FromUnixTimeMilliseconds(stamp.GetInt64());
        return new(price.Value,world,time);
    }
}