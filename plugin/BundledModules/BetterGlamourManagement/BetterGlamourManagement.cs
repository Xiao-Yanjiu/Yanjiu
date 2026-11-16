using System.Threading;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.BaseTypes;
using OmenTools;
using OmenTools.Extensions;
using OmenTools.Interop.Game.Helpers;
using OmenTools.OmenService;
using TreeTools.Threading;
using OmniToolbox.Commands;
using OmniToolbox.Collections;
using OmniToolbox.Common.Module.Abstractions;
using OmniToolbox.Common.Module.Enums;
using OmniToolbox.Common.Module.Models;
using OmniToolbox.Items;
using OmniToolbox.Notifications;

namespace OmniToolbox.TreePublic;

[ModuleApiVersion(1)]
public sealed unsafe partial class BetterGlamourManagement : OmniModuleBase, Yanjiu.NativeUI.INativeModuleUI
{
    public override ModuleInfo Info { get; } = new()
    {
        Category = ModuleCategory.Item,
        PreviewImageURL =
            "https://raw.githubusercontent.com/YouShux/OmniToolbox.Common/main/Assets/previews/Item/BetterGlamourManagement-1.png?v=18138f9d",
        RequiresPrivateProvider = false,
        Commands =
        [
            new ModuleCommand(
                "Feature.BetterGlamourManagement.CommandDescription",
                OmniCommandCatalog.FormatModule(nameof(BetterGlamourManagement), "open"))
        ]
    };

    public override ModuleLocalization Localization { get; } = ModuleLocalization.Create(ModuleText.Source);

    public override bool HasSettings => true;

    private static readonly int[] InspectSlots = [0, 1, 2, 3, 4, 6, 7, 8, 9, 10, 11, 12];

    private BetterGlamourActionsNativeUI? actionsNativeUI;
    private BetterGlamourManagerNativeUI? managerNativeUI;
    private BetterGlamourPortraitPreview? portraitPreview;
    private TaskHelper? taskHelper;
    private BetterGlamourPreset? activeTryOnPreset;
    private BetterGlamourPreset? boundTryOnPreset;
    private nint previewPlayerAddress;
    private bool pendingTryOnRestore;
    private int selectedPresetIndex = -1;
    private int lastGearsetIndex = int.MinValue;
    private ulong lastCharacterContentID;

    private void StartReady(FeatureLifetime activation)
    {
        Activation.Add(DisposePlateExport);
        EnablePlateExport();
        taskHelper = new()
        {
            TimeoutMS = 10_000
        };
        activation.Add(taskHelper);
        var tasks = taskHelper;
        Activation.OnStopping(tasks.Abort);
        Activation.Add(() =>
        {
            taskHelper = null;
        });
        actionsNativeUI = new(TryOnInspect, SaveInspect, ExportInspect, OpenManager, ClearTryOn);
        Activation.Add(DisposeActionsNativeUI);
        portraitPreview = new(Activation);
        var preview = portraitPreview;
        Activation.Add(() => portraitPreview = null);
        Activation.AddAsync(preview.DisposeAsync);
        Activation.OnStopping(preview.Disable);
        var services = DService.Instance();
        portraitPreview.Initialize(new()
        {
            PluginInterface = DalamudServices.PluginInterface,
            AddonLifecycle = services.AddonLifecycle,
            TextureReadback = services.TextureReadback,
            Framework = services.Framework,
            ClientState = services.ClientState,
            ObjectTable = services.ObjectTable,
            Condition = services.Condition,
            GameGUI = services.GameGUI,
            DataManager = services.Data
        }, () => config.Presets, () => itemPreviewService.TryGetOriginalHairAppearance(out var customize) ? customize : null);
        portraitPreview.Enable();
        Activation.OnStopping(DisableRemote);
        EnableRemote();
        managerNativeUI = new(
            SelectPreset,
            PresetChanged,
            ChangePresetGearset,
            ToggleTryOn,
            IsTryingOnPreset,
            CopyPreset,
            DeletePreset,
            ImportClipboard,
            CreatePreset,
            portraitPreview,
            itemPatchMap,
            collectionSourceService,
            isInGlamourDresser,
            isInArmoire,
            getInventoryRevision,
            () => config.EnableGlamourPlateExport,
            SetPlateExportEnabled);
        Activation.Add(DisposeManagerNativeUI);
        Activation.OnStopping(() => managerNativeUI?.ClearPreviewTexture());
        if (!FrameworkManager.Instance().Reg(OnFrameworkUpdate, 250))
        {
            throw new InvalidOperationException("Better glamour management update registration failed.");
        }

        Activation.OnStopping(() => FrameworkManager.Instance().Unreg(OnFrameworkUpdate));
        var addonEvents = new AddonEventRegistry(DalamudServices.AddonLifecycle);
        Activation.OnStopping(addonEvents.Dispose);
        foreach (var addonName in new[] { CHARACTER_INSPECT_ADDON_NAME, GLAMOUR_PLATE_ADDON_NAME })
        {
            addonEvents.Register(AddonEvent.PostSetup, addonName, OnAttachedAddonEvent);
            addonEvents.Register(AddonEvent.PostShow, addonName, OnAttachedAddonEvent);
            addonEvents.Register(AddonEvent.PostRefresh, addonName, OnAttachedAddonEvent);
            addonEvents.Register(AddonEvent.PostRequestedUpdate, addonName, OnAttachedAddonEvent);
            addonEvents.Register(AddonEvent.PostUpdate, addonName, OnAttachedAddonEvent);
            addonEvents.Register(AddonEvent.PreHide, addonName, OnAttachedAddonEvent);
            addonEvents.Register(AddonEvent.PreFinalize, addonName, OnAttachedAddonEvent);
        }
        if (TryGetReadyAddon(CHARACTER_INSPECT_ADDON_NAME, out var inspectAddon))
            RefreshInspectActions(inspectAddon);
        if (TryGetReadyAddon(GLAMOUR_PLATE_ADDON_NAME, out var plateAddon))
            RefreshPlateExport(plateAddon, true);
    }

    protected override void OnStop()
    {
        ClearTryOn();
        boundTryOnPreset = null;
        selectedPresetIndex = -1;
        lastGearsetIndex = int.MinValue;
        lastCharacterContentID = 0;
    }

    private void OnAttachedAddonEvent(AddonEvent type, AddonArgs args)
    {
        var closing = type is AddonEvent.PreHide or AddonEvent.PreFinalize;
        if (args.AddonName == CHARACTER_INSPECT_ADDON_NAME)
        {
            if (closing)
                actionsNativeUI?.Close();
            else
                RefreshInspectActions((AtkUnitBase*)args.Addon.Address);
        }
        else if (closing)
        {
            AbortPlateImport();
            plateExportNativeUI?.Close();
        }
        else
        {
            RefreshPlateExport((AtkUnitBase*)args.Addon.Address, type != AddonEvent.PostUpdate);
        }
    }

    private void RefreshInspectActions(AtkUnitBase* inspectAddon)
    {
        if (inspectAddon == null || !inspectAddon->IsVisible || !inspectAddon->IsAddonAndNodesReady() ||
            inspectAddon->WindowNode == null || actionsNativeUI is null)
        {
            actionsNativeUI?.Close();
            return;
        }

        PositionAttachedNativeUI(inspectAddon, actionsNativeUI, 2f);
    }

    private static void PositionAttachedNativeUI(AtkUnitBase* sourceAddon, NativeAddon nativeUI, float gap)
    {
        if (!nativeUI.IsOpen)
        {
            nativeUI.Open();
        }

        AtkUnitBase* actionAddon = nativeUI;
        var stage = AtkStage.Instance();
        if (sourceAddon->WindowNode == null || actionAddon == null || actionAddon->WindowNode == null || stage == null)
        {
            return;
        }

        OmniNativeWindowAttachment.SyncBeside(nativeUI, actionAddon, sourceAddon, gap, 0f, preferRight: true);
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        itemPreviewService.Tick(framework);
        inventory.Tick();
        FlushConfig();
        RemoteTick();
        if (!DService.Instance().ClientState.IsLoggedIn)
        {
            taskHelper?.Abort();
            activeTryOnPreset = null;
            boundTryOnPreset = null;
            managerNativeUI?.UpdateTryOnButton();
            previewPlayerAddress = nint.Zero;
            pendingTryOnRestore = false;
            lastGearsetIndex = int.MinValue;
            lastCharacterContentID = 0;
            return;
        }

        var contentID = DService.Instance().PlayerState.ContentId;
        if (contentID != lastCharacterContentID)
        {
            ClearTryOn();
            lastCharacterContentID = contentID;
            lastGearsetIndex = int.MinValue;
            managerNativeUI?.UpdateData(config.Presets, selectedPresetIndex);
        }

        var gearsetModule = RaptureGearsetModule.Instance();
        if (gearsetModule == null)
        {
            lastGearsetIndex = int.MinValue;
        }
        else
        {
            var currentGearsetIndex = gearsetModule->CurrentGearsetIndex;
            if (lastGearsetIndex != currentGearsetIndex)
            {
                lastGearsetIndex = currentGearsetIndex;
                SynchronizeBoundTryOn(currentGearsetIndex);
            }
            else if (boundTryOnPreset is not null &&
                     !ReferenceEquals(activeTryOnPreset, boundTryOnPreset))
            {
                StartTryOn(boundTryOnPreset);
            }
        }

        var playerAddress = DService.Instance().ObjectTable.LocalPlayer?.Address ?? nint.Zero;
        if (activeTryOnPreset is not null &&
            playerAddress != nint.Zero &&
            playerAddress != previewPlayerAddress)
        {
            previewPlayerAddress = playerAddress;
            pendingTryOnRestore = true;
        }

        if (!pendingTryOnRestore &&
            activeTryOnPreset is not null &&
            taskHelper is { IsBusy: false } &&
            !IsActiveTryOnApplied())
        {
            pendingTryOnRestore = true;
        }

        TryApplyActiveTryOn();
    }

    private static bool TryGetReadyAddon(string name, out AtkUnitBase* addon)
    {
        addon = null;
        return AddonHelper.TryGetByName(name, out addon) &&
               addon->IsAddonAndNodesReady() &&
               addon->WindowNode != null;
    }

    private void TryOnInspect()
    {
        if (CaptureInspectPreset() is { } preset)
        {
            StartTryOn(preset);
        }
        else
        {
            NotifyNoInspectData();
        }
    }

    private void SaveInspect()
    {
        if (config.Presets.Count >= MAX_PRESET_COUNT)
        {
            OmniNotifier.Popup(
                Title,
                string.Format(OmniLoc.Get(this, "Feature.BetterGlamourManagement.PresetLimitReached"), MAX_PRESET_COUNT),
                NotificationType.Warning);
            return;
        }

        var preset = CaptureInspectPreset();
        if (preset is null)
        {
            NotifyNoInspectData();
            return;
        }

        preset.Name = string.Format(
            OmniLoc.Get(this, "Feature.BetterGlamourManagement.DefaultName"),
            DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
        config.Presets.Add(preset);
        selectedPresetIndex = config.Presets.Count - 1;
        saveConfig();
        managerNativeUI?.UpdateData(config.Presets, selectedPresetIndex);
        OmniNotifier.Popup(
            Title,
            string.Format(OmniLoc.Get(this, "Feature.BetterGlamourManagement.Saved"), preset.Name),
            NotificationType.Success);
    }

    private void ExportInspect()
    {
        var preset = CaptureInspectPreset();
        if (preset is null)
        {
            NotifyNoInspectData();
            return;
        }

        CopyPreset(preset);
    }

    private BetterGlamourPreset? CaptureInspectPreset()
    {
        if (!TryGetReadyAddon(CHARACTER_INSPECT_ADDON_NAME, out _) ||
            !InventoryType.Examine.TryGetItems(
                item => item.ItemId != 0 && Array.IndexOf(InspectSlots, item.Slot) >= 0,
                out var inspectItems))
        {
            return null;
        }

        var preset = new BetterGlamourPreset();
        foreach (var item in inspectItems)
        {
            preset.Items.Add(new()
            {
                Slot = item.Slot,
                ItemID = item.GlamourId != 0 ? item.GlamourId : item.ItemId,
                Stain0 = item.Stains[0],
                Stain1 = item.Stains[1]
            });
        }

        var uiState = UIState.Instance();
        if (uiState != null)
        {
            preset.GlassesID = uiState->Inspect.GlassesIds[0];
            preset.HairstyleID = ResolveInspectHairstyleID(uiState->Inspect.CustomizeData);
            preset.HairColor = uiState->Inspect.CustomizeData.HairColor;
            preset.HighlightColor = uiState->Inspect.CustomizeData.HighlightsColor;
            preset.HighlightsEnabled = uiState->Inspect.CustomizeData.Highlights;
        }

        return preset.Items.Count == 0 ? null : preset;
    }

    private void StartTryOn(BetterGlamourPreset preset)
    {
        if (!ReferenceEquals(boundTryOnPreset, preset))
            boundTryOnPreset = null;
        activeTryOnPreset = preset;
        managerNativeUI?.UpdateTryOnButton();
        previewPlayerAddress = DService.Instance().ObjectTable.LocalPlayer?.Address ?? nint.Zero;
        pendingTryOnRestore = true;
        TryApplyActiveTryOn();
    }

    private void ToggleTryOn(BetterGlamourPreset preset)
    {
        if (IsTryingOnPreset(preset))
            ClearTryOn();
        else
            StartTryOn(preset);
    }

    internal bool IsTryingOnPreset(BetterGlamourPreset preset) => ReferenceEquals(activeTryOnPreset, preset);

    private bool IsActiveTryOnApplied()
    {
        if (activeTryOnPreset is not { } preset)
        {
            return true;
        }

        foreach (var item in preset.Items)
        {
            if (item.ItemID != 0 &&
                !itemPreviewService.IsEquipmentPreviewApplied(item.ItemID, item.Slot, item.Stain0, item.Stain1))
            {
                return false;
            }
        }

        return itemPreviewService.IsGlassesPreviewApplied(preset.GlassesID) &&
               itemPreviewService.IsHairAppearancePreviewApplied(preset.HairstyleID, preset.HairColor, preset.HighlightColor, preset.HighlightsEnabled);
    }

    private void TryApplyActiveTryOn()
    {
        if (!pendingTryOnRestore ||
            activeTryOnPreset is null ||
            taskHelper is null ||
            taskHelper.IsBusy ||
            previewPlayerAddress == nint.Zero ||
            DService.Instance().Condition.IsBetweenAreas)
        {
            return;
        }

        pendingTryOnRestore = false;
        var preset = activeTryOnPreset;
        var items = preset.Items
            .Where(static item => item.ItemID != 0)
            .OrderBy(static item => item.Slot)
            .ToArray();
        foreach (var item in items)
        {
            taskHelper.Enqueue(() =>
            {
                itemPreviewService.PreviewEquipment(item.ItemID, item.Slot, item.Stain0, item.Stain1);
                return true;
            });
            taskHelper.DelayNext(50);
        }

        taskHelper.Enqueue(() =>
        {
            itemPreviewService.PreviewGlasses(preset.GlassesID);
            return true;
        });

        taskHelper.DelayNext(100);
        taskHelper.Enqueue(() =>
        {
            itemPreviewService.PreviewHairAppearance(preset.HairstyleID, preset.HairColor, preset.HighlightColor, preset.HighlightsEnabled);

            return true;
        });
    }

    private void ClearTryOn()
    {
        taskHelper?.Abort();
        activeTryOnPreset = null;
        boundTryOnPreset = null;
        previewPlayerAddress = nint.Zero;
        pendingTryOnRestore = false;
        managerNativeUI?.UpdateTryOnButton();
        itemPreviewService?.ClearPreviewResidue();
    }

    public void OpenManager()
    {
        NormalizeSelection();
        managerNativeUI?.UpdateData(config.Presets, selectedPresetIndex);
        if (managerNativeUI is { IsOpen: false })
        {
            AtkUnitBase* existing = managerNativeUI;
            if (existing == null) managerNativeUI.Open();
            else existing->Show(true, 0); // NativeAddon.Open only allocates; hidden allocated windows need Show.
        }
        if (selectedPresetIndex >= 0 && selectedPresetIndex < config.Presets.Count)
            QueuePortraitPreview(config.Presets[selectedPresetIndex]);
        else
        {
            managerNativeUI?.ClearPreviewTexture();
            portraitPreview?.CancelPreview();
        }
    }

    private void SelectPreset(BetterGlamourPreset preset)
    {
        selectedPresetIndex = config.Presets.IndexOf(preset);
        QueuePortraitPreview(preset);
    }

    private void PresetChanged(BetterGlamourPreset preset)
    {
        saveConfig();
        plateExportNativeUI?.RefreshState();
        QueuePortraitPreview(preset);
    }

    private void QueuePortraitPreview(BetterGlamourPreset preset)
    {
        Volatile.Write(ref phoneSelectedID, string.Empty);
        managerNativeUI?.ClearPreviewTexture();
        portraitPreview?.QueuePreset(preset);
        Volatile.Write(ref phoneSelectedID, selectedPresetIndex >= 0 && selectedPresetIndex < config.Presets.Count
            ? config.Presets[selectedPresetIndex].ID
            : string.Empty);
    }

    private void ChangePresetGearset(BetterGlamourPreset preset, int gearsetIndex)
    {
        var gearsetIndices = preset.GetGearsetIndices(DService.Instance().PlayerState.ContentId).ToList();
        if (gearsetIndex < 0)
            gearsetIndices.Clear();
        else if (!gearsetIndices.Remove(gearsetIndex))
            gearsetIndices.Add(gearsetIndex);
        ChangePresetGearsets(preset, gearsetIndices);
    }

    private void ChangePresetGearsets(BetterGlamourPreset preset, List<int> gearsetIndices)
    {
        var contentID = DService.Instance().PlayerState.ContentId;
        if (!DService.Instance().ClientState.IsLoggedIn || contentID == 0)
            throw new InvalidOperationException("请登录角色后设置职业套装绑定。");
        if (gearsetIndices.Any(index => index is < 0 or >= 100 || !TryGetGearsetName(index, out _)))
            throw new InvalidOperationException("职业套装无效。");

        var previousBindings = new Dictionary<BetterGlamourPreset, List<int>?>();
        var previousGearsetIndex = preset.GearsetIndex;
        foreach (var other in config.Presets)
        {
            other.GearsetBindings.TryGetValue(contentID, out var previousIndices);
            if (ReferenceEquals(other, preset))
            {
                previousBindings.Add(other, previousIndices);
                if (gearsetIndices.Count == 0)
                    other.GearsetBindings.Remove(contentID);
                else
                    other.GearsetBindings[contentID] = gearsetIndices;
            }
            else if (previousIndices is not null && previousIndices.Any(gearsetIndices.Contains))
            {
                previousBindings.Add(other, previousIndices);
                var remainingIndices = previousIndices.Except(gearsetIndices).ToList();
                if (remainingIndices.Count == 0)
                    other.GearsetBindings.Remove(contentID);
                else
                    other.GearsetBindings[contentID] = remainingIndices;
            }
        }

        preset.GearsetIndex = -1;
        try
        {
            saveConfig();
        }
        catch
        {
            preset.GearsetIndex = previousGearsetIndex;
            foreach (var (other, previousIndices) in previousBindings)
            {
                if (previousIndices is null)
                    other.GearsetBindings.Remove(contentID);
                else
                    other.GearsetBindings[contentID] = previousIndices;
            }
            throw;
        }

        var gearsetModule = RaptureGearsetModule.Instance();
        if (gearsetModule != null)
        {
            var currentGearsetIndex = gearsetModule->CurrentGearsetIndex;
            if (previousBindings[preset]?.Contains(currentGearsetIndex) == true || gearsetIndices.Contains(currentGearsetIndex))
            {
                lastGearsetIndex = currentGearsetIndex;
                SynchronizeBoundTryOn(currentGearsetIndex);
            }
        }
    }

    private void SynchronizeBoundTryOn(int gearsetIndex)
    {
        var contentID = DService.Instance().PlayerState.ContentId;
        boundTryOnPreset = contentID == 0 ? null : config.Presets.Find(item => item.GetGearsetIndices(contentID).Contains(gearsetIndex));
        if (boundTryOnPreset is not null)
        {
            if (!ReferenceEquals(activeTryOnPreset, boundTryOnPreset))
            {
                StartTryOn(boundTryOnPreset);
            }

            return;
        }

        if (activeTryOnPreset is not null)
        {
            ClearTryOn();
        }
    }

    private void DeletePreset(BetterGlamourPreset preset)
    {
        var index = config.Presets.IndexOf(preset);
        if (index < 0)
        {
            return;
        }

        config.Presets.RemoveAt(index);
        if (ReferenceEquals(activeTryOnPreset, preset))
        {
            ClearTryOn();
        }

        if (ReferenceEquals(boundTryOnPreset, preset))
        {
            boundTryOnPreset = null;
        }

        selectedPresetIndex = Math.Min(index, config.Presets.Count - 1);
        saveConfig();
        managerNativeUI?.UpdateData(config.Presets, selectedPresetIndex);
        if (selectedPresetIndex >= 0)
            QueuePortraitPreview(config.Presets[selectedPresetIndex]);
        else
        {
            managerNativeUI?.ClearPreviewTexture();
            portraitPreview?.CancelPreview();
        }
    }

    private void NotifyNoInspectData() => OmniNotifier.Popup(
        Title,
        OmniLoc.Get(this, "Feature.BetterGlamourManagement.NoInspectData"),
        NotificationType.Warning);

    private void DisposeActionsNativeUI()
    {
        var ui = actionsNativeUI;
        actionsNativeUI = null;
        if (ui is null)
        {
            return;
        }

        ui.Close();
        ui.Dispose();
    }

    private void DisposeManagerNativeUI()
    {
        var ui = managerNativeUI;
        managerNativeUI = null;
        if (ui is null)
        {
            return;
        }

        ui.Close();
        ui.Dispose();
    }

    #region 常量

    private const string CHARACTER_INSPECT_ADDON_NAME = "CharacterInspect";

    private const float SCREEN_PADDING = 8f;

    internal const int MAX_PRESET_COUNT = 100;

    #endregion

    public sealed class Settings
    {
        public List<BetterGlamourPreset> Presets { get; set; } = [];

        public bool EnableGlamourPlateExport { get; set; } = true;
    }
}
