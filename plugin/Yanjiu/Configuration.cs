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
        var exact=Modules.FirstOrDefault(p=>p.Path.Equals(path,StringComparison.OrdinalIgnoreCase));
        if(exact!=null)return exact;
        // Bundled paths contain the installed plugin version; preserve user choices across upgrades.
        var previous=Modules.FirstOrDefault(p=>p.Origin==source&&System.IO.Path.GetFileName(p.Path).Equals(System.IO.Path.GetFileName(path),StringComparison.OrdinalIgnoreCase));
        if(previous!=null){previous.Path=path;previous.Hash="";return previous;}
        var created=new ModulePreference{Path=path,Origin=source};Modules.Add(created);return created;
    }
    public void Save()=>Services.PluginInterface.SavePluginConfig(this);
}
public sealed class ModulePreference
{
    public string Path { get; set; } = "";
    public string Hash { get; set; } = "";
    public int ApiVersion { get; set; }
    public bool Enabled { get; set; }
    public bool Favorite { get; set; }
    public string Origin { get; set; } = "本地";
    public string Repository { get; set; } = "";
}

