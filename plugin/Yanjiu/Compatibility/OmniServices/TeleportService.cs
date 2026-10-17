// SPDX-License-Identifier: GPL-3.0-or-later
using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using OmenTools.Info.Game.AetheryteRecord;
using OmenTools.Interop.Game.ExecuteCommand.Implementations;
using OmenTools.OmenService;
using OmniToolbox.Host;
namespace OmniToolbox.Teleport;
/// <summary>Normal game teleports; city transfers use the public Lifestream IPC contract.</summary>
public sealed class TeleportService:IDisposable
{
    private enum Ownership{None,Native,Lifestream}
    private Ownership owner;private AetheryteRecord? pendingShard;private uint pendingRootZone;private long started;private bool disposed;
    public string Status{get;private set;}="";
    private bool LifestreamBusy()=>DalamudServices.PluginInterface.GetIpcSubscriber<bool>("Lifestream.IsBusy").InvokeFunc();
    public bool IsBusy{
        get{
            if(disposed||owner==Ownership.None)return false;
            if(Environment.TickCount64-started>180_000){Cancel();Status="传送等待超时";return false;}
            if(pendingShard!=null)return true;
            if(owner==Ownership.Lifestream){
                try{if(LifestreamBusy())return true;}catch(Exception e){Status=e.GetBaseException().Message;}
                owner=Ownership.None;return false;
            }
            if(Environment.TickCount64-started<2000||NativeTransition)return true;
            owner=Ownership.None;return false;
        }
    }
    private static bool NativeTransition=>DalamudServices.PluginInterface!=null&&((OmenTools.DService.Instance().Condition[ConditionFlag.BetweenAreas]||OmenTools.DService.Instance().Condition[ConditionFlag.BetweenAreas51])||Yanjiu.Services.Objects.LocalPlayer?.IsCasting==true);
    public unsafe bool TryTeleport(uint aetheryteID){
        ObjectDisposedException.ThrowIf(disposed,this);
        if(IsBusy||!GameState.IsLoggedIn||NativeTransition)return false;
        var record=AetheryteRecordManager.Instance().AllRecords.FirstOrDefault(r=>r.RowID==aetheryteID);
        if(record==null||!record.IsUnlocked()){Status="目的地尚未解锁或不可用";return false;}
        return TryTeleport(record);
    }
    public unsafe bool TryTeleport(AetheryteRecord record){
        ObjectDisposedException.ThrowIf(disposed,this);
        if(IsBusy||!GameState.IsLoggedIn||NativeTransition||!record.IsUnlocked())return false;
        if(record.Group==254)return TryTeleport("无名众人广场");
        if(record.IsAetheryte){
            var telepo=Telepo.Instance();if(telepo==null)return false;
            if(!telepo->Teleport(record.RowID,record.SubIndex)){Status="游戏拒绝了传送请求";return false;}
            Start(Ownership.Native);Status="等待传送完成";return true;
        }
        if(record.ZoneID==GameState.TerritoryType&&record.UseAethernet()){Start(Ownership.Native);return true;}
        try{
            if(LifestreamBusy()){Status="Lifestream 正在执行其他任务";return false;}
            var root=AetheryteRecordManager.Instance().AllRecords.FirstOrDefault(r=>r.IsAetheryte&&r.Group==record.Group&&r.IsUnlocked());
            if(root==null){Status="找不到已解锁的同组主水晶";return false;}
            if(root.ZoneID==GameState.TerritoryType&&StartShard(record))return true;
            if(!TryTeleport(root))return false;
            pendingShard=record;pendingRootZone=root.ZoneID;Status="先前往主水晶，再进行城内传送";return true;
        }catch(Exception e){Status="城内自动传送需要可用的 Lifestream："+e.GetBaseException().Message;return false;}
    }
    public bool TryTeleport(string destination){
        ObjectDisposedException.ThrowIf(disposed,this);
        if(IsBusy||!GameState.IsLoggedIn||NativeTransition)return false;
        if(destination=="无名众人广场"||destination.Equals("firmament",StringComparison.OrdinalIgnoreCase)){
            try{
                if(LifestreamBusy()){Status="Lifestream 正在执行其他任务";return false;}
                DalamudServices.PluginInterface.GetIpcSubscriber<string,object>("Lifestream.ExecuteCommand").InvokeAction("firmament");
                if(!LifestreamBusy()){Status="Lifestream 未接受前往天穹街的请求";return false;}
                Start(Ownership.Lifestream);Status="正在前往天穹街";return true;
            }catch(Exception e){Status="前往天穹街需要可用的 Lifestream："+e.GetBaseException().Message;return false;}
        }
        var record=AetheryteRecordManager.Instance().AllRecords.FirstOrDefault(r=>string.Equals(r.Name,destination,StringComparison.OrdinalIgnoreCase));
        if(record==null){Status="未找到传送目的地："+destination;return false;}
        return TryTeleport(record);
    }
    private bool StartShard(AetheryteRecord record){
        if(!DalamudServices.PluginInterface.GetIpcSubscriber<uint,bool>("Lifestream.AethernetTeleportById").InvokeFunc(record.RowID))return false;
        pendingShard=null;Start(Ownership.Lifestream);Status="正在进行城内传送";return true;
    }
    private void Start(Ownership value){owner=value;started=Environment.TickCount64;}
    public void Tick(){
        if(disposed||owner==Ownership.None)return;
        if(!GameState.IsLoggedIn){owner=Ownership.None;pendingShard=null;return;}
        if(pendingShard is { } shard){
            if(Environment.TickCount64-started>180_000){Cancel();Status="城内传送等待超时";return;}
            if(Environment.TickCount64-started<2000||NativeTransition||GameState.TerritoryType!=pendingRootZone)return;
            try{if(!StartShard(shard)){Cancel();Status="无法在当前位置进行城内传送";}}catch(Exception e){Cancel();Status=e.GetBaseException().Message;}
        }else _=IsBusy;
    }
    public void Cancel(){
        if(owner==Ownership.None)return;
        var previous=owner;owner=Ownership.None;pendingShard=null;
        try{
            if(previous==Ownership.Lifestream){if(LifestreamBusy())DalamudServices.PluginInterface.GetIpcSubscriber<object>("Lifestream.Abort").InvokeAction();}
            else if(NativeTransition)TeleportCommand.Cancel();
        }catch(Exception e){Status="取消传送失败："+e.GetBaseException().Message;DalamudServices.PluginLog.Warning(e,"Yanjiu teleport cancellation failed");}
    }
    public void Dispose(){if(disposed)return;Cancel();disposed=true;}
}

