// SPDX-License-Identifier: GPL-3.0-or-later
using System.Security.Cryptography;
using System.Text.Json;
namespace Yanjiu.Modules;
public sealed class DRLocalModule
{
    public Dictionary<string,string> SettingLabels{get;set;}=new();public string[] Prerequisites{get;set;}=[];public string[] Conflicts{get;set;}=[];public string[] Pairs{get;set;}=[];public string Id{get;set;}="";public string Title{get;set;}="";public string Description{get;set;}="";
    public string Category{get;set;}="";public string Sha256{get;set;}="";public string Error{get;set;}="";
    public bool Importable{get;set;}=true;public bool Compiles{get;set;}public string Source{get;set;}="DR";
}
public static class DRCatalog
{
    public static string Root=>RootFor("DR");
    public static string RootFor(string source){if(source is not ("DR" or "Omni"))throw new ArgumentException("无效来源");return Path.Combine(PluginFiles.DirectoryPath,source,"Modules");}
    public static IReadOnlyList<DRLocalModule> Load(string source="DR")
    {
        var path=Path.Combine(RootFor(source),"catalog.json");
        if(!File.Exists(path))return [];
        var hostHashFile=Path.Combine(RootFor(source),"host.sha256");
        if(!File.Exists(hostHashFile)||new FileInfo(hostHashFile).Length>128||!File.ReadAllText(hostHashFile).Trim().Equals(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(PluginFiles.AssemblyPath))),StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("DR模块包与当前Yanjiu版本不一致，请重新审计并打包");
        if(new FileInfo(path).Length>2*1024*1024)throw new InvalidDataException("DR模块目录过大");
        var items=JsonSerializer.Deserialize<List<DRLocalModule>>(File.ReadAllText(path),new JsonSerializerOptions{PropertyNameCaseInsensitive=true})??[];
        if(items.Count>1000||items.Any(i=>!System.Text.RegularExpressions.Regex.IsMatch(i.Id,@"^[A-Za-z][A-Za-z0-9_]{0,127}$")))throw new InvalidDataException("无效的DR模块目录");
        foreach(var item in items)item.Source=source;return items;
    }
    public static string VerifiedPath(DRLocalModule item)
    {
        if(!item.Compiles||!item.Importable)throw new InvalidOperationException(item.Error);
        if(!System.Text.RegularExpressions.Regex.IsMatch(item.Id,@"^[A-Za-z][A-Za-z0-9_]{0,127}$"))throw new InvalidDataException("无效模块名称");
        var path=Path.Combine(RootFor(item.Source),item.Id+".dll");
        if(!File.Exists(path)||new FileInfo(path).Length>4*1024*1024)throw new InvalidDataException("DR模块文件缺失或过大");
        if(!Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).Equals(item.Sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("DR模块校验失败，请重新构建");
        return path;
    }
}






