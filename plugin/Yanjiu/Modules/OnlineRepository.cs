// SPDX-License-Identifier: GPL-3.0-or-later
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace Yanjiu.Modules;
public sealed class OnlineManifest
{public int SchemaVersion{get;set;}=1;public List<OnlineModule> Modules{get;set;}=new();}
public sealed class OnlineModule
{
    public string InternalName{get;set;}="";public string Name{get;set;}="";
    public string Author{get;set;}="";public string Description{get;set;}="";
    public string Version{get;set;}="";public string File{get;set;}="";public string Sha256{get;set;}="";
    public string MinimumOmniVersion{get;set;}="";
}
public sealed record OnlineListing(Uri ManifestUri,IReadOnlyList<OnlineModule> Modules);
public sealed class OnlineRepository : IDisposable
{
    private readonly HttpClient http;
    public OnlineRepository(HttpMessageHandler? handler=null){http=new HttpClient(handler??new HttpClientHandler{AllowAutoRedirect=false}){Timeout=TimeSpan.FromSeconds(25)};http.DefaultRequestHeaders.UserAgent.ParseAdd("Yanjiu/0.1.0");}
    public static Uri ManifestUrl(string value)
    {
        if(!Uri.TryCreate(value.Trim(),UriKind.Absolute,out var uri)||uri.Scheme!="https"||uri.UserInfo.Length>0||uri.Query.Length>0||uri.Fragment.Length>0)throw new ArgumentException("填写 HTTPS 仓库或 TreeHouseModules.json 地址");
        if(uri.Host.Equals("github.com",StringComparison.OrdinalIgnoreCase)){
            var parts=uri.AbsolutePath.Trim('/').Split('/');if(parts.Length==2)return new Uri($"https://raw.githubusercontent.com/{parts[0]}/{parts[1]}/HEAD/TreeHouseModules.json");
            if(parts.Length>=5&&parts[2]=="blob")return new Uri("https://raw.githubusercontent.com/"+string.Join('/',parts.Where((_,i)=>i!=2)));
            throw new ArgumentException("使用 GitHub 仓库根地址或清单文件地址");
        }
        if(!uri.AbsolutePath.EndsWith(".json",StringComparison.OrdinalIgnoreCase))throw new ArgumentException("直接地址需要指向 JSON 清单");return uri;
    }
    public static Uri ModuleUrl(Uri manifest,OnlineModule module)
    {
        var file=Uri.UnescapeDataString(module.File??"");if(string.IsNullOrWhiteSpace(file)||file.StartsWith('/')||file.Contains('\\')||file.Split('/').Any(p=>p==".."||p==".")||file.Contains('?')||file.Contains('#')||Uri.TryCreate(file,UriKind.Absolute,out _))throw new ArgumentException("清单文件路径无效");
        var extension=Path.GetExtension(file);if(extension!=".cs"&&extension!=".dll")throw new ArgumentException("在线模块仅支持 .cs / .dll");
        if(!Regex.IsMatch(module.InternalName,"^[A-Za-z_][A-Za-z0-9_]{0,79}$"))throw new ArgumentException("模块 InternalName 无效");
        if(!Regex.IsMatch(module.Sha256??"","^[0-9a-fA-F]{64}$"))throw new ArgumentException("模块必须提供 SHA256");
        return new Uri(manifest,file);
    }
    public async Task<OnlineListing> Fetch(string input,CancellationToken token=default)
    {
        var uri=ManifestUrl(input);var bytes=await Read(uri,1024*1024,token);var data=JsonSerializer.Deserialize<OnlineManifest>(bytes,new JsonSerializerOptions{PropertyNameCaseInsensitive=true})??throw new InvalidDataException("清单为空");
        if(data.SchemaVersion!=1||data.Modules==null||data.Modules.Count>300)throw new InvalidDataException("清单版本或模块数量不支持");
        if(data.Modules.Any(m=>m==null)||data.Modules.GroupBy(m=>m.InternalName,StringComparer.OrdinalIgnoreCase).Any(g=>g.Count()>1))throw new InvalidDataException("清单中存在空模块或 InternalName 重复");
        foreach(var module in data.Modules)ModuleUrl(uri,module);
        return new OnlineListing(uri,data.Modules);
    }
    public async Task<byte[]> Download(OnlineListing listing,OnlineModule module,CancellationToken token=default)
    {
        var bytes=await Read(ModuleUrl(listing.ManifestUri,module),4*1024*1024,token);
        var hash=Convert.ToHexString(SHA256.HashData(bytes));if(!hash.Equals(module.Sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("SHA256 不一致，拒绝安装");return bytes;
    }
    private async Task<byte[]> Read(Uri uri,int limit,CancellationToken token)
    {
        using var response=await http.GetAsync(uri,HttpCompletionOption.ResponseHeadersRead,token);response.EnsureSuccessStatusCode();
        if(response.Content.Headers.ContentLength>limit)throw new InvalidDataException("下载内容过大");
        using var input=await response.Content.ReadAsStreamAsync(token);using var output=new MemoryStream();var buffer=new byte[8192];int count;
        while((count=await input.ReadAsync(buffer,token))>0){if(output.Length+count>limit)throw new InvalidDataException("下载内容过大");output.Write(buffer,0,count);}return output.ToArray();
    }
    public void Dispose()=>http.Dispose();
}
