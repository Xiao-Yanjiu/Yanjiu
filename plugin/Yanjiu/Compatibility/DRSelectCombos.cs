// SPDX-License-Identifier: GPL-3.0-or-later
using Dalamud.Bindings.ImGui;
namespace DailyRoutines.Extensions;
public static class YanjiuSelectCombos
{
    extension(OmenTools.ImGuiOm.ImGuiOm)
    {
        public static bool SingleSelectCombo<T>(string id,Dictionary<uint,T> source,ref uint selected,ref string search,Func<T,string> preview,(string,ImGuiTableColumnFlags,float)[] columns,Func<T,Action>[] renderers,Func<T,string>[] searchFields,bool showHeaders=true)
        {
            uint before=selected;
            if(ImGui.BeginCombo(id,source.TryGetValue(selected,out var value)?preview(value):"请选择")){
                DrawChoices(source,ref search,columns,renderers,searchFields,showHeaders);
                ImGui.EndCombo();
            }
            return before!=selected;
        }
        public static bool MultiSelectCombo<T>(string id,Dictionary<uint,T> source,ref HashSet<uint> selected,ref string search,(string,ImGuiTableColumnFlags,float)[] columns,Func<T,Action>[] renderers,Func<T,string>[] searchFields,bool showHeaders=true)
        {
            var before=selected.ToHashSet();
            if(ImGui.BeginCombo(id,$"已选择 {selected.Count} 项")){
                DrawChoices(source,ref search,columns,renderers,searchFields,showHeaders);
                ImGui.EndCombo();
            }
            return !before.SetEquals(selected);
        }
    }
    private static void DrawChoices<T>(Dictionary<uint,T> source,ref string search,(string Name,ImGuiTableColumnFlags Flags,float Width)[] columns,Func<T,Action>[] renderers,Func<T,string>[] searchFields,bool showHeaders)
    {
        ImGui.SetNextItemWidth(-1);ImGui.InputTextWithHint("##filter","搜索",ref search,256);
        if(columns.Length==0||columns.Length!=renderers.Length)throw new ArgumentException("选择表格列与渲染器数量不一致");
        if(!ImGui.BeginTable("##choices",columns.Length,ImGuiTableFlags.ScrollY|ImGuiTableFlags.RowBg,new(0,320)))return;
        try{
            foreach(var column in columns)ImGui.TableSetupColumn(column.Name,column.Flags,column.Width);
            if(showHeaders)ImGui.TableHeadersRow();
            var filter=search;foreach(var pair in source){
                if(filter.Length>0&&!searchFields.Any(field=>field(pair.Value).Contains(filter,StringComparison.OrdinalIgnoreCase)))continue;
                ImGui.PushID(unchecked((int)pair.Key));
                try{ImGui.TableNextRow();foreach(var renderer in renderers){ImGui.TableNextColumn();renderer(pair.Value)();}}
                finally{ImGui.PopID();}
            }
        }finally{ImGui.EndTable();}
    }
}

