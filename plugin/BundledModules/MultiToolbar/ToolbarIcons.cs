using System.Diagnostics.CodeAnalysis;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Plugin;

namespace OmniToolbox.TreePublic;

internal static class ToolbarIcons
{
    internal static bool IsCurrency(Lumina.Excel.Sheets.Item item) =>
        item.FilterGroup is 29 or 38 or 40 or 47 or 52 or 53 or 54 or 55 or 56 or 57 or 58 or 59 ||
        item.ItemUICategory.RowId == 63 && item.ItemSearchCategory.RowId == 0 && item.ItemSortCategory.RowId == 3;

    internal static bool IsActionOrItemIcon(uint id) =>
        id is >= 100 and < 4000 or >= 5100 and < 10000 or >= 19600 and < 59000 or
            >= 61250 and < 61290 or >= 64200 and < 64325 or >= 64550 and < 65000 or
            >= 70000 and < 70120 or >= 82200 and < 82270 or >= 200000 and < 210000 or >= 246000 and < 250000;

    internal static string? GetBuiltInIconPath(uint id) => id switch
    {
        int.MaxValue - 1 => System.IO.Path.Combine(DalamudServices.PluginInterface.AssemblyLocation.DirectoryName!, "Resources", "CustomIcon.png"),
        int.MaxValue => System.IO.Path.Combine(DalamudServices.PluginInterface.AssemblyLocation.DirectoryName!, "Resources", "XIVLauncherCN.png"),
        _ => null
    };
    internal static IDalamudTextureWrap? GetIconTexture(uint id) => GetBuiltInIconPath(id) is {} path
        ? DalamudServices.TextureProvider.GetFromFile(path).GetWrapOrDefault()
        : DalamudServices.TextureProvider.GetFromGameIcon(id).GetWrapOrDefault();
}

// Let the shared ImageHelper own asynchronous texture fetching and caching; no private host reflection
// or module-owned background downloads survive module unloading.
internal static class PluginIconResolver
{
    internal static bool IsUsableTexture([NotNullWhen(true)] IDalamudTextureWrap? texture) =>
        texture is not null && texture.Handle != nint.Zero && Math.Max(texture.Width, texture.Height) > 4;

    internal static IDalamudTextureWrap? GetIcon(IExposedPlugin? plugin, float displaySize)
    {
        if (Uri.TryCreate(plugin?.Manifest.IconUrl, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http")
            return OmenTools.OmenService.ImageHelper.Instance().GetImage(uri.AbsoluteUri);
        return DalamudServices.TextureProvider.GetFromGameIcon(60071).GetWrapOrDefault();
    }
}
