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
 foreach(var folder in new[]{root+"/artifacts/plugin",api}){var file=Path.Combine(folder,name.Name+".dll");if(File.Exists(file))return context.LoadFromAssemblyPath(file);}return null;};
var status=await Tests.Run(root,api);Console.WriteLine(status);

static class Tests
{
 static void Check(bool test,string title){if(!test)throw new Exception(title);}
 public static async Task<string> Run(string root,string api)
 {
  Yanjiu.Services.ConfigDirectory=root+"/tests/runtime";Directory.CreateDirectory(Yanjiu.Services.ConfigDirectory);
  File.Delete(root+"/tests/runtime/modules/DRSample.json");
  var refs=Directory.EnumerateFiles(api,"*.dll").Concat(Directory.EnumerateFiles(root+"/artifacts/plugin","*.dll")).Concat(Directory.EnumerateFiles(Path.GetDirectoryName(typeof(object).Assembly.Location)!,"*.dll"));
  var compiler=new SourceCompiler(refs.Concat(SourceCompiler.ReferencePaths()));
  Check(SourceCompiler.ReferenceFiles([null,"",root+"/missing-directory",root+"/artifacts/plugin"]).Any(path=>Path.GetFileName(path)=="Yanjiu.dll"),"empty in-memory assembly paths skipped");
  _=new SourceCompiler();
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
  Check(Themes.Available.Length==6&&Themes.Available.Select(p=>p.Name).Distinct().Count()==6,"six unique palettes");
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
  int failures=0;var fixtureFiles=new List<string>();if(Directory.Exists(root+"/FF14-Omni-DR/DR"))fixtureFiles.AddRange(Directory.EnumerateFiles(root+"/FF14-Omni-DR/DR","*.cs"));if(Directory.Exists(root+"/FF14-Omni-DR/Omni"))fixtureFiles.AddRange(Directory.EnumerateFiles(root+"/FF14-Omni-DR/Omni","*.Omni.cs"));
  var results=new List<object>();foreach(var file in fixtureFiles){
   try{using var compiled=compiler.Compile(File.ReadAllBytes(file),file);bool constructed=false;if(typeof(DailyRoutines.Common.Module.Abstractions.ModuleBase).IsAssignableFrom(compiled.ModuleType)){using var instance=compiled.Create();Check(!instance.IsEnabled&&instance.HasSettings&&!string.IsNullOrWhiteSpace(instance.Descriptor.Title),"DR constructor, metadata and initial disabled state");constructed=true;}results.Add(new{file,ok=true,type=compiled.ModuleType.FullName,constructed,error=""});Console.WriteLine("COMPAT PASS "+Path.GetFileName(file)+(constructed?" (DR constructor verified)":""));}
   catch(Exception e){failures++;results.Add(new{file,ok=false,type="",error=e.Message});Console.WriteLine("COMPAT FAIL "+Path.GetFileName(file)+": "+e.Message.Split('\n')[0]);}
  }
  File.WriteAllText(root+"/tests/local-compatibility.json",System.Text.Json.JsonSerializer.Serialize(results,new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
  var phoneSource=root+"/temporary/legacy-publication/Modules/PhoneChat.cs";if(File.Exists(phoneSource)){using var phoneCompiled=compiler.Compile(File.ReadAllBytes(phoneSource),phoneSource);Check(phoneCompiled.ModuleType.Name=="PhoneChat","published phone module type");Console.WriteLine("PHONE MODULE PASS: publication source compiled");}
  using var online=new OnlineRepository();var listing=await online.Fetch("https://github.com/Xiao-Yanjiu/OmniToolbox.xiaoyanjiu");
  Check(listing.Modules.Count>0,"owned online repository has modules");Directory.CreateDirectory(root+"/tests/online-sources");var remote=new List<object>();
  foreach(var item in listing.Modules.Take(12)){
   try{var bytes=await online.Download(listing,item);var file=root+"/tests/online-sources/"+item.InternalName+Path.GetExtension(item.File);File.WriteAllBytes(file,bytes);using var compiled=compiler.Compile(bytes,file);Check(compiled.ModuleType.Name==item.InternalName,"online type matches manifest name");remote.Add(new{item.InternalName,ok=true,error=""});Console.WriteLine("ONLINE PASS "+item.InternalName);}
   catch(Exception e){failures++;remote.Add(new{item.InternalName,ok=false,error=e.Message});Console.WriteLine("ONLINE FAIL "+item.InternalName+": "+e.Message.Split('\n')[0]);}
  }
  File.WriteAllText(root+"/tests/online-compatibility.json",System.Text.Json.JsonSerializer.Serialize(remote,new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
  Check(failures==0,"Compatibility failures: "+failures+"; inspect JSON reports");
  return "PASS: independent Omni/DR lifecycle, failed startup cleanup, config persistence, six themes and safe manifest paths; compatibility reports saved";
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
