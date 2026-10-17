// SPDX-License-Identifier: GPL-3.0-or-later
using System.Numerics;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;
using OmenTools.Info.Game.AetheryteRecord;
using OmenTools.Interop.Game.Helpers;
using OmenTools.OmenService;
using OmniToolbox.Host;
namespace OmniToolbox.Teleport;
public sealed record AetheryteDestination(uint ID,string Name,string MapName,uint TerritoryID,uint MapID,Vector2 Position,bool IsAetheryte,byte Group);
public sealed record AetheryteRouteStep(uint AetheryteID,string Name);
public sealed class AetheryteRouteResolver
{
    private IReadOnlyList<AetheryteDestination>? destinations;
    public IReadOnlyList<AetheryteDestination> Destinations=>destinations??=Build();
    private static IReadOnlyList<AetheryteDestination> Build(){
        var rows=new List<AetheryteDestination>();
        foreach(var data in DalamudServices.DataManager.GetExcelSheet<Aetheryte>()){
            var record=AetheryteRecord.Parse(data);
            if(record==null||record.IsHouse||record.Group is 253 or 254)continue;
            rows.Add(new(record.RowID,record.Name,record.GetZone().PlaceName.Value.Name.ToString(),record.ZoneID,record.MapID,new(record.Position.X,record.Position.Z),record.IsAetheryte,record.Group));
        }
        return rows;
    }
    public unsafe bool TryResolve(MapLinkPayload link,IReadOnlySet<uint> ignored,out AetheryteRouteStep[] route){
        route=[];
        if(!GameState.IsLoggedIn||!link.Map.IsValid||UIState.Instance()==null)return false;
        var position=PositionHelper.MapToWorld(new Vector2(link.XCoord,link.YCoord),link.Map.Value);
        return Resolve(Destinations,link.TerritoryType.RowId,link.Map.RowId,position,ignored,id=>UIState.Instance()->IsAetheryteUnlocked(id),out route);
    }
    public static bool Resolve(IEnumerable<AetheryteDestination> source,uint territory,uint map,Vector2 position,IReadOnlySet<uint> ignored,Func<uint,bool> unlocked,out AetheryteRouteStep[] route){
        route=[];if(!float.IsFinite(position.X)||!float.IsFinite(position.Y))return false;
        var all=source.ToArray();
        var candidates=all.Where(d=>d.TerritoryID==territory&&d.MapID==map&&!ignored.Contains(d.ID)&&unlocked(d.ID)).OrderBy(d=>Vector2.DistanceSquared(d.Position,position)).ThenBy(d=>d.ID);
        foreach(var closest in candidates){
            if(closest.IsAetheryte){route=[new(closest.ID,closest.Name)];return true;}
            var root=all.Where(d=>d.IsAetheryte&&d.Group==closest.Group&&d.Group!=0&&!ignored.Contains(d.ID)&&unlocked(d.ID)).OrderBy(d=>d.ID).FirstOrDefault();
            if(root==null)continue;
            route=[new(root.ID,root.Name),new(closest.ID,closest.Name)];return true;
        }
        return false;
    }
    public static void AppendRoutePayloads(List<Payload> payloads,IReadOnlyList<AetheryteRouteStep> route,DalamudLinkPayload link){
        if(route.Count==0)return;
        payloads.Add(link);payloads.Add(new TextPayload(route[0].Name));payloads.Add(RawPayload.LinkTerminator);
        foreach(var step in route.Skip(1))payloads.Add(new TextPayload(" → "+step.Name));
    }
}
