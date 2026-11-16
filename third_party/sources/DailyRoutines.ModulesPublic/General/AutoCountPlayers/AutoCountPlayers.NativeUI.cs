// SPDX-License-Identifier: GPL-3.0-or-later
using System.Numerics;
using KamiToolKit.BaseTypes;
using KamiToolKit.Nodes;
using DailyRoutines.Manager;
using DailyRoutines.Extensions;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using Lumina.Excel.Sheets;
using OmenTools.Info.Lumina;
using OmenTools.Interop.Game.Lumina;
using OmenTools.OmenService;
namespace DailyRoutines.ModulesPublic;
public unsafe partial class AutoCountPlayers
{
    private NativePlayers? nativePlayers;
    private string nativeHoveredPlayer="";
    private void ToggleNativePlayers(){if(nativePlayers?.IsOpen==true){nativePlayers.Close();return;}nativePlayers?.Dispose();nativePlayers=new(this);nativePlayers.Open();}
    private sealed record NativePlayerRow(string Name,uint Icon,string World,string Detail,string ObjectID="",bool Header=false);
    private List<NativePlayerRow> ReadNativePlayers(int tab,string search)
    {
        var rows=new List<NativePlayerRow>();
        if(ICondition.Instance().IsBetweenAreas)return rows;
        if(tab==0){
            foreach(var player in PlayersManager.Instance().PlayersAround.Where(player=>MatchesSearch(player.Name,search)))rows.Add(new(player.Name,player.ClassJob.Value.GetIcon(),player.HomeWorld.Value.Name.ToString(),"",player.GameObjectID.ToString()));
        }else if(tab==1){
            if(!IsPlayerSearchLocation)return rows;
            var info=IsPlayerSearchZone?(InfoProxyCommonList*)InfoProxySearch.Instance():(InfoProxyCommonList*)InfoProxyContentMember.Instance();if(info==null)return rows;
            foreach(var (dc,players) in GroupZonePlayersByDataCenter(info,search)){
                rows.Add(new(LuminaWrapper.GetDataCenterName(dc),0,"",players.Count.ToString(),Header:true));
                foreach(var player in players)rows.Add(new(player.NameString,LuminaWrapper.GetJobIcon(player.Job),LuminaWrapper.GetWorldName(player.HomeWorld),""));
            }
        }else{
            foreach(var record in config.TargetingHistories.AsEnumerable().Reverse().Where(record=>MatchesSearch(record.Name,search)))rows.Add(new(record.Name,LuminaGetter.GetRowOrDefault<ClassJob>(record.JobID).GetIcon(),LuminaWrapper.GetWorldName(record.HomeWorldID),$"{record.Duration:mm\\:ss}  {LuminaWrapper.GetZonePlaceName(record.ZoneID)}  {record.StartTime:MM/dd HH:mm}"));
        }
        return rows;
    }
    private sealed class NativePlayers:NativeAddon
    {
        private readonly AutoCountPlayers owner;
        private readonly List<(TextButtonNode Button,IconImageNode Icon,TextNode Label)> pool=[];
        private ScrollingNode<ResNode>? scroll;
        private TextNode? status;
        private TextInputNode? search;
        private TextButtonNode? zone;
        private NativePlayerRow[] data=[];
        private readonly string[] filters=["","",""];
        private int tab;
        private long nextRead;
        [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
        internal NativePlayers(AutoCountPlayers owner){this.owner=owner;InternalName="YanjiuNearbyPlayersNative";Title="周边玩家列表";Size=new(840,760);}
        protected override void OnSetup(AtkUnitBase* addon,Span<AtkValue> args)
        {
            pool.Clear();
            for(int i=0;i<3;i++){int selected=i;var button=new TextButtonNode{Position=ContentStartPosition+new Vector2(i*140,0),Size=new(132,28),String=new[]{"周边玩家","区域玩家","被选中历史"}[i],OnClick=()=>{tab=selected;owner.nativeHoveredPlayer="";search!.String=filters[tab];scroll!.ScrollToStart();nextRead=0;}};button.AttachNode(addon);if(i==1)zone=button;}
            search=new TextInputNode{Position=ContentStartPosition+new Vector2(0,38),Size=new(790,28),PlaceholderString="搜索玩家名称",OnInputReceived=value=>{filters[tab]=value.ToString();nextRead=0;}};search.AttachNode(addon);
            scroll=new ScrollingNode<ResNode>{Position=ContentStartPosition+new Vector2(0,78),Size=new(790,560),ScrollSpeed=48};scroll.AttachNode(addon);
            for(int i=0;i<14;i++){
                int slot=i;
                var button=new TextButtonNode{Size=new(770,46),String="",IsVisible=false,OnClick=()=>{
                    if(slot>=visible.Length)return;var row=visible[slot];if(row.ObjectID.Length==0)return;
                    var player=PlayersManager.Instance().PlayersAround.FirstOrDefault(player=>player.GameObjectID.ToString()==row.ObjectID);if(player!=null)NotifyPlayerPosition(player);
                }};button.AttachNode(scroll.ContentNode);
                button.AddEvent(AtkEventType.MouseOver,()=>{if(slot<visible.Length)owner.nativeHoveredPlayer=visible[slot].ObjectID;});
                button.AddEvent(AtkEventType.MouseOut,()=>owner.nativeHoveredPlayer="");
                var icon=new IconImageNode{FitTexture=true,Position=new(8,11),Size=new(24,24)};icon.AttachNode(button);
                var label=new TextNode{Position=new(42,2),Size=new(720,42),FontSize=13,TextFlags=TextFlags.MultiLine};label.AttachNode(button);
                pool.Add((button,icon,label));
            }
            status=new TextNode{Position=ContentStartPosition+new Vector2(0,650),Size=new(790,28),FontSize=12};status.AttachNode(addon);Read();Bind();
        }
        private NativePlayerRow[] visible=[];
        private void Read(){data=owner.ReadNativePlayers(tab,filters[tab]).ToArray();zone!.IsEnabled=IsPlayerSearchLocation;}
        private void Bind()
        {
            if(scroll==null)return;
            int height=Math.Max(560,data.Length*48);if(scroll.ContentNode.Height!=height){scroll.ContentNode.Height=height;scroll.RecalculateSizes();}
            int first=Math.Max(0,(int)(-scroll.ContentNode.Y/48));visible=data.Skip(first).Take(pool.Count).ToArray();
            for(int i=0;i<pool.Count;i++){
                var row=pool[i];row.Button.IsVisible=i<visible.Length;if(i>=visible.Length)continue;var item=visible[i];
                row.Button.Position=new(0,(first+i)*48);row.Button.IsEnabled=item.ObjectID.Length>0;row.Button.TextTooltip=item.ObjectID.Length>0?"定位玩家":"";
                row.Button.BackgroundNode.IsVisible=!item.Header;
                if(row.Icon.IconId!=item.Icon)row.Icon.IconId=item.Icon;row.Icon.FitTexture=true;row.Icon.Size=new(24,24);row.Icon.IsVisible=item.Icon>0;
                row.Label.X=item.Icon>0?42:8;
                row.Label.String=item.Name+(item.World.Length>0?"  @"+item.World:"")+(item.Detail.Length>0?"\n"+item.Detail:"");
            }
            status!.String=data.Length==0?"当前没有匹配记录；区域搜索结果收到后会自动更新。":$"{data.Length} 条记录";
        }
        protected override void OnUpdate(AtkUnitBase* addon){if(Environment.TickCount64>=nextRead){nextRead=Environment.TickCount64+500;Read();}Bind();}
        protected override void OnFinalize(AtkUnitBase* addon){owner.nativeHoveredPlayer="";pool.Clear();visible=[];data=[];scroll=null;base.OnFinalize(addon);}
    }
}
