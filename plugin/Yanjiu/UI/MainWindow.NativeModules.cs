// SPDX-License-Identifier: GPL-3.0-or-later
using Yanjiu.Modules;
namespace Yanjiu.UI;
public sealed partial class MainWindow
{
    internal sealed record NativeModuleRow(string Title,string Description,ModuleEntry? Entry,
        bool Online,bool Busy,string VersionTip,Action? Refresh,Action? DownloadOrDelete);

    internal NativeModuleRow[] NativeModuleRows(string filter,string search)
    {
        var result=new List<NativeModuleRow>();
        var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var pair in listings)
        foreach(var item in pair.Value.Modules)
        {
            var address=pair.Key;var listing=pair.Value;var path=ModulePath(listing,item);
            if(!seen.Add(path))continue;
            var entry=modules.Entries.FirstOrDefault(e=>e.Path.Equals(path,StringComparison.OrdinalIgnoreCase));
            if(entry==null&&filter is not ("全部" or "在线模块"))continue;
            if(entry!=null&&!ModuleFilters.Matches(entry,filter))continue;
            var title=entry?.Title??item.Name+" [Omni]";
            var description=entry?.Error.Length>0?entry.Error:item.Description;
            if(!(title+description).Contains(search,StringComparison.OrdinalIgnoreCase))continue;
            var local=entry==null?"未下载":LocalVersion(entry.Preference.Hash,item);
            var update=entry!=null&&!entry.Preference.Hash.Equals(item.Sha256,StringComparison.OrdinalIgnoreCase);
            var tip=$"仓库版本：{item.Version}\n本地版本：{local}"+(update?"\n仓库内容已更新，点击刷新下载更新。":"");
            var busy=installing.Contains(path)||fetching.Contains(address)||entry?.Transitioning==true;
            result.Add(new(title,description,entry,true,busy,tip,
                ()=>{if(!installing.Contains(path))Fetch(address,false,entry==null?null:item.InternalName);},
                ()=>{
                    if(installing.Contains(path)||fetching.Contains(address))return;
                    var current=modules.Entries.FirstOrDefault(e=>e.Path.Equals(path,StringComparison.OrdinalIgnoreCase));
                    if(current?.Transitioning==true)return;
                    if(current!=null)RemovePath(path);else Install(address,listing,item);
                }));
        }
        foreach(var entry in modules.Entries)
        {
            if(seen.Contains(entry.Path)||!ModuleFilters.Matches(entry,filter))continue;
            var description=entry.Error.Length>0?entry.Error:entry.Instance?.Descriptor.Description??entry.Catalog?.Description??"";
            if(!(entry.Title+description).Contains(search,StringComparison.OrdinalIgnoreCase))continue;
            bool external=entry.Origin is "在线" or "本地";
            var tip=entry.Origin=="在线"?$"仓库版本：尚未读取\n本地版本：{LocalVersion(entry.Preference.Hash,null)}":"";
            result.Add(new(entry.Title,description,entry,entry.Origin=="在线",entry.Transitioning,tip,
                external?()=>{if(entry.Origin=="在线"&&entry.Preference.Repository.Length>0)Fetch(entry.Preference.Repository);else modules.Import(entry.Path,"本地");}:null,
                external?()=>{if(!entry.Transitioning)RemovePath(entry.Path);}:null));
        }
        return result.OrderBy(r=>r.Online?1:r.Entry?.Origin=="本地"?2:0)
            .ThenBy(r=>TinyPinyin.PinyinHelper.GetPinyin(r.Title,string.Empty),StringComparer.OrdinalIgnoreCase)
            .ThenBy(r=>r.Title,StringComparer.Ordinal).ToArray();
    }
    private string LocalVersion(string hash,OnlineModule? item)
    {
        if(item!=null&&hash.Length>0&&hash.Equals(item.Sha256,StringComparison.OrdinalIgnoreCase))return item.Version;
        return config.DownloadedModuleVersions?.GetValueOrDefault(hash.ToLowerInvariant())??"未知（旧安装未记录版本）";
    }
}
