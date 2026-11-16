// SPDX-License-Identifier: GPL-3.0-or-later
using System.Numerics;
using System.Text.Json.Nodes;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.BaseTypes;
using KamiToolKit.Nodes;
using Yanjiu.Modules;
namespace Yanjiu.UI;
internal sealed unsafe class NativeSettingsWindow : NativeAddon
{
    private readonly ModuleEntry entry;
    private readonly ModuleManager manager;
    private readonly JsonObject values;
    private readonly Action<JsonObject> save;
    private readonly List<(TextNode Label,TextInputNode Input,CheckboxNode Toggle)> rows=[];
    private readonly ModuleBase owner;
    private TextNode? status;
    private NativeConfigField[] fields=[];
    private int page;
    private bool refreshing;
    private readonly HashSet<string> invalid=[];
    [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
    internal NativeSettingsWindow(ModuleEntry module,ModuleManager modules)
    {
        entry=module;owner=module.Instance??throw new InvalidOperationException("模块尚未加载。");manager=modules;InternalName="YanjiuNativeSettings";Title="模块设置";Size=new(850,580);
        if(module.Instance is Api1ModuleAdapter api){values=api.NativeSettingsSnapshot();save=api.SaveNativeSettings;}
        else if(module.Instance is DailyRoutines.Common.Module.Abstractions.ModuleBase dr){
            var draft=new DisabledModuleSettings(dr);values=draft.Values;save=_=>draft.Save();
        }else throw new InvalidOperationException("此模块尚未提供可供原生编辑器读取的配置。");
        fields=NativeConfigFields.Flatten(values);
    }
    protected override void OnSetup(AtkUnitBase* addon,Span<AtkValue> arguments)
    {
        rows.Clear();
        new TextNode{Position=ContentStartPosition,Size=new(780,28),String=entry.Title,FontSize=16}.AttachNode(addon);
        for(int i=0;i<9;i++){
            int index=i;var pos=ContentStartPosition+new Vector2(0,42+i*43);
            var label=new TextNode{Position=pos,Size=new(270,24),FontSize=13};label.AttachNode(addon);
            var input=new TextInputNode{Position=pos+new Vector2(280,0),Size=new(490,28)};
            input.OnInputReceived=text=>{
                if(refreshing)return;
                int position=page*9+index;if(position>=fields.Length)return;
                var field=fields[position];var key=field.Path;
                try{
                    var original=field.Read();
                    field.Write(original is JsonValue scalar&&scalar.TryGetValue<string>(out _)?JsonValue.Create(text.ToString()):JsonNode.Parse(text.ToString(),documentOptions:new(){MaxDepth=64}));
                    invalid.Remove(key);
                }
                catch{invalid.Add(key);}
            };input.AttachNode(addon);
            var toggle=new CheckboxNode{Position=pos+new Vector2(280,0),Size=new(490,24),String="启用",OnClick=enabled=>{if(!refreshing&&page*9+index<fields.Length)fields[page*9+index].Write(JsonValue.Create(enabled));}};
            toggle.AttachNode(addon);rows.Add((label,input,toggle));
        }
        AddButton(addon,"上一页",0,()=>{if(invalid.Count>0){status!.String="请先修正当前输入。";return;}page=Math.Max(0,page-1);Refresh();});
        AddButton(addon,"下一页",130,()=>{if(invalid.Count>0){status!.String="请先修正当前输入。";return;}page=Math.Min(Math.Max(0,(fields.Length-1)/9),page+1);Refresh();});
        AddButton(addon,"停用模块",260,()=>manager.Enable(entry,false));
        AddButton(addon,"保存配置",390,()=>{
            try{
                if(!manager.Entries.Contains(entry)||!ReferenceEquals(entry.Instance,owner)||entry.Transitioning||entry.Instance?.IsEnabled==true)throw new InvalidOperationException("请先停用模块，并等待停用完成。");
                if(invalid.Count>0)throw new InvalidOperationException("存在无效输入。");
                save(values);status!.String="配置已保存。重新启用模块后应用。";
            }catch(Exception e){status!.String=e.GetBaseException().Message;}
        });
        status=new TextNode{Position=ContentStartPosition+new Vector2(0,480),Size=new(780,50),FontSize=12,String="开关可直接勾选，文字和数字直接填写。嵌套配置和已有数组元素已展开。空集合与空值使用 JSON。停用模块后保存。"};status.AttachNode(addon);
        Refresh();
    }
    private void AddButton(AtkUnitBase* addon,string text,int x,Action action)
    {
        new TextButtonNode{Position=ContentStartPosition+new Vector2(x,440),Size=new(120,28),String=text,OnClick=action}.AttachNode(addon);
    }
    private void Refresh()
    {
        refreshing=true;
        try{for(int i=0;i<rows.Count;i++){
            int index=page*9+i;var row=rows[i];bool visible=index<fields.Length;
            row.Label.IsVisible=visible;row.Input.IsVisible=visible;row.Toggle.IsVisible=false;
            if(visible){var field=fields[index];var key=field.Path;row.Label.String=entry.Catalog?.SettingLabels.GetValueOrDefault(key)??key;var value=field.Read();
                if(value is JsonValue primitive&&primitive.TryGetValue<bool>(out var enabled)){row.Input.IsVisible=false;row.Toggle.IsVisible=true;row.Toggle.IsChecked=enabled;}
                else row.Input.String=value is JsonValue text&&text.TryGetValue<string>(out var content)?content:value?.ToJsonString()??"null";}
        }}finally{refreshing=false;}
    }
}
