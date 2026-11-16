namespace OmniToolbox.TreePublic;
public sealed partial class BetterUserMacro { public override bool TryHandleCommand(string arguments) {
        if (!IsEnabled) return false;
        var parts = arguments.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1 && parts[0].Equals("icons", StringComparison.OrdinalIgnoreCase)) {
            OpenIconBrowser(id => { TrySetSelectedMacroIcon(id); }); return true;
        }
        return parts.Length == 2 && parts[0].Equals("icon", StringComparison.OrdinalIgnoreCase) && uint.TryParse(parts[1], out var value) && value != 0;
    }
    private string LocalizationText(string key) => OmniLoc.Get(this, key); }