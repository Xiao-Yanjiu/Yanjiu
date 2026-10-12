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
        public string Author { get; init; }="";
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
    }
}
namespace OmniToolbox.UI
{
    public static class OmniFonts
    {
        public static IFontHandle GetUIFont(float scale=1)=>Yanjiu.UI.UiFonts.Get(scale);
    }
}
namespace OmniToolbox.UI.Controls
{
    public static class OmniControls
    {
        public static bool Checkbox(string label,ref bool value)=>Dalamud.Bindings.ImGui.ImGui.Checkbox(label,ref value);
        public static bool Button(string label)=>Dalamud.Bindings.ImGui.ImGui.Button(label);
    }
}
namespace OmniToolbox.UI.Theme
{
    public sealed class ComicStyleScope : IDisposable
    {private readonly Yanjiu.UI.ThemeScope theme=new();public void Dispose()=>theme.Dispose();}
    public static class OmniTheme
    {
        public static Yanjiu.UI.Palette Tokens=>Yanjiu.UI.Themes.Current;
        public static Vector4 ControlAccent=>Tokens.Accent;
        public static Vector4 HoverBackground=>Vector4.Lerp(Tokens.Surface,Tokens.Accent,.25f);
        public static Vector4 ActiveBackground=>Vector4.Lerp(Tokens.Surface,Tokens.Accent,.45f);
        public static float CheckboxSize()=>Scale(20);
        public static float ScaleValue=>Yanjiu.UI.Themes.Scale;
        public static float Scale(float value)=>value*ScaleValue;
        public static Vector2 Scale(Vector2 value)=>value*ScaleValue;
        public static bool UsesDarkPalette=>Tokens.Dark;
    }
}
namespace OmniToolbox.Notifications
{
    public static class OmniNotifier
    {
        public static void Chat(string message)=>Yanjiu.Services.Framework.RunOnFrameworkThread(()=>Yanjiu.Services.Chat.Print(message));
    }
}
