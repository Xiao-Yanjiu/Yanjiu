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
  var compiler=new SourceCompiler(refs);
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
  public class DRSample:ModuleBase{public override ModuleInfo Info{get;}=new(){Title="DR配置",Description="测试",Author=["小烟酒"],Category=ModuleCategory.System};private class Config:ModuleConfig{public int Count{get;set;}}protected override void Init(){var c=LoadConfig<Config>()??new();c.Count++;SaveConfig(c);}}
  """;
  for(int i=0;i<2;i++)using(var compiled=compiler.Compile(Encoding.UTF8.GetBytes(dr),"dr.cs")){using var module=compiled.Create();module.SetEnabled(true);module.SetEnabled(false);}
  using(var persisted=System.Text.Json.JsonDocument.Parse(File.ReadAllText(root+"/tests/runtime/modules/DRSample.json")))Check(persisted.RootElement.GetProperty("Count").GetInt32()==2,"DR configuration persisted between instances");
  Check(Themes.Available.Length==6&&Themes.Available.Select(p=>p.Name).Distinct().Count()==6,"six unique palettes");
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
