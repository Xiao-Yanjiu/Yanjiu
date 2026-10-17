// SPDX-License-Identifier: GPL-3.0-or-later
namespace Yanjiu.Modules;
public sealed record ModuleOverlap(string Omni,string DR,string Function,bool SameScope);
public static class ModuleOverlaps
{
    public static readonly ModuleOverlap[] All=[
        new("AutoLogin","AutoLogin","自动登录",true),
        new("AutoSortItems","AutoSortItems","自动整理物品",true),
        new("CopyItemName","CopyItemNameContextMenu","复制物品名称",true),
        new("DuplicateStatusPrevention","AutoPreventDuplicateStatus","防止重复状态",true),
        new("CastMovementLock","AutoCastMovementLock","咏唱期间移动锁定",true),
        new("AntiCensorship","AutoAntiCensorship","聊天屏蔽词处理",true),
        new("FoodReminder","AutoCheckFoodUsage","食物状态提醒",false),
        new("BackgroundFPSLimit","BetterFPSLimitation","后台帧率限制",false),
        new("BetterUserMacro","OptimizedMacro","用户宏界面增强",false),
        new("BetterGlamourManagement","UnifiedGlamourManager","投影/幻化管理",false),
        new("MitigationMonitor","AutoDisplayMitigationInfo","减伤效果显示",false),
        new("AltF4Exit","InstantLogout","快捷退出游戏",false)
    ];
    public static bool Conflicts(ModuleEntry first,ModuleEntry second)=>All.Any(row=>row.SameScope&&(
        first.Source=="Omni"&&first.Instance?.ModuleName==row.Omni&&second.Source=="DR"&&second.Instance?.ModuleName==row.DR||
        second.Source=="Omni"&&second.Instance?.ModuleName==row.Omni&&first.Source=="DR"&&first.Instance?.ModuleName==row.DR));
}

