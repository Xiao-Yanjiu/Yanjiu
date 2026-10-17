// SPDX-License-Identifier: GPL-3.0-or-later
// Independently implemented API compatibility; no Omni implementation is copied.
using System.Numerics;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Yanjiu.Modules;

namespace OmniToolbox.Common.Module.Enums
{
    public enum ModuleCategory { All,Favorites,Daily,Combat,Item,Interface,Automation,Debug,Local,Online,Enabled,General,System,Assist,CraftGather,Duty,Notification,Recruitment,Script,GoldSaucer }
}
namespace OmniToolbox.Common.Module.Models
{
    public readonly record struct ModuleCommand(string DescriptionKey,string ClipboardText);
    public sealed class ModuleInfo
    {
        public string Title { get; init; }="";public string Description { get; init; }="";
        public Enums.ModuleCategory Category { get; init; }
        public string Author { get; init; }="YouShu";
        public IReadOnlyList<string>? SupportUrls { get; init; }
        public IReadOnlyList<ModuleCommand>? Commands { get; init; }
        public string? ReportURL { get; init; }public string? PreviewImageURL { get; init; }
        public IReadOnlyList<string>? PreviewImageURLs { get; init; }
        public bool RequiresPrivateProvider { get; init; }public bool RequiresElevatedAccess { get; init; }
        public int RequiredAccessLevel { get; init; }=1;public bool ShowsAutoPathStatus { get; init; }
    }
}
namespace OmniToolbox.Common.Module.Abstractions
{
    public abstract class ModuleBase : Yanjiu.Modules.ModuleBase
    {
        public abstract Models.ModuleInfo Info { get; }
        public OmniToolbox.UI.IconBrowser? HostIconBrowser{get;internal set;}
        internal Action? SaveHostConfig{get;set;}
        public override ModuleDescriptor Descriptor=>new(Info.Title,Info.Description,Info.Author,Info.Category.ToString());
    }
}
namespace OmniToolbox.Host
{
    public static class DalamudServices
    {
        public static IDalamudPluginInterface PluginInterface=>Yanjiu.Services.PluginInterface;
        public static ICommandManager CommandManager=>Yanjiu.Services.Commands;
        public static IFramework Framework=>Yanjiu.Services.Framework;
        public static IChatGui ChatGUI=>Yanjiu.Services.Chat;
        public static IPluginLog PluginLog=>Yanjiu.Services.Log;
        public static IDataManager DataManager=>Yanjiu.Services.Data;
        public static IAddonLifecycle AddonLifecycle=>Yanjiu.Services.Addons;
        public static ITextureProvider TextureProvider=>Yanjiu.Services.Textures;
        public static IKeyState KeyState=>Yanjiu.Services.Keys;
        public static IPlayerState PlayerState=>Yanjiu.Services.Player;
        public static IUnlockState UnlockState=>Yanjiu.Services.Unlocks;
        public static bool IsUnloading { get; internal set; }
        internal static object? GetInternalService(string fullName){var type=typeof(IDalamudPluginInterface).Assembly.GetType(fullName);return type==null?null:PluginInterface.GetService(type);}
    }
}
namespace OmniToolbox.UI.Theme
{
    public sealed class ComicStyleScope : IDisposable
    {private readonly Yanjiu.UI.ThemeScope theme=new();public void Dispose()=>theme.Dispose();}
}



