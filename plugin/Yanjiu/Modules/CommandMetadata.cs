// SPDX-License-Identifier: GPL-3.0-or-later
using Dalamud.Game.Command;
namespace Yanjiu.Modules;
public static class CommandMetadata
{
    public static void EnsureHelp(IEnumerable<KeyValuePair<string,CommandInfo>> commands)
    {
        foreach(var pair in commands)if(string.IsNullOrWhiteSpace(pair.Value.HelpMessage))
            pair.Value.HelpMessage="Yanjiu 模块命令："+pair.Key+"。使用 /yanjiu 打开对应模块设置查看参数和用法。";
    }
}
