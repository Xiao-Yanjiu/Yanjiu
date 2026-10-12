// SPDX-License-Identifier: GPL-3.0-or-later
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Interface.Windowing;

namespace Yanjiu;

public sealed class Services
{
    [PluginService] public static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] public static ICommandManager Commands { get; private set; } = null!;
    [PluginService] public static IPluginLog Log { get; private set; } = null!;
    [PluginService] public static IFramework Framework { get; private set; } = null!;
    [PluginService] public static IChatGui Chat { get; private set; } = null!;
    [PluginService] public static IDataManager Data { get; private set; } = null!;
    [PluginService] public static IObjectTable Objects { get; private set; } = null!;
    [PluginService] public static IAddonLifecycle Addons { get; private set; } = null!;
    [PluginService] public static ITextureProvider Textures { get; private set; } = null!;
    [PluginService] public static IKeyState Keys { get; private set; } = null!;
    [PluginService] public static IPlayerState Player { get; private set; } = null!;
    [PluginService] public static IUnlockState Unlocks { get; private set; } = null!;
    public static readonly WindowSystem Windows = new("Yanjiu");
    public static string ConfigDirectory { get; set; } = "";
    public static Action<Action<uint>>? IconPicker { get; set; }
    public static T? Get<T>() where T:class => new object[]{PluginInterface,Commands,Log,Framework,Chat,Data,Objects,Addons,Textures,Keys,Player,Unlocks}.OfType<T>().FirstOrDefault();
}
