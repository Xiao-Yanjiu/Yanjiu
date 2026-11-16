using OmniToolbox.Common.Module.Abstractions;
using OmniToolbox.Common.Module.Enums;
using OmniToolbox.Common.Module.Models;
using OmniToolbox.Teleport;
using OmniToolbox.Config;
using Dalamud.Plugin.Services;
using OmenTools;

namespace OmniToolbox.TreePublic;

[ModuleApiVersion(1)]
public sealed partial class MultiToolbar : OmniModuleBase, Yanjiu.NativeUI.INativeModuleUI
{
    public sealed class Settings : MultiToolbarConfig { }
    private Settings config = new();
    private ToolbarRenderer? renderer;
    private TeleportService? teleport;
    private IconBrowser? icons;
    private bool dirty;
    private long saveAfter;
    public override ModuleInfo Info { get; } = new() { Category = ModuleCategory.Interface };
    public override ModuleLocalization Localization { get; } = ModuleLocalization.Create(ModuleText.Source);
    public override bool HasSettings => true;

    private ToolbarRenderer Renderer()
    {
        ModuleText.Owner = this;
        teleport ??= new();
        icons ??= new(new IconBrowserConfig(), MarkDirty);
        return renderer ??= new(config, MarkDirty, icons, teleport) { NativeSettingsRequested = OpenToolbarNativeSettings, NativeSettingsSaved = () => { if(!IsEnabled)RequestEnabled(false,true); } };
    }

    private void MarkDirty() { dirty = true; saveAfter = Environment.TickCount64 + 300; }

    public override bool DrawSettings()
    {
        var changed = Renderer().DrawSettings();
        if (!IsEnabled) icons?.Draw();
        changed |= dirty;
        dirty = false;
        return changed;
    }

    protected override void OnStart(FeatureLifetime activation)
    {
        activation.Add(Release);
        Renderer().Start(activation);
        activation.OnStopping(() => DService.Instance().Framework.Update -= Persist);
        DService.Instance().Framework.Update += Persist;
    }

    private void Persist(IFramework _)
    {
        if (!dirty || !IsEnabled || Environment.TickCount64 < saveAfter) return;
        dirty = false;
        RequestEnabled(true, true);
    }

    private void Release()
    {
        nativeSettings?.Dispose(); nativeSettings = null;
        renderer?.Dispose(); renderer = null;
        teleport?.Dispose(); teleport = null;
        if (icons is not null) icons.Close();
        icons = null;
    }

    protected override void OnDestroy()
    {
        Release();
        if (ReferenceEquals(ModuleText.Owner, this)) ModuleText.Owner = null;
    }
}
