// SPDX-License-Identifier: GPL-3.0-or-later
using System.Numerics;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.BaseTypes;
using KamiToolKit.Nodes;
using Yanjiu.Modules;
namespace Yanjiu.UI;
internal sealed unsafe partial class NativeMainWindow : NativeAddon
{
    private const int VisibleRowCount = 9;
    private ScrollingNode<ResNode>? moduleScroll;
    private MainWindow.NativeModuleRow[] filteredEntries=[];
    private readonly Action<string> importSource;
    private readonly MainWindow controller;
    private readonly ModuleManager modules;
    private readonly Configuration config;
    private readonly List<(CheckboxNode Toggle,TextNode Description,NativeActionButton Favorite,NativeActionButton Settings,NativeActionButton Refresh,NativeActionButton Download,TextNode Version)> rows = [];
    private MainWindow.NativeModuleRow[] displayed = [];
    private bool bindingRows;
    private TextNode? summary;
    private ResNode? moduleContent;
    private readonly List<ResNode> pageRoots=[];
    private int selectedPage=1;
    private string search = "";
    private string filter = "全部";
    private long nextRefresh;

    [System.Diagnostics.CodeAnalysis.SetsRequiredMembers]
    public NativeMainWindow(ModuleManager manager,Configuration configuration,Action<string> import,MainWindow main)
    {
        importSource=import;controller=main;modules=manager;config=configuration;
        InternalName="YanjiuNativeMain";Title="小烟酒 · Yanjiu";Size=new Vector2(980,660);
    }
    protected override void OnSetup(AtkUnitBase* addon,Span<AtkValue> values)
    {
        rows.Clear();pageRoots.Clear();
        string[] pages=["首页","模块"];
        for(int i=0;i<pages.Length;i++){
            var selected=i;
            var button=new TextButtonNode{Position=ContentStartPosition+new Vector2(0,i*42),Size=new(130,32),String=pages[i],OnClick=()=>ShowPage(selected)};
            button.AttachNode(addon);
        }
        moduleContent=new ResNode{Size=Size};moduleContent.AttachNode(addon);
        var origin=ContentStartPosition+new Vector2(146,0);
        new TextInputNode{Position=origin,Size=new(750,28),PlaceholderString="搜索模块名称或说明",OnInputReceived=text=>{search=text.ToString();moduleScroll?.ScrollToStart();nextRefresh=0;}}.AttachNode(moduleContent);
        string[] filters=["全部","已启用","收藏","在线模块","本地模块"];
        for(int i=0;i<filters.Length;i++){
            var value=filters[i];
            new TextButtonNode{Position=origin+new Vector2(i*105,36),Size=new(100,28),String=value,OnClick=()=>{filter=value;moduleScroll?.ScrollToStart();nextRefresh=0;}}.AttachNode(moduleContent);
        }
        summary=new TextNode{Position=origin+new Vector2(0,72),Size=new(750,24),FontSize=14};summary.AttachNode(moduleContent);
        moduleScroll=new ScrollingNode<ResNode>{Position=origin+new Vector2(0,104),Size=new(750,470),ScrollSpeed=42};moduleScroll.AttachNode(moduleContent);
        for(int i=0;i<VisibleRowCount;i++){
            int index=i;var position=new Vector2(0,i*62);
            var toggle=new CheckboxNode{Position=position,Size=new(555,24),OnClick=enabled=>Act(index,entry=>{modules.Enable(entry,enabled);})};toggle.AttachNode(moduleScroll.ContentNode);
            var description=new TextNode{Position=position+new Vector2(20,27),Size=new(710,22),FontSize=12};description.AttachNode(moduleScroll.ContentNode);
            var favorite=new NativeActionButton{Position=position+new Vector2(565,0),Size=new(76,26),OnClick=()=>Act(index,entry=>{entry.Preference.Favorite=!entry.Preference.Favorite;config.Save();})};favorite.AttachNode(moduleScroll.ContentNode);
            var settings=new NativeActionButton{Position=position+new Vector2(650,0),Size=new(76,26),String="设置",OnClick=()=>Act(index,entry=>ShowNativeModuleDetails(entry))};settings.AttachNode(moduleScroll.ContentNode);
                        var refresh=new NativeActionButton{Size=new(62,26),String="刷新",OnClick=()=>RowAction(index,true)};refresh.AttachNode(moduleScroll.ContentNode);
            var download=new NativeActionButton{Size=new(62,26),OnClick=()=>RowAction(index,false)};download.AttachNode(moduleScroll.ContentNode);
            var version=new TextNode{Size=new(18,24),FontSize=14,String="!",TextColor=new(.55f,.55f,.55f,1),TextTooltip="版本信息"};version.AttachNode(moduleScroll.ContentNode);
            rows.Add((toggle,description,favorite,settings,refresh,download,version));
        }
        SetupPages(addon);ShowPage(selectedPage);RefreshRows();
    }
    private void Act(int index,Action<ModuleEntry> action)
    {
        if(bindingRows||index>=displayed.Length)return;
        var entry=displayed[index].Entry;
        if(entry==null||!modules.Entries.Contains(entry)||entry.Transitioning)return;
        try{action(entry);}catch(Exception e){Services.Log.Error(e,"Native module operation failed");}
        nextRefresh=0;
    }
    private void RefreshRows()
    {
        var entries=controller.NativeModuleRows(filter,search);
        filteredEntries=entries;
        if(moduleScroll!=null){var height=Math.Max(470,entries.Length*62);if(moduleScroll.ContentNode.Height!=height){moduleScroll.ContentNode.Height=height;moduleScroll.RecalculateSizes();}}
        if(summary!=null)summary.String=$"{filter}  ·  {entries.Length} 个模块";
        BindVisibleRows();
    }
    private void BindVisibleRows()
    {
        if(moduleScroll==null)return;
        int first=Math.Max(0,(int)(-moduleScroll.ContentNode.Y/62));
        displayed=filteredEntries.Skip(first).Take(VisibleRowCount).ToArray();
        bindingRows=true;
        try{
            for(int i=0;i<rows.Count;i++){
                var row=rows[i];bool visible=i<displayed.Length;
                row.Toggle.IsVisible=row.Description.IsVisible=row.Favorite.IsVisible=row.Settings.IsVisible=row.Refresh.IsVisible=row.Download.IsVisible=row.Version.IsVisible=visible;
                if(!visible)continue;
                var model=displayed[i];var entry=model.Entry;
                var position=new Vector2(0,(first+i)*62);
                row.Toggle.Position=position;row.Description.Position=position+new Vector2(20,27);
                row.Favorite.Position=position+new Vector2(670,0);row.Favorite.Size=new(28,28);
                row.Settings.Position=position+new Vector2(704,0);row.Settings.Size=new(28,28);row.Settings.String="⚙";row.Settings.TextTooltip="设置";
                row.Refresh.Position=position+new Vector2(602,0);row.Refresh.Size=new(28,28);row.Refresh.String="↻";row.Refresh.TextTooltip="刷新模块";row.Download.Position=position+new Vector2(636,0);row.Download.Size=new(28,28);
                row.Toggle.String=model.Title.Length>28?model.Title[..28]+"…":model.Title;
                row.Toggle.IsChecked=entry?.Instance?.IsEnabled==true;
                row.Toggle.IsEnabled=entry!=null&&!model.Busy;
                row.Favorite.IsEnabled=entry!=null&&!model.Busy;
                bool hasSettings=entry!=null&&NativeBuiltInSettings.HasConfiguration(entry);
                row.Settings.IsEnabled=hasSettings&&!model.Busy;
                row.Settings.MultiplyColor=hasSettings?Vector3.One:new Vector3(.4f);
                row.Settings.TextTooltip=hasSettings?"设置":"此模块无需额外配置";
                row.Refresh.IsVisible=model.Refresh!=null;row.Download.IsVisible=model.DownloadOrDelete!=null;
                row.Refresh.IsEnabled=row.Download.IsEnabled=!model.Busy;
                row.Download.String=model.Busy?"…":entry==null?"↓":"×";row.Download.TextTooltip=model.Busy?"处理中":entry==null?"下载模块":"删除模块";
                row.Version.Position=position+new Vector2(Math.Min(448,24+row.Toggle.Label.Width),0);
                row.Version.IsVisible=model.VersionTip.Length>0;row.Version.TextTooltip=model.VersionTip;
                row.Toggle.TextTooltip=model.Title;
                row.Description.String=model.Description.Replace("\r"," ").Replace("\n"," ");
                row.Description.TextTooltip=model.Description;
                row.Favorite.String=entry?.Preference.Favorite==true?"★":"☆";row.Favorite.TextTooltip=entry?.Preference.Favorite==true?"取消收藏":"收藏";
            }
        }finally{bindingRows=false;}
    }
    private void RowAction(int index,bool refresh)
    {
        if(bindingRows||index>=displayed.Length||displayed[index].Busy)return;
        try{(refresh?displayed[index].Refresh:displayed[index].DownloadOrDelete)?.Invoke();}
        catch(Exception e){Services.Log.Error(e,"Native online module action failed");}
        nextRefresh=0;
    }
    protected override void OnUpdate(AtkUnitBase* addon)
    {
        BindVisibleRows();BindVisibleSources();
        if(Environment.TickCount64<nextRefresh)return;
        nextRefresh=Environment.TickCount64+500;
        RefreshRows();
        RefreshSources();
        if(diagnostics!=null&&nativeDetails==null)diagnostics.String=modules.Status+"\n"+string.Join("\n",modules.Entries.Where(entry=>entry.Error.Length>0).Take(8).Select(entry=>entry.Title+"\n"+entry.Error));
    }
}
