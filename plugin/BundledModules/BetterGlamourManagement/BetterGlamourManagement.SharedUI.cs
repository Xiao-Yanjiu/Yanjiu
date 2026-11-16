// SPDX-License-Identifier: GPL-3.0-or-later
using Yanjiu.NativeUI;
using OmenTools.Interop.Game.Helpers;
namespace OmniToolbox.TreePublic;
public sealed unsafe partial class BetterGlamourManagement
{
    public bool TryOpenNativeSettings()
    {
        if (!IsEnabled || managerNativeUI is null) return false;
        OpenManager();
        return true;
    }

    // Disabled modules have no initialized preview/inventory services.
    public NativePanel CreateNativePanel()=>new("BetterGlamourManagement","更好的幻化管理",[
        new NativeLabel("请在主列表启用模块；启用完成后，点击设置即可进入幻化管理。"),
        new NativeToggle("启用幻化模板导出",()=>config.EnableGlamourPlateExport,SetPlateExportEnabled)
    ]);

    private void SetPlateExportEnabled(bool value)
    {
        config.EnableGlamourPlateExport=value;
        saveConfig();
        if(IsEnabled)RefreshPlateExport(AddonHelper.GetByName(GLAMOUR_PLATE_ADDON_NAME),true);
    }
}
