// SPDX-License-Identifier: GPL-3.0-or-later
namespace Yanjiu.Modules;
public static class ModuleFilters
{
    public static bool Matches(ModuleEntry entry,string filter){
        if(filter=="全部")return true;
        if(filter=="已启用")return entry.Instance?.IsEnabled==true;
        if(filter=="收藏")return entry.Preference.Favorite;
        if(filter=="在线模块")return entry.Origin=="在线";
        if(filter=="本地模块")return entry.Origin=="本地";
        var category=entry.Instance?.Descriptor.Category??entry.Catalog?.Category??"";
        return category==filter||category==(filter switch{
            "系统"=>"System","系统界面"=>"Interface","战斗"=>"Combat","技能"=>"Action","辅助"=>"Assist",
            "生产采集"=>"CraftGather","副本"=>"Duty","通知"=>"Notification","招募"=>"Recruitment","脚本"=>"Script",
            "金碟"=>"GoldSaucer","物品"=>"Item","日常"=>"Daily","自动化"=>"Automation","调试"=>"Debug","通用"=>"General",_=>filter});
    }
}


