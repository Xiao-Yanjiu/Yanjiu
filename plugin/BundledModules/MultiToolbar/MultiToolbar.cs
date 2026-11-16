using System.Linq;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Interface;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;
using OmniToolbox.Common.Module.Abstractions;
using OmniToolbox.Common.Module.Enums;
using OmniToolbox.Common.Module.Models;
using OmniToolbox.Host;
using TreeTools.Lifecycle;
using OmniToolbox.Notifications;
using OmniToolbox.Teleport;
using OmniToolbox.UI;
using OmenTools.Dalamud.Helpers;
using OmenTools.Extensions;
using OmenTools.OmenService;

namespace OmniToolbox.TreePublic;

public sealed partial class ToolbarRenderer : IDisposable
{
    public ModuleInfo Info { get; } = new()
    {
        Category = ModuleCategory.Interface
    };

    private const double PluginCacheRefreshIntervalSeconds = 1d;
    private const float PopupWidth = 380f;
    private const float PopupListHeight = 420f;

    private readonly MultiToolbarBarConfig config;
    private readonly string barID;
    private readonly Action saveConfig;
    private readonly Func<Newtonsoft.Json.Linq.JObject?>? getMultiDockConfig;
    private readonly IconBrowser iconBrowser;
    private readonly TeleportService teleportService;
    private readonly string pluginEntryLabel;
    private readonly string commandEntryLabel;
    private readonly string dtrEntryLabel;
    private FeatureLifetime? runtimeLifetime;
    

    public ToolbarRenderer(MultiToolbarConfig config, Action saveConfig, IconBrowser iconBrowser, TeleportService teleportService,
        Func<Newtonsoft.Json.Linq.JObject?>? getMultiDockConfig = null)
        : this(config, saveConfig, iconBrowser, teleportService, "main", getMultiDockConfig)
    {
        if (NormalizeWidgets())
        {
            saveConfig();
        }
    }

    private ToolbarRenderer(MultiToolbarBarConfig config, Action saveConfig, IconBrowser iconBrowser, TeleportService teleportService,
        string barID, Func<Newtonsoft.Json.Linq.JObject?>? getMultiDockConfig = null)
    {
        this.config = config;
        this.barID = barID;
        this.saveConfig = saveConfig;
        this.iconBrowser = iconBrowser;
        this.teleportService = teleportService;
        this.getMultiDockConfig = getMultiDockConfig;

        pluginEntryLabel = ModuleText.Get("Feature.MultiToolbar.PluginList");
        commandEntryLabel = ModuleText.Get("Feature.MultiToolbar.CommandList");
        dtrEntryLabel = ModuleText.Get("Feature.MultiToolbar.DtrList");
    }

    public bool HasSettings => true;

    // 兼容旧版本重复写入内置按钮的配置，保留用户自定义按钮。
    private bool NormalizeWidgets()
    {
        if (config.Widgets is null)
        {
            config.Widgets = [];
        }

        bool migrationChanged=!config.RequestedWidgetsAdded;
        config.RequestedWidgetsAdded=true;
        var seenBuiltIns = new HashSet<MultiToolbarWidgetType>();
        var normalized = new List<MultiToolbarWidgetConfig>(config.Widgets.Count);
        var changed = migrationChanged;
        var commands = config.Commands.DistinctBy(command =>
            (command.Type, command.Side, command.Enabled, command.ShowIcon, command.DisplayName,
                command.Name, command.Command, command.RightCommand, command.GameIconID, command.IconFilePath, command.IconGlyph)).ToList();
        if (commands.Count != config.Commands.Count)
        {
            config.Commands = commands;
            changed = true;
        }
        if (!config.TextEntryDefaultsApplied)
        {
            foreach (var entry in config.Widgets)
            {
                if (entry.Type is (MultiToolbarWidgetType.PluginList or MultiToolbarWidgetType.CommandList) &&
                    entry.GameIconID == 0 && entry.DisplayName is null)
                {
                    entry.ShowIcon = false;
                }
            }

            config.TextEntryDefaultsApplied = true;
            changed = true;
        }
        foreach (var widget in config.Widgets)
        {
            if (widget.Type is MultiToolbarWidgetType.ExperienceBar or MultiToolbarWidgetType.SanctuaryIndicator)
            {
                changed = true;
                continue;
            }

            if (widget.Type is not (MultiToolbarWidgetType.CustomButton or MultiToolbarWidgetType.DtrSingle) &&
                !seenBuiltIns.Add(widget.Type))
            {
                changed = true;
                continue;
            }

            normalized.Add(widget);
        }

        if (changed)
        {
            config.Widgets = normalized;
        }

        return changed;
    }

    public void Start(FeatureLifetime lifetime)
    {
        bellDue = 0;
        lifetime.OnStopping(() => bellDue = 0);
        runtimeLifetime = lifetime;
        foreach (var bar in auxiliaryToolbars) bar.runtimeLifetime = lifetime;
        nextEquipmentSnapshotRefresh = nextInventorySpaceRefresh = nextCurrencyLabelRefresh = 0;
        MigrateMultiDock(); MigrateMultiDockPlugins();
        RegisterHiddenWindowEntry(lifetime);
        lifetime.OnStopping(() => DalamudServices.PluginInterface.UiBuilder.Draw -= Draw);
        DalamudServices.PluginInterface.UiBuilder.Draw += Draw;
        lifetime.OnStopping(() => FrameworkManager.Instance().Unreg(OnFrameworkUpdate));
        if (!FrameworkManager.Instance().Reg(OnFrameworkUpdate)) throw new InvalidOperationException("Toolbar update registration failed.");
        lifetime.OnStopping(() => DalamudServices.AddonLifecycle.UnregisterListener(AddonEvent.PreDraw, "_DTR", OnNativeDtrPreDraw));
        DalamudServices.AddonLifecycle.RegisterListener(AddonEvent.PreDraw, "_DTR", OnNativeDtrPreDraw);
    }

    private void OnFrameworkUpdate(IFramework _)
    {
        PumpNativeHud();foreach(var bar in auxiliaryToolbars)bar.PumpNativeHud();
        PumpNativeHiddenWindows();
        PumpNativePopupSafely();foreach(var bar in auxiliaryToolbars)bar.PumpNativePopupSafely();
        TickRetainerBell();
        teleportService.Tick();
        UpdateNativeDtrVisibility(UsesNativeDtr());
    }

    private unsafe void OnNativeDtrPreDraw(AddonEvent _, AddonArgs args)
    {
        if (!UsesNativeDtr())
        {
            return;
        }

        var addon = (AtkUnitBase*)args.Addon.Address;
        if (addon is null)
        {
            return;
        }

        addon->IsVisible = false;
    }

    private static bool TryNormalizeCommand(string? value, out string command)
    {
        command = (value ?? string.Empty).Trim().TrimEnd(';').Trim();
        if (command.Length == 0)
        {
            return false;
        }

        if (command[0] == '／')
        {
            command = $"/{command[1..]}";
        }
        else if (command[0] == '＼')
        {
            command = $"\\{command[1..]}";
        }
        else if (command[0] is not ('/' or '\\'))
        {
            command = $"/{command}";
        }

        return command.Length > 1;
    }

    private static void ExecuteCommand(string command)
    {
        if (!TryNormalizeCommand(command, out var normalized))
        {
            return;
        }

        try
        {
            if (!DalamudServices.CommandManager.ProcessCommand(normalized))
            {
                ChatManager.Instance().SendCommand(normalized);
            }
        }
        catch (Exception ex)
        {
            DalamudServices.PluginLog.Error(ex, "ToolbarRenderer command execution failed: {Command}", normalized);
        }
    }

    private static bool TryEnablePlugin(IExposedPlugin plugin)
    {
        ExecuteCommand($"/xlenableplugin \"{plugin.InternalName}\"");
        return true;
    }

    private static bool TryDisablePlugin(IExposedPlugin plugin)
    {
        ExecuteCommand($"/xldisableplugin \"{plugin.InternalName}\"");
        return true;
    }
}
