using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using OmniToolbox.Collections;
using OmniToolbox.Config;

namespace OmniToolbox.UI;

public static partial class OmniLoc
{
    private static readonly Dictionary<string,string> Texts=CreateHostTexts();
    private static Dictionary<string,string> CreateHostTexts(){
        var texts=CreateCommonTexts();
        using var stream=typeof(OmniLoc).Assembly.GetManifestResourceStream("Yanjiu.Omni.ModuleTitles.json")??throw new InvalidDataException("Omni模块标题资源缺失");
        foreach(var pair in System.Text.Json.JsonSerializer.Deserialize<Dictionary<string,string>>(stream)!)texts.TryAdd(pair.Key,pair.Value);
        return texts;
    }
    private static Lazy<IReadOnlyDictionary<string, string>> TraditionalTexts = new(BuildTraditionalTexts);
    private static UILanguage Language;

    public static void RegisterTexts(IReadOnlyDictionary<string, string> texts)
    {
        AddTexts(Texts, texts);
        TraditionalTexts = new(BuildTraditionalTexts);
    }

    public static void SetLanguage(UILanguage value) => Language = value;

    public static string Get(string key)
    {
        if (!Texts.TryGetValue(key, out var text))
        {
            return TranslateFallback(key);
        }

        return Language == UILanguage.TraditionalChinese
            ? TraditionalTexts.Value[key]
            : text;
    }

    private static string TranslateFallback(string key)
    {
        var exact=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase){
            ["Feature.BetterGlamourManagement.CommandDescription"]="打开幻化管理",
            ["Feature.MultiToolbar.HiddenWindows"]="隐藏窗口",
            ["Feature.MultiDock.Export"]="导出配置",
            ["Feature.MultiDock.Import"]="导入配置",
            ["Feature.MultiToolbar.EditWidgets"]="编辑工具栏组件",
            ["Feature.MultiToolbar.EditPlugins"]="编辑插件按钮",
            ["Feature.MultiToolbar.EditCommands"]="编辑自定义命令",
            ["Feature.MultiToolbar.AutoExpandOnHover"]="悬停时自动展开",
            ["Feature.MultiToolbar.BackgroundColor"]="背景颜色",
            ["Feature.MultiToolbar.TextColor"]="文字颜色",
            ["Feature.MultiToolbar.AccentColor"]="强调颜色",
            ["Feature.MultiToolbar.BorderColor"]="边框颜色",
            ["Feature.MultiToolbar.BarOffset"]="工具栏偏移",
            ["Feature.MultiToolbar.BarOpacity"]="工具栏不透明度",
            ["Feature.MultiToolbar.ButtonOpacity"]="按钮不透明度",
            ["Feature.MultiToolbar.ToolbarScale"]="工具栏缩放",
            ["Feature.MultiToolbar.RowIconSize"]="图标大小",
            ["Feature.MultiToolbar.ComponentSpacing"]="组件间距",
            ["Feature.MultiToolbar.Alignment"]="对齐方式",
            ["Feature.MultiToolbar.Font"]="字体",
            ["Feature.BetterUserMacro.Tooltips"]="宏提示",
            ["Feature.BetterUserMacro.LineNumbers"]="显示行号",
            ["Feature.BetterUserMacro.DragSwap"]="拖拽交换",
            ["Feature.BetterUserMacro.CustomIcons"]="自定义图标"
        };
        if(exact.TryGetValue(key,out var value))return value;
        var text=key[(key.LastIndexOf('.')+1)..];
        var replacements=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase){
            ["Export"]="导出",["Import"]="导入",["Edit"]="编辑",["Open"]="打开",["Close"]="关闭",["Delete"]="删除",
            ["Save"]="保存",["Reset"]="重置",["Add"]="添加",["Remove"]="移除",["Enabled"]="启用",["Disabled"]="停用",
            ["Enable"]="启用",["Disable"]="停用",["Search"]="搜索",["Filter"]="筛选",["Name"]="名称",["Description"]="说明",
            ["Settings"]="设置",["Window"]="窗口",["Windows"]="窗口",["Toolbar"]="工具栏",["Bar"]="工具栏",["Button"]="按钮",
            ["Command"]="命令",["Commands"]="命令",["Plugin"]="插件",["Plugins"]="插件",["Widget"]="组件",["Widgets"]="组件",
            ["Color"]="颜色",["Background"]="背景",["Text"]="文字",["Border"]="边框",["Scale"]="缩放",["Opacity"]="透明度",
            ["Font"]="字体",["Size"]="大小",["Spacing"]="间距",["Alignment"]="对齐",["Auto"]="自动",["Expand"]="展开",
            ["Hover"]="悬停",["Icon"]="图标",["Icons"]="图标",["Custom"]="自定义",["Line"]="行",["Lines"]="行号"
        };
        foreach(var pair in replacements)text=System.Text.RegularExpressions.Regex.Replace(text,pair.Key,pair.Value,System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return text.Length==0?key:text;
    }
    public static string Get(ItemAvailability availability) => Get(availability switch
    {
        ItemAvailability.Obtainable => "Status.Obtainable",
        ItemAvailability.Purchasable => "Status.Purchasable",
        ItemAvailability.Unobtainable => "Status.Unobtainable",
        _ => throw new ArgumentOutOfRangeException(nameof(availability), availability, null)
    });

    private static void AddTexts(
        Dictionary<string, string> texts,
        IReadOnlyDictionary<string, string> additions)
    {
        foreach (var (key, value) in additions)
        {
            if (!texts.TryAdd(key, value))
            {
                throw new InvalidOperationException($"Duplicate localization key: {key}");
            }
        }
    }

    private static IReadOnlyDictionary<string, string> BuildTraditionalTexts()
        => Texts.ToDictionary(
            static entry => entry.Key,
            static entry => ConvertToTraditional(entry.Value),
            StringComparer.Ordinal);

    private static string ConvertToTraditional(string text)
    {
        var length = LCMapStringEx(
            "zh-TW",
            TRADITIONAL_CHINESE_MAP_FLAG,
            text,
            -1,
            null,
            0,
            nint.Zero,
            nint.Zero,
            nint.Zero);
        if (length <= 1)
        {
            return text;
        }

        var result = new StringBuilder(length);
        return LCMapStringEx(
                   "zh-TW",
                   TRADITIONAL_CHINESE_MAP_FLAG,
                   text,
                   -1,
                   result,
                   length,
                   nint.Zero,
                   nint.Zero,
                   nint.Zero) == length
            ? result.ToString()
            : text;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int LCMapStringEx(
        string localeName,
        uint mapFlags,
        string source,
        int sourceLength,
        StringBuilder? destination,
        int destinationLength,
        nint versionInformation,
        nint reserved,
        nint sortHandle);

    #region 常量

    private const uint TRADITIONAL_CHINESE_MAP_FLAG = 0x04000000;

    #endregion
}


