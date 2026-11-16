// SPDX-License-Identifier: GPL-3.0-or-later
using KamiToolKit.BaseTypes;
using KamiToolKit.Nodes;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using OmenTools;
using OmenTools.OmenService;
using OmniToolbox.UI;
using Lumina.Excel.Sheets;
using Yanjiu.NativeUI;
using SystemAction=System.Action;
using FFXIVClientStructs.FFXIV.Client.System.Input;
namespace OmniToolbox.TreePublic;

public sealed partial class ToolbarRenderer
{
    private static void DispatchPointerClick(int button,SystemAction? left,SystemAction? right)
    {
        if(button==0)left?.Invoke();
        else if(button==1)right?.Invoke();
    }
    private NativeRuntimePopup? runtimePopup;
    private bool nativePopupDirty;
    private string? pendingNativeEditor;
    private void PumpNativePopupSafely()
    {
        try{PumpNativePopup();}
        catch(Exception error){DService.Instance().Log.Error(error,"工具栏原生菜单更新失败");ClosePopup();runtimePopup?.Dispose();runtimePopup=null;nativePopupDirty=false;}
    }
    private void PumpNativePopup()
    {
        if(!DService.Instance().ClientState.IsLoggedIn){activePopup=PopupKind.None;}
        if(pendingNativeEditor is {} editor){pendingNativeEditor=null;OpenNativeSettingsEditor(editor);ClosePopup();}
        if(activePopup==PopupKind.None){runtimePopup?.Dispose();runtimePopup=null;nativePopupDirty=false;return;}
        if(runtimePopup is {Finished:true}){runtimePopup.Dispose();runtimePopup=null;activePopup=PopupKind.None;return;}
        if(!nativePopupDirty)return;
        nativePopupDirty=false;runtimePopup?.Dispose();runtimePopup=null;
        if(runtimeLifetime is null||runtimeLifetime.IsStopping)return;
        runtimePopup=new NativeRuntimePopup(this,activePopup,activeWidgetPopupType,popupPosition);
        try{runtimePopup.Open();}catch{runtimePopup.Dispose();runtimePopup=null;activePopup=PopupKind.None;throw;}
    }
    private sealed class PopupDismissState
    {
        private bool armed;
        internal bool ShouldClose(bool focused,bool held,bool pressed,bool inside)
        {
            if(!focused)return false;
            if(!armed){if(!held&&!pressed)armed=true;return false;}
            return pressed&&!inside;
        }
    }
    private sealed record PopupRow(string Name,uint Icon=0,Func<string>? Detail=null,SystemAction? Click=null,SystemAction? Right=null,int Column=0,Func<bool>? Enabled=null,Dalamud.Plugin.IExposedPlugin? Plugin=null,Func<Lumina.Text.ReadOnly.ReadOnlySeString>? RichText=null);
    private unsafe List<PopupRow> BuildPopupRows(PopupKind kind,MultiToolbarWidgetType type)
    {
        var rows=new List<PopupRow>();
        void Row(string name,uint icon=0,Func<string>? detail=null,SystemAction? click=null,SystemAction? right=null,int column=0,Func<bool>? enabled=null)=>rows.Add(new(name,icon,detail,click,right,column,enabled));
        if(kind==PopupKind.Plugins){
            pluginCacheValid=false;RefreshPluginCache();
            foreach(var id in config.SelectedPlugins){
                if(!installedPlugins.TryGetValue(id,out var plugin)){Row(id,detail:()=>"未加载");continue;}
                var selected=plugin;string name=config.PluginDisplayNames.TryGetValue(id,out var alias)&&!string.IsNullOrWhiteSpace(alias)?alias:plugin.Name;
                rows.Add(new(name,Click:()=>{if(!selected.IsLoaded)TryEnablePlugin(selected);else if(selected.HasMainUi)selected.OpenMainUi();else if(selected.HasConfigUi)selected.OpenConfigUi();},Right:()=>{if(selected.HasConfigUi)selected.OpenConfigUi();},Plugin:plugin));
            }
            return rows;
        }
        if(kind==PopupKind.Commands){foreach(var item in config.Commands.Where(item=>item.Enabled)){
            var command=item;Row(item.Name,item.ShowIcon?item.GameIconID:0,click:()=>{if(config.Commands.Contains(command)&&command.Enabled)ExecuteBuiltInWidgetAction(command);},right:()=>{if(config.Commands.Contains(command)&&command.Enabled)ExecuteCommand(command.RightCommand);});
        }return rows;}
        if(kind==PopupKind.Dtr){
            foreach(var entry in DService.Instance().DTRBar.Entries.Where(entry=>config.CollapsedDtrTitles.Contains(entry.Title))){
                var selected=entry;
                void Click(Dalamud.Game.Gui.Dtr.MouseClickType button)=>selected.OnClick?.Invoke(new Dalamud.Game.Gui.Dtr.DtrInteractionEvent{ClickType=button,ModifierKeys=GetDtrModifierKeys(),Position=popupPosition});
                rows.Add(new PopupRow(entry.Title,Click:()=>Click(Dalamud.Game.Gui.Dtr.MouseClickType.Left),Right:()=>Click(Dalamud.Game.Gui.Dtr.MouseClickType.Right),RichText:()=>selected.Text is {} text?new Lumina.Text.ReadOnly.ReadOnlySeString(text.Encode()):selected.Title));
            }return rows;
        }
        switch(type){
            case MultiToolbarWidgetType.OnlineStatus:
                foreach(uint id in OnlineStatusIds){
                    var player=PlayerState.Instance();if(player!=null&&!IsOnlineStatusAvailable(id,player))continue;
                    if(!DService.Instance().Data.GetExcelSheet<OnlineStatus>().TryGetRow(id,out var item))continue;
                    uint selected=id;Row(item.Name.ToString(),(uint)item.Icon,click:()=>{
                        var info=InfoModule.Instance();var detail=info==null?null:(InfoProxyDetail*)info->GetInfoProxyById(InfoProxyId.Detail);if(detail!=null)detail->SendOnlineStatusUpdate(selected);
                    },right:OpenOnlineStatusDetail);
                }break;
            case MultiToolbarWidgetType.GearsetSwitcher:
                var sets=RaptureGearsetModule.Instance();if(sets==null)break;
                string[] names=["防护职业","治疗职业","近战职业","远程物理职业","远程魔法职业","能工巧匠","大地使者"];
                foreach(int group in new[]{0,1,2,3,4,6,5}){
                    int col=group<2?0:group<5?1:2;Row(names[group],column:col);
                    for(byte i=0;i<100;i++){
                        var set=sets->GetGearset(i);if(set==null||!set->Flags.HasFlag(RaptureGearsetModule.GearsetFlag.Exists))continue;
                        if(!DService.Instance().Data.GetExcelSheet<ClassJob>().TryGetRow(set->ClassJob,out var job)||GetGearsetCategory(job)!=group)continue;
                        byte selected=i;string label=set->NameString;short level=set->ItemLevel;uint icon=(uint)(62100+set->ClassJob);
                        Row(label,icon,()=>"IL "+level,()=>LocalPlayerState.SwitchGearset(selected),()=>OpenNativeGearsetContext(selected),col);
                    }
                }break;
            case MultiToolbarWidgetType.Durability:
                Row("全职业BIS配装",click:()=>ExecuteOptionalCommand(config.EquipmentPlannerCommand,"配装插件"));
                var inventory=InventoryManager.Instance();var equipment=inventory==null?null:inventory->GetInventoryContainer(InventoryType.EquippedItems);
                if(equipment!=null&&equipment->IsLoaded){
                    var slots=new List<(int Slot,int Spirit)>();for(int i=0;i<equipment->Size;i++){var item=equipment->GetInventorySlot(i);if(item!=null&&item->ItemId!=0)slots.Add((i,GetItemSpiritbond(item)));}
                    foreach(var slot in slots.OrderByDescending(item=>item.Spirit)){
                        int index=slot.Slot;var item=equipment->GetInventorySlot(index);if(!DService.Instance().Data.GetExcelSheet<Item>().TryGetRow(item->GetBaseItemId(),out var sheet))continue;
                        Row(sheet.Name.ToString(),(uint)sheet.Icon,()=>{
                            var manager=InventoryManager.Instance();var container=manager==null?null:manager->GetInventoryContainer(InventoryType.EquippedItems);var current=container==null||!container->IsLoaded?null:container->GetInventorySlot(index);
                            return current==null?"":$"{GetItemDurability(current)}% / {GetItemSpiritbond(current)}%";
                        });
                    }
                }
                Row("操作");foreach(uint id in DurabilityActionIds){
                    if(!IsGeneralActionUnlocked(id)||!DService.Instance().Data.GetExcelSheet<GeneralAction>().TryGetRow(id,out var action))continue;
                    uint selected=id;Row(action.Name.ToString(),(uint)action.Icon,click:()=>{var manager=ActionManager.Instance();if(manager!=null&&manager->GetActionStatus(ActionType.GeneralAction,selected)==0)manager->UseAction(ActionType.GeneralAction,selected);},enabled:()=>ActionManager.Instance()!=null&&ActionManager.Instance()->GetActionStatus(ActionType.GeneralAction,selected)==0);
                }break;
            case MultiToolbarWidgetType.RetainerList:
                if(!CanReadRetainers()){Row("当前不支持读取雇员：请回到原始服务器并退出副本。");break;}
                var retainers=RetainerManager.Instance();
                if(retainers==null||!retainers->IsReady){Row("正在请求雇员数据，收到后会自动刷新。");break;}
                for(uint i=0;i<10;i++){
                    var item=retainers->GetRetainerBySortedIndex(i);if(item==null||item->RetainerId==0||!item->Available)continue;
                    uint index=i;string name=item->NameString;uint icon=(uint)(62100+item->ClassJob);
                    Row(name,icon,()=>{var manager=RetainerManager.Instance();if(manager==null||!manager->IsReady)return "";var current=manager->GetRetainerBySortedIndex(index);if(current==null)return "";return $"Lv.{current->Level}  道具 {current->ItemCount}/175  金币 {current->Gil:N0}  出售 {current->MarketItemCount}  探险 {(current->VentureComplete==0?"无":current->VentureComplete<=DateTimeOffset.UtcNow.ToUnixTimeSeconds()?"结束":TimeSpan.FromSeconds(current->VentureComplete-DateTimeOffset.UtcNow.ToUnixTimeSeconds()).ToString())}";});
                }if(rows.Count==0)Row("尚未取得雇员数据，可先与雇员铃交互以同步。");break;
            case MultiToolbarWidgetType.Currencies:
                void Currency(uint id,bool weekly=false){
                    if(id is >=20 and <=22&&id-19!=(uint)LocalPlayerState.GrandCompany)return;
                    if(!DService.Instance().Data.GetExcelSheet<Item>().TryGetRow(id,out var item))return;
                    uint cap=item.StackSize;if(id==1)cap=0;uint fixedCap=cap;
                    Row(item.Name.ToString(),(uint)item.Icon,()=>{
                        uint limit=fixedCap;var player=PlayerState.Instance();if(id is >=20 and <=22&&player!=null&&DService.Instance().Data.GetExcelSheet<GrandCompanyRank>().TryGetRow(player->GetGrandCompanyRank(),out var rank))limit=rank.MaxSeals;
                        uint count=LocalPlayerState.GetItemCount(id);string text=limit==0?$"{count:N0}":$"{count:N0} / {limit:N0}";
                        if(weekly&&InventoryManager.Instance()!=null)text+=$" ({InventoryManager.Instance()->GetWeeklyAcquiredTomestoneCount():N0} / {InventoryManager.GetLimitedTomestoneWeeklyLimit():N0})";return text;
                    },()=>{config.TrackedCurrencyID=config.TrackedCurrencyID==id?0:id;saveConfig();});
                }
                foreach(uint id in new uint[]{1,29,21072,20,21,22})Currency(id);
                Row("狩猎货币");foreach(uint id in new uint[]{27,10307,26533})Currency(id);
                Row("亚拉戈神典石");foreach(var item in DService.Instance().Data.GetExcelSheet<TomestonesItem>())if(item.Tomestones.RowId is 2 or 3)Currency(item.Item.RowId,item.Tomestones.RowId==3);Currency(28);
                Row("PvP");foreach(uint id in new uint[]{25,36656})Currency(id);
                Row("生产采集货币");foreach(uint id in new uint[]{33913,33914,41784,41785,28063})Currency(id);Row("其他");Currency(26807);break;
            case MultiToolbarWidgetType.Societies:
                foreach(var tribe in DService.Instance().Data.GetExcelSheet<BeastTribe>()){
                    if(tribe.RowId==0||tribe.RowId>255)continue;var player=PlayerState.Instance();byte id=(byte)tribe.RowId;if(player==null||player->GetBeastTribeRank(id)==0)continue;
                    Row(tribe.Name.ToString(),tribe.Icon,()=>{var current=PlayerState.Instance();return current==null?"":$"等级 {current->GetBeastTribeRank(id)}  {current->GetBeastTribeCurrentReputation(id)} / {current->GetBeastTribeNeededReputation(id)}";},()=>TeleportToSociety(id),()=>{config.TrackedSocietyID=config.TrackedSocietyID==id?0u:(uint)id;saveConfig();});
                }break;
            case MultiToolbarWidgetType.Weather:
                var weatherManager=WeatherManager.Instance();if(weatherManager==null)break;
                Row(GameState.MapData.PlaceName.Value.Name.ToString());
                for(int i=1;i<=4;i++){int offset=i-1;var id=weatherManager->GetWeatherForDaytime((ushort)GameState.TerritoryType,i);if(DService.Instance().Data.GetExcelSheet<Lumina.Excel.Sheets.Weather>().TryGetRow(id,out var weather))Row(weather.Name.ToString(),(uint)weather.Icon,()=>TimeSpan.FromSeconds(GetNextWeatherSeconds()+offset*1400).ToString());}break;
            case MultiToolbarWidgetType.QuickCommands:
                var categories=DService.Instance().Data.GetExcelSheet<MainCommandCategory>();
                foreach(var category in categories.Where(item=>item.RowId is >=1 and <=7)){
                    uint categoryId=category.RowId;Row(category.Name.ToString(),click:()=>runtimePopup?.SelectCategory(categoryId),column:0);
                }
                foreach(var item in DService.Instance().Data.GetExcelSheet<MainCommand>().Where(item=>item.RowId!=0&&item.MainCommandCategory.RowId!=0&&!string.IsNullOrWhiteSpace(item.Name.ToString())).OrderBy(item=>item.SortID)){
                    uint selected=item.RowId;uint category=item.MainCommandCategory.RowId;
                    rows.Add(new PopupRow(item.Name.ToString(),Click:()=>{if(UIModule.Instance()!=null)UIModule.Instance()->ExecuteMainCommand(selected);},Column:(int)category+10));
                }break;
            default:
                // Other tool menus retain their own controls through the native control renderer below.
                break;
        }
        return rows;
    }
}

public sealed partial class ToolbarRenderer
{
    private sealed unsafe partial class NativeRuntimePopup:NativeAddon
    {
        private readonly ToolbarRenderer owner;
        private readonly PopupKind kind;
        private readonly MultiToolbarWidgetType type;
        private Vector2 anchor;
        private readonly List<(PopupRow Row,ResNode Root,TextNode Label,TextNode Value,TextButtonNode Button)> rows=[];
        private readonly List<ScrollingNode<ResNode>> columns=[];
        private readonly List<SystemAction> updates=[];
        private TextNode? status;
        private uint category=1;
        private string search="";
        private long refreshAt;
        private string retainerKey="";private long retainerRetryAt,retainerOpenedAt;private int retainerRetries;
        private bool binding;
        private PopupDismissState dismiss=new();
        internal bool Finished {get;private set;}
        [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
        internal NativeRuntimePopup(ToolbarRenderer owner,PopupKind kind,MultiToolbarWidgetType type,Vector2 anchor)
        {
            this.owner=owner;this.kind=kind;this.type=kind==PopupKind.Widget?type:default;this.anchor=anchor;
            InternalName="YanjiuToolbarPopup_"+owner.barID;Title=kind switch{PopupKind.Plugins=>"插件收纳",PopupKind.Commands=>"指令收纳",PopupKind.Dtr=>"服务器信息栏",_=>WidgetTypeLabel(type)};
            Size=this.type==MultiToolbarWidgetType.GearsetSwitcher?new(900,920):this.type==MultiToolbarWidgetType.QuickCommands?new(280,300):this.type==MultiToolbarWidgetType.RetainerList?new(860,540):this.type==MultiToolbarWidgetType.Volume?new(760,680):new(520,600);
            RememberClosePosition=false;
        }
        private void Run(SystemAction action){if(binding||Finished||owner.runtimeLifetime is null||owner.runtimeLifetime.IsStopping)return;try{action();}catch(Exception error){if(status!=null)status.String=error.GetBaseException().Message;}}
        protected override void OnSetup(AtkUnitBase* addon,Span<AtkValue> args)
        {
            Finished=false;dismiss=new();rows.Clear();columns.Clear();updates.Clear();
            if(kind==PopupKind.Widget&&type==MultiToolbarWidgetType.QuickCommands){SetupQuickCommandMenu(addon);return;}
            float header=0;
            if(kind==PopupKind.Plugins){
                new TextInputNode{Position=ContentStartPosition,Size=new(ContentSize.X-40,28),PlaceholderString="搜索插件…",OnInputReceived=value=>{search=value.ToString();Relayout();}}.AttachNode(addon);
                new CircleButtonNode{Icon=KamiToolKit.Enums.CircleButtonIcon.GearCog,Position=ContentStartPosition+new Vector2(ContentSize.X-30,0),Size=new(26,26),TextTooltip="编辑插件收纳",OnClick=()=>Run(()=>{owner.OpenNativeSettingsEditor("插件");owner.ClosePopup();})}.AttachNode(addon);header=36;
            }else if(kind==PopupKind.Commands){
                new TextButtonNode{Position=ContentStartPosition,Size=new(150,28),String="编辑指令收纳",OnClick=()=>Run(()=>{owner.OpenNativeSettingsEditor("指令");owner.ClosePopup();})}.AttachNode(addon);header=36;
            }
            if(kind==PopupKind.Widget&&type==MultiToolbarWidgetType.GearsetSwitcher){BuildGearsetHeader(addon);header=96;}
            int count=type==MultiToolbarWidgetType.GearsetSwitcher?3:type==MultiToolbarWidgetType.QuickCommands?2:1;
            for(int i=0;i<count;i++){
                float width=ContentSize.X/count;float x=i*width;
                if(type==MultiToolbarWidgetType.QuickCommands){width=i==0?140:ContentSize.X-148;x=i==0?0:148;}
                var scroll=new ScrollingNode<ResNode>{Position=ContentStartPosition+new Vector2(x,header),Size=new(width-6,ContentSize.Y-header-32),ScrollSpeed=36};scroll.AttachNode(addon);columns.Add(scroll);
            }
            PopulateRows();retainerKey=ReadRetainerKey();retainerOpenedAt=Environment.TickCount64;retainerRetryAt=retainerOpenedAt+5000;retainerRetries=0;
            status=new TextNode{Position=ContentStartPosition+new Vector2(0,ContentSize.Y-28),Size=new(ContentSize.X,26),FontSize=12};status.AttachNode(addon);
            Relayout();Refresh();
            var screen=AtkStage.Instance()->ScreenSize;
            float popupX=Math.Clamp(anchor.X,0,Math.Max(0,screen.Width-Size.X));
            float targetY=owner.config.Alignment==MultiToolbarAlignment.Bottom?anchor.Y-Size.Y:anchor.Y;
            float y=Math.Clamp(targetY,0,Math.Max(0,screen.Height-Size.Y));addon->SetPosition((short)popupX,(short)y);
        }
        private void PopulateRows()
        {
            foreach(var item in rows)item.Root.Dispose();rows.Clear();
            int count=columns.Count;
            var data=owner.BuildPopupRows(kind,type);
            foreach(var row in data){
                int col=type==MultiToolbarWidgetType.QuickCommands?(row.Column==0?0:1):Math.Clamp(row.Column,0,count-1);
                float width=columns[col].Width-18;
                var root=new ResNode{Size=new(width,38)};root.AttachNode(columns[col].ContentNode);
                var button=new TextButtonNode{Size=new(width,36),OnClick=()=>Run(()=>{if(row.Enabled?.Invoke()==false)return;row.Click?.Invoke();if(kind==PopupKind.Plugins||kind==PopupKind.Commands||kind==PopupKind.Widget&&type==MultiToolbarWidgetType.OnlineStatus)owner.ClosePopup();}),IsEnabled=row.Click!=null,TextTooltip=row.Name};button.AttachNode(root);if(row.Click==null)button.BackgroundNode.IsVisible=false;
                // Raw MouseClick adds HasCollision and consumes the native button's mouse path.
                // Dispatch both buttons here; OnClick remains the keyboard/controller path.
                button.AddEvent(AtkEventType.MouseOver,()=>{if(button.IsEnabled)button.Timeline?.PlayAnimation(2);});
                button.AddEvent(AtkEventType.MouseOut,()=>{if(button.IsEnabled)button.Timeline?.PlayAnimation(4);});
                button.AddEvent(AtkEventType.MouseDown,()=>{if(button.IsEnabled)button.Timeline?.PlayAnimation(3);});
                button.AddEvent(AtkEventType.MouseUp,()=>{if(button.IsEnabled)button.Timeline?.PlayAnimation(2);});
                button.AddEvent(AtkEventType.MouseClick,(listener,eventType,param,ev,eventData)=>{
                    if(eventData==null||!button.IsEnabled)return;
                    DispatchPointerClick(eventData->MouseData.ButtonId,button.OnClick,()=>Run(()=>row.Right?.Invoke()));
                });
                var visuals=new ResNode{Size=new(width,36)};visuals.AttachNode(button);
                button.AddEvent(AtkEventType.MouseDown,()=>{if(button.IsEnabled)visuals.Y=1;});
                button.AddEvent(AtkEventType.MouseUp,()=>visuals.Y=0);
                button.AddEvent(AtkEventType.MouseOut,()=>visuals.Y=0);
                float inset=row.Icon!=0||row.Plugin!=null?36:8;
                if(row.Icon!=0)new IconImageNode{IconId=row.Icon,FitTexture=true,Position=new(6,7),Size=new(24,24)}.AttachNode(visuals);
                if(row.Plugin is {} plugin){
                    var image=new ImGuiImageNode{FitTexture=true,Position=new(6,7),Size=new(24,24),IsVisible=false};image.AttachNode(visuals);
                    Dalamud.Bindings.ImGui.ImTextureID bound=default;
                    updates.Add(()=>{var texture=PluginIconResolver.GetIcon(plugin,24);if(!PluginIconResolver.IsUsableTexture(texture))return;if(bound!=texture.Handle){image.LoadTexture(texture.CreateWrapSharingLowLevelResource());image.TextureSize=texture.Size;bound=texture.Handle;}image.FitTexture=true;image.Size=new(24,24);image.IsVisible=true;});
                    new CircleButtonNode{Icon=KamiToolKit.Enums.CircleButtonIcon.GearCog,Position=new(width-30,6),Size=new(24,24),TextTooltip="插件设置",IsEnabled=plugin.HasConfigUi,OnClick=()=>Run(()=>row.Right?.Invoke())}.AttachNode(root);
                }
                var label=new TextNode{Position=new(inset,0),Size=new(width-inset-(row.Plugin!=null?36:8),36),FontSize=14,String=row.Name};label.AttachNode(visuals);
                var detail=new TextNode{Position=new(width*.55f,0),Size=new(width*.45f-8,36),FontSize=13,AlignmentType=AlignmentType.Right};detail.AttachNode(visuals);
                if(row.Detail!=null)label.Width=width*.55f-inset;
                if(type==MultiToolbarWidgetType.RetainerList){label.Width=width*.22f-inset;detail.X=width*.22f;detail.Width=width*.78f-8;}
                rows.Add((row,root,label,detail,button));
            }
            if(data.Count==0)BuildControlMenu(columns[0].ContentNode);
        }
        private string ReadRetainerKey()
        {
            if(type!=MultiToolbarWidgetType.RetainerList)return "";
            if(!CanReadRetainers())return "unavailable";
            var manager=RetainerManager.Instance();if(manager==null||!manager->IsReady)return "loading";
            var ids=new List<string>();for(uint i=0;i<10;i++){var entry=manager->GetRetainerBySortedIndex(i);if(entry!=null&&entry->RetainerId!=0&&entry->Available)ids.Add($"{entry->RetainerId}:{entry->ClassJob}:{entry->NameString}");}
            return "ready:"+string.Join("|",ids);
        }
        private void RefreshRetainerRows()
        {
            if(type!=MultiToolbarWidgetType.RetainerList)return;
            string key=ReadRetainerKey();
            if(key!=retainerKey){
                if(retainerKey=="unavailable"){retainerOpenedAt=Environment.TickCount64;retainerRetryAt=retainerOpenedAt+5000;retainerRetries=0;}
                retainerKey=key;if(status!=null)status.String="";PopulateRows();Relayout();
            }
            if((key=="loading"||key=="ready:")&&retainerRetries<3&&Environment.TickCount64>=retainerRetryAt){retainerRetries++;retainerRetryAt=Environment.TickCount64+5000;OnInventoryWidgetPopupOpened(type);}
            if((key=="loading"||key=="ready:")&&Environment.TickCount64-retainerOpenedAt>=15000&&status!=null)status.String="暂未收到雇员数据，请先与雇员铃交互一次同步；收到后本窗口会自动更新。";
        }
        private void BuildGearsetHeader(AtkUnitBase* addon)
        {
            var module=RaptureGearsetModule.Instance();if(module==null||module->CurrentGearsetIndex<0)return;
            var current=module->GetGearset((byte)module->CurrentGearsetIndex);if(current==null)return;
            new IconImageNode{IconId=GetCurrentGearsetIcon(),FitTexture=true,Position=ContentStartPosition,Size=new(48,48)}.AttachNode(addon);
            new TextNode{Position=ContentStartPosition+new Vector2(56,0),Size=new(400,24),FontSize=14,String=GetCurrentGearsetLabel()}.AttachNode(addon);
            new TextNode{Position=ContentStartPosition+new Vector2(56,24),Size=new(400,24),FontSize=13,String=$"等级 {LocalPlayerState.GetClassJobLevel(current->ClassJob,false)} / {current->ItemLevel}"}.AttachNode(addon);
            new TextButtonNode{Position=ContentStartPosition+new Vector2(ContentSize.X-150,0),Size=new(150,28),String="全职业BIS配装",OnClick=()=>Run(()=>ExecuteOptionalCommand(owner.config.EquipmentPlannerCommand,"配装插件"))}.AttachNode(addon);
            void ActionButton(uint partsID,uint partID,string title,int column,SystemAction action)
            {
                owner.gearsetCharacterUld??=DService.Instance().Data.GetFile<Lumina.Data.Files.UldFile>("ui/uld/Character.uld");
                if(owner.gearsetCharacterUld is {} uld){
                    foreach(var parts in uld.Parts){if(parts.Id!=partsID||partID>=parts.Parts.Length)continue;var part=parts.Parts[partID];
                        foreach(var asset in uld.AssetData){if(asset.Id!=part.TextureId)continue;
                            new TextureButtonNode{TexturePath=new string(asset.Path).TrimEnd('\0'),TextureCoordinates=new(part.U,part.V),TextureSize=new(part.W,part.H),Position=ContentStartPosition+new Vector2(56+column*36,50),Size=new(30,30),TextTooltip=title,OnClick=()=>Run(action)}.AttachNode(addon);return;
                        }
                    }
                }
                new CircleButtonNode{Icon=KamiToolKit.Enums.CircleButtonIcon.GearCog,Position=ContentStartPosition+new Vector2(56+column*36,50),Size=new(28,28),TextTooltip=title,OnClick=()=>Run(action)}.AttachNode(addon);
            }
            ActionButton(13,2,"推荐装备",0,()=>{var agents=AgentModule.Instance();if(agents!=null)agents->GetAgentByInternalId(AgentId.RecommendEquip)->Show();});
            ActionButton(13,3,"关联幻化模板",1,()=>{var manager=ActionManager.Instance();if(manager!=null&&manager->GetActionStatus(ActionType.GeneralAction,25)==0)manager->UseAction(ActionType.GeneralAction,25);});
            ActionButton(15,4,"更新套装",2,()=>{var manager=RaptureGearsetModule.Instance();if(manager!=null&&manager->CurrentGearsetIndex>=0)manager->UpdateGearset(manager->CurrentGearsetIndex);});
            ActionButton(15,8,"新建套装",3,()=>{var manager=RaptureGearsetModule.Instance();if(manager!=null){int id=manager->CreateGearset();if(id>=0)LocalPlayerState.SwitchGearset((byte)id);}});
            ActionButton(15,3,"随机套装",4,()=>{var manager=RaptureGearsetModule.Instance();if(manager==null)return;var candidates=GetRandomGearsetCandidates(manager);if(candidates.Count>0)LocalPlayerState.SwitchGearset(candidates[Random.Shared.Next(candidates.Count)]);});
        }
        private void BuildControlMenu(ResNode root)
        {
            if(type==MultiToolbarWidgetType.Volume){BuildVolumeMenu(root);return;}
            var controls=owner.BuildNativeMenu(new MultiToolbarWidgetConfig{Type=type});float y=0;float width=root.Width-18;
            foreach(var control in controls){
                float top=y;
                if(control is NativeButton button){new TextButtonNode{Position=new(0,y),Size=new(width,28),String=button.Label,OnClick=()=>Run(button.Click)}.AttachNode(root);}
                else if(control is NativeText text){
                    new TextNode{Position=new(0,y),Size=new(width,22),FontSize=13,String=text.Label}.AttachNode(root);y+=24;
                    new TextInputNode{Position=new(0,y),Size=new(width,28),String=text.Read(),IsEnabled=control.Enabled?.Invoke()!=false,OnInputComplete=value=>Run(()=>text.Write(value.ToString()))}.AttachNode(root);
                }
                else if(control is NativeToggle toggle){var node=new CheckboxNode{Position=new(0,y),Size=new(width,28),String=toggle.Label,OnClick=value=>Run(()=>toggle.Write(value))};node.AttachNode(root);updates.Add(()=>{binding=true;try{node.IsChecked=toggle.Read();node.IsEnabled=control.Enabled?.Invoke()!=false;}finally{binding=false;}});}
                else if(control is NativeNumber number){
                    new TextNode{Position=new(0,y),Size=new(width,22),FontSize=13,String=number.Label}.AttachNode(root);y+=24;
                    var slider=new FloatSliderNode{Position=new(0,y),Size=new(width,24),Min=(float)number.Minimum,Max=(float)number.Maximum,Value=(float)number.Read(),Step=1};slider.FloatValueNode.IsVisible=true;slider.OnValueChanged=value=>Run(()=>number.Write(value));slider.AttachNode(root);
                }else if(control is NativeLabel label){var labelNode=new TextNode{Position=new(0,y),Size=new(width,30),FontSize=13,String=label.Value?.Invoke()??label.Label};labelNode.AttachNode(root);updates.Add(()=>labelNode.String=label.Value?.Invoke()??label.Label);}
                y+=36;
            }
            root.Height=Math.Max(columns[0].Height,y);columns[0].RecalculateSizes();
        }
        private void BuildVolumeMenu(ResNode root)
        {
            var controls=owner.BuildNativeMenu(new MultiToolbarWidgetConfig{Type=MultiToolbarWidgetType.Volume});
            float width=root.Width-18,buttonWidth=(width-24)/5;
            var presets=controls.OfType<NativeButton>().Take(5).ToArray();
            for(int i=0;i<presets.Length;i++){
                int index=i;var preset=presets[i];
                var button=new TextButtonNode{Position=new(i*(buttonWidth+6),0),Size=new(buttonWidth,28),String=preset.Label,TextTooltip=preset.Label,OnClick=()=>Run(preset.Click)};button.AttachNode(root);
                updates.Add(()=>{button.String=owner.config.VolumePresets[index].Name;button.IsChecked=owner.config.ActiveVolumePreset==index;});
            }
            var rename=controls.OfType<NativeText>().Single();bool editingName=false;
            new TextNode{Position=new(0,36),Size=new(110,28),FontSize=13,String="预设名称"}.AttachNode(root);
            var nameInput=new TextInputNode{Position=new(114,36),Size=new(width-114,28),String=rename.Read(),OnInputComplete=value=>Run(()=>rename.Write(value.ToString())),OnFocused=()=>editingName=true,OnUnfocused=()=>editingName=false};nameInput.AttachNode(root);
            updates.Add(()=>{nameInput.IsEnabled=rename.Enabled?.Invoke()!=false;if(!editingName)nameInput.String=rename.Read();});
            float y=78;
            foreach(var channel in VolumeChannels){
                var selected=channel;var number=controls.OfType<NativeNumber>().Single(field=>field.Label==selected.Label);
                new TextNode{Position=new(0,y),Size=new(94,28),FontSize=13,String=selected.Label}.AttachNode(root);
                var slider=new FloatSliderNode{Position=new(96,y+3),Size=new(width-298,24),Min=0,Max=100,Step=1,Value=(float)number.Read(),OnValueChanged=value=>Run(()=>number.Write(value))};slider.FloatValueNode.IsVisible=true;slider.AttachNode(root);
                updates.Add(()=>{binding=true;try{float value=(float)number.Read();if(slider.Value!=value)slider.Value=value;}finally{binding=false;}});
                if(selected.Mute is {} mute){
                    var toggle=controls.OfType<NativeToggle>().Single(field=>field.Label==selected.Label+" · 静音");
                    var node=new CheckboxNode{Position=new(width-186,y),Size=new(82,28),String="静音",IsChecked=toggle.Read(),OnClick=value=>Run(()=>toggle.Write(value))};node.AttachNode(root);
                    updates.Add(()=>{binding=true;try{node.IsChecked=toggle.Read();}finally{binding=false;}});
                }
                if(selected.Background is {} background){
                    var toggle=controls.OfType<NativeToggle>().Single(field=>field.Label==selected.Label+" · 后台播放");
                    var node=new CheckboxNode{Position=new(width-100,y),Size=new(100,28),String="后台播放",IsChecked=toggle.Read(),OnClick=value=>Run(()=>toggle.Write(value))};node.AttachNode(root);
                    updates.Add(()=>{binding=true;try{node.IsChecked=toggle.Read();}finally{binding=false;}});
                }
                y+=38;
            }
            int option=0;
            foreach(var toggle in controls.OfType<NativeToggle>().Where(field=>!field.Label.Contains(" · "))){
                float x=option%2*(width/2),top=y+(option/2)*34;
                var check=new CheckboxNode{Position=new(x,top),Size=new(width/2-8,28),String=toggle.Label,IsChecked=toggle.Read(),OnClick=value=>Run(()=>toggle.Write(value))};check.AttachNode(root);
                updates.Add(()=>{binding=true;try{check.IsChecked=toggle.Read();}finally{binding=false;}});option++;
            }
            root.Height=Math.Max(columns[0].Height,y+((option+1)/2)*34);columns[0].RecalculateSizes();
        }
        internal void SelectCategory(uint value){category=value;Relayout();}
        private void Relayout()
        {
            if(rows.Count==0)return;float[] y=new float[columns.Count];
            foreach(var item in rows){int col=type==MultiToolbarWidgetType.QuickCommands?(item.Row.Column==0?0:1):Math.Clamp(item.Row.Column,0,columns.Count-1);
                bool show=(search.Length==0||item.Row.Name.Contains(search,StringComparison.OrdinalIgnoreCase)||(item.Row.Plugin?.InternalName.Contains(search,StringComparison.OrdinalIgnoreCase)??false))&&(type!=MultiToolbarWidgetType.QuickCommands||item.Row.Column==0||item.Row.Column==category+10);
                item.Root.IsVisible=show;if(!show)continue;item.Root.Y=y[col];y[col]+=38;
            }
            for(int i=0;i<columns.Count;i++){columns[i].ContentNode.Height=Math.Max(columns[i].Height,y[i]);columns[i].RecalculateSizes();}
        }
        private void Refresh(){foreach(var item in rows){if(item.Row.RichText!=null)item.Label.String=item.Row.RichText();item.Value.String=item.Row.Detail?.Invoke()??"";item.Button.IsEnabled=item.Row.Click!=null&&(item.Row.Enabled?.Invoke()!=false);}foreach(var update in updates){try{update();}catch(Exception error){if(status!=null)status.String=error.GetBaseException().Message;}}}
        protected override void OnUpdate(AtkUnitBase* addon){
            if(type==MultiToolbarWidgetType.QuickCommands)ApplyQuickCommandCategory();
            var input=UIInputData.Instance();
            if(input!=null){
                var cursor=input->CursorInputs;var buttons=MouseButtonFlags.LBUTTON|MouseButtonFlags.RBUTTON;
                float scale=addon->Scale;float left=addon->X,top=addon->Y;
                bool inside=cursor.PositionX>=left&&cursor.PositionX<left+Size.X*scale&&cursor.PositionY>=top&&cursor.PositionY<top+Size.Y*scale;
                if(type==MultiToolbarWidgetType.QuickCommands)inside|=owner.IsPointerOverQuickCommandBar(cursor.PositionX,cursor.PositionY);
                if(dismiss.ShouldClose(cursor.IsGameWindowFocused,(cursor.MouseButtonHeldFlags&buttons)!=0,(cursor.MouseButtonPressedFlags&buttons)!=0,inside)){
                    owner.ClosePopup();Finished=true;Close();return;
                }
            }
            if(Environment.TickCount64<refreshAt)return;refreshAt=Environment.TickCount64+500;try{RefreshRetainerRows();Refresh();}catch(Exception error){if(status!=null)status.String=error.GetBaseException().Message;}}
        protected override void OnFinalize(AtkUnitBase* addon){Finished=true;rows.Clear();columns.Clear();updates.Clear();base.OnFinalize(addon);}
    }
}

public sealed partial class ToolbarRenderer
{
    private async void OpenNativeGearsetContext(byte id)
    {
        try{if(ModuleText.Owner is not MultiToolbar module)return;
            var controls=CreateGearsetContext(id);
            await NativeUi.OpenAsync(module,new NativePanel("ToolbarGearsetContext_"+barID,"套装操作",controls));
        }catch(Exception error){DService.Instance().Log.Error(error,"打开套装原生菜单失败");}
    }
    private unsafe IReadOnlyList<NativeControl> CreateGearsetContext(byte id)
    {
        bool Valid(){var manager=RaptureGearsetModule.Instance();var set=manager==null?null:manager->GetGearset(id);return runtimeLifetime is {IsStopping:false}&&set!=null&&set->Flags.HasFlag(RaptureGearsetModule.GearsetFlag.Exists);}
        int Neighbor(int direction){var manager=RaptureGearsetModule.Instance();if(manager==null)return -1;var original=manager->GetGearset(id);if(original==null||!DService.Instance().Data.GetExcelSheet<ClassJob>().TryGetRow(original->ClassJob,out var source))return -1;
            int group=GetGearsetCategory(source);for(int target=id+direction;target>=0&&target<100;target+=direction){var set=manager->GetGearset(target);if(set!=null&&set->Flags.HasFlag(RaptureGearsetModule.GearsetFlag.Exists)&&DService.Instance().Data.GetExcelSheet<ClassJob>().TryGetRow(set->ClassJob,out var job)&&GetGearsetCategory(job)==group)return target;}return -1;}
        return new NativeControl[]{
            new NativeButton("上移套装",()=>{int target=Neighbor(-1);if(target>=0)AgentGearSet.Instance()->ReassignGearsetId(id,target);}){Enabled=()=>Valid()&&Neighbor(-1)>=0},
            new NativeButton("下移套装",()=>{int target=Neighbor(1);if(target>=0)AgentGearSet.Instance()->ReassignGearsetId(id,target);}){Enabled=()=>Valid()&&Neighbor(1)>=0},
            new NativeButton("关联幻化模板",()=>{var manager=RaptureGearsetModule.Instance();var set=manager->GetGearset(id);if(UIState.Instance()->IsUnlockLinkUnlocked(15)&&TerritoryInfo.Instance()->InSanctuary)AgentMiragePrismMiragePlate.Instance()->OpenForGearset(id,set->GlamourSetLink,(ushort)AgentCharaCard.Instance()->AddonId);}){Enabled=Valid},
            new NativeButton("取消关联幻化模板",()=>RaptureGearsetModule.Instance()->LinkGlamourPlate(id,0)){Enabled=Valid},
            new NativeButton("编辑肖像",()=>{if(AgentBannerEditor.Instance()->IsActivatable())AgentGearSet.Instance()->OpenBannerEditorForGearset(id);}){Enabled=Valid},
            new NativeButton("重命名",()=>AgentGearSet.Instance()->OpenRenameDialog(id)){Enabled=Valid},
            new NativeButton("删除套装",()=>{var manager=RaptureGearsetModule.Instance();if(manager->CurrentGearsetIndex!=id)AgentGearSet.Instance()->OpenDeleteDialog(id);}){Enabled=Valid}
        };
    }
}
