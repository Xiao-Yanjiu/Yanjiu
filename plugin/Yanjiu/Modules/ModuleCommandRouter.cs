// SPDX-License-Identifier: GPL-3.0-or-later
namespace Yanjiu.Modules;
public static class ModuleCommandRouter
{
    public static string Dispatch(IEnumerable<ModuleEntry> entries,string command,string arguments)
    {
        var parts=arguments.Trim().Split((char[]?)null,2,StringSplitOptions.RemoveEmptyEntries);
        if(parts.Length==0)return "请指定模块名称";
        var entry=entries.FirstOrDefault(e=>string.Equals(e.Instance?.ModuleName,parts[0],StringComparison.OrdinalIgnoreCase));
        if(entry?.Instance==null)return "未找到模块："+parts[0];
        if(!entry.Instance.IsEnabled)return "请先启用模块："+entry.Instance.Descriptor.Title;
        return entry.Instance.TryHandleCommand(command,parts.Length>1?parts[1]:"")?"":"此模块未处理该命令";
    }
}
