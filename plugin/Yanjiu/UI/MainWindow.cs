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
    private int page;
    private string localPath="",repository="",search="",status="";
    private bool newAutoRefresh;
    private long nextRefresh=Environment.TickCount64+900_000;
    private ModuleEntry? selected;
    private Action<uint>? iconSelection;
    private uint iconId=60001;
    private float fontPreview=20;

    public MainWindow(Configuration config,ModuleManager modules):base("小烟酒 · Yanjiu###YanjiuMain")
    {
        this.config=config;this.modules=modules;
        Size=new Vector2(1120,740);SizeCondition=ImGuiCond.FirstUseEver;
        SizeConstraints=new WindowSizeConstraints{MinimumSize=new Vector2(880,540),MaximumSize=new Vector2(float.MaxValue)};
        Services.IconPicker=callback=>iconSelection=callback;
        foreach(var address in config.Repositories.ToArray())Fetch(address);
    }

    public override void Draw()
    {
        while(uiActions.TryDequeue(out var action))action();Themes.Configure(config);
        if(Environment.TickCount64>=nextRefresh){nextRefresh=Environment.TickCount64+900_000;foreach(var address in config.Repositories.ToArray())if(config.RepositoryAutoRefresh.GetValueOrDefault(address))Fetch(address);}
        var pos=ImGui.GetWindowPos();var size=ImGui.GetWindowSize();
        ImGui.GetBackgroundDrawList().AddRectFilled(pos+new Vector2(6),pos+size+new Vector2(6),ImGui.ColorConvertFloat4ToU32(Themes.Current.Shadow),14*Themes.Scale);
        ImGui.BeginChild("sidebar",new Vector2(170*Themes.Scale,0),true);
        ImGui.Spacing();ImGui.TextUnformatted("小烟酒");ImGui.TextDisabled("Yanjiu · 0.1.0");ImGui.Spacing();ImGui.Separator();ImGui.Spacing();
        string[] pages=["首页","模块","外观","诊断"];
        for(int i=0;i<pages.Length;i++){bool active=page==i;if(active)ImGui.PushStyleColor(ImGuiCol.Button,Themes.Current.Selected);if(ImGui.Button(pages[i],new Vector2(-1,40*Themes.Scale)))page=i;if(active)ImGui.PopStyleColor();}
        ImGui.EndChild();ImGui.SameLine();ImGui.BeginChild("content",Vector2.Zero,false);
        switch(page){case 0:DrawHome();break;case 1:DrawModules();break;case 2:DrawAppearance();break;case 3:DrawDiagnostics();break;}
        DrawIconPicker();ImGui.EndChild();
    }

    private static bool IconButton(string icon,string id,string tooltip)
    {
        bool clicked=ImGui.Button(icon+"##"+id,new Vector2(ImGui.GetFrameHeight()));
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
                    ImGui.TableNextColumn();ImGui.BeginDisabled(fetching.Contains(address));if(IconButton(Refresh,"refresh","刷新模块清单"))Fetch(address);ImGui.EndDisabled();ImGui.SameLine();if(IconButton(Trash,"remove","删除地址并卸载该来源的模块；保留磁盘文件"))RemoveRepository(address);
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
        if(status.Length>0)ImGui.TextWrapped(status);if(modules.Status.Length>0)ImGui.TextWrapped(modules.Status);
    }

    private void AddRepository()
    {
        var address=repository.Trim();try{
            var canonical=OnlineRepository.ManifestUrl(address);
            if(config.Repositories.Any(existing=>SameRepository(existing,canonical))){status="这个在线地址已经添加";return;}
            config.Repositories.Add(address);config.RepositoryAutoRefresh[address]=newAutoRefresh;config.Save();repository="";status="";Fetch(address);
        }catch(Exception e){status=e.Message;}
    }
    private static bool SameRepository(string address,Uri canonical){try{return OnlineRepository.ManifestUrl(address)==canonical;}catch{return false;}}
    private void RemovePath(string path)
    {
        if(selected?.Path.Equals(path,StringComparison.OrdinalIgnoreCase)==true)selected=null;modules.Forget([path]);
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
        ImGui.SetNextItemWidth(-1);ImGui.InputTextWithHint("##search","搜索模块名称或说明",ref search,200);ImGui.Spacing();
        foreach(var entry in modules.Entries.Where(e=>Matches(e.Title+e.Instance?.Descriptor.Description+e.Instance?.Descriptor.Author)).ToArray())DrawEntry(entry);
        foreach(var pair in listings.ToArray())foreach(var item in pair.Value.Modules){
            if(modules.Entries.Any(e=>e.Path.Equals(ModulePath(pair.Value,item),StringComparison.OrdinalIgnoreCase)||e.Origin=="内置"&&e.Instance?.ModuleName==item.InternalName))continue;
            if(Matches(item.Name+item.Description+item.Author))DrawAvailable(pair.Key,pair.Value,item);
        }
        if(modules.Status.Length>0)ImGui.TextWrapped(modules.Status);if(status.Length>0)ImGui.TextWrapped(status);
    }
    private bool Matches(string text)=>search.Length==0||text.Contains(search,StringComparison.OrdinalIgnoreCase);

    private Vector2 BeginCard(string id)
    {
        ImGui.PushID(id);var start=ImGui.GetCursorScreenPos();var width=ImGui.GetContentRegionAvail().X;
        float height=cardHeights.GetValueOrDefault(id,82*Themes.Scale);var draw=ImGui.GetWindowDrawList();
        draw.AddRectFilled(start+new Vector2(4),start+new Vector2(width+4,height+4),ImGui.ColorConvertFloat4ToU32(Themes.Current.Shadow),12*Themes.Scale);
        draw.AddRectFilled(start,start+new Vector2(width,height),ImGui.ColorConvertFloat4ToU32(Themes.Current.Surface),12*Themes.Scale);
        ImGui.BeginGroup();ImGui.Dummy(new Vector2(0,10*Themes.Scale));ImGui.Indent(12*Themes.Scale);return new(start.X,start.Y);
    }
    private void EndCard(string id,Vector2 start)
    {
        ImGui.Unindent(12*Themes.Scale);ImGui.Dummy(new Vector2(0,12*Themes.Scale));ImGui.EndGroup();
        var end=ImGui.GetItemRectMax();float width=ImGui.GetContentRegionAvail().X;float height=end.Y-start.Y;cardHeights[id]=height;
        ImGui.GetWindowDrawList().AddRect(start,new Vector2(start.X+width,end.Y),ImGui.ColorConvertFloat4ToU32(Themes.Current.Border),12*Themes.Scale,ImDrawFlags.None,Themes.BorderWidth*Themes.Scale);
        ImGui.PopID();ImGui.Dummy(new Vector2(0,10*Themes.Scale));
    }
    private bool BeginHeader(){if(!ImGui.BeginTable("header",3,ImGuiTableFlags.SizingStretchProp))return false;ImGui.TableSetupColumn("toggle",ImGuiTableColumnFlags.WidthFixed,36*Themes.Scale);ImGui.TableSetupColumn("title",ImGuiTableColumnFlags.WidthStretch);ImGui.TableSetupColumn("actions",ImGuiTableColumnFlags.WidthFixed,190*Themes.Scale);return true;}
    private static void Author(string author){ImGui.TextDisabled("作者："+(author.Length>12?author[..12]+"…":author));if(ImGui.IsItemHovered())ImGui.SetTooltip(author);}

    private void DrawEntry(ModuleEntry entry)
    {
        var start=BeginCard(entry.Path);bool remove=false;
        if(BeginHeader()){
            ImGui.TableNextColumn();bool enabled=entry.Instance?.IsEnabled==true;ImGui.BeginDisabled(entry.Instance==null);if(ImGui.Checkbox("##enabled",ref enabled)){ClearSettingsError(entry);modules.Enable(entry,enabled);}ImGui.EndDisabled();
            ImGui.TableNextColumn();if(ImGui.Selectable((selected==entry?"▼ ":"▶ ")+entry.Title,selected==entry))selected=selected==entry?null:entry;
            ImGui.TableNextColumn();Author(entry.Instance?.Descriptor.Author??entry.Origin);
            if(entry.Origin!="内置"){if(IconButton(Trash,"removeModule","移除模块"))remove=true;ImGui.SameLine();}
            if(IconButton(Menu,"moduleMenu","模块操作"))ImGui.OpenPopup("actions");
            if(ImGui.BeginPopup("actions")){
                if(entry.Compiled!=null&&ImGui.MenuItem("重载"))modules.Import(entry.Path,entry.Origin,entry.Preference.Hash,entry.Preference.Repository);
                if(entry.Origin=="在线"){
                    var update=FindOnline(entry);if(update!=null&&ImGui.MenuItem("下载更新"))Install(update.Value.Address,update.Value.Listing,update.Value.Item);
                }
                ImGui.EndPopup();
            }
            ImGui.EndTable();
        }
        if(entry.Error.Length>0)ImGui.TextColored(Themes.Current.Error,entry.Error);
        if(selected==entry&&entry.Instance!=null&&!remove){
            if(!string.IsNullOrWhiteSpace(entry.Instance.Descriptor.Description))ImGui.TextWrapped(entry.Instance.Descriptor.Description);ImGui.Separator();
            if(!entry.Instance.IsEnabled){if(ImGui.Button("启用并打开")){ClearSettingsError(entry);modules.Enable(entry,true);}}
            else if(entry.Instance.HasSettings){try{if(entry.DrawSettings())config.Save();}catch(Exception e){Services.Log.Error(e,"Settings UI failed for {Module}",entry.Title);modules.Enable(entry,false);entry.Error="设置界面错误："+e.GetBaseException().Message;selected=null;}}
        }
        EndCard(entry.Path,start);if(remove)RemovePath(entry.Path);
    }
    private (string Address,OnlineListing Listing,OnlineModule Item)? FindOnline(ModuleEntry entry)
    {
        foreach(var pair in listings)foreach(var item in pair.Value.Modules)if(ModulePath(pair.Value,item).Equals(entry.Path,StringComparison.OrdinalIgnoreCase))return(pair.Key,pair.Value,item);return null;
    }
    private void DrawAvailable(string address,OnlineListing listing,OnlineModule item)
    {
        var path=ModulePath(listing,item);var start=BeginCard(path);
        if(BeginHeader()){
            ImGui.TableNextColumn();bool off=false;ImGui.BeginDisabled();ImGui.Checkbox("##notInstalled",ref off);ImGui.EndDisabled();
            ImGui.TableNextColumn();ImGui.TextUnformatted(item.Name);if(item.Description.Length>0)ImGui.TextWrapped(item.Description);
            ImGui.TableNextColumn();Author(item.Author);ImGui.BeginDisabled(installing.Contains(path));if(IconButton(Download,"download","下载并导入模块（默认停用）"))Install(address,listing,item);ImGui.EndDisabled();ImGui.EndTable();
        }
        EndCard(path,start);
    }
    private static void ClearSettingsError(ModuleEntry entry){if(entry.Error.StartsWith("设置界面错误：",StringComparison.Ordinal))entry.Error="";}

    private void DrawAppearance()
    {
        for(int i=0;i<Themes.Available.Length;i++){
            ImGui.PushID(i);if(ImGui.Button(Themes.Available[i].Name,new Vector2(120,44))){config.Theme=i;config.ThemeColors.Clear();config.Save();}
            ImGui.PopID();if(i%3!=2)ImGui.SameLine();
        }
        ImGui.Spacing();var p=Themes.Current;
        if(ImGui.BeginTable("colors",2,ImGuiTableFlags.SizingStretchProp)){
            ColorControl("主体","body",p.Background);ColorControl("面板","surface",p.Surface);ColorControl("阴影","shadow",p.Shadow);ColorControl("边框","border",p.Border);ColorControl("选中状态","selected",p.Selected);ColorControl("强调色","accent",p.Accent);ColorControl("文字","text",p.Text);ImGui.EndTable();
        }
        float border=config.BorderWidth;ImGui.SetNextItemWidth(280);if(ImGui.SliderFloat("边框粗细",ref border,1,4,"%.2f")){config.BorderWidth=border;config.Save();}
        if(ImGui.Button("恢复当前主题配色")){config.ThemeColors.Clear();config.BorderWidth=2.25f;config.Save();}
        ImGui.Separator();var scale=config.Scale;ImGui.SetNextItemWidth(280);if(ImGui.SliderFloat("界面缩放",ref scale,.8f,1.5f,"%.2f")){config.Scale=scale;config.Save();}
        var fontSize=config.FontSize;ImGui.SetNextItemWidth(280);if(ImGui.SliderFloat("字号",ref fontSize,16,28,"%.0f"))fontPreview=fontSize;if(ImGui.IsItemDeactivatedAfterEdit()){config.FontSize=MathF.Round(fontPreview);config.Save();}
        int family=config.FontFamily;ImGui.SetNextItemWidth(280);if(ImGui.Combo("字体",ref family,UiFonts.Families,UiFonts.Families.Length)){config.FontFamily=family;config.Save();}
    }
    private void ColorControl(string label,string key,Vector4 value)
    {
        ImGui.TableNextColumn();ImGui.TextUnformatted(label);ImGui.TableNextColumn();ImGui.SetNextItemWidth(-1);
        if(ImGui.ColorEdit4("##"+key,ref value,ImGuiColorEditFlags.AlphaBar)){config.ThemeColors[key]=[value.X,value.Y,value.Z,value.W];config.Save();}
    }

    private async void Fetch(string address)
    {
        if(!fetching.Add(address))return;sourceStatus[address]="读取中…";
        try{var result=await online.Fetch(address,stop.Token);uiActions.Enqueue(()=>{if(stop.IsCancellationRequested||!config.Repositories.Contains(address))return;listings[address]=result;sourceStatus[address]="";});}
        catch(Exception e){string message=e.GetBaseException().Message;uiActions.Enqueue(()=>{if(config.Repositories.Contains(address))sourceStatus[address]=message;});}
        finally{uiActions.Enqueue(()=>fetching.Remove(address));}
    }
    private async void Install(string address,OnlineListing listing,OnlineModule item)
    {
        var path=ModulePath(listing,item);if(!installing.Add(path))return;
        try{
            var bytes=await online.Download(listing,item,stop.Token);if(stop.IsCancellationRequested)return;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.WriteAllBytes(path+".tmp",bytes);File.Move(path+".tmp",path,true);
            uiActions.Enqueue(()=>{if(stop.IsCancellationRequested||!config.Repositories.Contains(address))return;modules.Import(path,"在线",repository:address);status="校验通过，正在导入 "+item.Name;});
        }catch(Exception e){var message=e.GetBaseException().Message;uiActions.Enqueue(()=>status=message);}
        finally{uiActions.Enqueue(()=>installing.Remove(path));}
    }

    private void DrawDiagnostics(){ImGui.TextWrapped(modules.Status);foreach(var entry in modules.Entries.Where(e=>e.Error.Length>0)){ImGui.Separator();ImGui.TextUnformatted(entry.Title);ImGui.TextWrapped(entry.Error);}foreach(var pair in sourceStatus.Where(p=>p.Value.Length>0)){ImGui.TextWrapped(pair.Key);ImGui.TextWrapped(pair.Value);}}
    private void DrawIconPicker(){if(iconSelection==null)return;ImGui.Separator();ImGui.TextUnformatted("选择图标 ID");int id=(int)iconId;if(ImGui.InputInt("##iconId",ref id))iconId=(uint)Math.Max(0,id);var texture=Services.Textures.GetFromGameIcon(new Dalamud.Interface.Textures.GameIconLookup(iconId)).GetWrapOrDefault();if(texture!=null)ImGui.Image(texture.Handle,new Vector2(48));if(ImGui.Button("应用图标")){var callback=iconSelection;iconSelection=null;callback(iconId);}ImGui.SameLine();if(ImGui.Button("取消选择"))iconSelection=null;}
    public void Dispose(){stop.Cancel();online.Dispose();Services.IconPicker=null;}
}
