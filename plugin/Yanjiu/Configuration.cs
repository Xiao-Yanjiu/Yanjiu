// SPDX-License-Identifier: GPL-3.0-or-later
using Dalamud.Configuration;
namespace Yanjiu;
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;
    public int Theme { get; set; }
    public float Scale { get; set; } = 1;
    public float FontSize { get; set; } = 20;
    public int FontFamily { get; set; } = 1;
    public float BorderWidth { get; set; } = 2.25f;
    public Dictionary<string,float[]> ThemeColors { get; set; } = new();
    public Dictionary<string,bool> RepositoryAutoRefresh { get; set; } = new();
    public List<ModulePreference> Modules { get; set; } = new();
    public List<string> Repositories { get; set; } = new();
    public void Save()=>Services.PluginInterface.SavePluginConfig(this);
}
public sealed class ModulePreference
{
    public string Path { get; set; } = "";
    public string Hash { get; set; } = "";
    public bool Enabled { get; set; }
    public bool Favorite { get; set; }
    public string Origin { get; set; } = "本地";
    public string Repository { get; set; } = "";
}
