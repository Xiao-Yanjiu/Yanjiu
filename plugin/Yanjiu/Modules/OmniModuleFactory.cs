// SPDX-License-Identifier: GPL-3.0-or-later
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OmniToolbox.Items;
using OmniToolbox.TreeHouse;
using OmniToolbox.UI;
using OmniToolbox.Lifecycle;
namespace Yanjiu.Modules;
public static class OmniModuleFactory
{
    private static readonly Dictionary<Type,object> services=[];private static ModuleManager? manager;public static void Bind(ModuleManager value)=>manager=value;
    private static readonly List<IDisposable> owned=[];
    private static readonly JsonSerializerSettings settings=new(){TypeNameHandling=TypeNameHandling.None,MaxDepth=64};
    public static bool IsOmni(Type type)=>typeof(OmniToolbox.Common.Module.Abstractions.ModuleBase).IsAssignableFrom(type);
    private static bool IsConfig(Type type)=>type.IsClass&&(type.Name.EndsWith("Config",StringComparison.Ordinal)||type.Name=="Config")&&type.GetConstructor(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,Type.EmptyTypes,null)!=null;
    public static string? UnsupportedReason(Type type){
        if(!IsOmni(type))return type.GetConstructor(Type.EmptyTypes)==null?"模块需要公共无参构造函数":null;
        foreach(var ctor in type.GetConstructors().OrderBy(c=>c.GetParameters().Length)){
            if(ctor.GetParameters().All(p=>CanBind(p)))return null;
        }
        return "尚缺少真实宿主提供器："+string.Join("、",type.GetConstructors().SelectMany(c=>c.GetParameters()).Where(p=>!CanBind(p)).Select(p=>p.ParameterType.Name+" "+p.Name).Distinct());
    }
    private static bool TeleportCallback(ParameterInfo p)=>p.Member.DeclaringType?.FullName=="OmniToolbox.TreePublic.AutoIshgardRestoration"&&((p.ParameterType==typeof(Func<string,bool>)&&p.Name=="tryTeleport")||(p.ParameterType==typeof(Func<bool>)&&p.Name=="isTeleportBusy")||(p.ParameterType==typeof(Action)&&p.Name=="cancelTeleport"));
    private static bool CanBind(ParameterInfo p)=>IsSave(p)||IsConfig(p.ParameterType)||SupportedService(p.ParameterType)||TeleportCallback(p)||p.HasDefaultValue;
    private static bool IsSave(ParameterInfo p)=>p.ParameterType==typeof(Action)&&p.Name?.Contains("save",StringComparison.OrdinalIgnoreCase)==true;
    private static bool SupportedService(Type type)=>type==typeof(OmniToolbox.Market.MarketPriceService)||type==typeof(OmniToolbox.Tooltips.ItemTooltipInventoryContext)||type==typeof(OmniToolbox.Tooltips.ItemSupplementTooltip)||type==typeof(CollectionItemIndex)||type==typeof(OmniToolbox.Collections.CollectionSourceService)||type==typeof(PlayerInventoryService)||type==typeof(OmniToolbox.Teleport.TeleportService)||type==typeof(OmniToolbox.Teleport.AetheryteRouteResolver)||type==typeof(OmniToolbox.Common.Module.Abstractions.IAutoSwitchConfigurationSource)||type==typeof(HookRegistry)||type==typeof(IconBrowser)||type==typeof(ItemPreviewService)||type==typeof(NonEntityTargetVisibility);
    public static ModuleBase Create(Type type){
        var reason=UnsupportedReason(type);if(reason!=null)throw new ModuleCompilationException(reason);
        if(!IsOmni(type))return (ModuleBase)Activator.CreateInstance(type)!;
        var path=Path.Combine(Services.ConfigDirectory,"modules",type.FullName+".constructor.json");
        JObject stored=new();
        if(File.Exists(path)){
            if(new FileInfo(path).Length>4*1024*1024)throw new InvalidDataException("Omni模块配置过大");
            stored=JsonConvert.DeserializeObject<JObject>(File.ReadAllText(path),settings)??new();
        }
        var configs=new Dictionary<string,object>();
        void Save(){
            var serializer=JsonSerializer.Create(settings);
            var json=new JObject(configs.Select(pair=>new JProperty(pair.Key,JToken.FromObject(pair.Value,serializer)))).ToString(Formatting.Indented);
            if(json.Length>4*1024*1024)throw new InvalidDataException("Omni模块配置过大");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.WriteAllText(path+".tmp",json);File.Move(path+".tmp",path,true);
        }
        var constructor=type.GetConstructors().OrderBy(c=>c.GetParameters().Length).First(c=>c.GetParameters().All(p=>CanBind(p)));
        var arguments=constructor.GetParameters().Select(p=>{
            if(IsSave(p))return (object)(Action)Save;
            if(TeleportCallback(p)){
                var teleport=(OmniToolbox.Teleport.TeleportService)GetService(typeof(OmniToolbox.Teleport.TeleportService));
                if(p.ParameterType==typeof(Func<string,bool>))return (object)(Func<string,bool>)teleport.TryTeleport;
                if(p.ParameterType==typeof(Func<bool>))return (object)(Func<bool>)(()=>teleport.IsBusy);
                return (object)(Action)teleport.Cancel;
            }
            if(p.HasDefaultValue&&!IsConfig(p.ParameterType)&&!SupportedService(p.ParameterType))return p.DefaultValue!;
            if(SupportedService(p.ParameterType))return GetService(p.ParameterType);
            string key=p.ParameterType.FullName!;if(!configs.TryGetValue(key,out var value)){
                value=stored[key]?.ToObject(p.ParameterType,JsonSerializer.Create(settings))??Activator.CreateInstance(p.ParameterType,true)!;configs[key]=value;
            }
            return value;
        }).ToArray();
        var module=(OmniToolbox.Common.Module.Abstractions.ModuleBase)constructor.Invoke(arguments);
        module.HostIconBrowser=GetIconBrowser();
        module.SaveHostConfig=Save;module.PersistImportedConfiguration=Save;
        return module;
    }
    public static OmniToolbox.Teleport.TeleportService GetTeleportService()=>(OmniToolbox.Teleport.TeleportService)GetService(typeof(OmniToolbox.Teleport.TeleportService));
    private static object GetService(Type type){
        if(services.TryGetValue(type,out var service))return service;
        if(type==typeof(OmniToolbox.Common.Module.Abstractions.IAutoSwitchConfigurationSource))service=new OmniConfigurationSource(manager??throw new InvalidOperationException("Yanjiu模块管理器未就绪"));
        else if(type==typeof(OmniToolbox.Teleport.TeleportService))service=new OmniToolbox.Teleport.TeleportService();
        else if(type==typeof(OmniToolbox.Teleport.AetheryteRouteResolver))service=new OmniToolbox.Teleport.AetheryteRouteResolver();
        else if(type==typeof(OmniToolbox.Tooltips.ItemTooltipInventoryContext))service=new OmniToolbox.Tooltips.ItemTooltipInventoryContext((HookRegistry)GetService(typeof(HookRegistry)));
        else if(type==typeof(HookRegistry))service=new HookRegistry();
        else if(type==typeof(IconBrowser)){
            var path=Path.Combine(Services.ConfigDirectory,"OmniIconBrowser.json");
            var config=File.Exists(path)?JsonConvert.DeserializeObject<IconBrowserConfig>(File.ReadAllText(path),settings)??new():new IconBrowserConfig();
            service=new IconBrowser(config,()=>{Directory.CreateDirectory(Services.ConfigDirectory);File.WriteAllText(path,JsonConvert.SerializeObject(config,settings));});
        }
        else if(type==typeof(ItemPreviewService))service=new ItemPreviewService();
        else if(type==typeof(OmniToolbox.Market.MarketPriceService))service=new OmniToolbox.Market.MarketPriceService();
        else if(type==typeof(PlayerInventoryService))service=new PlayerInventoryService();
        else if(type==typeof(OmniToolbox.Tooltips.ItemSupplementTooltip))service=new OmniToolbox.Tooltips.ItemSupplementTooltip();
        else if(type==typeof(CollectionItemIndex))service=new CollectionItemIndex();
        else if(type==typeof(OmniToolbox.Collections.CollectionSourceService))service=new OmniToolbox.Collections.CollectionSourceService();
        else if(type==typeof(NonEntityTargetVisibility)){
            var hooks=new HookRegistry();owned.Add(hooks);service=new NonEntityTargetVisibility(hooks);
        }else throw new InvalidOperationException("未实现的宿主提供器："+type.Name);
        services[type]=service;if(service is IDisposable disposable)owned.Add(disposable);return service;
    }
    public static IconBrowser GetIconBrowser()=>(IconBrowser)GetService(typeof(IconBrowser));
    public static void Draw(){if(services.TryGetValue(typeof(IconBrowser),out var value))((IconBrowser)value).Draw();}
    public static void Tick(Dalamud.Plugin.Services.IFramework framework){if(services.TryGetValue(typeof(OmniToolbox.Market.MarketPriceService),out var market))((OmniToolbox.Market.MarketPriceService)market).Tick();if(services.TryGetValue(typeof(PlayerInventoryService),out var inventory))((PlayerInventoryService)inventory).Tick();if(services.TryGetValue(typeof(OmniToolbox.Teleport.TeleportService),out var teleport))((OmniToolbox.Teleport.TeleportService)teleport).Tick();if(services.TryGetValue(typeof(ItemPreviewService),out var value))((ItemPreviewService)value).Tick(framework);}
    public static void Dispose(){foreach(var value in owned.AsEnumerable().Reverse())try{value.Dispose();}catch(Exception e){Services.Log?.Error(e,"Omni provider disposal failed");}owned.Clear();services.Clear();}
}





