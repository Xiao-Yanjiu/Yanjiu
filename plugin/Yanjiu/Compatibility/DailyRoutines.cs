// SPDX-License-Identifier: GPL-3.0-or-later
// Independent lifecycle/config/overlay compatibility for DailyRoutines source modules.
using System.Numerics;
using Dalamud.Plugin.Services;
using Dalamud.Interface.Windowing;
using OmenTools.Threading.TaskHelper;
using Yanjiu.Modules;

namespace DailyRoutines.Common.Module.Enums
{
    public enum ModuleCategory { All,Favorites,General,System,Interface,Combat,Action,Assist,CraftGather,Duty,Notification,Recruitment,Script,GoldSaucer,Item,Daily,Automation }
}
namespace DailyRoutines.Common.Module.Models
{
    public sealed class ModulePermission
    {
        public bool NeedAuth{get;init;}public bool CNOnly{get;init;}public bool TCOnly{get;init;}public bool KROnly{get;init;}public bool GLOnly{get;init;}
        public bool AllTester{get;init;}public bool CNTester{get;init;}public bool GLTester{get;init;}public bool TCTester{get;init;}public bool KRTester{get;init;}
        public bool AllDefaultEnabled{get;init;}public bool CNDefaultEnabled{get;init;}public bool GLDefaultEnabled{get;init;}public bool TCDefaultEnabled{get;init;}public bool KRDefaultEnabled{get;init;}
        public string? UnsupportedReason()
        {
            // Yanjiu owns module enablement; upstream account/tester flags are metadata only.
            bool restricted=CNOnly||TCOnly||KROnly||GLOnly;
            if(restricted&&!((CNOnly&&OmenTools.OmenService.GameState.IsCN)||(TCOnly&&OmenTools.OmenService.GameState.IsTC)||(KROnly&&OmenTools.OmenService.GameState.IsKR)||(GLOnly&&OmenTools.OmenService.GameState.IsGL)))return "此DR模块不支持当前客户端区域";
            return null;
        }
    }
    public sealed class ModuleInfo
    {
        public string Title { get; init; }="";public string Description { get; init; }="";
        public Enums.ModuleCategory Category { get; init; }
        public string[] Author { get; init; }=Array.Empty<string>();public string[] ModulesPrerequisite{get;init;}=[];public string[] ModulesPair{get;init;}=[];public string[] ModulesConflict{get;init;}=[];public string[] PreviewImageURL{get;init;}=[];public string ReportURL{get;init;}="";
    }
}
namespace DailyRoutines.Common.Module.Abstractions
{
    public class ModuleConfig { public virtual string? PreviousModuleName=>null; }
    public abstract class ModuleBase : Yanjiu.Modules.ModuleBase
    {
        public abstract Models.ModuleInfo Info { get; }
        public virtual Models.ModulePermission Permission{get;}=new();
        public override ModuleDescriptor Descriptor=>new(Info.Title,Info.Description,string.Join("、",Info.Author),Info.Category.ToString());
        public TaskHelper? TaskHelper { get; protected set; }
        public Common.Interface.Windows.Overlay? Overlay { get; protected set; }
        public override bool HasSettings=>true;
        protected virtual object CreateSettingsConfiguration(Type type)=>Activator.CreateInstance(type,true)??throw new InvalidOperationException("配置无法初始化");
        public object CreateSettingsModel(Type type)=>CreateSettingsConfiguration(type);
        protected Common.Interface.Windows.OverlayConfig? OverlayConfig{get;set;}
        public void ToggleOverlayConfig(bool? enabled=null){OverlayConfig??=new(this);OverlayConfig.IsOpen=enabled??!OverlayConfig.IsOpen;}
        protected virtual void Init(){}
        protected virtual void Uninit(){}
        protected virtual void ConfigUI(){}
        protected virtual void OverlayUI()=>ConfigUI();
        protected virtual void OverlayOnOpen(){}protected virtual void OverlayOnClose(){}protected virtual void OverlayPreDraw(){}protected virtual void OverlayPostDraw(){}protected virtual void OverlayUpdate(){}protected virtual void OverlayPreOpenCheck(){}
        public bool IsInitialized=>IsEnabled;public bool IsDisposed=>Disposed;
        protected string ConfigDirectoryPath{get{var path=Path.Combine(Yanjiu.Services.ConfigDirectory,"modules",ModuleName);Directory.CreateDirectory(path);return path;}}
        protected string ConfigFilePath=>Path.Combine(Yanjiu.Services.ConfigDirectory,"modules",GetType().FullName!.Replace(':' ,'_')+".json");
        public void RenderOverlayOpen()=>OverlayOnOpen();public void RenderOverlayClose()=>OverlayOnClose();public void RenderOverlayPreDraw()=>OverlayPreDraw();public void RenderOverlayPostDraw()=>OverlayPostDraw();public void RenderOverlayUpdate()=>OverlayUpdate();public void RenderOverlayPreOpenCheck()=>OverlayPreOpenCheck();
        protected static void ExportToClipboard<T>(T value) where T:class{
            var json=Newtonsoft.Json.JsonConvert.SerializeObject(value);
            if(json.Length>1_500_000)throw new InvalidDataException("配置过大");
            Dalamud.Bindings.ImGui.ImGui.SetClipboardText(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json)));
        }
        protected static T? ImportFromClipboard<T>() where T:class{
            try{var data=Dalamud.Bindings.ImGui.ImGui.GetClipboardText();if(data.Length>2_000_000)throw new InvalidDataException("配置过大");
                return Newtonsoft.Json.JsonConvert.DeserializeObject<T>(System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(data)),new Newtonsoft.Json.JsonSerializerSettings{TypeNameHandling=Newtonsoft.Json.TypeNameHandling.None,MaxDepth=64});}
            catch(Exception e){Yanjiu.Services.Log?.Error(e,"配置导入失败");return null;}
        }
        protected override void OnEnable(){var reason=Permission.UnsupportedReason();if(reason!=null)throw new InvalidOperationException(reason);OmenTools.Dalamud.IPCAttributeRegistry.RegObjectIPCs(this);Init();}
        private void CleanupNativeResources(){
            var failures=new List<Exception>();var seen=new HashSet<object>(ReferenceEqualityComparer.Instance);
            for(var type=GetType();type!=null&&type!=typeof(ModuleBase);type=type.BaseType){
                foreach(var field in type.GetFields(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.DeclaredOnly)){
                    bool hook=field.FieldType.IsGenericType&&field.FieldType.GetGenericTypeDefinition()==typeof(Dalamud.Hooking.Hook<>);
                    if(!hook&&!typeof(OmenTools.Interop.Game.MemoryPatch).IsAssignableFrom(field.FieldType))continue;
                    try{if(field.GetValue(field.IsStatic?null:this) is IDisposable resource&&seen.Add(resource)){resource.Dispose();if(!field.IsInitOnly)field.SetValue(field.IsStatic?null:this,null);}}catch(Exception e){failures.Add(e);}
                }
            }
            if(failures.Count>0)throw new AggregateException("DR原生资源清理失败",failures);
        }
        protected override void OnDisable(){try{try{Uninit();}finally{try{CleanupNativeResources();}finally{TaskHelper?.Abort();TaskHelper?.Dispose();TaskHelper=null;if(OverlayConfig!=null){Yanjiu.Services.Windows.RemoveWindow(OverlayConfig);OverlayConfig=null;}if(Overlay!=null){Yanjiu.Services.Windows.RemoveWindow(Overlay);Overlay=null;}}}}finally{OmenTools.Dalamud.IPCAttributeRegistry.UnregObjectIPCs(this);}}
        protected override void OnDispose(){OmenTools.Dalamud.IPCAttributeRegistry.UnregObjectIPCs(this);if(OverlayConfig!=null){Yanjiu.Services.Windows.RemoveWindow(OverlayConfig);OverlayConfig=null;}TaskHelper?.Dispose();if(Overlay!=null)Yanjiu.Services.Windows.RemoveWindow(Overlay);}
        public override bool DrawSettings(){ConfigUI();return false;}
        public void RenderOverlay()=>OverlayUI();
        public T? LoadConfig<T>()=>ReadConfiguration<T>();
        public void SaveConfig<T>(T config)=>WriteConfiguration(config);
    }
}
namespace DailyRoutines.Common.Interface.Windows
{
    public sealed class OverlayConfig : Window
    {
        private readonly Module.Abstractions.ModuleBase owner;
        public OverlayConfig(Module.Abstractions.ModuleBase owner):base(owner.Info.Title+"###YanjiuConfig:"+owner.GetType().FullName){this.owner=owner;Yanjiu.Services.Windows.AddWindow(this);}
        public override bool DrawConditions()=>owner.IsEnabled;
        public override void Draw(){using var theme=new Yanjiu.UI.ThemeScope();owner.DrawSettings();}
    }
    public sealed class Overlay : Window
    {
        private readonly Module.Abstractions.ModuleBase owner;
        public Overlay(Module.Abstractions.ModuleBase owner):this(owner,owner.Info.Title){}
        public Overlay(Module.Abstractions.ModuleBase owner,string title):base(title+"###YanjiuOverlay:"+owner.GetType().FullName){this.owner=owner;Size=new Vector2(650,450);SizeCondition=Dalamud.Bindings.ImGui.ImGuiCond.FirstUseEver;Yanjiu.Services.Windows.AddWindow(this);}
        public override void OnOpen()=>owner.RenderOverlayOpen();
        public override void OnClose()=>owner.RenderOverlayClose();
        public override void PreDraw(){owner.RenderOverlayPreOpenCheck();owner.RenderOverlayUpdate();owner.RenderOverlayPreDraw();}
        public override void PostDraw()=>owner.RenderOverlayPostDraw();
        public override void Draw(){using var theme=new Yanjiu.UI.ThemeScope();owner.RenderOverlay();}
    }
}
namespace DailyRoutines.Common.Info.Models
{
    public sealed record PlayerInfo(string Name,ulong GameObjectId);
    public readonly record struct PlayerTargetingInfo(OmenTools.Dalamud.Services.Game.Object.Abstractions.ObjectKinds.IPlayerCharacter Player,DateTime TargetingStartTime,int TargetingDurationSeconds,bool IsNew=false);
}
namespace DailyRoutines.Manager
{
    public sealed class PlayersManager
    {
        private static readonly PlayersManager current=new();
        public static PlayersManager Instance()=>current;
        public event Action<IReadOnlyList<OmenTools.Dalamud.Services.Game.Object.Abstractions.ObjectKinds.IPlayerCharacter>>? ReceivePlayersAround;
        public event Action<IReadOnlyList<Common.Info.Models.PlayerTargetingInfo>>? ReceivePlayersTargetingMe;
        public List<OmenTools.Dalamud.Services.Game.Object.Abstractions.ObjectKinds.IPlayerCharacter> PlayersAround{get;private set;}=[];
        public int PlayersAroundCount=>PlayersAround.Count;
        public List<Common.Info.Models.PlayerTargetingInfo> PlayersTargetingMe{get;private set;}=[];
        private readonly Dictionary<ulong,DateTime> starts=[];
        private long scanned;private uint zone;
        public void Update(){
            if(Environment.TickCount64-scanned<500)return;scanned=Environment.TickCount64;
            var objects=Yanjiu.Services.Objects;var local=objects?.LocalPlayer;
            var now=DateTime.Now;var currentZone=OmenTools.OmenService.GameState.TerritoryType;
            if(zone!=currentZone){starts.Clear();zone=currentZone;}
            var around=objects==null||local==null?[]:objects.OfType<Dalamud.Game.ClientState.Objects.SubKinds.IPlayerCharacter>().Where(p=>p.GameObjectId!=local.GameObjectId).ToArray();
            PlayersAround=around.Select(p=>OmenTools.Dalamud.Services.Game.Object.Abstractions.ObjectKinds.IPlayerCharacter.Create(p.Address)).ToList();
            var targeting=new List<Common.Info.Models.PlayerTargetingInfo>();var active=new HashSet<ulong>();
            foreach(var p in around.Where(p=>p.TargetObjectId==local!.GameObjectId)){
                active.Add(p.GameObjectId);bool isNew=!starts.TryGetValue(p.GameObjectId,out var since);
                if(isNew)starts[p.GameObjectId]=since=now;
                targeting.Add(new(OmenTools.Dalamud.Services.Game.Object.Abstractions.ObjectKinds.IPlayerCharacter.Create(p.Address),since,Math.Max(0,(int)(now-since).TotalSeconds),isNew));
            }
            foreach(var id in starts.Keys.Where(id=>!active.Contains(id)).ToArray())starts.Remove(id);
            PlayersTargetingMe=targeting;
            Notify(ReceivePlayersAround,PlayersAround);Notify(ReceivePlayersTargetingMe,PlayersTargetingMe);
        }
        private static void Notify<T>(Action<IReadOnlyList<T>>? listeners,IReadOnlyList<T> items){
            if(listeners==null)return;foreach(Action<IReadOnlyList<T>> handler in listeners.GetInvocationList())try{handler(items);}catch(Exception e){Yanjiu.Services.Log?.Error(e,"DR player subscriber failed");}
        }
        public void Clear(){starts.Clear();PlayersAround=[];PlayersTargetingMe=[];ReceivePlayersAround=null;ReceivePlayersTargetingMe=null;}
    }
    public static class LanguageManager
    {private static readonly Lazy<Dictionary<string,string>> entries=new(Load);
        private static Dictionary<string,string> Load(){
            var root=Yanjiu.PluginFiles.DirectoryPath;var file=Path.Combine(root,"DR","ChineseSimplified.json");
            if(!File.Exists(file))return new();
            if(new FileInfo(file).Length>4*1024*1024)throw new InvalidDataException("DR语言文件过大");
            return System.Text.Json.JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(file))??new();
        }
        public static string Get(string key,params object[] args){var value=entries.Value.GetValueOrDefault(key,key);return Format(value,args);}
        public static string Format(string value,params object[] args){
            if(args.Length==0)return value;
            if(args.Length==1&&(args[0] is System.Collections.IDictionary||args[0] is IReadOnlyDictionary<string,object>)){
                var named=new Dictionary<string,object?>(StringComparer.Ordinal);
                if(args[0] is System.Collections.IDictionary dictionary){foreach(System.Collections.DictionaryEntry pair in dictionary)if(pair.Key is string name)named[name]=pair.Value;}
                else foreach(var pair in (IReadOnlyDictionary<string,object>)args[0])named[pair.Key]=pair.Value;
                return System.Text.RegularExpressions.Regex.Replace(value,@"(?<!\{)\{([A-Za-z_]\w*)(,-?\d+)?(:[^{}]*)?\}(?!\})",match=>{
                    if(!named.TryGetValue(match.Groups[1].Value,out var item))return match.Value;
                    return string.Format(System.Globalization.CultureInfo.CurrentCulture,"{0"+match.Groups[2].Value+match.Groups[3].Value+"}",item);
                },System.Text.RegularExpressions.RegexOptions.None,TimeSpan.FromMilliseconds(100));
            }
            return string.Format(System.Globalization.CultureInfo.CurrentCulture,value,args);
        }
        public static string GetText(string key,params object[] args)=>Get(key,args);
        public static Lumina.Text.ReadOnly.ReadOnlySeString GetSe(string key,params object[] args)=>FormatSe(entries.Value.GetValueOrDefault(key,key),args);
        public static Lumina.Text.ReadOnly.ReadOnlySeString FormatSe(string template,params object[] args){
            var builder=new Lumina.Text.SeStringBuilder();var literal=new System.Text.StringBuilder();
            void Flush(){if(literal.Length>0){builder.Append(literal.ToString());literal.Clear();}}
            for(int i=0;i<template.Length;i++){
                char c=template[i];
                if(i+1<template.Length&&((c=='{'&&template[i+1]=='{')||(c=='}'&&template[i+1]=='}'))){literal.Append(c);i++;continue;}
                if(c!='{'){literal.Append(c);continue;}
                int end=template.IndexOf('}',i+1);if(end<0)throw new FormatException("本地化参数缺少右括号");
                string spec=template[(i+1)..end];int cut=spec.IndexOfAny([',',':']);string number=cut<0?spec:spec[..cut];
                if(!int.TryParse(number,out int index)||index<0||index>=args.Length)throw new FormatException("本地化参数无效");
                Flush();object? value=args[index];
                if(cut>=0)builder.Append(string.Format(System.Globalization.CultureInfo.CurrentCulture,"{0"+spec[cut..]+"}",value));
                else switch(value){
                    case Lumina.Text.ReadOnly.ReadOnlySeString rich:builder.Append(rich);break;
                    case Dalamud.Game.Text.SeStringHandling.SeString rich:builder.Append(new Lumina.Text.ReadOnly.ReadOnlySeString(rich.Encode()));break;
                    case Dalamud.Game.Text.SeStringHandling.Payload payload:builder.Append(new Lumina.Text.ReadOnly.ReadOnlySeString(payload.Encode()));break;
                    case Dalamud.Utility.RentedSeStringBuilder rented:builder.Append(rented.Builder.ToReadOnlySeString());break;
                    default:builder.Append(value?.ToString()??"");break;
                }
                i=end;
            }
            Flush();return builder.ToReadOnlySeString();
        }}
}





