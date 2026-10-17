// SPDX-License-Identifier: GPL-3.0-or-later
namespace Yanjiu.Modules;
public static class ModuleFileActions
{
    public static bool IsManagedDownload(string configDirectory,string path)
    {
        try{
            var root=Path.GetFullPath(Path.Combine(configDirectory,"online"));var target=Path.GetFullPath(path);
            if(!target.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||Path.GetExtension(target).ToLowerInvariant() is not (".cs" or ".dll"))return false;
            for(var current=new FileInfo(target).Directory;current!=null;current=current.Parent){if(current.Exists&&(current.Attributes&FileAttributes.ReparsePoint)!=0)return false;if(current.FullName.Equals(root,StringComparison.OrdinalIgnoreCase))return true;}
            return false;
        }catch{return false;}
    }
}
