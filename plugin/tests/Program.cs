using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Security.Cryptography;
using Yanjiu.Modules;
using Yanjiu.UI;

if(args.Length!=2)throw new ArgumentException("Pass project root and Dalamud library directory");
var root=Path.GetFullPath(args[0]);
var api=Path.GetFullPath(args[1]);
AssemblyLoadContext.Default.Resolving+=(context,name)=>{
 foreach(var folder in new[]{(Environment.GetEnvironmentVariable("YANJIU_ARTIFACT_PATH")??root+"/artifacts/plugin"),api}){var file=Path.Combine(folder,name.Name+".dll");if(File.Exists(file))return context.LoadFromAssemblyPath(file);}return null;};
var status=await Tests.Run(root,api);Console.WriteLine(status);

static class Tests
{
 static void Check(bool test,string title){if(!test)throw new Exception(title);}
 public static async Task<string> Run(string root,string api)
 {
  Yanjiu.Services.ConfigDirectory=root+"/tests/runtime";Directory.CreateDirectory(Yanjiu.Services.ConfigDirectory);Directory.CreateDirectory(Path.Combine(Yanjiu.Services.ConfigDirectory,"modules"));
  File.Delete(root+"/tests/runtime/modules/DRSample.json");
  var refs=Directory.EnumerateFiles(api,"*.dll").Concat(Directory.EnumerateFiles((Environment.GetEnvironmentVariable("YANJIU_ARTIFACT_PATH")??root+"/artifacts/plugin"),"*.dll")).Concat(Directory.EnumerateFiles(Path.GetDirectoryName(typeof(object).Assembly.Location)!,"*.dll"));
  var compiler=new SourceCompiler(refs.Concat(SourceCompiler.ReferencePaths()));
  Check(SourceCompiler.ReferenceFiles([null,"",root+"/missing-directory",(Environment.GetEnvironmentVariable("YANJIU_ARTIFACT_PATH")??root+"/artifacts/plugin")]).Any(path=>Path.GetFileName(path)=="Yanjiu.dll"),"empty in-memory assembly paths skipped");
  _=new SourceCompiler();
  var blankHelp=new Dalamud.Game.Command.CommandInfo((_,_)=>{});
  var existingHelp=new Dalamud.Game.Command.CommandInfo((_,_)=>{}){HelpMessage="模块原有帮助"};
  CommandMetadata.EnsureHelp(new Dictionary<string,Dalamud.Game.Command.CommandInfo>{{"/test",blankHelp},{"/existing",existingHelp}});
  Check(blankHelp.HelpMessage.Contains("/test")&&existingHelp.HelpMessage=="模块原有帮助","host fills missing command metadata and preserves module help");
  var commandProbeSource="""
  public class CommandProbe:Yanjiu.Modules.ModuleBase{public string Received{get;private set;}="";public override Yanjiu.Modules.ModuleDescriptor Descriptor=>new("命令测试","","","");public override bool TryHandleCommand(string args){Received=args;return true;}}
  """;
  using(var probeBinary=compiler.Compile(Encoding.UTF8.GetBytes(commandProbeSource),"CommandProbe.cs")){
   using var probe=probeBinary.Create();var probeEntry=new ModuleEntry{Path="probe",Origin="本地",Preference=new(),Instance=probe};
   Check(ModuleCommandRouter.Dispatch([probeEntry],"/yanjiu","CommandProbe").Contains("启用"),"disabled module commands do not enable automation");
   probe.SetEnabled(true);
   Check(ModuleCommandRouter.Dispatch([probeEntry],"/yanjiu","  commandprobe   play track name ")=="","module command dispatch accepts case and spacing");
   Check((string)probeBinary.ModuleType.GetProperty("Received")!.GetValue(probe)! =="play track name","module receives complete argument tail");
   Check(ModuleCommandRouter.Dispatch([probeEntry],"/yanjiu","UnknownModule").Contains("未找到"),"unknown command cannot trigger an unrelated module");
   Check(ModuleCommandRouter.Dispatch([probeEntry],"/ymusic","CommandProbe")=="","empty module arguments remain supported");
  }
  Check(ModuleFileActions.IsManagedDownload(root+"/tests/runtime",root+"/tests/runtime/online/repo/sample.dll"),"online module deletion accepts managed download");
  Check(!ModuleFileActions.IsManagedDownload(root+"/tests/runtime",root+"/tests/runtime/modules/sample.dll"),"online module deletion cannot remove local configuration");
  Check(!ModuleFileActions.IsManagedDownload(root+"/tests/runtime",root+"/tests/runtime/online/../sample.dll"),"online module deletion rejects path escape");

  bool invalidUtf8Rejected=false;try{using var invalid=compiler.Compile([0xff,0xfe,0x80],"invalid.cs");}catch(ModuleCompilationException e){invalidUtf8Rejected=e.Message.Contains("UTF-8");}Check(invalidUtf8Rejected,"invalid source encoding is rejected instead of silently corrupting text");
  Check(DailyRoutines.Manager.LanguageManager.Get("LeaveDutyCommandDescription",new Dictionary<string,object>{{"command","leaveduty"}}).Contains("/pdr leaveduty"),"real DR named command localization can construct module metadata");
  Check(DailyRoutines.Manager.LanguageManager.Format("{name} {count:000}",new Dictionary<string,object>{{"name","{literal}"},{"count",7}})=="{literal} 007","named localization preserves literal value braces and numeric formats");
  Check(DailyRoutines.Manager.LanguageManager.Format("{0} {1:000}","value",7)=="value 007","positional localization retained");
  var installedHost=Path.Combine(Environment.GetEnvironmentVariable("YANJIU_ARTIFACT_PATH")??root+"/artifacts/plugin","Yanjiu.dll");
  Check(Yanjiu.PluginFiles.ResolveAssemblyPath(installedHost,"")==Path.GetFullPath(installedHost),"installed plugin path works when Assembly.Location is empty");
  Check(Yanjiu.PluginFiles.ResolveAssemblyPath(null,installedHost)==Path.GetFullPath(installedHost),"offline assembly path fallback");
  bool missingHost=false;try{Yanjiu.PluginFiles.ResolveAssemblyPath(null,"");}catch(InvalidDataException){missingHost=true;}Check(missingHost,"missing plugin path produces actionable error");
  var memoryHost=Assembly.Load(File.ReadAllBytes(installedHost));
  Check(memoryHost.Location=="","reproduce Dalamud memory-loaded host");
  memoryHost.GetType("Yanjiu.PluginFiles")!.GetMethod("Initialize")!.Invoke(null,[installedHost]);
  var memoryCatalog=memoryHost.GetType("Yanjiu.Modules.DRCatalog")!;
  foreach(var expected in new[]{("DR",311),("Omni",0)}){
   var catalog=memoryCatalog.GetMethod("Load")!.Invoke(null,[expected.Item1])!;
   Check(((System.Collections.ICollection)catalog).Count==expected.Item2,"memory-loaded host reads complete "+expected.Item1+" catalog and verifies installed host hash");
  }
  var source="""
  using OmniToolbox.Common.Module.Abstractions;using OmniToolbox.Common.Module.Models;using OmniToolbox.Common.Module.Enums;
  public class LifecycleSample:ModuleBase{public override ModuleInfo Info{get;}=new(){Title="生命周期",Description="测试",Author="小烟酒",Category=ModuleCategory.Interface};protected override void OnEnable(){AppDomain.CurrentDomain.SetData("enabledCount",(int)(AppDomain.CurrentDomain.GetData("enabledCount")??0)+1);}protected override void OnDisable(){AppDomain.CurrentDomain.SetData("disabledCount",(int)(AppDomain.CurrentDomain.GetData("disabledCount")??0)+1);}protected override void OnDispose(){AppDomain.CurrentDomain.SetData("disposedCount",(int)(AppDomain.CurrentDomain.GetData("disposedCount")??0)+1);}}
  """;
  using(var compiled=compiler.Compile(Encoding.UTF8.GetBytes(source),"sample.cs")){
   var module=compiled.Create();Check(!module.IsEnabled,"new source module disabled");module.SetEnabled(true);module.SetEnabled(true);module.SetEnabled(false);module.SetEnabled(false);module.Dispose();module.Dispose();
   Check((int)AppDomain.CurrentDomain.GetData("enabledCount")! ==1&&(int)AppDomain.CurrentDomain.GetData("disabledCount")! ==1&&(int)AppDomain.CurrentDomain.GetData("disposedCount")! ==1,"exact lifecycle calls");
  }
  var failSource=source.Replace("LifecycleSample","FailSample").Replace("protected override void OnEnable(){", "protected override void OnEnable(){throw new Exception(\"expected failure\");");
  using(var compiled=compiler.Compile(Encoding.UTF8.GetBytes(failSource),"fail.cs")){using var module=compiled.Create();try{module.SetEnabled(true);}catch{}Check(!module.IsEnabled,"failed startup cannot remain enabled");}
  var dr="""
  using DailyRoutines.Common.Module.Abstractions;using DailyRoutines.Common.Module.Models;using DailyRoutines.Common.Module.Enums;
  public class DRSample:ModuleBase{public override ModuleInfo Info{get;}=new(){Title="DR配置",Description="测试",Author=["小烟酒"],Category=ModuleCategory.System};private class Config:ModuleConfig{public int Count{get;set;}}private Config? c;protected override void Init(){c=LoadConfig<Config>()??new();c.Count++;SaveConfig(c);}protected override void ConfigUI(){if(c==null)throw new NullReferenceException("Settings drawn before Init");AppDomain.CurrentDomain.SetData("drSettingsCount",(int)(AppDomain.CurrentDomain.GetData("drSettingsCount")??0)+1);}}
  """;
  for(int i=0;i<2;i++)using(var compiled=compiler.Compile(Encoding.UTF8.GetBytes(dr),"dr.cs")){using var module=compiled.Create();var entry=new ModuleEntry{Path="test",Origin="test",Preference=new(),Instance=module};entry.DrawSettings();module.SetEnabled(true);entry.DrawSettings();module.SetEnabled(false);entry.DrawSettings();}
  Check((int)AppDomain.CurrentDomain.GetData("drSettingsCount")! ==2,"settings drawn only after DR initialization, never while disabled");
  using(var persisted=System.Text.Json.JsonDocument.Parse(File.ReadAllText(root+"/tests/runtime/modules/DRSample.json")))Check(persisted.RootElement.GetProperty("Count").GetInt32()==2,"DR configuration persisted between instances");
  var injectedSource="""
  using OmniToolbox.Common.Module.Abstractions;using OmniToolbox.Common.Module.Models;
  public sealed class InjectedSampleConfig{public int Count;}
  public sealed class InjectedSample(InjectedSampleConfig config,System.Action saveConfig):ModuleBase{
   public override ModuleInfo Info{get;}=new(){Title="构造配置测试"};
   public int SavedCount=>config.Count;
   public override bool TryHandleCommand(string arguments){config.Count++;saveConfig();return true;}
  }
  """;
  File.Delete(Path.Combine(Yanjiu.Services.ConfigDirectory,"modules","InjectedSample.constructor.json"));
  using(var compiled=compiler.Compile(Encoding.UTF8.GetBytes(injectedSource),"injected.cs")){
   using(var module=compiled.Create()){Check(!module.IsEnabled,"Omni injected module starts disabled");module.TryHandleCommand("");Check(new ModuleEntry{Path="sample",Origin="本地",Preference=new(),Instance=module}.Title.EndsWith("[Omni]"),"imported Omni source is labelled by its actual base type");}
   using var restoredModule=compiled.Create();Check((int)compiled.ModuleType.GetProperty("SavedCount")!.GetValue(restoredModule)! ==1,"Omni constructor config and save callback persist between instances");
  }
  var unsupportedCtor=injectedSource.Replace("System.Action saveConfig","System.Action cancelTeleport").Replace("saveConfig();","cancelTeleport();");
  bool refusedCallback=false;try{using var invalid=compiler.Compile(Encoding.UTF8.GetBytes(unsupportedCtor),"unsupported.cs");}catch(ModuleCompilationException e){refusedCallback=e.Message.Contains("cancelTeleport");}Check(refusedCallback,"constructor cancellation callback cannot be mistaken for a save callback");
  using(var teleport=new OmniToolbox.Teleport.TeleportService()){Check(!teleport.IsBusy,"idle teleport is not busy");teleport.Cancel();teleport.Cancel();Check(!teleport.IsBusy,"idle cancellation is idempotent");}
  var destinations=new OmniToolbox.Teleport.AetheryteDestination[]{
   new(1,"主水晶","城",10,20,new(50,50),true,1),new(2,"近处碎晶","城",10,20,new(1,1),false,1),
   new(3,"其他地图","城",10,21,new(0,0),true,0),new(4,"未解锁","城",10,20,new(0,0),true,0)};
  Check(OmniToolbox.Teleport.AetheryteRouteResolver.Resolve(destinations,10,20,System.Numerics.Vector2.Zero,new HashSet<uint>(),id=>id!=4,out var route)&&route.Select(r=>r.AetheryteID).SequenceEqual(new uint[]{1,2}),"nearest shard routes through its unlocked main crystal and excludes other maps");
  Check(!OmniToolbox.Teleport.AetheryteRouteResolver.Resolve(destinations,10,20,System.Numerics.Vector2.Zero,new HashSet<uint>{1},id=>id!=4,out _),"ignored root also excludes inaccessible shard");
  Check(!OmniToolbox.Teleport.AetheryteRouteResolver.Resolve(destinations,99,20,System.Numerics.Vector2.Zero,new HashSet<uint>(),_=>true,out _),"no route from another territory");
  Check(!OmniToolbox.Teleport.AetheryteRouteResolver.Resolve(destinations,10,20,new(float.NaN,0),new HashSet<uint>(),_=>true,out _),"invalid coordinate rejected");
  var inventory=new OmniToolbox.Items.ObservedInventoryCache();
  var slot=new OmniToolbox.Items.InventorySnapshotItem(100,2,11,"",FFXIVClientStructs.FFXIV.Client.Game.InventoryType.Inventory1,0,OmniToolbox.Items.ItemInventoryLocation.Inventory,0,0);
  inventory.Update(1,11,slot.ContainerType,[slot],true);var revision=inventory.Revision;
  Check(!inventory.Update(1,11,slot.ContainerType,[slot],true)&&inventory.Revision==revision,"unchanged observation preserves revision");
  inventory.Update(1,11,slot.ContainerType,[],false);
  Check(inventory.Snapshot(1).Count==1&&inventory.Snapshot(2).Count==0,"unavailable container preserves observation and player isolation");
  bool invalidOwner=false;try{inventory.Update(1,12,slot.ContainerType,[slot],true);}catch(InvalidDataException){invalidOwner=true;}Check(invalidOwner,"mixed holder snapshot rejected");
  inventory.Update(1,11,slot.ContainerType,[],true);Check(inventory.Snapshot(1).Count==0,"observed empty container removes stale items");
  for(ulong holder=1;holder<=520;holder++)inventory.Update(1,holder,slot.ContainerType,[slot with{HolderID=holder}],true);
  Check(inventory.SegmentCount==512&&inventory.Snapshot(1).Count==512,"inventory cache evicts oldest containers at its bound");
  var optionalSource="""
  using OmniToolbox.Common.Module.Abstractions;using OmniToolbox.Common.Module.Models;
  public sealed class OptionalSample(System.Action? optionalCallback=null):ModuleBase{
   public override ModuleInfo Info{get;}=new(){Title="可选构造"};public bool UsesDefault=>optionalCallback==null;
  }
  """;
  using(var optional=compiler.Compile(Encoding.UTF8.GetBytes(optionalSource),"optional.cs")){using var module=optional.Create();Check((bool)optional.ModuleType.GetProperty("UsesDefault")!.GetValue(module)!,"declared optional callback uses actual default");}
  var emptyLinks=new HashSet<uint>();
  var emoteLinks=new HashSet<uint>{7};var hairLinks=new HashSet<uint>{8};
  Check(OmniToolbox.Items.CollectionItemIndex.Classify(25183,1,emptyLinks,emptyLinks)==OmniToolbox.Collections.CollectionType.Orchestrion,"current orchestrion action contract");
  Check(OmniToolbox.Items.CollectionItemIndex.Classify(29459,1,emptyLinks,emptyLinks)==OmniToolbox.Collections.CollectionType.Portrait,"framer kit category contract");
  Check(OmniToolbox.Items.CollectionItemIndex.Classify(2633,7,emoteLinks,hairLinks)==OmniToolbox.Collections.CollectionType.Emote&&OmniToolbox.Items.CollectionItemIndex.Classify(2633,8,emoteLinks,hairLinks)==OmniToolbox.Collections.CollectionType.Hairstyle,"unlock links resolve against real category indexes");
  Check(OmniToolbox.Items.CollectionItemIndex.Classify(2633,9,emoteLinks,hairLinks)==null&&OmniToolbox.Items.CollectionItemIndex.Classify(2136,1,emptyLinks,emptyLinks)==null&&OmniToolbox.Items.CollectionItemIndex.Classify(0,1,emptyLinks,emptyLinks)==null,"unrelated unlock links and recipe books are not misclassified as collectibles");
  var calculated=DailyRoutines.Extensions.RaphaelCalculationResults.Decode(7,Tuple.Create(7u,"Success","",new List<uint>{100,200},1234,5678));
  Check(calculated.Status==OmenTools.Dalamud.RaphaelCalculationStatus.Success&&calculated.FinalProgress==1234&&calculated.FinalQuality==5678&&calculated.Actions.SequenceEqual(new uint[]{100,200}),"Raphael progress and quality preserved from real response fields");
  Check(DailyRoutines.Extensions.RaphaelCalculationResults.Decode(8,Tuple.Create(7u,"Success","",new List<uint>(),1,1)).Status==OmenTools.Dalamud.RaphaelCalculationStatus.Failed,"mismatched Raphael request cannot populate another recipe");
  Check(DailyRoutines.Extensions.RaphaelCalculationResults.Decode(7,Tuple.Create(7u,"Unknown","",new List<uint>(),1,1)).Status==OmenTools.Dalamud.RaphaelCalculationStatus.Failed&&DailyRoutines.Extensions.RaphaelCalculationResults.Decode(7,Tuple.Create(7u,"Success","",new List<uint>(),-1,1)).Status==OmenTools.Dalamud.RaphaelCalculationStatus.Failed,"unknown status and invalid calculation values fail explicitly");
  var upstreamFailure=DailyRoutines.Extensions.RaphaelCalculationResults.Decode(7,Tuple.Create(7u,"Failed","求解器不可用",new List<uint>(),0,0));
  Check(upstreamFailure.Status==OmenTools.Dalamud.RaphaelCalculationStatus.Failed&&upstreamFailure.ErrorMessage=="求解器不可用","upstream solver failure remains visible");
  Check(OmniToolbox.Tooltips.DesynthesisEnhancementState.Calculate(99.9f,100).Status==OmniToolbox.Tooltips.DesynthesisStatus.BelowRecommended&&OmniToolbox.Tooltips.DesynthesisEnhancementState.Calculate(100,100).Status==OmniToolbox.Tooltips.DesynthesisStatus.MeetsRecommended,"desynthesis comparison handles fractional skill and exact item level");
  bool invalidSkill=false;try{OmniToolbox.Tooltips.DesynthesisEnhancementState.Calculate(float.NaN,100);}catch(ArgumentOutOfRangeException){invalidSkill=true;}Check(invalidSkill,"invalid desynthesis skill is rejected");
  var queryItems=new[]{
   new OmniToolbox.Items.InventorySnapshotItem(100,3,11,"",FFXIVClientStructs.FFXIV.Client.Game.InventoryType.Inventory1,0,OmniToolbox.Items.ItemInventoryLocation.Inventory,0,0),
   new OmniToolbox.Items.InventorySnapshotItem(100,4,11,"",FFXIVClientStructs.FFXIV.Client.Game.InventoryType.Inventory2,0,OmniToolbox.Items.ItemInventoryLocation.Inventory,0,0),
   new OmniToolbox.Items.InventorySnapshotItem(100,2,22,"雇员甲",FFXIVClientStructs.FFXIV.Client.Game.InventoryType.RetainerPage1,0,OmniToolbox.Items.ItemInventoryLocation.Retainer,0,0),
   new OmniToolbox.Items.InventorySnapshotItem(101,9,11,"",FFXIVClientStructs.FFXIV.Client.Game.InventoryType.Inventory1,1,OmniToolbox.Items.ItemInventoryLocation.Inventory,0,0)};
  var owned=OmniToolbox.Tooltips.ItemOwnedCountTooltip.Summarize(queryItems,100,OmniToolbox.Items.ItemInventoryLocation.All);
  Check(owned.Count==2&&owned.Single(g=>g.HolderID==11).Quantity==7&&owned.Single(g=>g.HolderID==22).Quantity==2,"owned counts combine bag pages while preserving retainer ownership and filtering item IDs");
  Check(OmniToolbox.Tooltips.ItemOwnedCountTooltip.Summarize(queryItems,100,OmniToolbox.Items.ItemInventoryLocation.Retainer).Single().Quantity==2,"disabled inventory locations excluded from owned totals");
  Check(OmniToolbox.Items.PlayerInventoryService.Classify(FFXIVClientStructs.FFXIV.Client.Game.InventoryType.HousingInteriorStoreroom1)==OmniToolbox.Items.ItemInventoryLocation.HousingStoreroom&&OmniToolbox.Items.PlayerInventoryService.Classify(FFXIVClientStructs.FFXIV.Client.Game.InventoryType.HousingInteriorPlacedItems1)==OmniToolbox.Items.ItemInventoryLocation.None,"housing storeroom classification excludes placed furnishings");
  var gcOffer=new DailyRoutines.Extensions.GrandCompanyOffer(100,200,1,3,0,0,1,99,false);
  var gcRow=new DailyRoutines.Extensions.GrandCompanyVisibleRow(4,100,200,3,false);
  Check(DailyRoutines.Extensions.GrandCompanyExchangeSurface.FindOffer([gcRow],gcOffer)==4&&DailyRoutines.Extensions.GrandCompanyExchangeSurface.FindOffer([gcRow,gcRow with{Index=5}],gcOffer)==-1,"GC purchase requires one unique live matching row");
  Check(DailyRoutines.Extensions.GrandCompanyExchangeSurface.FindOffer([gcRow with{UnitCost=201}],gcOffer)==-1&&DailyRoutines.Extensions.GrandCompanyExchangeSurface.FindOffer([gcRow with{RequiredRank=4}],gcOffer)==-1,"GC price and rank mismatch cannot select a row");
  var gcSurface=new DailyRoutines.Extensions.GrandCompanyExchangeSurface();gcSurface.Cancel();Check(gcSurface.Tick(),"cancelled GC operation cannot issue callbacks");
  Check(DailyRoutines.Extensions.GrandCompanyExchangeSurface.Observe(3,5,1000,1000,2,200)==DailyRoutines.Extensions.GrandCompanyObservation.Pending,"GC inventory update can arrive before seal update without repeating purchase");
  Check(DailyRoutines.Extensions.GrandCompanyExchangeSurface.Observe(3,5,1000,600,2,200)==DailyRoutines.Extensions.GrandCompanyObservation.Complete,"GC completion requires exact item and seal deltas");
  Check(DailyRoutines.Extensions.GrandCompanyExchangeSurface.Observe(3,6,1000,600,2,200)==DailyRoutines.Extensions.GrandCompanyObservation.Invalid&&DailyRoutines.Extensions.GrandCompanyExchangeSurface.Observe(3,5,1000,500,2,200)==DailyRoutines.Extensions.GrandCompanyObservation.Invalid,"unexpected GC item or seal changes cannot trigger another batch");
  var ipcFixture="""
  using DailyRoutines.Common.Module.Abstractions;using DailyRoutines.Common.Module.Models;
  using OmenTools.Dalamud.Attributes;using OmenTools.Dalamud.Abstractions;
  public sealed class IpcLifecycleSample:ModuleBase{
   public override ModuleInfo Info{get;}=new(){Title="IPC生命周期"};
   [IPCProvider("Yanjiu.Tests.IpcLifecycle")]public int Value=>42;
   [IPCSubscriber("Yanjiu.Tests.IpcLifecycle")]private IPCSubscriber<int> subscriber=null!;
   protected override void Init(){AppDomain.CurrentDomain.SetData("ipcLifecycleValue",subscriber.InvokeFunc());}
   protected override void Uninit(){subscriber?.Dispose();}
  }
  """;
  var omen=OmenTools.DService.Instance();
  var piProperty=typeof(OmenTools.DService).GetProperty("PI")!;
  var logProperty=typeof(OmenTools.DService).GetProperty("Log")!;
  var oldPi=piProperty.GetValue(omen);var oldLog=logProperty.GetValue(omen);
  piProperty.SetValue(omen,DispatchProxy.Create<Dalamud.Plugin.IDalamudPluginInterface,TestIpcPluginInterface>());
  logProperty.SetValue(omen,DispatchProxy.Create<Dalamud.Plugin.Services.IPluginLog,TestNoopInterface>());
  try{
   using(var fixture=compiler.Compile(Encoding.UTF8.GetBytes(ipcFixture),"ipc-lifecycle.cs")){
    using var module=fixture.Create();module.SetEnabled(true);
    Check((int)AppDomain.CurrentDomain.GetData("ipcLifecycleValue")! ==42,"actual public IPC registry binds instance provider and subscriber before module Init");
    Check(TestIpcCallGate.Callbacks.ContainsKey("Yanjiu.Tests.IpcLifecycle"),"module IPC provider lives while enabled");
    module.SetEnabled(false);Check(!TestIpcCallGate.Callbacks.ContainsKey("Yanjiu.Tests.IpcLifecycle"),"disable unregisters module IPC provider");
    module.SetEnabled(true);Check((int)AppDomain.CurrentDomain.GetData("ipcLifecycleValue")! ==42,"reenable rebuilds disposed subscriber binding");
   }
   Check(!TestIpcCallGate.Callbacks.ContainsKey("Yanjiu.Tests.IpcLifecycle"),"dispose unregisters module IPC provider");
   var brokenIpc=ipcFixture.Replace("IpcLifecycleSample","FailedIpcSample").Replace("AppDomain.CurrentDomain.SetData(\"ipcLifecycleValue\",subscriber.InvokeFunc());","throw new Exception(\"expected startup failure\");");
   using(var fixture=compiler.Compile(Encoding.UTF8.GetBytes(brokenIpc),"ipc-failure.cs")){
    using var module=fixture.Create();try{module.SetEnabled(true);}catch{}
    Check(!module.IsEnabled&&!TestIpcCallGate.Callbacks.ContainsKey("Yanjiu.Tests.IpcLifecycle"),"failed initialization releases already registered IPC providers");
   }
  }finally{piProperty.SetValue(omen,oldPi);logProperty.SetValue(omen,oldLog);TestIpcCallGate.Callbacks.Clear();}
  var rich=new Dalamud.Game.Text.SeStringHandling.SeStringBuilder().AddUiForeground(17).AddText("物品链接").AddUiForegroundOff().Build();
  var localized=DailyRoutines.Manager.LanguageManager.FormatSe("前缀{0}后缀",rich);
  Check(localized.Data.Span.IndexOf(rich.Encode())>=0,"localized rich-text payload bytes remain intact");
  Check(DailyRoutines.Manager.LanguageManager.FormatSe("{{{0}}}","值").ToString()=="{值}","localized escaped braces are preserved");
  Check(DailyRoutines.Manager.LanguageManager.Get("AutoLoginTitle")=="自动登录","real DR Chinese resource is available without original host");
  Check(new DailyRoutines.Common.Module.Models.ModulePermission{NeedAuth=true,AllTester=true,CNTester=true}.UnsupportedReason()==null,"Yanjiu does not require original host account or tester verification");
  Check(Themes.Available.Length==7&&Themes.Available.Select(p=>p.Name).Distinct().Count()==7,"seven unique palettes");
  var migrated=new Yanjiu.Configuration{Modules=[new(){Path="builtin:phone",Enabled=true},new(){Path="external-phone.cs",Origin="本地"}]};Check(migrated.RemoveRetiredBuiltInModules()&&migrated.Modules.Count==1&&migrated.Modules[0].Path=="external-phone.cs"&&!migrated.RemoveRetiredBuiltInModules(),"remove only retired built-in phone preference, migration idempotent");
  Check(typeof(Yanjiu.Plugin).Assembly.GetType("Yanjiu.BuiltIn.PhoneChat")==null,"plugin assembly no longer embeds phone support");
  Check(new Yanjiu.Configuration().Theme==Themes.NativeGameIndex,"new configurations default to native game theme");
  Themes.Configure(new Yanjiu.Configuration());Check(Themes.Current.NativeGame&&Themes.Current.Background==new System.Numerics.Vector4(49/255f,48/255f,49/255f,1)&&Math.Abs(Themes.Current.BorderThickness-1)<.001f,"native palette uses game gray and fine gold frame");
  Check(Themes.Current.Surface==Themes.Current.Background&&Themes.Current.Text==new System.Numerics.Vector4(238/255f,225/255f,197/255f,1),"native child panels and text match sampled game screenshot colors");
  var custom=new Yanjiu.Configuration{Theme=2,BorderWidth=3,ThemeColors=new(){["body"]=[.2f,.3f,.4f,1],["border"]=[1,1,1,1],["shadow"]=[.1f,.2f,.3f,.4f],["selected"]=[.7f,.6f,.5f,1]}};
  custom.Repositories.Add("https://github.com/Xiao-Yanjiu/OmniToolbox.xiaoyanjiu");custom.RepositoryAutoRefresh[custom.Repositories[0]]=true;
  var restored=System.Text.Json.JsonSerializer.Deserialize<Yanjiu.Configuration>(System.Text.Json.JsonSerializer.Serialize(custom))!;Themes.Configure(restored);
  Check(Themes.Current.Background==new System.Numerics.Vector4(.2f,.3f,.4f,1)&&Themes.Current.Border==System.Numerics.Vector4.One&&Themes.Current.Shadow.W==.4f&&Themes.Current.Selected.X==.7f&&Themes.BorderWidth==3,"custom palette and border width survive reload");
  Check(restored.RepositoryAutoRefresh[restored.Repositories[0]],"repository refresh choice survives reload");
  var safePalette=Themes.Resolve(Themes.Available[0],new Dictionary<string,float[]>{["body"]=[float.NaN,0,0,1],["border"]=[2,-1,.5f,1]});
  Check(safePalette.Background==Themes.Available[0].Background&&safePalette.Border==new System.Numerics.Vector4(1,0,.5f,1),"invalid colors rejected and finite components clamped");
  var proxy=DispatchProxy.Create<Dalamud.Plugin.IDalamudPluginInterface,NoopPluginInterface>();
  typeof(Yanjiu.Services).GetProperty("PluginInterface")!.SetValue(null,proxy);
  try{
   var disabledConfigSource="""
   using DailyRoutines.Common.Module.Abstractions;using DailyRoutines.Common.Module.Models;
   public class DisabledConfigSample:ModuleBase{
       private Config config=null!;
       public override ModuleInfo Info{get;}=new(){Title="停用配置"};
       private class Config:ModuleConfig{public bool Notify=true;public int Count=5;public System.Collections.Generic.List<uint> Items=new(){2,3};}
       protected override object CreateSettingsConfiguration(System.Type type)=>new Config{Count=9};
       protected override void Init(){config=LoadConfig<Config>()??new();System.AppDomain.CurrentDomain.SetData("disabled-config-started",config.Count);}
   }
   """;
   using(var configBinary=compiler.Compile(Encoding.UTF8.GetBytes(disabledConfigSource),"disabled-config.cs")){
       using var configModule=configBinary.Create();
       var configPath=Path.Combine(Yanjiu.Services.ConfigDirectory,"modules","DisabledConfigSample.json");
       File.Delete(configPath);
       var editor=new DisabledModuleSettings((DailyRoutines.Common.Module.Abstractions.ModuleBase)configModule);
       Check(!configModule.IsEnabled&&AppDomain.CurrentDomain.GetData("disabled-config-started")==null,"opening stopped configuration never invokes Init");
       Check(editor.Values["Count"]!.GetValue<int>()==9&&editor.Values["Items"]!.AsArray().Count==2,"disabled configuration uses original typed defaults and collections");
       editor.Values["Notify"]=false;editor.Values["Count"]=17;editor.Save();
       Check(!configModule.IsEnabled&&AppDomain.CurrentDomain.GetData("disabled-config-started")==null&&File.Exists(configPath),"saving disabled configuration does not enable module");
       var restoredEditor=new DisabledModuleSettings((DailyRoutines.Common.Module.Abstractions.ModuleBase)configModule);
       Check(restoredEditor.Values["Count"]!.GetValue<int>()==17&&!restoredEditor.Values["Notify"]!.GetValue<bool>(),"disabled edits survive closing and reopening settings");
       var beforeInvalid=File.ReadAllText(configPath);
       restoredEditor.Values["Count"]="invalid";
       bool invalid=false;try{restoredEditor.Save();}catch(System.Text.Json.JsonException){invalid=true;}
       Check(invalid&&File.ReadAllText(configPath)==beforeInvalid,"invalid disabled settings never overwrite last valid configuration");
       configModule.SetEnabled(true);
       Check((int)AppDomain.CurrentDomain.GetData("disabled-config-started")! ==17,"normal enabling reads exact saved disabled configuration");
   }
   using(var dependencies=new ModuleManager(new Yanjiu.Configuration(),compiler)){
    ModuleEntry Fixture(string name,string metadata){
     var text="using DailyRoutines.Common.Module.Abstractions;using DailyRoutines.Common.Module.Models;public class "+name+":ModuleBase{public override ModuleInfo Info{get;}=new(){Title=\""+name+"\""+metadata+"};}";
     var binary=compiler.Compile(Encoding.UTF8.GetBytes(text),name+".cs");
     var entry=new ModuleEntry{Path=name+".cs",Origin="test",Preference=new(),Compiled=binary,Instance=binary.Create()};dependencies.Entries.Add(entry);return entry;
    }
    var prerequisite=Fixture("RequiredSample","");
    var dependent=Fixture("DependentSample",",ModulesPrerequisite=[\"RequiredSample\"]");
    var conflict=Fixture("ConflictSample",",ModulesConflict=[\"DependentSample\"]");
    Check(dependencies.Enable(dependent,true)&&dependent.Instance!.IsEnabled&&prerequisite.Instance!.IsEnabled,"DR prerequisite is enabled automatically before dependent");
    var chain=Fixture("ChainSample",",ModulesPrerequisite=[\"DependentSample\"]");
    Check(dependencies.Enable(chain,true)&&chain.Instance!.IsEnabled&&dependent.Instance!.IsEnabled&&prerequisite.Instance!.IsEnabled,"transitive DR prerequisite chain enabled");
    Check(dependencies.Enable(chain,false),"chain can be disabled");
    var missing=Fixture("MissingDependencySample",",ModulesPrerequisite=[\"NoSuchModule\"]");
    Check(!dependencies.Enable(missing,true)&&!missing.Instance!.IsEnabled&&dependencies.Status.Contains("NoSuchModule"),"missing real dependency reported without fake success");
    var cycleA=Fixture("CycleSampleA",",ModulesPrerequisite=[\"CycleSampleB\"]");
    var cycleB=Fixture("CycleSampleB",",ModulesPrerequisite=[\"CycleSampleA\"]");
    Check(!dependencies.Enable(cycleA,true)&&!cycleA.Instance!.IsEnabled&&!cycleB.Instance!.IsEnabled&&dependencies.Status.Contains("循环"),"dependency cycle blocked without recursion overflow");
    var drFilter=new ModuleEntry{Path="filter.dll",Origin="DR",Preference=new(){Favorite=true},Catalog=new(){Id="FilterSample",Title="示例",Category="Interface"}};
    Check(ModuleFilters.Matches(drFilter,"全部")&&ModuleFilters.Matches(drFilter,"收藏")&&ModuleFilters.Matches(drFilter,"系统界面")&&!ModuleFilters.Matches(drFilter,"已启用")&&!ModuleFilters.Matches(drFilter,"战斗"),"category filters use bundled metadata before constructor completes");
    Check(ModuleFilters.Matches(dependent,"已启用"),"enabled filter uses actual lifecycle state");
    var onlineEntry=new ModuleEntry{Path="online.dll",Origin="在线",Preference=new()};var localEntry=new ModuleEntry{Path="local.dll",Origin="本地",Preference=new()};Check(ModuleFilters.Matches(onlineEntry,"在线模块")&&!ModuleFilters.Matches(onlineEntry,"本地模块")&&ModuleFilters.Matches(localEntry,"本地模块")&&!ModuleFilters.Matches(localEntry,"在线模块"),"online and local filters use module origin");
    var savedFavorite=System.Text.Json.JsonSerializer.Deserialize<Yanjiu.ModulePreference>(System.Text.Json.JsonSerializer.Serialize(drFilter.Preference))!;
    Check(savedFavorite.Favorite,"favorite survives configuration serialization");
    Check(dependencies.Enable(prerequisite,true)&&dependencies.Enable(dependent,true),"DR actual enabled prerequisite satisfies requirement");
    Check(!dependencies.Enable(conflict,true)&&!conflict.Instance!.IsEnabled,"DR declared conflict is blocked");
    Check(!dependencies.Enable(prerequisite,false)&&prerequisite.Instance!.IsEnabled,"active dependent prevents prerequisite shutdown");
    Check(dependencies.Enable(dependent,false)&&dependencies.Enable(prerequisite,false),"dependent can be stopped before prerequisite");
    Check(dependencies.Enable(conflict,true)&&dependencies.Enable(prerequisite,true)&&!dependencies.Enable(dependent,true),"DR reverse conflict is blocked");
   }
   using(var overlaps=new ModuleManager(new Yanjiu.Configuration(),compiler)){
    ModuleEntry OverlapFixture(string name,bool isDr){
     string common=isDr?"DailyRoutines":"OmniToolbox";
     string text="using "+common+".Common.Module.Abstractions;using "+common+".Common.Module.Models;public class "+name+":ModuleBase{public override ModuleInfo Info{get;}=new(){Title=\""+name+"\"};}";
     var binary=compiler.Compile(Encoding.UTF8.GetBytes(text),name+".cs");
     var entry=new ModuleEntry{Path=name+".cs",Origin="test",Preference=new(),Compiled=binary,Instance=binary.Create()};overlaps.Entries.Add(entry);return entry;
    }
    var drCopy=OverlapFixture("CopyItemNameContextMenu",true);var omniCopy=OverlapFixture("CopyItemName",false);
    Check(drCopy.Title.EndsWith("[DR]")&&omniCopy.Title.EndsWith("[Omni]"),"each imported module title ends in its actual source");
    Check(overlaps.Enable(drCopy,true)&&!overlaps.Enable(omniCopy,true),"same-function Omni/DR pair cannot run together");
    Check(overlaps.Enable(drCopy,false)&&overlaps.Enable(omniCopy,true)&&!overlaps.Enable(drCopy,true),"duplicate guard works in reverse order");
    var drFood=OverlapFixture("AutoCheckFoodUsage",true);var omniFood=OverlapFixture("FoodReminder",false);
    Check(overlaps.Enable(drFood,true)&&overlaps.Enable(omniFood,true),"partial feature overlap alone does not remove either option");
   }
   var pendingFile=root+"/tests/cancelled-import.cs";File.WriteAllText(pendingFile,source);var pendingConfig=new Yanjiu.Configuration();using(var manager=new ModuleManager(pendingConfig,compiler)){manager.Import(pendingFile);manager.Forget([pendingFile]);for(int retry=0;retry<60;retry++){await Task.Delay(25);manager.Pump();}Check(manager.Entries.Count==0&&pendingConfig.Modules.Count==0,"deleted address cannot return after asynchronous compilation");}
   var queueConfig=new Yanjiu.Configuration();using var queueManager=new ModuleManager(queueConfig,compiler);for(int i=0;i<3;i++){var fixture=root+"/tests/queued-"+i+".cs";File.WriteAllText(fixture,source.Replace("LifecycleSample","QueuedSample"+i));queueManager.Import(fixture);}for(int retry=0;retry<160&&queueManager.Entries.Count<3;retry++){await Task.Delay(25);queueManager.Pump();}Check(queueManager.Entries.Count==3&&queueManager.Entries.All(e=>e.Instance!=null&&!e.Instance.IsEnabled),"more than two source imports are queued without dropping modules");
   var removedEntry=queueManager.Entries[0];string removedName=removedEntry.Instance!.ModuleName;queueManager.Forget([removedEntry.Path.Replace('\\','/')]);Check(queueManager.Entries.Count==2&&queueConfig.Modules.Count==2&&queueConfig.HiddenAvailableModules.Contains(removedName),"local removal deletes instance and suppresses matching online candidates");
   var hiddenConfig=System.Text.Json.JsonSerializer.Deserialize<Yanjiu.Configuration>(System.Text.Json.JsonSerializer.Serialize(queueConfig))!;Check(hiddenConfig.HiddenAvailableModules.Contains(removedName),"candidate removal survives restart");
  }
  finally{typeof(Yanjiu.Services).GetProperty("PluginInterface")!.SetValue(null,null);}
  Check(OnlineRepository.ManifestUrl("https://github.com/Xiao-Yanjiu/OmniToolbox.xiaoyanjiu").ToString().EndsWith("/HEAD/TreeHouseModules.json"),"GitHub repository conversion");
  foreach(var path in new[]{"../evil.cs","%2e%2e/evil.cs","https://evil/evil.cs","/evil.cs"}){bool rejected=false;try{OnlineRepository.ModuleUrl(new Uri("https://example.com/TreeHouseModules.json"),new OnlineModule{InternalName="Sample",File=path,Sha256=new string('0',64)});}catch(ArgumentException){rejected=true;}Check(rejected,"unsafe module path rejected");}
  var payload=Encoding.UTF8.GetBytes(source);var itemFixture=new OnlineModule{InternalName="LifecycleSample",File="sample.cs",Sha256=Convert.ToHexString(SHA256.HashData(payload))};
  using(var fixture=new OnlineRepository(new BytesHandler(payload))){var sampleListing=new OnlineListing(new Uri("https://example.com/TreeHouseModules.json"),[itemFixture]);Check((await fixture.Download(sampleListing,itemFixture)).SequenceEqual(payload),"valid payload hash accepted");itemFixture.Sha256=new string('0',64);bool refused=false;try{await fixture.Download(sampleListing,itemFixture);}catch(InvalidDataException){refused=true;}Check(refused,"tampered payload rejected before compilation");}
  var omniCatalog=DRCatalog.Load("Omni");
  Check(omniCatalog.Count==0,"all Omni modules removed by user request");
  Check(ModuleOverlaps.All.Count(r=>r.SameScope)==6,"six pairs have the same core function");
  var drPackage=Path.Combine(root,"dr-integration","package","Modules");
  if(File.Exists(Path.Combine(drPackage,"catalog.json"))){
   var catalog=System.Text.Json.JsonSerializer.Deserialize<List<DRLocalModule>>(File.ReadAllText(Path.Combine(drPackage,"catalog.json")),new System.Text.Json.JsonSerializerOptions{PropertyNameCaseInsensitive=true})!;
   Check(catalog.Count==311&&catalog.Select(i=>i.Id).Distinct().Count()==311,"all retained DR modules catalogued once");
   Check(catalog.All(i=>i.Source=="DR"&&!string.IsNullOrWhiteSpace(i.Description)),"every DR module retains source and visible description");
   Check(catalog.Where(i=>!i.Compiles).All(i=>!i.Importable&&i.Error.Length>0),"unimplemented modules remain explicitly blocked");
   Check(ModuleOverlaps.All.All(pair=>catalog.Any(i=>i.Id==pair.DR)),"all twelve DR alternatives restored");
   Check(catalog.Single(i=>i.Id=="AutoSubmarineCollect").Prerequisites.Contains("AutoCutsceneSkip")&&catalog.Single(i=>i.Id=="AutoSubmarineCollect").Pairs.Contains("OptimizedInteraction"),"real prerequisite and pair metadata retained before module construction");
   Check(catalog.Single(i=>i.Id=="AutoReadOutTalk").Conflicts.Contains("AutoTalkSkip"),"real conflict metadata retained");
   Check(DRCatalog.Load().Count==311,"deployed DR catalog matches the host checksum");
   var first=DRCatalog.Load().First(i=>i.Compiles);Check(File.Exists(DRCatalog.VerifiedPath(first)),"deployed DR binary hash is accepted");
   first.Sha256=new string('0',64);bool rejectedHash=false;try{DRCatalog.VerifiedPath(first);}catch(InvalidDataException){rejectedHash=true;}Check(rejectedHash,"DR import refuses a mismatched binary hash");
   bool rejectedPath=false;try{DRCatalog.VerifiedPath(new(){Id="../escape",Compiles=true});}catch(InvalidDataException){rejectedPath=true;}Check(rejectedPath,"DR import rejects catalog path traversal");
   int loaded=0;
   foreach(var item in catalog.Where(i=>i.Compiles)){
    var file=Path.Combine(drPackage,item.Id+".dll");var bytes=File.ReadAllBytes(file);
    Check(Convert.ToHexString(SHA256.HashData(bytes)).Equals(item.Sha256,StringComparison.OrdinalIgnoreCase),"bundled DR module checksum");
    CompiledModule compiled;try{compiled=compiler.Compile(bytes,file);}catch(Exception e){throw new Exception("DR type load failed: "+item.Id,e);}using var ownedCompiled=compiled;Check(compiled.ModuleType.Name==item.Id,"bundled DR module type can load against actual host");loaded++;
   }
   Console.WriteLine($"DR PACKAGE TYPE-LOAD PASS: {loaded}/{catalog.Count}; no constructors or Init executed.");
  }
  int failures=0;var fixtureFiles=new List<string>();if(Directory.Exists(root+"/FF14-Omni-DR/DR"))fixtureFiles.AddRange(Directory.EnumerateFiles(root+"/FF14-Omni-DR/DR","*.cs"));if(Directory.Exists(root+"/FF14-Omni-DR/Omni"))fixtureFiles.AddRange(Directory.EnumerateFiles(root+"/FF14-Omni-DR/Omni","*.Omni.cs"));
  var results=new List<object>();foreach(var file in fixtureFiles){
   try{using var compiled=compiler.Compile(File.ReadAllBytes(file),file);bool constructed=false;if(typeof(DailyRoutines.Common.Module.Abstractions.ModuleBase).IsAssignableFrom(compiled.ModuleType)){using var instance=compiled.Create();Check(!instance.IsEnabled&&instance.HasSettings&&!string.IsNullOrWhiteSpace(instance.Descriptor.Title),"DR constructor, metadata and initial disabled state");constructed=true;}results.Add(new{file,ok=true,type=compiled.ModuleType.FullName,constructed,error=""});Console.WriteLine("COMPAT PASS "+Path.GetFileName(file)+(constructed?" (DR constructor verified)":""));}
   catch(Exception e){failures++;results.Add(new{file,ok=false,type="",error=e.Message});Console.WriteLine("COMPAT FAIL "+Path.GetFileName(file)+": "+e.Message.Split('\n')[0]);}
  }
  File.WriteAllText(root+"/tests/local-compatibility.json",System.Text.Json.JsonSerializer.Serialize(results,new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
  var phoneSource=root+"/temporary/legacy-publication/Modules/PhoneChat.cs";if(File.Exists(phoneSource)){using var phoneCompiled=compiler.Compile(File.ReadAllBytes(phoneSource),phoneSource);Check(phoneCompiled.ModuleType.Name=="PhoneChat","published phone module type");Console.WriteLine("PHONE MODULE PASS: publication source compiled");}
  if(Environment.GetEnvironmentVariable("YANJIU_OFFLINE_TESTS")=="1"){
    Check(failures==0,"local compatibility failures");
    return "PASS: local lifecycle, failed startup cleanup, config persistence, seven themes, native theme defaults and safe manifests. SKIPPED: live online repository (offline test mode).";
  }
  using var online=new OnlineRepository();var listing=await online.Fetch("https://github.com/Xiao-Yanjiu/OmniToolbox.xiaoyanjiu");
  Check(listing.Modules.Count>0,"owned online repository has modules");Directory.CreateDirectory(root+"/tests/online-sources");var remote=new List<object>();
  foreach(var item in listing.Modules.Take(12)){
   try{var bytes=await online.Download(listing,item);var file=root+"/tests/online-sources/"+item.InternalName+Path.GetExtension(item.File);File.WriteAllBytes(file,bytes);using var compiled=compiler.Compile(bytes,file);Check(compiled.ModuleType.Name==item.InternalName,"online type matches manifest name");remote.Add(new{item.InternalName,ok=true,error=""});Console.WriteLine("ONLINE PASS "+item.InternalName);}
   catch(Exception e){failures++;remote.Add(new{item.InternalName,ok=false,error=e.Message});Console.WriteLine("ONLINE FAIL "+item.InternalName+": "+e.Message.Split('\n')[0]);}
  }
  File.WriteAllText(root+"/tests/online-compatibility.json",System.Text.Json.JsonSerializer.Serialize(remote,new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
  Check(failures==0,"Compatibility failures: "+failures+"; inspect JSON reports");
  return "PASS: independent Omni/DR lifecycle, failed startup cleanup, config persistence, seven themes and safe manifest paths; compatibility reports saved";
 }
}
sealed class BytesHandler(byte[] bytes):System.Net.Http.HttpMessageHandler
{
 protected override Task<System.Net.Http.HttpResponseMessage> SendAsync(System.Net.Http.HttpRequestMessage request,CancellationToken token)=>Task.FromResult(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK){Content=new System.Net.Http.ByteArrayContent(bytes)});
}
public class NoopPluginInterface:DispatchProxy
{
 protected override object? Invoke(MethodInfo? method,object?[]? args)=>method?.Name=="get_AssemblyLocation"?new FileInfo(typeof(Yanjiu.Modules.ModuleBase).Assembly.Location):null;
}






public class TestNoopInterface:DispatchProxy
{
 protected override object? Invoke(MethodInfo? method,object?[]? args)=>method?.ReturnType.IsValueType==true?Activator.CreateInstance(method.ReturnType):null;
}
public class TestIpcPluginInterface:DispatchProxy
{
 protected override object? Invoke(MethodInfo? method,object?[]? args){
  if(method?.Name is "GetIpcProvider" or "GetIpcSubscriber"){
   var gate=DispatchProxy.Create(method.ReturnType,typeof(TestIpcCallGate));
   ((TestIpcCallGate)gate).Endpoint=(string)args![0]!;return gate;
  }
  return null;
 }
}
public class TestIpcCallGate:DispatchProxy
{
 public static readonly Dictionary<string,Delegate> Callbacks=[];
 public string Endpoint="";
 protected override object? Invoke(MethodInfo? method,object?[]? args){
  switch(method?.Name){
   case "RegisterFunc":case "RegisterAction":Callbacks[Endpoint]=(Delegate)args![0]!;return null;
   case "UnregisterFunc":case "UnregisterAction":Callbacks.Remove(Endpoint);return null;
   case "get_HasFunction":case "get_HasAction":return Callbacks.ContainsKey(Endpoint);
   case "InvokeFunc":case "InvokeAction":return Callbacks[Endpoint].DynamicInvoke(args);
   default:return method?.ReturnType.IsValueType==true?Activator.CreateInstance(method.ReturnType):null;
  }
 }
}













