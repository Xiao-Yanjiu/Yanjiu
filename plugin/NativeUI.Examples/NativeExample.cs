// SPDX-License-Identifier: GPL-3.0-or-later
using OmniToolbox.Common.Module.Abstractions;
using OmniToolbox.Common.Module.Enums;
using OmniToolbox.Common.Module.Models;
using TreeTools.Lifecycle;
using OmniToolbox.UI;
using Yanjiu.NativeUI;

[ModuleApiVersion(1)]
public sealed class NativeExample : OmniModuleBase,INativeModuleUI
{
    public sealed class Settings
    {
        public bool ShowDetails=true;
        public string Name="原生界面示例";
        public int Count;
    }
    private Settings config=new();
    public override ModuleInfo Info {get;}=new(){Category=ModuleCategory.Interface};
    public override ModuleLocalization Localization {get;}=ModuleLocalization.Create(new Dictionary<string,string>{["Title"]="原生界面示例",["Description"]="通过Yanjiu公共层创建游戏原生控件。"});
    public override bool HasSettings=>true;
    protected override void OnStart(FeatureLifetime activation){}
    public NativePanel CreateNativePanel()=>new("NativeExample",config.Name,[
        new NativeLabel("计数",()=>"计数 "+config.Count),
        new NativeToggle("显示详细信息",()=>config.ShowDetails,value=>config.ShowDetails=value),
        new NativeText("名称",()=>config.Name,value=>config.Name=value),
        new NativeNumber("计数",()=>config.Count,value=>config.Count=(int)value,0,1000),
        new NativeButton("计数加一",()=>config.Count=Math.Min(1000,config.Count+1)),
        new NativeButton("重置",()=>config.Count=0)
    ]);
    public override bool TryHandleCommand(string arguments)
    {
        if(!IsEnabled||arguments.Trim() is not ("" or "open"))return false;
        _=OpenPanel();return true;
    }
    private async Task OpenPanel()
    {
        try{await NativeUi.OpenAsync(this,CreateNativePanel());}
        catch(Exception e){OmenTools.DService.Instance().Chat.PrintError("[Yanjiu] "+e.GetBaseException().Message);}
    }
}

