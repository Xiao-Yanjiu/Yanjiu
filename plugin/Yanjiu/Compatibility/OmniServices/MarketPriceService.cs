// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Text;
using OmniToolbox.Host;
using OmenTools.OmenService;
namespace OmniToolbox.Market;
public sealed class MarketPriceService:IDisposable
{
    private sealed record CacheEntry(MarketPrice? Price,string Error,DateTimeOffset NextRequest,long Sequence);
    private readonly object sync=new();
    private readonly Dictionary<(uint World,uint Item),CacheEntry> cache=[];
    private readonly Dictionary<(uint World,uint Item),Task<MarketPrice?>> pending=[];
    private readonly ConcurrentQueue<uint> completedWorlds=new();
    private readonly CancellationTokenSource cancellation=new();
    private readonly SemaphoreSlim slots=new(2);
    private readonly HttpClient client;private readonly bool ownsClient;private readonly TimeProvider clock;
    private DateTimeOffset blockedUntil;
    private uint homeWorld;private bool disposed;private long sequence;
    public int CacheCount{get{lock(sync)return cache.Count;}}
    public int PendingCount{get{lock(sync)return pending.Count;}}
    public MarketPriceService(HttpClient? client=null,TimeProvider? clock=null){
        ownsClient=client==null;
        this.client=client??new HttpClient(new HttpClientHandler{AllowAutoRedirect=false,AutomaticDecompression=DecompressionMethods.All}){Timeout=TimeSpan.FromSeconds(12)};
        this.clock=clock??TimeProvider.System;
    }
    public MarketPrice? Get(uint itemID)=>Get(Volatile.Read(ref homeWorld),itemID);
    public MarketPrice? Get(uint world,uint itemID){
        lock(sync){if(!cache.TryGetValue((world,itemID),out var entry))return null;cache[(world,itemID)]=entry with{Sequence=++sequence};return entry.Price;}
    }
    public string Status(uint itemID)=>Status(Volatile.Read(ref homeWorld),itemID);
    public string Status(uint world,uint itemID){
        lock(sync){if(world==0)return "等待角色原服信息";if(pending.ContainsKey((world,itemID)))return "正在查询 Universalis";if(clock.GetUtcNow()<blockedUntil)return "Universalis 暂时限流，请稍后再试";return cache.TryGetValue((world,itemID),out var entry)?entry.Error:"尚未读取市场数据";}
    }
    public void Request(uint itemID){uint world=Volatile.Read(ref homeWorld);if(world!=0)_=FetchAsync(world,itemID);}
    public Task<MarketPrice?> FetchAsync(uint world,uint itemID){
        if(world==0||itemID==0)return Task.FromResult<MarketPrice?>(null);
        lock(sync){
            if(disposed)return Task.FromResult<MarketPrice?>(null);
            var key=(world,itemID);
            if(pending.TryGetValue(key,out var running))return running;
            cache.TryGetValue(key,out var before);
            if(before!=null&&clock.GetUtcNow()<before.NextRequest)return Task.FromResult(before.Price);
            if(pending.Count>=32||clock.GetUtcNow()<blockedUntil)return Task.FromResult(before?.Price);
            var task=Task.Run(()=>FetchCore(key));pending[key]=task;return task;
        }
    }
    private async Task<MarketPrice?> FetchCore((uint World,uint Item) key){
        bool entered=false;MarketPrice? price=null;string error="";TimeSpan retry=TimeSpan.FromSeconds(60);
        try{
            await slots.WaitAsync(cancellation.Token).ConfigureAwait(false);entered=true;
            lock(sync){if(clock.GetUtcNow()<blockedUntil){retry=blockedUntil-clock.GetUtcNow();throw new HttpRequestException("Universalis 暂时限流");}}
            using var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancellation.Token);timeout.CancelAfter(TimeSpan.FromSeconds(10));
            using var request=new HttpRequestMessage(HttpMethod.Get,$"https://universalis.app/api/v2/aggregated/{key.World}/{key.Item}");
            request.Headers.UserAgent.ParseAdd("Yanjiu/0.1.4");
            using var response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,timeout.Token).ConfigureAwait(false);
            if(response.StatusCode==HttpStatusCode.TooManyRequests){
                var requestedDelay=response.Headers.RetryAfter?.Delta??(response.Headers.RetryAfter?.Date-clock.GetUtcNow());
                retry=TimeSpan.FromSeconds(Math.Clamp(requestedDelay?.TotalSeconds??60,1,900));
                lock(sync){var until=clock.GetUtcNow()+retry;if(until>blockedUntil)blockedUntil=until;}
            }
            response.EnsureSuccessStatusCode();
            if(response.Content.Headers.ContentLength>2097152)throw new InvalidDataException("物价响应超出大小限制");
            using var body=await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
            using var buffer=new MemoryStream();var chunk=new byte[8192];
            int length;while((length=await body.ReadAsync(chunk,timeout.Token).ConfigureAwait(false))>0){
                if(buffer.Length+length>2097152)throw new InvalidDataException("物价响应超出大小限制");
                buffer.Write(chunk,0,length);
            }
            price=UniversalisMarketParser.Decode(Encoding.UTF8.GetString(buffer.GetBuffer(),0,(int)buffer.Length),key.Item,key.World,clock.GetUtcNow());
            retry=TimeSpan.FromMinutes(5);
        }catch(OperationCanceledException){error="市场查询已取消或超时";}
        catch(Exception e){error="Universalis 查询失败："+e.GetBaseException().Message;}
        finally{
            if(entered)slots.Release();
            lock(sync){
                pending.Remove(key);
                if(!disposed){
                    if(price==null&&cache.TryGetValue(key,out var previous))price=previous.Price;
                    cache[key]=new(price,error,clock.GetUtcNow()+retry,++sequence);
                    while(cache.Count>256)cache.Remove(cache.MinBy(p=>p.Value.Sequence).Key);
                    if(completedWorlds.Count<256)completedWorlds.Enqueue(key.World);
                }
            }
        }
        return price;
    }
    public void Tick(){
        if(disposed)return;
        uint world=DalamudServices.PlayerState.IsLoaded?GameState.HomeWorld:0;
        Volatile.Write(ref homeWorld,world);
        bool refresh=false;while(completedWorlds.TryDequeue(out var completed))if(completed==world)refresh=true;
        if(refresh)TooltipManager.Instance().TriggerItemDetailUpdate();
    }
    public void Dispose(){
        Task[] tasks;
        lock(sync){if(disposed)return;disposed=true;tasks=pending.Values.ToArray();cache.Clear();}
        cancellation.Cancel();if(ownsClient)client.Dispose();
        _=Task.WhenAll(tasks).ContinueWith(_=>{slots.Dispose();cancellation.Dispose();},TaskScheduler.Default);
    }
}