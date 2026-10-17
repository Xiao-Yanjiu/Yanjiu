// SPDX-License-Identifier: GPL-3.0-or-later
using System.Numerics;
using System.Reflection;
using System.Text.Json.Nodes;
using Dalamud.Bindings.ImGui;
using Yanjiu.Modules;
namespace Yanjiu.UI;
public sealed class DisabledSettingsEditor
{
    private readonly Dictionary<string,(ModuleBase Owner,DisabledModuleSettings Draft)> drafts=new(StringComparer.OrdinalIgnoreCase);
    private string status="";
    public void Forget(ModuleEntry entry)=>drafts.Remove(entry.Path);
    public void Draw(ModuleEntry entry)
    {
        if(entry.Instance is not DailyRoutines.Common.Module.Abstractions.ModuleBase owner){ImGui.TextDisabled("此模块没有独立保存的设置");return;}
        if(DisabledModuleSettings.FindConfigType(owner.GetType())==null){ImGui.TextDisabled("此模块没有独立保存的设置");return;}
        if(!drafts.TryGetValue(entry.Path,out var state)||!ReferenceEquals(state.Owner,owner)){
            state=(owner,new DisabledModuleSettings(owner));drafts[entry.Path]=state;status="";
        }
        var draft=state.Draft;
        if(ThemeWidgets.Button("保存设置")){
            try{draft.Save();status="设置已保存";}catch(Exception e){status="保存失败："+e.GetBaseException().Message;}
        }
        ImGui.SameLine();
        if(ThemeWidgets.Button("重新读取")){drafts.Remove(entry.Path);status="";return;}
        if(draft.Dirty){ImGui.SameLine();ImGui.TextDisabled("有未保存的修改");}
        if(status.Length>0)ImGui.TextWrapped(status);
        DrawObject(draft.Values,draft.ModelType,entry.Catalog?.SettingLabels??new Dictionary<string,string>(),0,ref draft);
    }
    private static Type? MemberType(Type? type,string name)
    {
        if(type==null)return null;
        return type.GetField(name,BindingFlags.Public|BindingFlags.Instance)?.FieldType??type.GetProperty(name,BindingFlags.Public|BindingFlags.Instance)?.PropertyType;
    }
    private static void DrawObject(JsonObject obj,Type? type,IReadOnlyDictionary<string,string> labels,int depth,ref DisabledModuleSettings draft)
    {
        if(depth>8){ImGui.TextDisabled("嵌套层级过深");return;}
        foreach(var pair in obj.ToArray()){
            ImGui.PushID(pair.Key);
            var label=labels.TryGetValue(pair.Key,out var translated)?translated:pair.Key;
            // Strip runtime counters and hidden ImGui suffixes from source labels.
            label=System.Text.RegularExpressions.Regex.Replace(label.Split("##")[0],@"\{[^{}]*\}","");
            DrawValue(label,pair.Value,MemberType(type,pair.Key),node=>obj[pair.Key]=node,depth,labels,ref draft,pair.Key);
            ImGui.PopID();
        }
    }
    private static JsonNode? NewValue(Type? type)
    {
        if(type==null||type==typeof(string))return JsonValue.Create("");
        type=Nullable.GetUnderlyingType(type)??type;
        if(type.IsEnum)return JsonValue.Create(Convert.ToInt64(Enum.GetValues(type).GetValue(0)));
        if(type==typeof(bool))return JsonValue.Create(false);
        if(type.IsPrimitive||type==typeof(decimal))return JsonValue.Create(0);
        if(type.IsArray||type.GetInterfaces().Any(i=>i.IsGenericType&&i.GetGenericTypeDefinition()==typeof(IEnumerable<>)))return new JsonArray();
        try{return System.Text.Json.JsonSerializer.SerializeToNode(Activator.CreateInstance(type,true),type,new System.Text.Json.JsonSerializerOptions(){IncludeFields=true});}catch{return new JsonObject();}
    }
    private static void DrawValue(string label,JsonNode? node,Type? type,Action<JsonNode?> assign,int depth,IReadOnlyDictionary<string,string> labels,ref DisabledModuleSettings draft,string key)
    {
        if(depth>8){ImGui.TextDisabled("嵌套层级过深");return;}
        type=type==null?null:Nullable.GetUnderlyingType(type)??type;
        if(node is JsonObject child){
            if(ImGui.TreeNode(label)){
                // String-keyed dictionaries share their value type, ordinary objects use member types.
                var dictionary=type?.GetInterfaces().Append(type).FirstOrDefault(t=>t.IsGenericType&&t.GetGenericTypeDefinition()==typeof(IDictionary<,>));
                if(dictionary!=null){
                    var valueType=dictionary.GetGenericArguments()[1];
                    foreach(var item in child.ToArray()){
                        ImGui.PushID(item.Key);
                        if(ImGui.SmallButton("移除")){child.Remove(item.Key);draft.Dirty=true;ImGui.PopID();continue;}
                        ImGui.SameLine();DrawValue(item.Key,item.Value,valueType,v=>child[item.Key]=v,depth+1,labels,ref draft,item.Key);
                        ImGui.PopID();
                    }
                    string pending=ImGui.GetStateStorage().GetInt(ImGui.GetID("newCount"),0).ToString();
                    // Keys are chosen by the user, never replace an existing mapping.
                    var keyName=NewKey(child);
                    if(ImGui.SmallButton("添加条目")){child[keyName]=NewValue(valueType);draft.Dirty=true;}
                }else DrawObject(child,type,labels,depth+1,ref draft);
                ImGui.TreePop();
            }
            return;
        }
        if(node is JsonArray list){
            if(ImGui.TreeNode(label+"（"+list.Count+"）")){
                var itemType=type?.IsArray==true?type.GetElementType():type?.GetGenericArguments().FirstOrDefault();
                for(int i=0;i<Math.Min(list.Count,500);i++){
                    int index=i;ImGui.PushID(i);
                    if(ImGui.SmallButton("移除")){list.RemoveAt(i);draft.Dirty=true;ImGui.PopID();break;}
                    ImGui.SameLine();DrawValue((i+1).ToString(),list[i],itemType,v=>list[index]=v,depth+1,labels,ref draft,key);
                    ImGui.PopID();
                }
                if(list.Count>500)ImGui.TextDisabled("仅显示前500项");
                if(ImGui.SmallButton("添加")){list.Add(NewValue(itemType));draft.Dirty=true;}
                ImGui.TreePop();
            }
            return;
        }
        if(node==null){if(ImGui.SmallButton(label+"：添加")){assign(NewValue(type));draft.Dirty=true;}return;}
        if(node is not JsonValue value)return;
        if(type?.IsEnum==true){
            var names=Enum.GetNames(type);var values=Enum.GetValues(type);
            string raw=value.ToJsonString();int selected=Array.FindIndex(names,name=>Convert.ToInt64(Enum.Parse(type,name)).ToString()==raw);
            ImGui.SetNextItemWidth(Math.Min(350*Themes.Scale,ImGui.GetContentRegionAvail().X*.65f));
            if(ImGui.Combo(label,ref selected,names,names.Length)&&selected>=0){assign(JsonValue.Create(Convert.ToInt64(values.GetValue(selected))));draft.Dirty=true;}
            return;
        }
        if(value.TryGetValue<bool>(out var flag)){if(ImGui.Checkbox(label,ref flag)){assign(JsonValue.Create(flag));draft.Dirty=true;}return;}
        bool isText=value.TryGetValue<string>(out var text);
        text=isText?text!:value.ToJsonString();
        uint inputId=ImGui.GetID(label);
        if(!isText&&draft.Inputs.TryGetValue(inputId,out var editing))text=editing;
        var secret=key.Contains("password",StringComparison.OrdinalIgnoreCase)||key.Contains("token",StringComparison.OrdinalIgnoreCase)||key.Contains("apikey",StringComparison.OrdinalIgnoreCase);
        ImGui.SetNextItemWidth(Math.Min(430*Themes.Scale,ImGui.GetContentRegionAvail().X*.65f));
        if(ImGui.InputText(label,ref text,65536,secret?ImGuiInputTextFlags.Password:ImGuiInputTextFlags.None)){
            if(isText){assign(JsonValue.Create(text));draft.Dirty=true;}
            else{
                draft.Inputs[inputId]=text;
                try{
                    var parsed=JsonNode.Parse(text);
                    if(parsed is not JsonValue numeric||numeric.GetValueKind()!=System.Text.Json.JsonValueKind.Number)throw new System.Text.Json.JsonException();
                    assign(parsed);draft.Dirty=true;draft.InvalidInputs.Remove(inputId);
                }catch(System.Text.Json.JsonException){draft.InvalidInputs.Add(inputId);}
            }
        }
    }
    private static string NewKey(JsonObject obj){for(int i=1;i<10000;i++)if(!obj.ContainsKey(i.ToString()))return i.ToString();throw new InvalidOperationException("条目过多");}
}



