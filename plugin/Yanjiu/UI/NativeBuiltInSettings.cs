// SPDX-License-Identifier: GPL-3.0-or-later
using System.Reflection;
using System.Numerics;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.BaseTypes;
using KamiToolKit.Nodes;
using Yanjiu.Modules;
namespace Yanjiu.UI;
internal sealed unsafe partial class NativeBuiltInSettings : NativeAddon
{
    private readonly ModuleEntry entry;
    private readonly ModuleManager manager;
    private readonly ModuleBase owner;
    private TextNode? status;
    internal static bool HasConfiguration(ModuleEntry entry)
    {
        if(entry.Instance==null)return false;
        if(entry.Origin!="DR")return entry.Instance.HasSettings;
        if(entry.Instance.ModuleName=="AutoUseItemStacks")return false;
        var method=entry.Instance.GetType().GetMethod("ConfigUI",BindingFlags.Instance|BindingFlags.NonPublic);
        return method!=null&&method.DeclaringType!=typeof(DailyRoutines.Common.Module.Abstractions.ModuleBase);
    }
    internal static bool Supports(ModuleEntry entry)=>entry.Instance is AfkKeepAlive||entry.Origin=="DR"&&entry.Instance is {} configured&&simpleModules.Contains(configured.ModuleName)||
        entry.Origin=="DR"&&entry.Instance is {} instance&&
        instance.GetType().GetMethod("ConfigUI",BindingFlags.Instance|BindingFlags.NonPublic)?.DeclaringType==typeof(DailyRoutines.Common.Module.Abstractions.ModuleBase);
    [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
    internal NativeBuiltInSettings(ModuleEntry module,ModuleManager modules)
    {
        entry=module;manager=modules;owner=module.Instance!;
        InternalName="YanjiuBuiltInSettings";Title=module.Title;Size=new(760,module.Instance?.ModuleName is "AutoDisplayPlayerHitbox" or "CustomizeSightDistance"?550:300);
    }
    protected override void OnSetup(AtkUnitBase* addon,Span<AtkValue> values)
    {
        var addonRoot=new ResNode{Size=Size};addonRoot.AttachNode(addon);
        if(owner is AfkKeepAlive afk){
            new TextNode{Position=ContentStartPosition+new Vector2(0,0),Size=new(260,28),String="空闲阈值（30～300 秒）"}.AttachNode(addon);
            new SliderNode{Position=ContentStartPosition+new Vector2(270,0),Size=new(380,28),Min=30,Max=300,Value=afk.NativeIdleSeconds,OnValueChanged=value=>{
                if(!Available())return;try{afk.SetNativeIdleSeconds(value);}catch(Exception e){if(status!=null)status.String=e.GetBaseException().Message;}
            }}.AttachNode(addon);
        }else if(simpleModules.Contains(owner.ModuleName)){SetupSimple(addonRoot!);}else new TextNode{Position=ContentStartPosition+new Vector2(0,0),Size=new(710,30),String="此模块无需额外配置。"}.AttachNode(addon);
        status=new TextNode{Position=ContentStartPosition+new Vector2(0,Size.Y-115),Size=new(710,50)};status.AttachNode(addon);
    }
    private bool Available()=>manager.Entries.Contains(entry)&&ReferenceEquals(entry.Instance,owner)&&!entry.Transitioning;
    protected override void OnUpdate(AtkUnitBase* addon){if(status!=null)status.String=!Available()?"模块已移除、替换或正在切换状态，请稍候或重新打开。":owner.IsEnabled?"当前状态：已启用":"当前状态：未启用，可先调整设置。";}
}
