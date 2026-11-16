namespace OmniToolbox.TreePublic;
public sealed partial class ToolbarRenderer
{
    private static void ExecuteOptionalCommand(string command, string feature)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            OmniToolbox.Notifications.OmniNotifier.Popup("多功能工具栏",
                $"请在当前工具栏设置中填写{feature}的命令。", Dalamud.Interface.ImGuiNotification.NotificationType.Info);
            return;
        }
        ExecuteCommand(command);
    }
}
