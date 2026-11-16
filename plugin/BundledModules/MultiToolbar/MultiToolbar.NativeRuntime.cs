// SPDX-License-Identifier: GPL-3.0-or-later
using KamiToolKit.BaseTypes;
using Control = FFXIVClientStructs.FFXIV.Client.Game.Control.Control;
using KamiToolKit.Nodes;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using OmenTools;
using Yanjiu.NativeUI;

namespace OmniToolbox.TreePublic;

public sealed partial class ToolbarRenderer
{
    private NativeToolbarAddon? nativeToolbar;
    private long nextNativeToolbarRefresh;
    private void TickNativeToolbar()
    {
        if(runtimeLifetime is null||runtimeLifetime.IsStopping)return;
        SyncAuxiliaryBars();
        TickOneNativeToolbar();
        foreach(var bar in auxiliaryToolbars)bar.TickOneNativeToolbar();
    }
    private void TickOneNativeToolbar()
    {
        if(Environment.TickCount64<nextNativeToolbarRefresh)return;
        nextNativeToolbarRefresh=Environment.TickCount64+250;
        if(!DService.Instance().ClientState.IsLoggedIn){nativeToolbar?.Close();return;}
        nativeToolbar??=new NativeToolbarAddon(this);
        if(!nativeToolbar.IsOpen)nativeToolbar.Open();
        nativeToolbar.Refresh();
    }
    private async void OpenNativeMenu(MultiToolbarWidgetConfig widget)
    {
        try{
            if(ModuleText.Owner is not MultiToolbar owner)return;
            var controls=BuildNativeMenu(widget);
            await NativeUi.OpenAsync(owner,new NativePanel("ToolbarMenu_"+barID,"工具栏 · "+WidgetTypeLabel(widget.Type),controls));
        }catch(Exception error){DService.Instance().Log.Error(error,"打开工具栏原生菜单失败");}
    }
    private unsafe void NativeClick(MultiToolbarWidgetConfig widget,bool right)
    {
        try{
            if(runtimeLifetime is null||runtimeLifetime.IsStopping)return;
            if(right){
                switch(widget.Type){
                    case MultiToolbarWidgetType.PluginList:ExecuteCommand("/xlplugins");return;
                    case MultiToolbarWidgetType.CommandList:case MultiToolbarWidgetType.DtrList:NativeSettingsRequested?.Invoke();return;
                    case MultiToolbarWidgetType.RetainerList:InteractWithRetainerBell();return;
                    case MultiToolbarWidgetType.GearsetSwitcher:ExecuteOptionalCommand(config.EquipmentPlannerCommand,"配装插件");return;
                    case MultiToolbarWidgetType.Durability:OpenRepairWindow();return;
                    case MultiToolbarWidgetType.Societies:TeleportToSociety(config.TrackedSocietyID);return;
                    case MultiToolbarWidgetType.OnlineStatus:OpenOnlineStatusDetail();return;
                    case MultiToolbarWidgetType.CustomButton:ExecuteCommand(widget.RightCommand);return;
                    case MultiToolbarWidgetType.Location:ExecuteOptionalCommand(config.IDInformationCommand,"ID 信息插件");return;
                    case MultiToolbarWidgetType.InventorySpace:ExecuteCommand("/keyitem");return;
                    case MultiToolbarWidgetType.Flag:if(AgentMap.Instance()!=null)AgentMap.Instance()->FlagMarkerCount=0;return;
                    case MultiToolbarWidgetType.Volume:var system=DService.Instance().GameConfig.System;system.Set("IsSndMaster",!system.GetBool("IsSndMaster"));return;
                    case MultiToolbarWidgetType.Coordinates:var coords=GetPlayerMapCoordinates();NativeClipboard(()=>{System.Windows.Forms.Clipboard.SetText($"{coords.X:F1}, {coords.Y:F1}");return "";});return;
                }
                return;
            }
            switch(widget.Type){
                case MultiToolbarWidgetType.CustomButton:ExecuteBuiltInWidgetAction(widget);return;
                case MultiToolbarWidgetType.ToolbarPin:config.IsLocked=!config.IsLocked;saveConfig();return;
                case MultiToolbarWidgetType.WalkingIndicator:if(Control.Instance()!=null)Control.Instance()->IsWalking=!Control.Instance()->IsWalking;return;
                case MultiToolbarWidgetType.Clock:config.ClockTimeSource=(config.ClockTimeSource+1)%3;saveConfig();return;
                case MultiToolbarWidgetType.InventorySpace:ExecuteCommand("/inventory");return;
                case MultiToolbarWidgetType.Location:if(UIModule.Instance()!=null)UIModule.Instance()->ExecuteMainCommand(16);return;
                case MultiToolbarWidgetType.Flag:var destination=GetFlagAetheryte();if(destination!=null)teleportService.TryTeleport(destination.RowID);return;
                case MultiToolbarWidgetType.FpsCounter:case MultiToolbarWidgetType.Coordinates:case MultiToolbarWidgetType.WorldName:case MultiToolbarWidgetType.MailIndicator:return;
                default:OpenNativeMenu(widget);return;
            }
        }catch(Exception error){DService.Instance().Log.Error(error,"工具栏原生操作失败");}
    }

    private sealed unsafe class NativeToolbarAddon:NativeAddon
    {
        private readonly ToolbarRenderer owner;
        private readonly List<(MultiToolbarWidgetConfig Widget,TextButtonNode Button)> buttons=[];
        private string signature="";
        [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
        internal NativeToolbarAddon(ToolbarRenderer renderer)
        {
            owner=renderer;InternalName="YanjiuToolbar_"+renderer.barID;Title="多功能工具栏";
            Size=new(1000,100);RememberClosePosition=true;
        }
        protected override void OnSetup(AtkUnitBase* addon,Span<AtkValue> arguments)
        {
            signature="";buttons.Clear();Rebuild(addon);
        }
        private void Rebuild(AtkUnitBase* addon)
        {
            foreach(var row in buttons)row.Button.Dispose();buttons.Clear();
            var items=owner.config.Widgets.Where(item=>item.Enabled).OrderBy(item=>item.Side).ToArray();
            signature=string.Join("|",items.Select(item=>$"{System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(item)}:{item.Side}:{item.Type}"));
            float x=0,y=0;
            foreach(var widget in items){
                if(x+150>ContentSize.X){x=0;y+=32;}
                var captured=widget;
                var button=new TextButtonNode{Position=ContentStartPosition+new Vector2(x,y),Size=new(146,28),OnClick=()=>owner.NativeClick(captured,false)};
                button.AddEvent(AtkEventType.MouseClick,(listener,type,param,ev,data)=>{if(data!=null)DispatchPointerClick(data->MouseData.ButtonId,()=>owner.NativeClick(captured,false),()=>owner.NativeClick(captured,true));});
                button.AttachNode(addon);buttons.Add((widget,button));x+=150;
            }
        }
        internal void Refresh()
        {
            AtkUnitBase* addon=this;if(addon==null)return;
            var items=owner.config.Widgets.Where(item=>item.Enabled).OrderBy(item=>item.Side).ToArray();
            var current=string.Join("|",items.Select(item=>$"{System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(item)}:{item.Side}:{item.Type}"));
            if(current!=signature)Rebuild(addon);
            for(int i=0;i<buttons.Count;i++){
                var row=buttons[i];var label=owner.WidgetLabel(row.Widget,i);
                row.Button.String=string.IsNullOrEmpty(label)?WidgetTypeLabel(row.Widget.Type):label;
                row.Button.TextTooltip=WidgetTypeLabel(row.Widget.Type);
            }
        }
        protected override void OnFinalize(AtkUnitBase* addon){buttons.Clear();signature="";base.OnFinalize(addon);}
    }
}
