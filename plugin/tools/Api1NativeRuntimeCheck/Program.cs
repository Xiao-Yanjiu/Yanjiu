using System.Reflection;
using System.Runtime.Loader;
using Yanjiu.Modules;
if(args.Length!=2) throw new ArgumentException("plugin-folder SDK-folder");
AssemblyLoadContext.Default.Resolving+=(c,n)=>{foreach(var root in args){var f=Path.Combine(root,n.Name+".dll");if(File.Exists(f))return c.LoadFromAssemblyPath(f);}return null;};
Yanjiu.PluginFiles.Initialize(Path.Combine(args[0],"Yanjiu.dll"));
const BindingFlags all=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
var native=Api1Runtime.Type("KamiToolKit.KamiToolKitLibrary","KamiToolKit");
var init=native.GetMethods(all).Single(m=>m.Name=="InitializeAsync" && m.GetParameters().Length==2);
if(init.ReturnType!=typeof(Task))throw new Exception("Native init return contract changed");
if(native.GetMethod("DisposeAsync",all)!.ReturnType!=typeof(Task))throw new Exception("Native disposal return contract changed");
var addon=Api1Runtime.Type("KamiToolKit.BaseTypes.NativeAddon","KamiToolKit");
if(addon.GetProperty("CreatedAddons",all)==null||addon.GetProperty("InternalName",all)==null)throw new Exception("Native ownership contract changed");
var create=typeof(Api1PluginInterface).GetMethod("Create",BindingFlags.Public|BindingFlags.Static|BindingFlags.DeclaredOnly)!;
var contract=create.GetParameters()[0].ParameterType;
var fake=DispatchProxy.Create(contract,typeof(ConfigProxy));
var proxy=create.Invoke(null,[fake])!;
var a=(string)contract.GetMethod("GetPluginConfigDirectory")!.Invoke(proxy,null)!;
var b=(DirectoryInfo)contract.GetProperty("ConfigDirectory")!.GetValue(proxy)!;
if(a!=b.FullName||Path.GetFileName(a)!="api1-runtime"||Path.GetDirectoryName(a)!=ConfigProxy.Root)throw new Exception("Native config is not isolated");
Console.WriteLine("PASS: installed API1 native initialize/dispose/ownership signatures and both configuration-directory proxy accessors. No native initialization was executed.");
public class ConfigProxy:DispatchProxy
{
 public static readonly string Root=Path.Combine(Path.GetTempPath(),"Yanjiu-native-contract-"+Guid.NewGuid().ToString("N"));
 protected override object? Invoke(MethodInfo? method,object?[]? args)=>method?.Name=="GetPluginConfigDirectory"?Root:throw new NotSupportedException(method?.Name);
}
