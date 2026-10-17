// SPDX-License-Identifier: GPL-3.0-or-later
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Yanjiu.Modules;

namespace Yanjiu.UI;
public sealed class MainWindow : Window,IDisposable
{
    private const string Plus="\uf067",Trash="\uf1f8",Refresh="\uf021",Download="\uf019",Menu="\uf0c9";
    private readonly Configuration config;
    private readonly ModuleManager modules;
    private readonly OnlineRepository online=new();
    private readonly CancellationTokenSource stop=new();
    private readonly System.Collections.Concurrent.ConcurrentQueue<Action> uiActions=new();
    private readonly Dictionary<string,OnlineListing> listings=new();
    private readonly Dictionary<string,string> sourceStatus=new();
    private readonly HashSet<string> fetching=new(),installing=new();
    private readonly Dictionary<string,float> cardHeights=new();
    private string moduleFilter="全部";private int page;private IReadOnlyList<DRLocalModule> drCatalog=[];private string drCatalogError="";
    private string localPath="",repository="",search="",status="";
    private bool newAutoRefresh;
    private long nextRefresh=Environment.TickCount64+900_000;
    private readonly DisabledSettingsEditor disabledSettings=new();private ModuleEntry? selected;
    private float fontPreview=20;

    public MainWindow(Configuration config,ModuleManager modules):base("小烟酒 · Yanjiu###YanjiuMain")
    {
        this.config=config;this.modules=modules;try{drCatalog=DRCatalog.Load().ToArray();}catch(Exception e){drCatalogError=e.Message;}
        Size=new Vector2(1120,740);SizeCondition=ImGuiCond.FirstUseEver;
        SizeConstraints=new WindowSizeConstraints{MinimumSize=new Vector2(880,540),MaximumSize=new Vector2(float.MaxValue)};
        Services.IconPicker=callback=>OmniModuleFactory.GetIconBrowser().OpenForSelection(false,callback,null);
        foreach(var address in config.Repositories.ToArray())Fetch(address);
    }

    public override void Draw()
    {
        while(uiActions.TryDequeue(out var action))action();Themes.Configure(config);
        if(Environment.TickCount64>=nextRefresh){nextRefresh=Environment.TickCount64+900_000;foreach(var address in config.Repositories.ToArray())if(config.RepositoryAutoRefresh.GetValueOrDefault(address))Fetch(address);}
        var pos=ImGui.GetWindowPos();var size=ImGui.GetWindowSize();
        ImGui.GetBackgroundDrawList().AddRectFilled(pos+new Vector2(6),pos+size+new Vector2(6),ImGui.ColorConvertFloat4ToU32(Themes.Current.Shadow),Themes.Current.BorderRadius*Themes.Scale);
        ImGui.BeginChild("sidebar",new Vector2(170*Themes.Scale,0),true);
        ImGui.Spacing();ImGui.TextUnformatted("小烟酒");ImGui.TextDisabled("Yanjiu · 0.1.5");ImGui.Spacing();ImGui.Separator();ImGui.Spacing();
        string[] pages=["首页","模块","外观","诊断"];
        for(int i=0;i<pages.Length;i++){bool active=page==i;if(active)ImGui.PushStyleColor(ImGuiCol.Button,Themes.Current.Selected);if(ThemeWidgets.Button(pages[i],new Vector2(-1,40*Themes.Scale)))page=i;if(active)ImGui.PopStyleColor();}
        if(ThemeWidgets.Button("图标浏览器",new Vector2(-1,40*Themes.Scale)))OmniModuleFactory.GetIconBrowser().Toggle();
        ImGui.EndChild();ImGui.SameLine();ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding,new Vector2(18)*Themes.Scale);ImGui.BeginChild("content",Vector2.Zero,false,ImGuiWindowFlags.AlwaysUseWindowPadding);ImGui.PopStyleVar();
        switch(page){case 0:DrawHome();break;case 1:DrawModules();break;case 2:DrawAppearance();break;case 3:DrawDiagnostics();break;}
        ImGui.EndChild();
    }

    private static bool IconButton(string icon,string id,string tooltip)
    {
        bool clicked=ThemeWidgets.Button(icon+"##"+id,new Vector2(ImGui.GetFrameHeight()));
        if(ImGui.IsItemHovered())ImGui.SetTooltip(tooltip);return clicked;
    }

    private void DrawHome()
    {
        if(ImGui.CollapsingHeader("在线模块地址",ImGuiTreeNodeFlags.DefaultOpen)){
            if(ImGui.BeginTable("repositories",3,ImGuiTableFlags.SizingStretchProp)){
                ImGui.TableSetupColumn("address",ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("automatic",ImGuiTableColumnFlags.WidthFixed,120*Themes.Scale);
                ImGui.TableSetupColumn("actions",ImGuiTableColumnFlags.WidthFixed,100*Themes.Scale);
                ImGui.TableNextColumn();ImGui.SetNextItemWidth(-1);ImGui.InputTextWithHint("##newRepository","GitHub 仓库地址或 TreeHouseModules.json 地址",ref repository,2000);
                ImGui.TableNextColumn();ImGui.Checkbox("自动刷新##new",ref newAutoRefresh);
                if(ImGui.IsItemHovered())ImGui.SetTooltip("自动刷新清单；下载和更新模块仍由你点击确认");
                ImGui.TableNextColumn();if(IconButton(Plus,"addRepository","添加在线模块地址"))AddRepository();
                foreach(var address in config.Repositories.ToArray()){
                    ImGui.PushID(address);ImGui.TableNextColumn();string display=address;ImGui.SetNextItemWidth(-1);ImGui.InputText("##address",ref display,2000,ImGuiInputTextFlags.ReadOnly);
                    if(sourceStatus.TryGetValue(address,out var message)&&message.Length>0)ImGui.TextWrapped(message);
                    ImGui.TableNextColumn();bool automatic=config.RepositoryAutoRefresh.GetValueOrDefault(address);if(ImGui.Checkbox("自动刷新",ref automatic)){config.RepositoryAutoRefresh[address]=automatic;config.Save();}
                    ImGui.TableNextColumn();ImGui.BeginDisabled(fetching.Contains(address));if(IconButton(Refresh,"refresh","刷新清单并恢复已移除的候选模块"))Fetch(address,true);ImGui.EndDisabled();ImGui.SameLine();if(IconButton(Trash,"remove","删除地址并卸载该来源的模块；保留磁盘文件"))RemoveRepository(address);
                    ImGui.PopID();
                }
                ImGui.EndTable();
            }
        }
        ImGui.Spacing();
        if(ImGui.CollapsingHeader("本地模块地址",ImGuiTreeNodeFlags.DefaultOpen)){
            if(ImGui.BeginTable("localSources",2,ImGuiTableFlags.SizingStretchProp)){
                ImGui.TableSetupColumn("path",ImGuiTableColumnFlags.WidthStretch);ImGui.TableSetupColumn("actions",ImGuiTableColumnFlags.WidthFixed,100*Themes.Scale);
                ImGui.TableNextColumn();ImGui.SetNextItemWidth(-1);ImGui.InputTextWithHint("##newLocal","本地 .dll 或 .cs 文件路径",ref localPath,2000);
                ImGui.TableNextColumn();if(IconButton(Plus,"addLocal","添加本地模块")){modules.Import(localPath);localPath="";}
                foreach(var pref in config.Modules.Where(p=>p.Origin=="本地"&&!p.Path.StartsWith("builtin:")).ToArray()){
                    ImGui.PushID(pref.Path);ImGui.TableNextColumn();string display=pref.Path;ImGui.SetNextItemWidth(-1);ImGui.InputText("##path",ref display,2000,ImGuiInputTextFlags.ReadOnly);
                    ImGui.TableNextColumn();if(IconButton(Refresh,"reload","重新读取本地文件"))modules.Import(pref.Path,pref.Origin,pref.Hash);ImGui.SameLine();if(IconButton(Trash,"remove","移除本地地址并卸载模块；保留原文件"))RemovePath(pref.Path);ImGui.PopID();
                }
                ImGui.EndTable();
            }
        }
        if(status.Length>0)ImGui.TextWrapped(status);
        if(modules.Status.Length>0)ImGui.TextWrapped(modules.Status);
    }

    private void AddRepository()
    {
        var address=repository.Trim();try{
            var canonical=OnlineRepository.ManifestUrl(address);
            if(config.Repositories.Any(existing=>SameRepository(existing,canonical))){status="这个在线地址已经添加";return;}
            config.Repositories.Add(address);config.RepositoryAutoRefresh[address]=newAutoRefresh;config.Save();repository="";status="";Fetch(address,true);
        }catch(Exception e){status=e.Message;}
    }
    private static bool SameRepository(string address,Uri canonical){try{return OnlineRepository.ManifestUrl(address)==canonical;}catch{return false;}}
    private void RemovePath(string path)
    {
        if(selected?.Path.Equals(path,StringComparison.OrdinalIgnoreCase)==true)selected=null;
        var entry=modules.Entries.FirstOrDefault(e=>e.Path.Equals(path,StringComparison.OrdinalIgnoreCase));
        if(entry?.Origin=="在线"&&!ModuleFileActions.IsManagedDownload(Services.ConfigDirectory,path)){status="此文件不在 Yanjiu 在线下载目录中，不能删除";return;}
        var name=entry?.Instance?.ModuleName??Path.GetFileNameWithoutExtension(path);
        modules.Forget([path]);
        if(entry?.Origin=="在线"){try{if(File.Exists(path))File.Delete(path);config.HiddenAvailableModules.Remove(name);config.Save();status="已删除在线模块，可从在线列表重新下载";}catch(Exception e){status="模块已卸载，但文件删除失败："+e.Message;}}
    }
    private void RemoveRepository(string address)
    {
        string? directory=null;try{directory=SourceDirectory(OnlineRepository.ManifestUrl(address));}catch{}
        var paths=config.Modules.Where(p=>p.Origin=="在线"&&(p.Repository==address||directory!=null&&Path.GetDirectoryName(p.Path)?.Equals(directory,StringComparison.OrdinalIgnoreCase)==true)).Select(p=>p.Path).ToList();
        if(listings.TryGetValue(address,out var listing))paths.AddRange(listing.Modules.Select(item=>ModulePath(listing,item)));
        if(selected!=null&&paths.Contains(selected.Path))selected=null;modules.Forget(paths);
        config.Repositories.Remove(address);config.RepositoryAutoRefresh.Remove(address);listings.Remove(address);sourceStatus.Remove(address);config.Save();
    }
    private static string SourceDirectory(Uri manifest)=>Path.GetFullPath(Path.Combine(Services.ConfigDirectory,"online",Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(manifest.ToString())))[..12]));
    private static string ModulePath(OnlineListing listing,OnlineModule item)=>Path.Combine(SourceDirectory(listing.ManifestUri),item.InternalName+Path.GetExtension(item.File));

    private void DrawModules()
    {
        ImGui.BeginChild("moduleCategories",new Vector2(115*Themes.Scale,0),false);
        string[] categories=["通用","系统","系统界面","战斗","通知","脚本","辅助","副本","招募","技能","生产采集","金碟","物品","日常","自动化"];
        foreach(var category in categories){
            var label=category=="通用"?"一般":category=="系统界面"?"界面":category;
            if(ImGui.Selectable(label+"##category",moduleFilter==category,ImGuiSelectableFlags.None,new Vector2(0,30*Themes.Scale)))moduleFilter=category;
        }
        ImGui.EndChild();ImGui.SameLine();
        ImGui.BeginChild("moduleList",Vector2.Zero,false);
        ImGui.SetNextItemWidth(-1);ImGui.InputTextWithHint("##search","搜索模块名称或说明",ref search,200);
        foreach(var filter in new[]{"全部","已启用","收藏","在线模块","本地模块"}){
            if(ThemeWidgets.Button((moduleFilter==filter?"● ":"")+filter+"##moduleFilter"))moduleFilter=filter;
            ImGui.SameLine();
        }
        ImGui.NewLine();
        ImGui.TextDisabled($"DR 模块 {modules.Entries.Count} · 已启用 {modules.Entries.Count(e=>e.Instance?.IsEnabled==true)}");
        foreach(var entry in modules.Entries.Where(e=>Matches(e.Title+(e.Instance?.Descriptor.Description??e.Catalog?.Description)+e.Instance?.Descriptor.Author)&& ModuleFilters.Matches(e,moduleFilter)).OrderByDescending(e=>e.Preference.Favorite).ThenBy(e=>e.Title).ToArray())DrawEntry(entry);
        if(drCatalogError.Length>0)ImGui.TextWrapped(drCatalogError);
        if(moduleFilter=="全部"||moduleFilter=="在线模块"){
            foreach(var pair in listings.ToArray())foreach(var item in pair.Value.Modules){
                if(config.HiddenAvailableModules.Contains(item.InternalName))continue;
                var path=ModulePath(pair.Value,item);
                if(modules.Entries.Any(e=>e.Path.Equals(path,StringComparison.OrdinalIgnoreCase)))continue;
                if(Matches(item.Name+item.Description+item.Author))DrawAvailable(pair.Key,pair.Value,item);
            }
        }
        if(modules.Status.Length>0)ImGui.TextWrapped(modules.Status);
        if(status.Length>0)ImGui.TextWrapped(status);
        ImGui.EndChild();
    }
    private bool Matches(string text)=>search.Length==0||text.Contains(search,StringComparison.OrdinalIgnoreCase);

    private Vector2 BeginCard(string id)
    {
        ImGui.PushID(id);var start=ImGui.GetCursorScreenPos();var width=ImGui.GetContentRegionAvail().X;
        float height=cardHeights.GetValueOrDefault(id,82*Themes.Scale);var draw=ImGui.GetWindowDrawList();
        draw.AddRectFilled(start+new Vector2(4),start+new Vector2(width+4,height+4),ImGui.ColorConvertFloat4ToU32(Themes.Current.Shadow),Themes.Current.BorderRadius*Themes.Scale);
        draw.AddRectFilled(start,start+new Vector2(width,height),ImGui.ColorConvertFloat4ToU32(Themes.Current.Surface),Themes.Current.BorderRadius*Themes.Scale);
        ImGui.BeginGroup();ImGui.Dummy(new Vector2(0,10*Themes.Scale));ImGui.Indent(12*Themes.Scale);return new(start.X,start.Y);
    }
    private void EndCard(string id,Vector2 start)
    {
        ImGui.Unindent(12*Themes.Scale);ImGui.Dummy(new Vector2(0,12*Themes.Scale));ImGui.EndGroup();
        var end=ImGui.GetItemRectMax();float width=ImGui.GetContentRegionAvail().X;float height=end.Y-start.Y;cardHeights[id]=height;
        ImGui.GetWindowDrawList().AddRect(start,new Vector2(start.X+width,end.Y),ImGui.ColorConvertFloat4ToU32(Themes.Current.Border),12*Themes.Scale,ImDrawFlags.None,Themes.Current.BorderThickness*Themes.Scale);
        ImGui.PopID();ImGui.Dummy(new Vector2(0,10*Themes.Scale));
    }
    private bool BeginHeader(string author){float available=ImGui.GetContentRegionAvail().X-12*Themes.Scale;float width=Math.Clamp(ImGui.CalcTextSize("作者："+author).X+12*Themes.Scale,150*Themes.Scale,Math.Max(150*Themes.Scale,available*.65f));if(!ImGui.BeginTable("header",3,ImGuiTableFlags.SizingStretchProp,new Vector2(available,0)))return false;ImGui.TableSetupColumn("toggle",ImGuiTableColumnFlags.WidthFixed,36*Themes.Scale);ImGui.TableSetupColumn("title",ImGuiTableColumnFlags.WidthStretch);ImGui.TableSetupColumn("actions",ImGuiTableColumnFlags.WidthFixed,width);return true;}
    private static void Author(string author){string text="作者："+author;float x=ImGui.GetCursorPosX(),available=ImGui.GetContentRegionAvail().X,width=ImGui.CalcTextSize(text).X;if(width<=available){ImGui.SetCursorPosX(x+available-width);ImGui.TextDisabled(text);}else{ImGui.PushStyleColor(ImGuiCol.Text,new Vector4(Themes.Current.Text.X,Themes.Current.Text.Y,Themes.Current.Text.Z,.65f));ImGui.TextWrapped(text);ImGui.PopStyleColor();}ImGui.SetCursorPosX(x);}
    private static void AlignButtons(int count){float width=count*ImGui.GetFrameHeight()+(count-1)*ImGui.GetStyle().ItemSpacing.X;ImGui.SetCursorPosX(ImGui.GetCursorPosX()+Math.Max(0,ImGui.GetContentRegionAvail().X-width));}

    private void DrawEntry(ModuleEntry entry)
    {
        var start=BeginCard(entry.Path);bool remove=false;
        var author=entry.Instance?.Descriptor.Author;
        bool showAuthor=!string.IsNullOrWhiteSpace(author)&&ImGui.GetContentRegionAvail().X>620*Themes.Scale;
        if(ImGui.BeginTable("moduleHeader",showAuthor?5:4,ImGuiTableFlags.SizingStretchProp)){
            ImGui.TableSetupColumn("enabled",ImGuiTableColumnFlags.WidthFixed,32*Themes.Scale);
            ImGui.TableSetupColumn("expand",ImGuiTableColumnFlags.WidthFixed,28*Themes.Scale);
            ImGui.TableSetupColumn("title",ImGuiTableColumnFlags.WidthStretch);
            if(showAuthor)ImGui.TableSetupColumn("author",ImGuiTableColumnFlags.WidthFixed,Math.Min(180*Themes.Scale,ImGui.GetContentRegionAvail().X*.23f));
            ImGui.TableSetupColumn("actions",ImGuiTableColumnFlags.WidthFixed,(entry.Origin=="在线"?112:36)*Themes.Scale);
            ImGui.TableNextColumn();
            bool enabled=entry.Instance?.IsEnabled==true;ImGui.BeginDisabled(entry.Error.Length>0);
            if(ImGui.Checkbox("##enabled",ref enabled)){ClearSettingsError(entry);modules.Enable(entry,enabled);}ImGui.EndDisabled();
            ImGui.TableNextColumn();
            bool open=selected?.Path==entry.Path;
            if(ImGui.ArrowButton("##settings",open?ImGuiDir.Down:ImGuiDir.Right))selected=open?null:entry;
            if(ImGui.IsItemHovered())ImGui.SetTooltip(open?"收起设置":"展开设置");
            ImGui.TableNextColumn();ImGui.AlignTextToFramePadding();
            ImGui.TextWrapped((entry.Preference.Favorite?"★ ":"")+entry.Title);
            if(ImGui.IsItemClicked())selected=open?null:entry;
            if(showAuthor){
                ImGui.TableNextColumn();using var small=UiFonts.Get(.82f).Push();
                ImGui.AlignTextToFramePadding();ImGui.TextDisabled("作者："+author);
            }
            ImGui.TableNextColumn();
            if(entry.Origin=="在线"){if(ThemeWidgets.Button("删除##deleteOnline"))remove=true;ImGui.SameLine();}
            if(IconButton(Menu,"moduleMenu","模块操作"))ImGui.OpenPopup("actions");
            if(ImGui.BeginPopup("actions")){
                if(ImGui.MenuItem(entry.Preference.Favorite?"取消收藏":"收藏")){entry.Preference.Favorite=!entry.Preference.Favorite;config.Save();}
                if(entry.Compiled!=null&&ImGui.MenuItem("重载"))modules.Import(entry.Path,entry.Origin,entry.Preference.Hash,entry.Preference.Repository);
                if(entry.Origin=="在线"&&ImGui.MenuItem("删除并卸载"))remove=true;
                ImGui.EndPopup();
            }
            ImGui.EndTable();
        }
        ImGui.Indent(46*Themes.Scale);
        var description=entry.Instance?.Descriptor.Description??entry.Catalog?.Description;
        if(!string.IsNullOrWhiteSpace(description)){
            using var small=UiFonts.Get(.85f).Push();
            ImGui.PushStyleColor(ImGuiCol.Text,Vector4.Lerp(Themes.Current.Text,Themes.Current.Surface,.42f));
            ImGui.TextWrapped(description);ImGui.PopStyleColor();
        }
        DrawRelationships(entry);
        if(entry.DependencyNotice.Length>0&&ImGui.IsItemHovered())ImGui.SetTooltip(entry.DependencyNotice);
        if(entry.Error.Length>0){
            using var small=UiFonts.Get(.85f).Push();
            ImGui.PushStyleColor(ImGuiCol.Text,Themes.Current.Error);ImGui.TextWrapped(entry.Error);ImGui.PopStyleColor();
        }
        if(selected?.Path==entry.Path){
            ImGui.Separator();
            if(entry.Instance is OmniToolbox.Common.Module.Abstractions.ModuleBase){var macro="/yanjiu "+entry.Instance.ModuleName;ImGui.TextDisabled("Yanjiu 宏命令："+macro);ImGui.SameLine();if(ThemeWidgets.Button("复制宏命令"))ImGui.SetClipboardText(macro);}
            if(entry.Instance==null)ImGui.TextDisabled(entry.Error.Length>0?"此模块仍需适配，暂时无法打开设置":"模块正在加载，请稍候");
            else if(!entry.Instance.IsEnabled){ try{disabledSettings.Draw(entry);}catch(Exception e){ImGui.TextWrapped("配置读取失败："+e.GetBaseException().Message);} }
            else if(entry.Instance.HasSettings){
                disabledSettings.Forget(entry);
                try{if(entry.DrawSettings())config.Save();}
                catch(Exception e){Services.Log.Error(e,"Settings UI failed for {Module}",entry.Title);modules.Enable(entry,false);entry.Error="设置界面错误："+e.GetBaseException().Message;selected=null;}
            }else ImGui.TextDisabled("此模块没有额外设置。");
        }
        ImGui.Unindent(46*Themes.Scale);
        EndCard(entry.Path,start);if(remove)RemovePath(entry.Path);
    }

    private void DrawRelationships(ModuleEntry entry)
    {
        if(entry.Source!="DR")return;
        var info=(entry.Instance as DailyRoutines.Common.Module.Abstractions.ModuleBase)?.Info;
        var prerequisites=info?.ModulesPrerequisite??entry.Catalog?.Prerequisites??[];
        var id=entry.Instance?.ModuleName??entry.Catalog?.Id??"";
        var conflicts=(info?.ModulesConflict??entry.Catalog?.Conflicts??[]).Concat(modules.Entries.Where(other=>other.Source=="DR"&&((other.Instance as DailyRoutines.Common.Module.Abstractions.ModuleBase)?.Info.ModulesConflict??other.Catalog?.Conflicts??[]).Contains(id)).Select(other=>other.Instance?.ModuleName??other.Catalog!.Id)).Distinct().ToArray();
        var pairs=info?.ModulesPair??entry.Catalog?.Pairs??[];
        ModuleEntry? Find(string name)=>modules.Entries.FirstOrDefault(other=>other.Source=="DR"&&(other.Instance?.ModuleName??other.Catalog?.Id)==name);
        using var small=UiFonts.Get(.85f).Push();
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding,new Vector2(7,2)*Themes.Scale);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding,5*Themes.Scale);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize,0);
        ImGui.PushStyleColor(ImGuiCol.Button,Themes.Current.Selected);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered,Vector4.Lerp(Themes.Current.Selected,Themes.Current.Accent,.2f));
        bool first=true;float lastRight=0;float rowRight=ImGui.GetCursorScreenPos().X+ImGui.GetContentRegionAvail().X;
        void Tag(string label,string popup,string[] names){
            if(names.Length==0)return;
            int on=names.Count(name=>Find(name)?.Instance?.IsEnabled==true);
            var text=$"{label}（{on}/{names.Length}）";
            float width=ImGui.CalcTextSize(text).X+14*Themes.Scale;
            if(!first&&lastRight+ImGui.GetStyle().ItemSpacing.X+width<=rowRight)ImGui.SameLine();
            if(ImGui.Button(text+"##"+popup))ImGui.OpenPopup(popup);
            lastRight=ImGui.GetItemRectMax().X;
            first=false;
            if(ImGui.BeginPopup(popup)){
                foreach(var name in names){
                    var target=Find(name);
                    string title=target?.Instance?.Descriptor.Title??target?.Catalog?.Title??name;
                    bool active=target?.Instance?.IsEnabled==true;
                    bool unavailable=target==null||target.Error.Length>0;
                    ImGui.BeginDisabled(unavailable);
                    if(ImGui.Checkbox(title+"##"+name,ref active)&&target!=null){
                        ClearSettingsError(target);
                        if(!modules.Enable(target,active))status=modules.Status;
                    }
                    ImGui.EndDisabled();
                    if(unavailable&&ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))ImGui.SetTooltip(target?.Error??"尚未收录该模块");
                }
                ImGui.EndPopup();
            }
        }
        string category=entry.Instance?.Descriptor.Category??entry.Catalog?.Category??"";
        var categoryName=category switch{"Interface"=>"界面","System"=>"系统","Combat"=>"战斗","Action"=>"技能","Assist"=>"辅助","CraftGather"=>"生产采集","Duty"=>"副本","Notification"=>"通知","Recruitment"=>"招募","Script"=>"脚本","GoldSaucer"=>"金碟","Item"=>"物品","General"=>"一般",_=>category};
        if(ImGui.Button(categoryName+"##categoryTag"))moduleFilter=category=="General"?"通用":category=="Interface"?"系统界面":categoryName;
        first=false;
        Tag("前置模块","prerequisites",prerequisites);
        Tag("冲突模块","conflicts",conflicts);
        Tag("可搭配模块","pairs",pairs);
        ImGui.PopStyleColor(2);ImGui.PopStyleVar(3);
    }
    private (string Address,OnlineListing Listing,OnlineModule Item)? FindOnline(ModuleEntry entry)
    {
        foreach(var pair in listings)foreach(var item in pair.Value.Modules)if(ModulePath(pair.Value,item).Equals(entry.Path,StringComparison.OrdinalIgnoreCase))return(pair.Key,pair.Value,item);return null;
    }
    private void DrawAvailable(string address,OnlineListing listing,OnlineModule item)
    {
        var path=ModulePath(listing,item);var start=BeginCard(path);bool remove=false;
        if(BeginHeader(item.Author)){
            ImGui.TableNextColumn();bool off=false;ImGui.BeginDisabled();ImGui.Checkbox("##notInstalled",ref off);ImGui.EndDisabled();
            ImGui.TableNextColumn();ImGui.TextUnformatted(item.Name);if(item.Description.Length>0)ImGui.TextWrapped(item.Description);
            ImGui.TableNextColumn();Author(item.Author);AlignButtons(2);ImGui.BeginDisabled(installing.Contains(path));if(IconButton(Download,"download","下载并导入模块（默认停用）"))Install(address,listing,item);ImGui.EndDisabled();ImGui.SameLine();if(IconButton(Trash,"hide","从列表移除；刷新来源可恢复"))remove=true;ImGui.EndTable();
        }
        EndCard(path,start);if(remove){if(!config.HiddenAvailableModules.Contains(item.InternalName))config.HiddenAvailableModules.Add(item.InternalName);modules.Forget([path]);}
    }
    private static void ClearSettingsError(ModuleEntry entry){if(entry.Error.StartsWith("设置界面错误：",StringComparison.Ordinal))entry.Error="";}

    private void DrawAppearance()
    {
        for(int i=0;i<Themes.Available.Length;i++){
            ImGui.PushID(i);if(ThemeWidgets.Button(Themes.Available[i].Name,new Vector2(120,44))){config.Theme=i;config.ThemeColors.Clear();config.Save();Themes.Configure(config);}
            ImGui.PopID();if(i%3!=2)ImGui.SameLine();
        }
        ImGui.Spacing();var p=Themes.Current;
        if(ImGui.BeginTable("colors",2,ImGuiTableFlags.SizingStretchProp)){
            ColorControl("主体","body",p.Background);ColorControl("面板","surface",p.Surface);ColorControl("阴影","shadow",p.Shadow);ColorControl("边框","border",p.Border);ColorControl("选中状态","selected",p.Selected);ColorControl("强调色","accent",p.Accent);ColorControl("文字","text",p.Text);ImGui.EndTable();
        }
        float border=config.BorderWidth;ImGui.SetNextItemWidth(280);if(ImGui.SliderFloat("边框粗细",ref border,1,4,"%.2f")){config.BorderWidth=border;config.Save();}
        if(ThemeWidgets.Button("恢复当前主题配色")){config.ThemeColors.Clear();config.BorderWidth=2.25f;config.Save();}
        ImGui.Separator();var scale=config.Scale;ImGui.SetNextItemWidth(280);if(ImGui.SliderFloat("界面缩放",ref scale,.8f,1.5f,"%.2f")){config.Scale=scale;config.Save();}
        var fontSize=config.FontSize;ImGui.SetNextItemWidth(280);if(ImGui.SliderFloat("字号",ref fontSize,16,28,"%.0f"))fontPreview=fontSize;if(ImGui.IsItemDeactivatedAfterEdit()){config.FontSize=MathF.Round(fontPreview);config.Save();}
        int family=config.FontFamily;ImGui.SetNextItemWidth(280);if(ImGui.Combo("字体",ref family,UiFonts.Families,UiFonts.Families.Length)){config.FontFamily=family;config.Save();}
    }
    private void ColorControl(string label,string key,Vector4 value)
    {
        ImGui.TableNextColumn();ImGui.TextUnformatted(label);ImGui.TableNextColumn();ImGui.SetNextItemWidth(-1);
        if(ImGui.ColorEdit4("##"+key,ref value,ImGuiColorEditFlags.AlphaBar)){config.ThemeColors[key]=[value.X,value.Y,value.Z,value.W];config.Save();}
    }

    private async void Fetch(string address,bool restoreHidden=false)
    {
        if(!fetching.Add(address))return;sourceStatus[address]="读取中…";
        try{var result=await online.Fetch(address,stop.Token);uiActions.Enqueue(()=>{if(stop.IsCancellationRequested||!config.Repositories.Contains(address))return;listings[address]=result;sourceStatus[address]="";if(restoreHidden){config.HiddenAvailableModules.RemoveAll(name=>result.Modules.Any(item=>item.InternalName==name));config.Save();}});}
        catch(Exception e){string message=e.GetBaseException().Message;uiActions.Enqueue(()=>{if(config.Repositories.Contains(address))sourceStatus[address]=message;});}
        finally{uiActions.Enqueue(()=>fetching.Remove(address));}
    }
    private async void Install(string address,OnlineListing listing,OnlineModule item)
    {
        var path=ModulePath(listing,item);if(!installing.Add(path))return;
        try{
            var bytes=await online.Download(listing,item,stop.Token);if(stop.IsCancellationRequested)return;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.WriteAllBytes(path+".tmp",bytes);File.Move(path+".tmp",path,true);
            uiActions.Enqueue(()=>{if(stop.IsCancellationRequested||!config.Repositories.Contains(address)||config.HiddenAvailableModules.Contains(item.InternalName))return;modules.Import(path,"在线",repository:address);status="校验通过，正在导入 "+item.Name;});
        }catch(Exception e){var message=e.GetBaseException().Message;uiActions.Enqueue(()=>status=message);}
        finally{uiActions.Enqueue(()=>installing.Remove(path));}
    }

    private void DrawDiagnostics(){ImGui.TextWrapped(modules.Status);foreach(var entry in modules.Entries.Where(e=>e.Error.Length>0)){ImGui.Separator();ImGui.TextUnformatted(entry.Title);ImGui.TextWrapped(entry.Error);}foreach(var pair in sourceStatus.Where(p=>p.Value.Length>0)){ImGui.TextWrapped(pair.Key);ImGui.TextWrapped(pair.Value);}}
    public void Dispose(){stop.Cancel();online.Dispose();Services.IconPicker=null;}
}























