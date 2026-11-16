// SPDX-License-Identifier: GPL-3.0-or-later
using System.Globalization;
using System.Numerics;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.Addons;
using KamiToolKit.BaseTypes;
using KamiToolKit.Nodes;
using Yanjiu.Modules;
using Yanjiu.NativeUI;
namespace Yanjiu.UI;

internal sealed unsafe class NativePanelWindow : NativeAddon
{
    private static long nextIdentifier;
    private readonly NativePanel panel;
    private readonly ModuleEntry entry;
    private readonly ModuleBase owner;
    private readonly ModuleManager modules;
    private readonly List<Action<bool>> refreshers=[];
    private TextNode? status;
    private ColorPickerAddon? colorPicker;
    private bool pickingColor;
    private bool binding;
    private bool stale;
    private bool disposed;
    private long nextRefresh;
    internal bool Finalized {get;private set;}
    private readonly HashSet<int> invalid=[];
    private readonly HashSet<int> editing=[];

    [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
    internal NativePanelWindow(ModuleEntry module,ModuleManager manager,NativePanel description)
    {
        description.Validate();
        owner=module.Instance??throw new InvalidOperationException("模块尚未加载。");
        entry=module;modules=manager;panel=description with{Controls=description.Controls.ToArray()};
        InternalName="YanjiuNative_"+Interlocked.Increment(ref nextIdentifier);Title=description.Title;Size=new Vector2(860,560);
    }
    protected override void OnSetup(AtkUnitBase* addon,Span<AtkValue> arguments)
    {
        Finalized=false;refreshers.Clear();editing.Clear();invalid.Clear();
        var scroll=new ScrollingNode<ResNode>{Position=ContentStartPosition,Size=new(810,430),ScrollSpeed=42};scroll.AttachNode(addon);
        for(int i=0;i<panel.Controls.Count;i++){
            int index=i;var control=panel.Controls[i];var position=new Vector2(0,i*44);
            var label=new TextNode{Position=position,Size=new(300,32),FontSize=14,String=control.Label};label.AttachNode(scroll.ContentNode);
            var fieldPosition=position+new Vector2(310,0);var fieldSize=new Vector2(480,28);
            bool Enabled(bool available)=>available&&!pickingColor&&control.Enabled?.Invoke()!=false;
            switch(control){
                case NativeLabel text:
                    label.Width=790;refreshers.Add(available=>{if(available)label.String=text.Value?.Invoke()??text.Label;});break;
                case NativeToggle toggle:
                    var check=new CheckboxNode{Position=fieldPosition,Size=fieldSize,OnClick=value=>Apply(index,()=>toggle.Write(value))};check.AttachNode(scroll.ContentNode);
                    refreshers.Add(available=>{check.IsEnabled=Enabled(available);if(available)check.IsChecked=toggle.Read();});break;
                case NativeButton button:
                    var node=new TextButtonNode{Position=fieldPosition,Size=fieldSize,String=button.Label,OnClick=()=>Apply(index,button.Click)};node.AttachNode(scroll.ContentNode);
                    refreshers.Add(available=>node.IsEnabled=Enabled(available));break;
                case NativeSelect select:
                    var dropdown=new DropDownNode<NativeOption>{Position=fieldPosition,Size=fieldSize,GetLabelFunction=option=>option.Label,
                        OnOptionSelected=option=>Apply(index,()=>{
                            if(!select.Options().Any(current=>current.Id==option.Id))throw new InvalidOperationException("条目已变化，请重新选择。");
                            select.Write(option.Id);
                        })};dropdown.AttachNode(scroll.ContentNode);
                    refreshers.Add(available=>{
                        if(!Enabled(available))dropdown.Collapse(false);
                        dropdown.IsEnabled=Enabled(available);if(!available)return;
                        var options=select.Options().ToList();
                        if(!dropdown.Options.SequenceEqual(options)){dropdown.Collapse(false);dropdown.Options=options;}
                        var selected=options.FirstOrDefault(option=>option.Id==select.Read());
                        if(dropdown.SelectedOption!=selected)dropdown.SelectedOption=selected;
                    });break;
                case NativeColor color:
                    var colorButton=new ColorSquareTextButtonNode{Position=fieldPosition,Size=fieldSize,String="选择颜色",OnClick=()=>Apply(index,()=>OpenColorPicker(index,color))};colorButton.AttachNode(scroll.ContentNode);
                    refreshers.Add(available=>{colorButton.IsEnabled=Enabled(available);if(available)colorButton.DefaultColor=color.Read();});break;
                default:
                    var input=new TextInputNode{Position=fieldPosition,Size=fieldSize};
                    if(control is NativeNumber iconNumber && iconNumber.Label=="图标编号")
                        input.OnInputReceived=text=>{if(uint.TryParse(text.ToString(),out var id)&&id>=iconNumber.Minimum&&id<=iconNumber.Maximum)Apply(index,()=>iconNumber.Write(id));};
                    input.OnFocused=()=>editing.Add(index);input.OnUnfocused=()=>editing.Remove(index);
                    input.OnInputComplete=text=>Apply(index,()=>{
                        var value=text.ToString();
                        if(control is NativeText field)field.Write(value);
                        else if(control is NativeNumber number){
                            if(!double.TryParse(value,NumberStyles.Float,CultureInfo.InvariantCulture,out var parsed)||!double.IsFinite(parsed)||parsed<number.Minimum||parsed>number.Maximum)throw new ArgumentException($"请输入 {number.Minimum} 到 {number.Maximum} 的数值。");
                            number.Write(parsed);
                        }
                    });input.AttachNode(scroll.ContentNode);
                    refreshers.Add(available=>{
                        input.IsEnabled=Enabled(available);
                        if(available&&!editing.Contains(index)&&!invalid.Contains(index)){
                            var value=control is NativeText text?text.Read():((NativeNumber)control).Read().ToString(CultureInfo.InvariantCulture);
                            if(input.String.ToString()!=value)input.String=value;
                        }
                    });break;
            }
        }
        scroll.ContentNode.Height=Math.Max(430,panel.Controls.Count*44);scroll.RecalculateSizes();
        new TextButtonNode{Position=ContentStartPosition+new Vector2(660,440),Size=new(130,28),String="保存配置",OnClick=()=>{
            try{if(Available()&&invalid.Count==0&&editing.Count==0&&!pickingColor){owner.SaveImportedConfiguration();status!.String="配置已保存。";}
                else if(status!=null)status.String="请先完成当前输入。";
            }catch(Exception e){if(status!=null)status.String=e.GetBaseException().Message;}
        }}.AttachNode(addon);
        status=new TextNode{Position=ContentStartPosition+new Vector2(0,480),Size=new(790,28),FontSize=12};status.AttachNode(addon);
        Refresh();
    }
    private void OpenColorPicker(int index,NativeColor color)
    {
        colorPicker?.Dispose();
        colorPicker=new ColorPickerAddon{InternalName=InternalName+"Color",Title=color.Label,InitialColor=color.Read(),
            OnColorCancelled=()=>{pickingColor=false;nextRefresh=0;},
            OnColorConfirmed=value=>{pickingColor=false;Apply(index,()=>color.Write(value));}};
        pickingColor=true;
        try{colorPicker.Open();}catch{pickingColor=false;colorPicker.Dispose();colorPicker=null;throw;}
    }
    private bool Available()
    {
        if(disposed||stale)return false;
        if(!modules.Entries.Contains(entry)||!ReferenceEquals(entry.Instance,owner)){stale=true;if(status!=null)status.String="模块已被卸载或替换，请关闭此窗口。";return false;}
        return !entry.Transitioning;
    }
    private void Apply(int index,Action action)
    {
        if(binding||!Available()||pickingColor)return;
        var control=panel.Controls[index];
        try{
            if(control.Enabled?.Invoke()==false)return;
            if(control is not (NativeText or NativeNumber)&&(editing.Count>0||invalid.Count>0)){
                if(status!=null)status.String="请先完成当前输入。";nextRefresh=0;return;
            }
            action();invalid.Remove(index);owner.SaveImportedConfiguration();
            if(status!=null)status.String="操作完成。";
        }catch(Exception e){if(control is NativeText or NativeNumber)invalid.Add(index);if(status!=null)status.String=e.GetBaseException().Message;}
        nextRefresh=0;
    }
    private void Refresh()
    {
        bool available=Available();binding=true;
        try{foreach(var refresh in refreshers){try{refresh(available);}catch(Exception e){if(status!=null)status.String=e.GetBaseException().Message;}}}
        finally{binding=false;}
    }
    protected override void OnUpdate(AtkUnitBase* addon){if(Environment.TickCount64<nextRefresh)return;nextRefresh=Environment.TickCount64+100;Refresh();}
    protected override void OnFinalize(AtkUnitBase* addon){Finalized=true;colorPicker?.Dispose();colorPicker=null;pickingColor=false;base.OnFinalize(addon);}
    public override void Dispose(){disposed=true;colorPicker?.Dispose();colorPicker=null;refreshers.Clear();base.Dispose();}
}
