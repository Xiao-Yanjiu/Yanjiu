using System.Linq;
using System.Runtime.InteropServices;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface;
using OmniToolbox.Host;
using TreeTools.Lifecycle;
using OmniToolbox.UI;
using OmniToolbox.UI.Controls;
using OmniToolbox.UI.Theme;
using OmenTools;
using OmenTools.OmenService;

namespace OmniToolbox.TreePublic;

public sealed partial class ToolbarRenderer
{
    private const string HiddenWindowDtrTitle = "Yanjiu.OmniModule.Toolbar.HiddenWindows";
    private bool hiddenWindowManagerOpen;
    private bool pickingHiddenWindow;
    private string hiddenWindowFilter = string.Empty;
    private long nextHiddenWindowRefresh;
    private readonly HashSet<string> revealedWindows = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> appliedHiddenWindows = new(StringComparer.OrdinalIgnoreCase);

    private void RegisterHiddenWindowEntry(FeatureLifetime lifetime)
    {
        var entry = DService.Instance().DTRBar.Get(HiddenWindowDtrTitle);
        entry.Text = new SeStringBuilder().AddText(ModuleText.Get("Feature.MultiToolbar.HiddenWindows")).Build();
        entry.OnClick = click =>
        {
            if (click.ClickType == MouseClickType.Left)
            {
                hiddenWindowManagerOpen = !hiddenWindowManagerOpen;
            }
        };
        entry.Shown = true;
        lifetime.Add(() => DService.Instance().DTRBar.Remove(HiddenWindowDtrTitle));
    }

    private void DrawHiddenWindowManager()
    {
        DrawHiddenWindowPicker();
        if(Environment.TickCount64>=nextHiddenWindowRefresh){ApplyHiddenWindows();nextHiddenWindowRefresh=Environment.TickCount64+250;}
    }

    private unsafe void DrawHiddenWindowPicker()
    {
        if (!pickingHiddenWindow)
        {
            return;
        }
        if (ImGui.IsKeyPressed(ImGuiKey.Escape) || ImGui.IsMouseClicked(ImGuiMouseButton.Right))
        {
            pickingHiddenWindow = false;
            return;
        }
        var context = ImGuiNative.GetCurrentContext();
        if (context == null)
        {
            return;
        }
        var window = new ImGuiContextPtr(context).HoveredWindow;
        while (window.Handle != null && (window.Flags & ImGuiWindowFlags.ChildWindow) != 0 && window.ParentWindow.Handle != null)
        {
            window = window.ParentWindow;
        }
        if (window.Handle == null || window.Name == null ||
            (window.Flags & (ImGuiWindowFlags.ChildWindow | ImGuiWindowFlags.Popup | ImGuiWindowFlags.Tooltip)) != 0)
        {
            return;
        }
        var name = NormalizeHiddenWindowName(Marshal.PtrToStringUTF8((nint)window.Name));
        if (!CanManageHiddenWindow(name))
        {
            return;
        }
        ImGui.GetForegroundDrawList().AddRect(window.Pos, window.Pos + window.Size,
            ImGui.GetColorU32(OmniTheme.ControlAccent), 0f, ImDrawFlags.None, OmniTheme.Scale(2f));
        hiddenPickName=name;
        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            config.HiddenWindows.Add(name);
            nextHiddenWindowRefresh = 0;
            revealedWindows.Remove(name);
            pickingHiddenWindow = false;
            saveConfig();
        }
    }

    private static string NormalizeHiddenWindowName(string? value)
    {
        var name = (value ?? string.Empty).Trim();
        var separator = name.IndexOf("###", StringComparison.Ordinal);
        return separator >= 0 ? name[separator..] : name;
    }

    private static bool CanManageHiddenWindow(string name) => !string.IsNullOrWhiteSpace(name) &&
        !name.Contains("Omni", StringComparison.OrdinalIgnoreCase) &&
        !name.StartsWith("##multiToolbar", StringComparison.OrdinalIgnoreCase) &&
        !name.StartsWith("##multiDock", StringComparison.OrdinalIgnoreCase) &&
        !name.Equals("Debug##Default", StringComparison.Ordinal);

    private unsafe void ApplyHiddenWindows(bool restore = false)
    {
        if (config.HiddenWindows.Count == 0 && appliedHiddenWindows.Count == 0)
        {
            return;
        }
        var context = ImGuiNative.GetCurrentContext();
        if (context == null)
        {
            return;
        }
        ref var windows = ref new ImGuiContextPtr(context).Windows;
        for (var index = 0; index < windows.Size; index++)
        {
            var window = windows[index];
            if (window.Handle == null || window.Name == null)
            {
                continue;
            }
            var root = window;
            while ((root.Flags & ImGuiWindowFlags.ChildWindow) != 0 && root.ParentWindow.Handle != null)
            {
                root = root.ParentWindow;
            }
            var name = NormalizeHiddenWindowName(Marshal.PtrToStringUTF8((nint)root.Name));
            var hidden = !restore && config.HiddenWindows.Contains(name) && !revealedWindows.Contains(name) && CanManageHiddenWindow(name);
            if (!hidden && !appliedHiddenWindows.Contains(name))
            {
                continue;
            }
            // 仅隐藏窗口，保留第三方窗口的 IsOpen 状态。
            window.Hidden = hidden;
            window.SkipItems = hidden;
            window.HiddenFramesCanSkipItems = hidden ? sbyte.MaxValue : (sbyte)0;
            window.HiddenFramesCannotSkipItems = hidden ? sbyte.MaxValue : (sbyte)0;
            window.HiddenFramesForRenderOnly = hidden ? sbyte.MaxValue : (sbyte)0;
            window.DisableInputsFrames = hidden ? sbyte.MaxValue : (sbyte)0;
            if (hidden)
            {
                ref var drawList = ref window.DrawList;
                drawList.CmdBuffer.Clear();
                drawList.IdxBuffer.Clear();
                drawList.VtxBuffer.Clear();
            }
        }
        appliedHiddenWindows.Clear();
        if (!restore)
        {
            appliedHiddenWindows.UnionWith(config.HiddenWindows.Where(name => !revealedWindows.Contains(name) && CanManageHiddenWindow(name)));
        }
    }

    private void RestoreHiddenWindows()
    {
        ApplyHiddenWindows(true);
        nextHiddenWindowRefresh = 0;
        revealedWindows.Clear();
        hiddenWindowManagerOpen = false;
        pickingHiddenWindow = false;
    }
}
