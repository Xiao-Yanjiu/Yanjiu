// SPDX-License-Identifier: GPL-3.0-or-later
using Yanjiu.NativeUI;
using FFXIVClientStructs.FFXIV.Client.UI;
using OmenTools.Interop.Game.Helpers;
namespace OmniToolbox.TreePublic;
public sealed unsafe partial class BetterUserMacro
{
    public NativePanel CreateNativePanel()=>new("BetterUserMacro","更好的用户宏",[
        new NativeToggle("宏提示",()=>config.Tooltips,value=>{config.Tooltips=value;RefreshNativeSettings();}),
        new NativeToggle("宏行号",()=>config.LineNumbers,value=>{config.LineNumbers=value;RefreshNativeSettings();}),
        new NativeToggle("拖动同步宏位置",()=>config.DragSwap,value=>{config.DragSwap=value;RefreshNativeSettings();}),
        new NativeToggle("自定义宏图标",()=>config.CustomIcons,value=>{config.CustomIcons=value;RefreshNativeSettings();})
    ]);
    private void RefreshNativeSettings()
    {
        macroIconsDirty=true;
        if(IsEnabled&&AddonHelper.TryGetByName("Macro",out AddonMacro* addon)&&IsMacroAddonVisible(addon))RefreshMacroUI(addon);
    }
}
