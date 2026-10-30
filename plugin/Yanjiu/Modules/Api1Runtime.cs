// SPDX-License-Identifier: GPL-3.0-or-later
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;
namespace Yanjiu.Modules;

// API 1's real public Common/TreeTools libraries live in a separate load context.
// This prevents their Omen/Kami service state from replacing the legacy DR runtime.
public static class Api1Runtime
{
    private const BindingFlags Members=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
    private static readonly object gate=new();
    private static RuntimeContext? context;
    private static Task? initialization;
    private static object? tools,kit,icons;
    private static Type? nativeLibrary;
    private static bool nativeInitializationStarted;
    private static bool stopping;private static string initializationError="";
    public static string DirectoryPath=>Path.Combine(PluginFiles.DirectoryPath,"Api1");
    private sealed class RuntimeContext:AssemblyLoadContext
    {
        public RuntimeContext():base("Yanjiu-API1",true){}
        protected override Assembly? Load(AssemblyName name)
        {
            var path=Path.Combine(DirectoryPath,name.Name+".dll");
            if(File.Exists(path))return LoadFromAssemblyPath(path);
            return AssemblyLoadContext.GetLoadContext(typeof(Plugin).Assembly)!.LoadFromAssemblyName(name);
        }
    }
    private static RuntimeContext Context
    {
        get{lock(gate){if(stopping)throw new ObjectDisposedException("API 1 runtime");if(context!=null)return context;
            var hashes=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(DirectoryPath,"SHA256.json")))??throw new InvalidDataException("API 1 runtime hashes missing");
            foreach(var pair in hashes){if(Path.GetFileName(pair.Key)!=pair.Key||!pair.Key.EndsWith(".dll",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Invalid API 1 dependency path");var bytes=File.ReadAllBytes(Path.Combine(DirectoryPath,pair.Key));if(!Convert.ToHexString(SHA256.HashData(bytes)).Equals(pair.Value,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("API 1 runtime checksum failed: "+pair.Key);}
            return context=new RuntimeContext();}}
    }
    public static Assembly Resolve(AssemblyName name)=>Context.LoadFromAssemblyName(name);
    public static Type Type(string name,string assembly="OmniToolbox.Common")=>Resolve(new AssemblyName(assembly)).GetType(name,true)!;
    public static bool IsModule(Type type){for(var t=type;t!=null;t=t.BaseType)if(t.FullName=="OmniToolbox.Common.Module.Abstractions.OmniModuleBase"&&t.Assembly.GetName().Name=="OmniToolbox.Common")return true;return false;}
    public static object? Invoke(Type type,object? owner,string name,params object?[] args)
    {
        var methods=type.GetMethods(Members).Where(m=>m.Name==name&&m.GetParameters().Length==args.Length).ToArray();
        var method=methods.FirstOrDefault(m=>m.GetParameters().Select((p,i)=>args[i]==null||p.ParameterType.IsInstanceOfType(args[i])).All(x=>x))??throw new MissingMethodException(type.FullName,name);
        try{return method.Invoke(owner,args);}catch(TargetInvocationException e) when(e.InnerException!=null){System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e.InnerException).Throw();throw;}
    }
    public static Task Dispatch(Action action)
    {
        if(Services.Framework.IsInFrameworkUpdateThread){action();return Task.CompletedTask;}
        return Services.Framework.RunOnFrameworkThread(action);
    }
    public static Task EnsureReady(){lock(gate)return initialization??=ObserveInitialization();}
    public static string StateText=>initializationError.Length>0?"API 1 服务初始化失败，请重载并查看日志："+initializationError:"API 1 公共服务正在初始化";
    private static async Task ObserveInitialization(){try{await Initialize().ConfigureAwait(false);}catch(Exception e){initializationError=e.GetBaseException().Message;throw;}}
    private static async Task Initialize()
    {
        Dalamud.Plugin.IDalamudPluginInterface? pi=null;
        await Dispatch(()=>{
            pi=Api1PluginInterface.Create(Services.PluginInterface);
            Invoke(Type("OmniToolbox.Host.DalamudServices"),null,"Initialize",pi);
            tools=Invoke(Type("TreeTools.Runtime","TreeTools"),null,"Prepare",pi);
        }).ConfigureAwait(false);
        await (Task)Invoke(tools!.GetType(),tools,"StartServicesAsync",Array.Empty<Type>(),CancellationToken.None)!;
        Task? nativeInitialization=null;
        await Dispatch(()=>{
            nativeLibrary=Type("KamiToolKit.KamiToolKitLibrary","KamiToolKit");
            nativeInitializationStarted=true;
            nativeInitialization=(Task)Invoke(nativeLibrary,null,"InitializeAsync",pi,"Yanjiu API 1")!;
        }).ConfigureAwait(false);
        // TreeToolKit initializes fonts/scale helpers only. The isolated KamiToolKit
        // must have its own services and close callback before any module creates nodes.
        await nativeInitialization!.ConfigureAwait(false);
        await Dispatch(()=>{
            var nativeAddon=Type("KamiToolKit.BaseTypes.NativeAddon","KamiToolKit");
            var created=nativeAddon.GetProperty("CreatedAddons",Members)??throw new MissingMemberException(nativeAddon.FullName,"CreatedAddons");
            var addonName=nativeAddon.GetProperty("InternalName",Members)??throw new MissingMemberException(nativeAddon.FullName,"InternalName");
            bool OwnsAddon(string name)=>created.GetValue(null) is System.Collections.IEnumerable addons&&addons.Cast<object>().Any(addon=>string.Equals((string?)addonName.GetValue(addon),name,StringComparison.Ordinal));
            kit=Invoke(Type("TreeToolKit.Runtime","TreeToolKit"),null,"Initialize",Services.Framework,Services.PluginInterface.UiBuilder,Services.Addons,(Func<string,bool>)OwnsAddon,(Action<Exception>)(e=>Services.Log.Error(e,"API 1 native UI")));
            var iconType=Type("OmniToolbox.UI.IconBrowser");
            var configType=Type("OmniToolbox.UI.IconBrowserConfig");
            var path=Path.Combine(Services.ConfigDirectory,"Api1IconBrowser.json");
            if(File.Exists(path)&&new FileInfo(path).Length>4*1024*1024)throw new InvalidDataException("API 1 图标浏览器配置过大");
            var config=File.Exists(path)?JsonSerializer.Deserialize(File.ReadAllText(path),configType):null;
            config??=Activator.CreateInstance(configType)!;
            var captured=config;
            icons=Activator.CreateInstance(iconType,config,(Action)(()=>SaveJson(path,captured,configType)))!;
            SyncTheme();
        }).ConfigureAwait(false);
    }
    public static object? Icons=>icons;
    public static bool IsReady=>initialization?.IsCompletedSuccessfully==true;
    public static void Draw(){if(initialization?.IsCompletedSuccessfully!=true||stopping)return;SyncTheme();if(icons!=null)Invoke(icons.GetType(),icons,"Draw");}
    private static void SyncTheme()
    {
        var theme=Type("OmniToolbox.UI.Theme.OmniTheme");
        var p=theme.GetProperty("CurrentTheme",Members)!;
        var palette=UI.Themes.Current;
        p.SetValue(null,Enum.Parse(p.PropertyType,palette.Dark?"LineGraphite":"LineGreen"));
        // The provider lives in an isolated load context. Copy values rather than
        // passing a host ThemeTokens instance across incompatible type identities.
        var tokens=theme.GetProperty("BaseTokens",Members)!.GetValue(null)!;
        void Set(string name,object value)=>tokens.GetType().GetProperty(name,Members)!.SetValue(tokens,value);
        Set("Primary",palette.Surface);Set("Secondary",palette.Selected);Set("Accent",palette.Accent);
        Set("Background",palette.Background);Set("Surface",palette.Surface);Set("Text",palette.Text);
        Set("Border",palette.Border);Set("Shadow",palette.Shadow);
        Set("BorderRadius",palette.BorderRadius);Set("ButtonRadius",palette.NativeGame?12f:8f);
        Set("BorderThickness",palette.BorderThickness);
        theme.GetField("scopedTokens",Members)!.SetValue(null,tokens);
        theme.GetField("scopedControlAccent",Members)!.SetValue(null,palette.Accent);
    }
    public static void SaveJson(string path,object value,Type type)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var text=JsonSerializer.Serialize(value,type,new JsonSerializerOptions{IncludeFields=true,WriteIndented=true,MaxDepth=64});
        if(System.Text.Encoding.UTF8.GetByteCount(text)>4*1024*1024)throw new InvalidDataException("模块配置超过 4 MiB");
        File.WriteAllText(path+".tmp",text);File.Move(path+".tmp",path,true);
    }
    public static async ValueTask DisposeAsync()
    {
        if(initialization!=null)try{await initialization.ConfigureAwait(false);}catch(Exception e){Services.Log.Error(e,"API 1 initialization failed during shutdown");}
        List<Exception> errors=[];
        async Task Capture(Func<Task> f){try{await f().ConfigureAwait(false);}catch(Exception e){errors.Add(e);}}
        if(tools!=null){Invoke(tools.GetType(),tools,"BeginShutdown");await Capture(async()=>await (Task)Invoke(tools.GetType(),tools,"StopTaskHelpersAsync")!);}
        if(kit!=null)await Capture(async()=>await ((ValueTask)Invoke(kit.GetType(),kit,"DisposeAsync")!).AsTask());
        // Modules have drained before this method. Release native windows/nodes
        // while their services still exist, before unloading Omen/TreeTools.
        if(nativeInitializationStarted&&nativeLibrary!=null)await Capture(async()=>await (Task)Invoke(nativeLibrary,null,"DisposeAsync")!);
        if(tools!=null)await Capture(async()=>await ((ValueTask)Invoke(tools.GetType(),tools,"DisposeAsync")!).AsTask());
        lock(gate){stopping=true;icons=null;tools=null;kit=null;nativeLibrary=null;nativeInitializationStarted=false;context?.Unload();context=null;}
        if(errors.Count>0)throw new AggregateException("API 1 runtime shutdown failed",errors);
    }
}
