using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Yanjiu.Modules;
if(args.Length!=3)throw new ArgumentException("plugin-folder SDK-folder module-repository-folder");
AssemblyLoadContext.Default.Resolving+=(c,n)=>{foreach(var root in args.Take(2)){var f=Path.Combine(root,n.Name+".dll");if(File.Exists(f))return c.LoadFromAssemblyPath(f);}return null;};
await Checks.Run(args);
static class Checks
{
 static void Require(bool condition,string message){if(!condition)throw new Exception(message);Console.WriteLine("PASS "+message);}
 static async Task RepositoryChecks()
 {
  var bytes=Encoding.UTF8.GetBytes("API1 repository payload");
  var module=new OnlineModule{InternalName="Api1Sample",Author="Test",ApiVersion=1,Version="2.0.0",MinimumOmniVersion="1.1.8.2",File="Modules/Api1Sample.cs",RepositoryURL="https://github.com/test-owner/public-test",Sha256=Convert.ToHexString(SHA256.HashData(bytes)),Localization=new(){Source=new(){{"Title","示例"},{"Description","测试仓库回退"}}}};
  var manifest=JsonSerializer.SerializeToUtf8Bytes(new OnlineManifest{Modules=[module]});
  using var handler=new RepositoryHandler(manifest,bytes);using var client=new OnlineRepository(handler);
  var listing=await client.Fetch("https://github.com/test-owner/public-test");
  Require(handler.ApiFailed&&listing.Modules[0].Name=="示例","API failure falls back to raw and reads API1 Localization.Source");
  Require((await client.Download(listing,listing.Modules[0])).SequenceEqual(bytes),"anonymous public-repository check and download SHA256");
  bool refused=false;try{await client.VerifyPublicRepository("https://github.com/test-owner/private-test");}catch(InvalidDataException){refused=true;}
  Require(refused,"private repository cannot bypass public-source validation");
  module.RepositoryURL="https://github.com/another-owner/repo";refused=false;try{OnlineRepository.ValidateApi1(listing.ManifestUri,module);}catch(InvalidDataException){refused=true;}
  Require(refused,"manifest repository substitution is rejected");
 }
 private sealed class RepositoryHandler(byte[] manifest,byte[] payload):System.Net.Http.HttpMessageHandler
 {
  public bool ApiFailed;
  protected override Task<System.Net.Http.HttpResponseMessage> SendAsync(System.Net.Http.HttpRequestMessage request,CancellationToken token)
  {
   var uri=request.RequestUri!;
   if(uri.Host=="api.github.com"&&uri.AbsolutePath.Contains("/contents/")){ApiFailed=true;return Task.FromResult(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.Forbidden));}
   var bytes=uri.Host=="api.github.com"?Encoding.UTF8.GetBytes("{\"full_name\":\"test-owner/"+(uri.AbsolutePath.EndsWith("private-test")?"private-test\",\"private\":true}":"public-test\",\"private\":false}")):uri.AbsolutePath.EndsWith(".json")?manifest:payload;
   return Task.FromResult(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK){Content=new System.Net.Http.ByteArrayContent(bytes)});
  }
 }
 public static async Task Run(string[] args)
 {
  Yanjiu.PluginFiles.Initialize(Path.Combine(args[0],"Yanjiu.dll"));
  Yanjiu.Services.ConfigDirectory=Path.Combine(Path.GetDirectoryName(args[0])!,"Api1-test-state",Guid.NewGuid().ToString("N"));
  Directory.CreateDirectory(Yanjiu.Services.ConfigDirectory);
  var compiler=new SourceCompiler(Directory.GetFiles(args[0],"*.dll").Concat(Directory.GetFiles(args[1],"*.dll")).Concat(SourceCompiler.ReferencePaths()));
  var manifest=JsonSerializer.Deserialize<OnlineManifest>(File.ReadAllText(Path.Combine(args[2],"TreeHouseModules.json")),new JsonSerializerOptions{PropertyNameCaseInsensitive=true})!;
  var rows=new List<object>();var held=new List<CompiledModule>();int passed=0;
  foreach(var item in manifest.Modules){
   try{
    OnlineRepository.ValidateApi1(new Uri("https://raw.githubusercontent.com/Xiao-Yanjiu/OmniToolbox.xiaoyanjiu/main/TreeHouseModules.json"),item);
    var path=Path.Combine(args[2],item.File);var bytes=File.ReadAllBytes(path);
    Require(Convert.ToHexString(SHA256.HashData(bytes)).Equals(item.Sha256,StringComparison.OrdinalIgnoreCase),item.InternalName+" published SHA256");
    var compiled=compiler.Compile(bytes,path);held.Add(compiled);
    var adapter=compiled.Create();
    Require(adapter.ModuleName==item.InternalName,item.InternalName+" type and real API 1 adapter");
    Require(adapter.Descriptor.Title==item.Localization!.Source["Title"],item.InternalName+" isolated Chinese localization");
    Require(!adapter.IsEnabled,item.InternalName+" remains disabled (OnStart not executed)");
    if(adapter is Api1ModuleAdapter api){
        Require(api.MacroCommands().All(c=>!c.Value.StartsWith("/omni ",StringComparison.OrdinalIgnoreCase)),item.InternalName+" host-specific macro copy entries");
        api.Save();var field=compiled.ModuleType.GetField("config",BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public);
        if(field!=null){var configFile=Path.Combine(Yanjiu.Services.ConfigDirectory,"modules/api1",item.InternalName+".json");var json=Api1SettingsStore.Read(configFile,item.InternalName);Require(JsonSerializer.Deserialize(json,field.FieldType,new JsonSerializerOptions{IncludeFields=true})!=null,item.InternalName+" typed Settings encryption/roundtrip");}
    }
    passed++;rows.Add(new{item.InternalName,ok=true,title=adapter.Descriptor.Title,settings=adapter.HasSettings});
   }catch(Exception e){rows.Add(new{item.InternalName,ok=false,error=e.ToString()});Console.WriteLine("FAIL "+item.InternalName+": "+e);}
  }
  File.WriteAllText(Path.Combine(Path.GetDirectoryName(args[0])!,"Api1Compatibility.json"),JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true}));
  Require(passed==7,"all seven actual published files compile/load and bind metadata without modification");
  const string fixture="""
  using System;using System.Threading;using System.Threading.Tasks;using System.Collections.Generic;using OmniToolbox.Common.Module.Abstractions;using OmniToolbox.Common.Module.Models;using OmniToolbox.UI;using TreeTools.Lifecycle;
  [ModuleApiVersion(1)] public class Api1LifecycleFixture:OmniModuleBase{
    public override ModuleInfo Info{get;}=new(){Author="Test",Category=OmniToolbox.Common.Module.Enums.ModuleCategory.Debug};
    public override ModuleLocalization Localization{get;}=ModuleLocalization.Create(new Dictionary<string,string>{{"Title","生命周期测试"},{"Description","仅测试取消和保存"}});
    private Settings config=new();private sealed class Settings{public int Value{get;set;}=7;public string Cookie{get;set;}="unit-test-only";}
    public static List<string> Events=new();
    protected override void OnStart(FeatureLifetime scope){Events.Add("start");scope.OnStopping(()=>Events.Add("stopping"));scope.TrackStartup(Task.Delay(10));scope.Track(Worker(scope.CancellationToken));}
    private static async Task Worker(CancellationToken token){try{await Task.Delay(Timeout.Infinite,token);}catch(OperationCanceledException)when(token.IsCancellationRequested){}finally{Events.Add("worker-stopped");}}
    protected override void OnStop(){Events.Add("stop");}
    protected override void OnDestroy(){Events.Add("destroy");}
    public override bool ResetSettings(){config=new Settings{Value=99};return true;}
    public override bool TryHandleCommand(string args)=>args=="probe";
  }
  """;
  using var test=compiler.Compile(Encoding.UTF8.GetBytes(fixture),"fixture.cs");
  var constructor=typeof(Api1ModuleAdapter).GetConstructors(BindingFlags.NonPublic|BindingFlags.Instance).Single();
  var instance=(Api1ModuleAdapter)constructor.Invoke([test.ModuleType,(Func<Task>)(()=>Task.CompletedTask),(Func<Action,Task>)(a=>{a();return Task.CompletedTask;})]);
  await instance.ChangeEnabledAsync(true).WaitAsync(TimeSpan.FromSeconds(5));Require(instance.IsEnabled,"adapter waits for startup before enabled");
  Require(instance.TryHandleCommand("probe"),"enabled API1 module handles commands");
  var stopping=instance.ChangeEnabledAsync(false);Require(!instance.TryHandleCommand("probe"),"command entry closes before stop/drain finishes");
  await stopping.WaitAsync(TimeSpan.FromSeconds(5));Require(!instance.IsEnabled,"adapter awaits actual asynchronous stop");
  var events=(List<string>)test.ModuleType.GetField("Events")!.GetValue(null)!;
  Require(events.IndexOf("worker-stopped")<events.IndexOf("stop"),"background work ends before OnStop");
  instance.ResetSettings();instance.Save();
  var configPath=Path.Combine(Yanjiu.Services.ConfigDirectory,"modules/api1/Api1LifecycleFixture.json");
  Require(JsonDocument.Parse(Api1SettingsStore.Read(configPath,"Api1LifecycleFixture")).RootElement.GetProperty("Value").GetInt32()==99,"saves replaced config object after reset");
  Require(!File.ReadAllText(configPath).Contains("unit-test-only"),"nested settings/credentials are protected at rest");
  await instance.ChangeEnabledAsync(true).WaitAsync(TimeSpan.FromSeconds(5));
  await instance.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
  Require(events.Last()=="destroy"&&events.Count(e=>e=="start")==2,"re-enable uses fresh lifetime and destruction completes before unload");
  var resumed=(Api1ModuleAdapter)constructor.Invoke([test.ModuleType,(Func<Task>)(()=>Task.CompletedTask),(Func<Action,Task>)(a=>{a();return Task.CompletedTask;})]);
  var rawModule=typeof(Api1ModuleAdapter).GetField("module",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(resumed)!;
  var reloaded=test.ModuleType.GetField("config",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(rawModule)!;
  Require((int)reloaded.GetType().GetProperty("Value")!.GetValue(reloaded)! ==99,"encrypted Settings reload into the named config field");
  await resumed.DisposeAsync();
  using var bad=compiler.Compile(Encoding.UTF8.GetBytes(fixture.Replace("Api1LifecycleFixture","Api1SaveFailureFixture")),"save-failure.cs");
  var failed=(Api1ModuleAdapter)constructor.Invoke([bad.ModuleType,(Func<Task>)(()=>Task.CompletedTask),(Func<Action,Task>)(a=>{a();return Task.CompletedTask;})]);
  var failedModule=typeof(Api1ModuleAdapter).GetField("module",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(failed)!;
  var failedSettings=bad.ModuleType.GetField("config",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(failedModule)!;
  failedSettings.GetType().GetProperty("Cookie")!.SetValue(failedSettings,new string('x',4*1024*1024+1));
  bool saveRejected=false;try{await failed.ChangeEnabledAsync(true).WaitAsync(TimeSpan.FromSeconds(5));}catch(InvalidDataException){saveRejected=true;}
  Require(saveRejected&&!failed.IsEnabled,"failed configuration save rolls back the active module");
  failed.ResetSettings();await failed.DisposeAsync();
  foreach(var compiled in held)compiled.Dispose();
  bool rejectedApi=false;try{using var invalid=compiler.Compile(Encoding.UTF8.GetBytes(fixture.Replace("ModuleApiVersion(1)","ModuleApiVersion(0)")),"old.cs");}catch(InvalidDataException){rejectedApi=true;}
  Require(rejectedApi,"new base with old API declaration is rejected");
  await RepositoryChecks();
  Console.WriteLine("API1 PASS: seven real files; synthetic lifecycle/config fixture. Game/native/network actions were NOT run.");
 }
}
