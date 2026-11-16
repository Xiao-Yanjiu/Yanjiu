using Dalamud.Interface;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Client.UI.Shell;
using OmenTools;
using OmniToolbox.UI.Controls;
namespace OmniToolbox.TreePublic;
public sealed partial class ToolbarRenderer
{
    private uint quickCommandCategory = 1;
    private static string AdditionalWidgetLabel(MultiToolbarWidgetType type) => type == MultiToolbarWidgetType.QuickCommands ? "快捷指令" : string.Empty;
    private static string? AdditionalWidgetDynamicLabel(MultiToolbarWidgetType type) => type == MultiToolbarWidgetType.QuickCommands ? "快捷指令" : null;
    private static FontAwesomeIcon? AdditionalWidgetIcon(MultiToolbarWidgetType type) => null;
    private bool TryHandleAdditionalWidgetInteraction(MultiToolbarWidgetConfig widget)
    {
        if (widget.Type != MultiToolbarWidgetType.QuickCommands) return false;
        if(ImGui.IsItemHovered()){
            int index=QuickCategoryIndex(ImGui.GetMousePos().X,ImGui.GetItemRectMin().X,ImGui.GetItemRectSize().X);
            uint category=(uint)(index+1);
            bool open=activePopup==PopupKind.Widget&&activeWidgetPopupType==MultiToolbarWidgetType.QuickCommands;
            if(ImGui.IsItemClicked()&&open&&category==quickCommandCategory){ClosePopup();return true;}
            if(ImGui.IsItemClicked()||open&&category!=quickCommandCategory){
                quickCommandCategory=category;
                var anchor=CalculatePopupPosition(PopupKind.Widget);
                anchor.X=ImGui.GetItemRectMin().X+index*ImGui.GetItemRectSize().X/7;
                if(!open){if(ImGui.IsItemClicked()){ToggleWidgetPopup(widget.Type);popupPosition=anchor;}}
                else runtimePopup?.QueueQuickCommandCategory(category,anchor);
            }
            OmniControls.HelpTooltip(DService.Instance().Data.GetExcelSheet<Lumina.Excel.Sheets.MainCommandCategory>().GetRow(category).Name.ToString());
        }
        return true;
    }
    private static int QuickCategoryIndex(float cursorX,float left,float width)=>width>0?Math.Clamp((int)((cursorX-left)/(width/7)),0,6):0;
    private bool DrawAdditionalWidgetPopup(MultiToolbarWidgetType type)
    {
        if (type != MultiToolbarWidgetType.QuickCommands) return false;
        DrawQuickCommands(); return true;
    }
    private unsafe void DrawQuickCommands()
    {
        var commands = DalamudServices.DataManager.GetExcelSheet<Lumina.Excel.Sheets.MainCommand>()
            .Where(c => c.RowId != 0 && c.MainCommandCategory.RowId != 0 && !string.IsNullOrWhiteSpace(c.Name.ToString())).ToArray();
        if (commands.Length == 0) { ImGui.TextDisabled("当前无法读取游戏主菜单数据。"); return; }
        var categories = new uint[] { 1, 2, 3, 4, 5, 6, 7 };
        if (!categories.Contains(quickCommandCategory)) quickCommandCategory = categories[0];
        var categorySheet = DalamudServices.DataManager.GetExcelSheet<Lumina.Excel.Sheets.MainCommandCategory>();
        using var columns = ImRaii.Table("##quickCommandColumns", 2, ImGuiTableFlags.SizingStretchProp);
        if (!columns) return;
        ImGui.TableSetupColumn("分类", ImGuiTableColumnFlags.WidthFixed, 115f);
        ImGui.TableSetupColumn("菜单", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableNextColumn();
        foreach (var id in categories)
        {
            var category = categorySheet.GetRow(id).Name.ToString();
            if (ImGui.Selectable(category + "##category" + id, quickCommandCategory == id)) quickCommandCategory = id;
        }
        ImGui.TableNextColumn();
        using var child = ImRaii.Child("##gameCommands", new Vector2(0, 280));
        if (!child) return;
        var ui = FFXIVClientStructs.FFXIV.Client.UI.UIModule.Instance();
        foreach (var command in commands.Where(c => c.MainCommandCategory.RowId == quickCommandCategory).OrderBy(c => c.SortID))
        {
            var name = command.Name.ToString();
            using var disabled = ImRaii.Disabled(ui == null || !DService.Instance().ClientState.IsLoggedIn);
            if (ImGui.Selectable(name + "##mainCommand" + command.RowId))
            {
                ui->ExecuteMainCommand(command.RowId);
                ClosePopup();
                return;
            }
        }
    }
}
