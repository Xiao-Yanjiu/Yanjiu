// SPDX-License-Identifier: GPL-3.0-or-later
using Yanjiu.NativeUI;
namespace Yanjiu.Modules;
public sealed partial class QuickCrossWorldTravel
{
    public bool TryOpenNativeSettings()
    {
        if (!IsEnabled || nativeWindow is null) return false;
        visible = true;
        return true;
    }
    public NativePanel CreateNativePanel()=>new("QuickCrossWorldTravel","快捷跨界传送",[
        new NativeLabel("请在主列表启用模块；启用完成后，点击设置即可进入传送面板。")
    ]);
}
