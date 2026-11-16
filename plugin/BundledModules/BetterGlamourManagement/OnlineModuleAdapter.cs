using OmniToolbox.Collections;
using OmniToolbox.Items;
using OmenTools;

namespace OmniToolbox.TreePublic;

public sealed partial class BetterGlamourManagement
{
    private Settings config = new();
    private ItemPreviewService itemPreviewService = null!;
    private ItemPatchMap itemPatchMap = null!;
    private CollectionSourceService collectionSourceService = null!;
    private StorageSnapshot inventory = null!;
    private bool configDirty;
    private long saveAfter;
    private bool isInGlamourDresser(uint id) => inventory.Dresser.Contains(id);
    private bool isInArmoire(uint id) => inventory.Armoire.Contains(id);
    private long getInventoryRevision() => inventory.Revision;

    protected override void OnStart(FeatureLifetime activation)
    {
        NativeRuntimeGuard.RequireInitialized();
        ModuleText.Owner = this;
        activation.TrackStartup(GlamourStartup.Run(activation, sources =>
        {
            collectionSourceService = sources;
            itemPreviewService = new();
            activation.Add(itemPreviewService);
            itemPatchMap = new();
            inventory = new();
            inventory.Tick();
            StartReady(activation);
        }));
    }

    private void saveConfig() { configDirty = true; saveAfter = Environment.TickCount64 + 300; }
    private void FlushConfig()
    {
        if (!configDirty || !IsEnabled || Environment.TickCount64 < saveAfter) return;
        configDirty = false;
        // API 1's public host request preserves the enabled state and persists the bound config.
        RequestEnabled(true, true);
    }

    public override bool TryHandleCommand(string arguments)
    {
        if (!IsEnabled) return false;
        switch (arguments.Trim().ToLowerInvariant())
        {
            case "": case "open": OpenManager(); return true;
            case "close": managerNativeUI?.Close(); return true;
            default: return false;
        }
    }

    protected override void OnDestroy()
    {
        if (ReferenceEquals(ModuleText.Owner, this)) ModuleText.Owner = null;
    }
}

internal static class GlamourStartup
{
    internal static async Task Run(FeatureLifetime activation, Action<CollectionSourceService> ready)
    {
        var sources = await CollectionSourceService.CreateAsync(activation.CancellationToken).ConfigureAwait(false);
        try { activation.AddAsync(sources.DisposeAsync); }
        catch { await sources.DisposeAsync().ConfigureAwait(false); throw; }
        await DService.Instance().Framework.RunOnFrameworkThread(() =>
        {
            activation.CancellationToken.ThrowIfCancellationRequested();
            ready(sources);
        }).ConfigureAwait(false);
    }
}
