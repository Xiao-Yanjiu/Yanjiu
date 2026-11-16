// SPDX-License-Identifier: GPL-3.0-or-later
using Yanjiu.NativeUI;
using Action = System.Action;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using OmenTools;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using OmenTools.OmenService;
using Lumina.Excel.Sheets;
namespace OmniToolbox.TreePublic;
public sealed partial class ToolbarRenderer
{
    private unsafe IReadOnlyList<NativeControl> BuildNativeMenu(MultiToolbarWidgetConfig widget)
    {
        var rows=new List<NativeControl>();
        void Button(string title,Action run)=>rows.Add(new NativeButton(title,run));
        void Label(string title,Func<string> read)=>rows.Add(new NativeLabel(title,read));
        switch(widget.Type){
            case MultiToolbarWidgetType.PluginList:
                RefreshPluginCache();
                foreach(var name in config.SelectedPlugins){
                    if(!installedPlugins.TryGetValue(name,out var plugin))continue;
                    var selected=plugin;
                    Button(plugin.Name,()=>{if(selected.HasMainUi)selected.OpenMainUi();else if(selected.HasConfigUi)selected.OpenConfigUi();});
                    if(plugin.HasConfigUi)Button(plugin.Name+" · 设置",selected.OpenConfigUi);
                }
                Button("编辑插件收纳",()=>NativeSettingsRequested?.Invoke());break;
            case MultiToolbarWidgetType.CommandList:
                foreach(var command in config.Commands.Where(item=>item.Enabled)){
                    var selected=command;Button(command.Name,()=>ExecuteBuiltInWidgetAction(selected));
                    if(!string.IsNullOrWhiteSpace(command.RightCommand))Button(command.Name+" · 右键指令",()=>ExecuteCommand(selected.RightCommand));
                }
                Button("编辑指令收纳",()=>NativeSettingsRequested?.Invoke());break;
            case MultiToolbarWidgetType.QuickCommands:
                foreach(var command in DService.Instance().Data.GetExcelSheet<MainCommand>().Where(item=>item.RowId!=0&&item.MainCommandCategory.RowId!=0&&!string.IsNullOrWhiteSpace(item.Name.ToString())).OrderBy(item=>item.MainCommandCategory.RowId).ThenBy(item=>item.SortID)){
                    uint id=command.RowId;Button(command.Name.ToString(),()=>{if(UIModule.Instance()!=null)UIModule.Instance()->ExecuteMainCommand(id);});
                }break;
            case MultiToolbarWidgetType.GearsetSwitcher:
                var sets=RaptureGearsetModule.Instance();
                if(sets!=null)for(byte i=0;i<100;i++){
                    var set=sets->GetGearset(i);if(set==null||!set->Flags.HasFlag(RaptureGearsetModule.GearsetFlag.Exists))continue;
                    byte id=i;Button($"{i+1}. {set->NameString}  IL {set->ItemLevel}",()=>LocalPlayerState.SwitchGearset(id));
                }
                Button("角色",()=>ExecuteCommand("/character"));break;
            case MultiToolbarWidgetType.Durability:
                Label("装备耐久",()=>GetDurabilityLabel());Button("修理",OpenRepairWindow);Button("精制魔晶石",()=>ExecuteCommand("/generalaction \"精制魔晶石\""));break;
            case MultiToolbarWidgetType.RetainerList:
                for(uint i=0;i<10;i++){
                    uint index=i;var manager=RetainerManager.Instance();if(manager==null||!manager->IsReady)break;
                    var item=manager->GetRetainerBySortedIndex(i);if(item==null||item->RetainerId==0||!item->Available)continue;
                    Label(item->NameString,()=>{
                        var current=RetainerManager.Instance();if(current==null||!current->IsReady)return "雇员数据不可用";
                        var retainer=current->GetRetainerBySortedIndex(index);if(retainer==null)return "";
                        return $"{retainer->NameString}  Lv.{retainer->Level}  金币 {retainer->Gil:N0}  道具 {retainer->ItemCount}/175  出售 {retainer->MarketItemCount}  探险 {(retainer->VentureComplete==0?"无":retainer->VentureComplete<=DateTimeOffset.UtcNow.ToUnixTimeSeconds()?"结束":TimeSpan.FromSeconds(retainer->VentureComplete-DateTimeOffset.UtcNow.ToUnixTimeSeconds()).ToString())}";
                    });
                }
                Button("交互雇员铃",InteractWithRetainerBell);break;
            case MultiToolbarWidgetType.Currencies:
                foreach(uint id in new uint[]{1,29,20,21,22,23,25,27,28,46,47,48,40,41,42,43,45}){
                    uint currency=id;if(!DService.Instance().Data.GetExcelSheet<Item>().TryGetRow(id,out var item))continue;
                    string name=item.Name.ToString();Label(name,()=>{var inventory=InventoryManager.Instance();return name+"  "+(inventory==null?0:inventory->GetInventoryItemCount(currency)).ToString("N0");});
                }
                Button("货币一览",()=>ExecuteCommand("/currency"));break;
            case MultiToolbarWidgetType.BattleEffects:
                foreach(var pair in new[]{("自身特效","BattleEffectSelf"),("队友特效","BattleEffectParty"),("其他玩家特效","BattleEffectOther"),("PvP 敌方特效","BattleEffectPvPEnemyPc"),("巴哈姆特尺寸","BahamutSize"),("凤凰尺寸","PhoenixSize"),("迦楼罗尺寸","GarudaSize"),("泰坦尺寸","TitanSize"),("伊弗利特尺寸","IfritSize")}){
                    var selected=pair;rows.Add(new NativeNumber(pair.Item1,()=>DService.Instance().GameConfig.UiConfig.GetUInt(selected.Item2),value=>DService.Instance().GameConfig.UiConfig.Set(selected.Item2,(uint)value),0,2));
                }break;
            case MultiToolbarWidgetType.Societies:
                foreach(var tribe in DService.Instance().Data.GetExcelSheet<BeastTribe>()){
                    var player=PlayerState.Instance();if(tribe.RowId==0||tribe.RowId>255||player==null||player->GetBeastTribeRank((byte)tribe.RowId)==0)continue;
                    uint id=tribe.RowId;string name=tribe.Name.ToString();
                    Button(name+" · 传送",()=>TeleportToSociety(id));
                    rows.Add(new NativeToggle(name+" · 追踪",()=>config.TrackedSocietyID==id,value=>{if(value)config.TrackedSocietyID=id;else if(config.TrackedSocietyID==id)config.TrackedSocietyID=0;saveConfig();}));
                }break;
            case MultiToolbarWidgetType.OnlineStatus:
                foreach(var id in OnlineStatusIds){
                    var player=PlayerState.Instance();if(player!=null&&!IsOnlineStatusAvailable(id,player))continue;
                    if(!DService.Instance().Data.GetExcelSheet<OnlineStatus>().TryGetRow(id,out var status))continue;
                    uint selected=id;Button(status.Name.ToString(),()=>{var info=InfoModule.Instance();var detail=info==null?null:(InfoProxyDetail*)info->GetInfoProxyById(InfoProxyId.Detail);if(detail!=null)detail->SendOnlineStatusUpdate(selected);});
                }
                Button("个人状态详情",OpenOnlineStatusDetail);break;
            case MultiToolbarWidgetType.Volume:
                while(config.VolumePresets.Count<5)config.VolumePresets.Add(new(){Name="音量预设 "+(config.VolumePresets.Count+1)});
                for(int i=0;i<5;i++){
                    int index=i;
                    Button(config.VolumePresets[i].Name,()=>{
                        SaveActiveVolumePreset();bool same=config.ActiveVolumePreset==index;config.ActiveVolumePreset=same?-1:index;
                        if(!same){var preset=config.VolumePresets[index];var system=DService.Instance().GameConfig.System;
                            foreach(var channel in VolumeChannels){if(preset.Volumes.TryGetValue(channel.Volume,out var volume))system.Set(channel.Volume,Math.Clamp(volume,0u,100u));
                                if(channel.Mute is {} mute&&preset.Toggles.TryGetValue(mute,out var muted))system.Set(mute,muted);
                                if(channel.Background is {} background&&preset.Toggles.TryGetValue(background,out var enabled))system.Set(background,enabled);}
                            foreach(var key in MusicOptions)if(preset.Toggles.TryGetValue(key,out var enabled))system.Set(key,enabled);
                        }SaveActiveVolumePreset();saveConfig();
                    });
                }
                rows.Add(new NativeText("当前预设名称",()=>config.ActiveVolumePreset>=0?config.VolumePresets[config.ActiveVolumePreset].Name:"",value=>{if(config.ActiveVolumePreset>=0){config.VolumePresets[config.ActiveVolumePreset].Name=value;saveConfig();}}){Enabled=()=>config.ActiveVolumePreset>=0});
                foreach(var key in MusicOptions){string selected=key;rows.Add(new NativeToggle(ModuleText.Get("Feature.MultiToolbar."+key),()=>DService.Instance().GameConfig.System.GetBool(selected),value=>{DService.Instance().GameConfig.System.Set(selected,value);SaveActiveVolumePreset();}));}
                rows.Add(new NativeToggle("后台静音",()=>!DService.Instance().GameConfig.System.GetBool("IsSoundAlways"),value=>{DService.Instance().GameConfig.System.Set("IsSoundAlways",!value);SaveActiveVolumePreset();}));
                foreach(var channel in VolumeChannels){
                    var selected=channel;rows.Add(new NativeNumber(channel.Label,()=>DService.Instance().GameConfig.System.GetUInt(selected.Volume),value=>{var system=DService.Instance().GameConfig.System;system.Set(selected.Volume,(uint)value);SaveActiveVolumePreset();},0,100));
                    if(channel.Mute is {} mute)rows.Add(new NativeToggle(channel.Label+" · 静音",()=>DService.Instance().GameConfig.System.GetBool(mute),value=>{DService.Instance().GameConfig.System.Set(mute,value);SaveActiveVolumePreset();}));
                    if(channel.Background is {} background)rows.Add(new NativeToggle(channel.Label+" · 后台播放",()=>DService.Instance().GameConfig.System.GetBool(background),value=>{DService.Instance().GameConfig.System.Set(background,value);SaveActiveVolumePreset();}));
                }break;
            case MultiToolbarWidgetType.MarkerControl:
                rows.Add(new NativeToggle("显示世界标记",()=>config.ShowWorldMarkerOverlay,value=>{config.ShowWorldMarkerOverlay=value;saveConfig();}));
                foreach(var type in WorldMarkerTypes){var selected=type;rows.Add(new NativeToggle(ModuleText.Get($"Feature.MultiToolbar.Marker{type}"),()=>config.EnabledWorldMarkers.Contains(selected),value=>{if(value)config.EnabledWorldMarkers.Add(selected);else config.EnabledWorldMarkers.Remove(selected);nextMarkerRefresh=0;saveConfig();}));}break;
            case MultiToolbarWidgetType.Clock:case MultiToolbarWidgetType.StackedClock:
                Label("时间",()=>$"本地 {DateTime.Now:HH:mm:ss}   服务器 {GetServerTime():HH:mm:ss}   艾欧泽亚 {GetEorzeaTime():HH:mm:ss}");break;
            default:Label("信息",()=>WidgetLabel(widget,0));break;
        }
        if(rows.Count==0)rows.Add(new NativeLabel("暂无可用条目。"));
        return rows;
    }
}
