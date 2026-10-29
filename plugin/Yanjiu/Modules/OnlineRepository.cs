// SPDX-License-Identifier: GPL-3.0-or-later
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace Yanjiu.Modules;
public sealed class OnlineManifest
{public int SchemaVersion{get;set;}=1;public List<OnlineModule> Modules{get;set;}=new();}
public sealed class OnlineLocalization{public Dictionary<string,string> Source{get;set;}=new();public Dictionary<string,Dictionary<string,string>>? Translations{get;set;}}
public sealed class OnlineModule
{
    public string InternalName{get;set;}="";public string Name{get;set;}="";
    public string Author{get;set;}="";public string Description{get;set;}="";
    public string Version{get;set;}="";public string File{get;set;}="";public string Sha256{get;set;}="";
    public string MinimumOmniVersion{get;set;}="";
    public int ApiVersion{get;set;}
    public string RepositoryURL{get;set;}="";
    public OnlineLocalization? Localization{get;set;}

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
        var (uri,bytes)=await ReadManifest(input,token);var data=JsonSerializer.Deserialize<OnlineManifest>(bytes,new JsonSerializerOptions{PropertyNameCaseInsensitive=true})??throw new InvalidDataException("清单为空");
        if(data.SchemaVersion!=1||data.Modules==null||data.Modules.Count>300)throw new InvalidDataException("清单版本或模块数量不支持");
        if(data.Modules.Any(m=>m==null)||data.Modules.GroupBy(m=>m.InternalName,StringComparer.OrdinalIgnoreCase).Any(g=>g.Count()>1))throw new InvalidDataException("清单中存在空模块或 InternalName 重复");
        foreach(var module in data.Modules){ModuleUrl(uri,module);ValidateApi1(uri,module);}

        return new OnlineListing(uri,data.Modules);
    }
    public async Task<byte[]> Download(OnlineListing listing,OnlineModule module,CancellationToken token=default)
    {
        if(module.ApiVersion==1)await VerifyPublicRepository(module.RepositoryURL,token);
        var bytes=await Read(ModuleUrl(listing.ManifestUri,module),4*1024*1024,token);
        var hash=Convert.ToHexString(SHA256.HashData(bytes));if(!hash.Equals(module.Sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("SHA256 不一致，拒绝安装");return bytes;
    }
    private async Task<(Uri Uri,byte[] Bytes)> ReadManifest(string input,CancellationToken token)
    {
        var canonical=ManifestUrl(input);
        var original=new Uri(input);
        if(original.Host!="github.com"||original.AbsolutePath.Trim('/').Split('/').Length!=2)return(canonical,await Read(canonical,1024*1024,token));
        var repo=RepositoryIdentity(input);
        try{
            var bytes=await Read(new Uri("https://api.github.com/repos/"+repo+"/contents/TreeHouseModules.json"),2*1024*1024,token);
            using var json=JsonDocument.Parse(bytes);
            if(json.RootElement.GetProperty("encoding").GetString()!="base64")throw new InvalidDataException("Unsupported GitHub content encoding");
            return(canonical,Convert.FromBase64String(json.RootElement.GetProperty("content").GetString()!));
        }catch(Exception e)when(e is HttpRequestException or JsonException or InvalidDataException or KeyNotFoundException or FormatException){
            Exception? last=null;
            foreach(var branch in new[]{"HEAD","main","master"})try{var raw=new Uri("https://raw.githubusercontent.com/"+repo+"/"+branch+"/TreeHouseModules.json");return(raw,await Read(raw,1024*1024,token));}catch(HttpRequestException failure){last=failure;}
            throw new HttpRequestException("GitHub API 和 raw 仓库清单均不可用",last);
        }
    }
    public static void ValidateApi1(Uri manifest,OnlineModule module)
    {
        if(module.ApiVersion==0)return; // Existing non-API1 local repositories remain readable.
        if(module.ApiVersion!=1)throw new InvalidDataException("不支持的在线模块 API："+module.ApiVersion);
        if(!System.Version.TryParse(module.Version,out _)||!System.Version.TryParse(module.MinimumOmniVersion,out var minimum)||minimum>new System.Version(1,1,8,7))throw new InvalidDataException("模块要求更新的 API 1 公共运行库："+module.InternalName);
        if(string.IsNullOrWhiteSpace(module.Author)||module.Localization?.Source==null||!module.Localization.Source.TryGetValue("Title",out var title)||string.IsNullOrWhiteSpace(title)||!module.Localization.Source.TryGetValue("Description",out var description)||string.IsNullOrWhiteSpace(description))throw new InvalidDataException("API 1 清单缺少作者或 Localization.Source："+module.InternalName);
        if(!Uri.TryCreate(module.RepositoryURL,UriKind.Absolute,out var repositoryUri)||repositoryUri.Host!="github.com"||repositoryUri.Query.Length>0||repositoryUri.Fragment.Length>0||repositoryUri.AbsolutePath.Trim('/').Split('/').Length!=2)throw new InvalidDataException("RepositoryURL 必须为 GitHub 仓库根地址");
        if(!RepositoryIdentity(manifest.ToString()).Equals(RepositoryIdentity(module.RepositoryURL),StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("模块 RepositoryURL 与实际仓库不一致");
        module.Name=title;module.Description=description;
    }
    public static string RepositoryIdentity(string input)
    {
        if(!Uri.TryCreate(input,UriKind.Absolute,out var uri)||uri.Scheme!="https"||uri.UserInfo.Length>0)throw new InvalidDataException("在线模块需要公开 GitHub 仓库");
        if(uri.Host is not ("github.com" or "raw.githubusercontent.com"))throw new InvalidDataException("API 1 来源必须是 GitHub 仓库");
        var parts=uri.AbsolutePath.Trim('/').Split('/');if(parts.Length<2||parts.Take(2).Any(s=>!Regex.IsMatch(s,@"^[A-Za-z0-9_.-]+$")))throw new InvalidDataException("无效 GitHub 仓库地址");
        return parts[0]+"/"+parts[1];
    }
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string,DateTime> verified=new(StringComparer.OrdinalIgnoreCase);
    public async Task VerifyPublicRepository(string input,CancellationToken token=default)
    {
        var repository=RepositoryIdentity(input);
        if(verified.TryGetValue(repository,out var at)&&DateTime.UtcNow-at<TimeSpan.FromSeconds(30))return;
        var bytes=await Read(new Uri("https://api.github.com/repos/"+repository),256*1024,token);
        using var document=JsonDocument.Parse(bytes);
        if(!document.RootElement.TryGetProperty("private",out var privacy)||privacy.ValueKind!=JsonValueKind.False||!document.RootElement.TryGetProperty("full_name",out var name)||!string.Equals(name.GetString(),repository,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("无法确认模块仓库存在且公开");
        if(verified.Count>64)verified.Clear();verified[repository]=DateTime.UtcNow;
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
