using OmniToolbox.UI;
// SPDX-License-Identifier: GPL-3.0-or-later
using Dalamud.Bindings.ImGui;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;
using OmniToolbox.Common.Module.Abstractions;
using OmniToolbox.Common.Module.Models;
using OmniToolbox.Common.Module.Enums;
using OmniToolbox.Host;
using OmniToolbox.UI.Theme;
using OmenTools;
using OmenTools.Extensions;
using OmenTools.Interop.Game.AddonEvent;
using TreeTools.Lifecycle;
namespace Yanjiu.Modules;
[ModuleApiVersion(1)]
public sealed unsafe partial class AutoRetainer : OmniModuleBase, Yanjiu.NativeUI.INativeModuleUI
{
 public sealed class Settings { public bool AutoPriceOnListing; public bool AutoCollect; public bool AutoCrystals; public bool AutoReprice; public int CrystalKeep=8000; public int CrystalPrice=999; public int Undercut=2; public int MinimumPrice=1; public int MaxDropPercent=20; }
 private Settings config=new();
 private readonly Queue<(string Label,Func<bool> Run)> steps=new();
 private long deadline,nextTick;private long queueGeneration;
 private ulong expectedRetainer;
 private NativeRetainerPanel? nativePanel;
 private bool bellSession; private readonly Queue<Action> pendingJobs=new(); private bool merging,noMergeItems;
 private string status="请打开雇员铃列表。";
 public override ModuleInfo Info {get;}=new(){Category=ModuleCategory.Interface};
 public override ModuleLocalization Localization {get;}=ModuleLocalization.Create(new Dictionary<string,string>{{"Title","独立自动雇员"},{"Description","独立收取并重新派遣雇员、取出金币；不依赖其他功能模块。"}});
 public override bool HasSettings=>true;
 private void NormalizeSettings()
 {
  config??=new();config.Undercut=Math.Clamp(config.Undercut,0,999999999);
  config.MinimumPrice=Math.Clamp(config.MinimumPrice,1,999999999);config.MaxDropPercent=Math.Clamp(config.MaxDropPercent,0,100);
  config.CrystalKeep=Math.Clamp(config.CrystalKeep,1,9999);config.CrystalPrice=Math.Clamp(config.CrystalPrice,1,999999999);
 }
 protected override void OnStart(FeatureLifetime activation)
 {
  nativePanel=new NativeRetainerPanel(this);
  activation.OnStopping(()=>{nativePanel?.Dispose();nativePanel=null;});
  NormalizeSettings();Stop("请打开雇员铃列表。");nextTick=0;bellSession=false;listingVisible=false;listingFinished=false;pendingJobs.Clear();steps.Clear();
  OmenTools.OmenService.LogMessageManager.Instance().RegPost(OnLog);
  activation.OnStopping(()=>OmenTools.OmenService.LogMessageManager.Instance().Unreg(OnLog));
  DService.Instance().AddonLifecycle.RegisterListener(Dalamud.Game.Addon.Lifecycle.AddonEvent.PreFinalize,"RetainerItemTransferList",OnMergeClosed);
  DService.Instance().AddonLifecycle.RegisterListener(Dalamud.Game.Addon.Lifecycle.AddonEvent.PreFinalize,"RetainerItemTransferProgress",OnMergeClosed);
  activation.OnStopping(()=>DService.Instance().AddonLifecycle.UnregisterListener(OnMergeClosed));
  DService.Instance().MarketBoard.OfferingsReceived+=OnOfferings;
  activation.OnStopping(()=>{DService.Instance().MarketBoard.OfferingsReceived-=OnOfferings;CancelQuote();});
  DalamudServices.Framework.Update+=Tick;
  activation.OnStopping(()=>{DalamudServices.Framework.Update-=Tick;Stop("已停用");});
 }
 public override bool TryHandleCommand(string args)
 {
  if(!IsEnabled)return false;
  if(args.Trim()=="stop"){Stop("已停止。");return true;}
  if(args.Trim() is not ("" or "open"))return false;
  if(Ready("RetainerList"))nativePanel?.Open();else DService.Instance().Chat.Print("[Yanjiu] 请先打开雇员铃列表。");
  return true;
 }
 public override bool DrawSettings()
 {
  var changed=ImGui.Checkbox("上架时自动填写市场价格",ref config.AutoPriceOnListing);
  changed |= ImGui.Checkbox("打开雇员铃时自动收取并重新派遣",ref config.AutoCollect);
  changed |= ImGui.Checkbox("打开雇员铃时自动上架超额水晶",ref config.AutoCrystals);
  changed |= ImGui.Checkbox("打开雇员铃时在自动任务之后改价",ref config.AutoReprice);
  if(ImGui.Button("打开操作面板"))TryHandleCommand("open");
  changed |= ImGui.InputInt("比最低同品质报价降低金币",ref config.Undercut);
  changed |= ImGui.InputInt("最低售价",ref config.MinimumPrice);
  changed |= ImGui.InputInt("最大降幅百分比",ref config.MaxDropPercent);
  config.Undercut=Math.Clamp(config.Undercut,0,999999999);config.MinimumPrice=Math.Clamp(config.MinimumPrice,1,999999999);config.MaxDropPercent=Math.Clamp(config.MaxDropPercent,0,100);
  changed |= ImGui.InputInt("水晶保留数量",ref config.CrystalKeep);
  changed |= ImGui.InputInt("水晶上架单价",ref config.CrystalPrice);
  config.CrystalKeep=Math.Clamp(config.CrystalKeep,1,9999);config.CrystalPrice=Math.Clamp(config.CrystalPrice,1,999999999);
  ImGui.TextWrapped("改价排除自己的雇员及套装挂单；没有有效报价或超出降幅限制时跳过。水晶仅上架超出保留数量的部分，单价按上述设置。");
  return changed;
 }
 private static AtkUnitBase* Addon(string name){var a=DService.Instance().GameGUI.GetAddonByName(name,1);return a.IsNull?null:(AtkUnitBase*)a.Address;}
 private static bool Ready(string name){var a=Addon(name);return a!=null&&a->IsVisible&&a->IsAddonAndNodesReady();}
 private void Stop(string message){queueGeneration++;CancelQuote();steps.Clear();pendingJobs.Clear();merging=false;mergeCancelled=false;mergeSubmitted=false;noMergeItems=false;expectedRetainer=0;listingFinished=true;status=message;}
 private void Add(string label,Func<bool> action)=>steps.Enqueue((label,action));
 private void AddRetainerSelection(ulong id)
 {
  Add("选择雇员",()=>{
   if(!Ready("RetainerList"))return false;
   var manager=RetainerManager.Instance();if(manager==null)return false;
   // Resolve the current sorted position rather than reusing a stale list index.
   for(uint i=0;i<manager->GetRetainerCount();i++){
    var r=manager->GetRetainerBySortedIndex(i);if(r==null||r->RetainerId!=id)continue;
    expectedRetainer=0;Addon("RetainerList")->Callback(2,(int)i,0,0);return true;
   }
   throw new InvalidOperationException("雇员列表已改变，找不到目标雇员。");
  });
  Add("等待目标雇员",()=>{
   var manager=RetainerManager.Instance();if(manager==null)return false;
   var active=manager->GetActiveRetainer();if(active==null||active->RetainerId!=id)return false;
   expectedRetainer=id;return true;
  });
 }
 private void Start(bool gil)
 {
  if(steps.Count>0||!Ready("RetainerList"))return;
  var manager=RetainerManager.Instance();if(manager==null)return;
  var now=FFXIVClientStructs.FFXIV.Client.System.Framework.Framework.GetServerTime();
  for(uint i=0;i<manager->GetRetainerCount();i++){
   var retainer=manager->GetRetainerBySortedIndex(i);if(retainer==null)continue;
   if(gil?retainer->Gil==0:retainer->VentureId==0||retainer->VentureComplete==0||retainer->VentureComplete+1>now)continue;
   AddRetainerSelection(retainer->RetainerId);
   if(gil)EnqueueWithdraw();else EnqueueVenture(retainer->VentureId,retainer->VentureComplete);
   Add("返回雇员列表",()=>{if(Ready("RetainerList"))return true;if(Ready("SelectString"))Addon("SelectString")->Callback(-1);return false;});
  }
  deadline=Environment.TickCount64+30000;status=steps.Count==0?"没有需要处理的雇员。":"开始处理。";
 }
 private void OnLog(uint id,FFXIVClientStructs.FFXIV.Client.UI.Misc.LogMessageQueueItem item){if(merging&&id==10350)noMergeItems=true;}
 private void Tick(Dalamud.Plugin.Services.IFramework framework)
 {
  try{
   if(!DService.Instance().ClientState.IsLoggedIn){nativePanel?.Close();Stop("退出登录，已停止。");bellSession=false;listingVisible=false;return;}
   if(!DService.Instance().Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.OccupiedSummoningBell]){
    nativePanel?.Close();Stop("已离开雇员铃交互，停止自动操作。");bellSession=false;listingVisible=false;return;
   }
   if(mergeCancelled){mergeCancelled=false;Stop("合并窗口被取消或关闭，未确认完成，停止后续任务。");return;}
   var open=Ready("RetainerList");
   if(open){if(nativePanel is {IsOpen:false})nativePanel.Open();}else if(nativePanel is {IsOpen:true})nativePanel.Close();
   if(open&&!bellSession){
    bellSession=true;
    if(config.AutoCollect)pendingJobs.Enqueue(()=>Start(false));
    if(config.AutoCrystals)pendingJobs.Enqueue(()=>StartInventory(true));
    if(config.AutoReprice)pendingJobs.Enqueue(StartReprice);
   }
   if(open&&steps.Count==0&&pendingJobs.Count>0)pendingJobs.Dequeue()();
   if(steps.Count==0){TickListingPrice();return;}
   if(!DService.Instance().ClientState.IsLoggedIn){nativePanel?.Close();Stop("退出登录，已停止。");return;}
   var now=Environment.TickCount64;if(now>=deadline){Stop("步骤超时："+steps.Peek().Label+"；结果未确认，已停止后续操作。");return;}if(now<nextTick)return;nextTick=now+350;
   if(expectedRetainer!=0&&!open){
    var manager=RetainerManager.Instance();if(manager==null)return;
    var active=manager->GetActiveRetainer();if(active==null)return;
    if(active->RetainerId!=expectedRetainer)throw new InvalidOperationException("当前雇员已改变，停止自动操作。");
   }
   // Only advance dialogue during the same summoning-bell interaction.
   if(expectedRetainer!=0&&Ready("Talk")){Addon("Talk")->Callback(0);return;}
   var generation=queueGeneration;var step=steps.Peek();status=step.Label;
   if(step.Run()&&generation==queueGeneration&&steps.Count>0){steps.Dequeue();deadline=now+30000;if(steps.Count==0)status=pendingJobs.Count>0?"等待下一项任务。":"队列已结束，请检查游戏中的实际结果。";}
  }catch(Exception e){Stop("已停止："+e.Message);}
 }
}


