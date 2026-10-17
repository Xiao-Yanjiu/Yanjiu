// SPDX-License-Identifier: GPL-3.0-or-later
namespace Yanjiu;
public static class PluginFiles
{
    private static string? installedAssembly;
    public static void Initialize(string path)
    {
        if(string.IsNullOrWhiteSpace(path)||!Path.IsPathFullyQualified(path)||!File.Exists(path))
            throw new InvalidDataException("Yanjiu安装程序集路径无效");
        installedAssembly=Path.GetFullPath(path);
    }
    public static string AssemblyPath=>ResolveAssemblyPath(installedAssembly??Services.PluginInterface?.AssemblyLocation.FullName,typeof(PluginFiles).Assembly.Location);
    public static string DirectoryPath=>Path.GetDirectoryName(AssemblyPath)!;
    public static string ResolveAssemblyPath(string? installed,string? loaded)
    {
        foreach(var path in new[]{installed,loaded})
            if(!string.IsNullOrWhiteSpace(path)&&Path.IsPathFullyQualified(path))return Path.GetFullPath(path);
        throw new InvalidDataException("无法确定Yanjiu安装路径，请重新加载插件");
    }
}
