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
    public class ModuleConfig { }
    public sealed class ModuleInfo
    {
        public string Title { get; init; }="";public string Description { get; init; }="";
        public Enums.ModuleCategory Category { get; init; }
        public string[] Author { get; init; }=Array.Empty<string>();
    }
}
namespace DailyRoutines.Common.Module.Abstractions
{
    public abstract class ModuleBase : Yanjiu.Modules.ModuleBase
    {
        public abstract Models.ModuleInfo Info { get; }
        public override ModuleDescriptor Descriptor=>new(Info.Title,Info.Description,string.Join("、",Info.Author),Info.Category.ToString());
        public TaskHelper? TaskHelper { get; protected set; }
        public Common.Interface.Windows.Overlay? Overlay { get; protected set; }
        public override bool HasSettings=>true;
        protected virtual void Init(){}
        protected virtual void Uninit(){}
        protected virtual void ConfigUI(){}
        protected virtual void OverlayUI()=>ConfigUI();
        protected override void OnEnable()=>Init();
        protected override void OnDisable(){try{Uninit();}finally{TaskHelper?.Abort();TaskHelper?.Dispose();TaskHelper=null;if(Overlay!=null){Yanjiu.Services.Windows.RemoveWindow(Overlay);Overlay=null;}}}
        protected override void OnDispose(){TaskHelper?.Dispose();if(Overlay!=null)Yanjiu.Services.Windows.RemoveWindow(Overlay);}
        public override bool DrawSettings(){ConfigUI();return false;}
        public void RenderOverlay()=>OverlayUI();
        protected T? LoadConfig<T>()=>ReadConfiguration<T>();
        protected void SaveConfig<T>(T config)=>WriteConfiguration(config);
    }
}
namespace DailyRoutines.Common.Interface.Windows
{
    public sealed class Overlay : Window
    {
        private readonly Module.Abstractions.ModuleBase owner;
        public Overlay(Module.Abstractions.ModuleBase owner,string title):base(title+"###YanjiuOverlay:"+owner.GetType().FullName){this.owner=owner;Size=new Vector2(650,450);SizeCondition=Dalamud.Bindings.ImGui.ImGuiCond.FirstUseEver;Yanjiu.Services.Windows.AddWindow(this);}
        public override void Draw(){using var theme=new Yanjiu.UI.ThemeScope();owner.RenderOverlay();}
    }
}
namespace DailyRoutines.Common.Info.Models
{
    public sealed record PlayerInfo(string Name,ulong GameObjectId);
    public sealed record PlayerTargetingInfo(PlayerInfo Player);
}
namespace DailyRoutines.Manager
{
    public sealed class PlayersManager
    {
        private static readonly PlayersManager current=new();
        public static PlayersManager Instance()=>current;
        public event Action<IReadOnlyList<Common.Info.Models.PlayerTargetingInfo>>? ReceivePlayersTargetingMe;
        private long scanned;
        public void Update(){if(Environment.TickCount64-scanned<500)return;scanned=Environment.TickCount64;var objects=Yanjiu.Services.Objects;var local=objects?.LocalPlayer;if(objects==null||local==null)return;
            var items=objects.OfType<Dalamud.Game.ClientState.Objects.SubKinds.IPlayerCharacter>().Where(p=>p.GameObjectId!=local.GameObjectId&&p.TargetObjectId==local.GameObjectId).Select(p=>new Common.Info.Models.PlayerTargetingInfo(new(p.Name.TextValue,p.GameObjectId))).ToArray();ReceivePlayersTargetingMe?.Invoke(items);}
    }
    public static class LanguageManager
    {public static string Get(string key)=>key;public static string GetText(string key)=>key;}
}
