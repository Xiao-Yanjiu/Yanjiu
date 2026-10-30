// SPDX-License-Identifier: GPL-3.0-or-later
using Dalamud.Configuration;
namespace Yanjiu;
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;
    public int NativeThemeRevision {get;set;}
    public int Theme { get; set; } = UI.Themes.NativeGameIndex;
    public float Scale { get; set; } = 1;
    public float FontSize { get; set; } = 20;
    public int FontFamily { get; set; } = 1;
    public float BorderWidth { get; set; } = 2.25f;
    public Dictionary<string,float[]> ThemeColors { get; set; } = new();
    public Dictionary<string,bool> RepositoryAutoRefresh { get; set; } = new();
    public List<string> HiddenAvailableModules { get; set; } = new();
    public List<ModulePreference> Modules { get; set; } = new();
    public List<string> Repositories { get; set; } = new();
    public bool RemoveRetiredBuiltInModules()=>Modules.RemoveAll(p=>p.Path=="builtin:phone")>0;
    public ModulePreference ResolveBundledPreference(string path,string source)
    {
        var key=source+":"+System.IO.Path.GetFileName(path);
        var candidates=Modules.Where(p=>
            string.Equals(p.BundledKey,key,StringComparison.OrdinalIgnoreCase)||
            (string.Equals(p.Origin,source,StringComparison.OrdinalIgnoreCase)&&
             System.IO.Path.GetFileName(p.Path).Equals(System.IO.Path.GetFileName(path),StringComparison.OrdinalIgnoreCase))) .ToArray();
        var exact=candidates.FirstOrDefault(p=>p.Path.Equals(path,StringComparison.OrdinalIgnoreCase));
        var result=exact??candidates.FirstOrDefault()??new ModulePreference{Origin=source};
        // Merge stranded favorites once, then remove old identities so an explicit
        // later un-favorite cannot be resurrected on the next startup.
        result.Favorite|=candidates.Any(p=>p.Favorite);
        if(!result.Path.Equals(path,StringComparison.OrdinalIgnoreCase)) result.Hash="";
        result.Path=path;result.BundledKey=key;
        foreach(var previous in candidates) if(!ReferenceEquals(previous,result)) Modules.Remove(previous);
        if(!Modules.Contains(result))Modules.Add(result);
        return result;
    }
    public void Save()=>Services.PluginInterface.SavePluginConfig(this);
}
public sealed class ModulePreference
{
    public string BundledKey { get; set; } = "";
    public string Path { get; set; } = "";
    public string Hash { get; set; } = "";
    public int ApiVersion { get; set; }
    public bool Enabled { get; set; }
    public bool Favorite { get; set; }
    public string Origin { get; set; } = "本地";
    public string Repository { get; set; } = "";
}

