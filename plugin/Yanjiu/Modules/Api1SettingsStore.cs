// SPDX-License-Identifier: GPL-3.0-or-later
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace Yanjiu.Modules;

// The JSON inside the envelope retains the author's exact Settings keys. Encrypt
// the complete document so nested music cookies and future credentials are safe.
public static class Api1SettingsStore
{
    private const int Limit=4*1024*1024;
    private static byte[] Entropy(string module)=>Encoding.UTF8.GetBytes("Yanjiu/API1/settings/v1/"+module);
    public static string Read(string path,string module)
    {
        if(new FileInfo(path).Length>8*1024*1024)throw new InvalidDataException("API 1 配置文件过大");
        var text=File.ReadAllText(path);using var document=JsonDocument.Parse(text);
        if(document.RootElement.TryGetProperty("_YanjiuApi1ProtectedSettings",out var marker)){
            if(marker.GetInt32()!=1)throw new InvalidDataException("不支持的 API 1 配置格式");
            var bytes=ProtectedData.Unprotect(Convert.FromBase64String(document.RootElement.GetProperty("Payload").GetString()!),Entropy(module),DataProtectionScope.CurrentUser);
            if(bytes.Length>Limit)throw new InvalidDataException("API 1 配置内容过大");
            return Encoding.UTF8.GetString(bytes);
        }
        if(Encoding.UTF8.GetByteCount(text)>Limit)throw new InvalidDataException("API 1 配置内容过大");
        return text; // Existing plaintext is migrated on the next successful save.
    }
    public static void Save(string path,string module,object value,Type type)
    {
        var bytes=JsonSerializer.SerializeToUtf8Bytes(value,type,new JsonSerializerOptions{IncludeFields=true,MaxDepth=64});
        if(bytes.Length>Limit)throw new InvalidDataException("API 1 配置内容过大");
        var encrypted=ProtectedData.Protect(bytes,Entropy(module),DataProtectionScope.CurrentUser);
        var envelope=JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string,object>{{"_YanjiuApi1ProtectedSettings",1},{"Protection","DPAPI.CurrentUser"},{"Payload",Convert.ToBase64String(encrypted)}});
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.WriteAllBytes(path+".tmp",envelope);File.Move(path+".tmp",path,true);
    }
}
