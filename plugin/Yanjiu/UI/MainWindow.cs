// SPDX-License-Identifier: GPL-3.0-or-later
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Yanjiu.Modules;
namespace Yanjiu.UI;
public sealed class MainWindow : Window, IDisposable
{
    private readonly Configuration config;private readonly ModuleManager modules;
    private readonly OnlineRepository online=new();private readonly CancellationTokenSource stop=new();
    private readonly System.Collections.Concurrent.ConcurrentQueue<Action> uiActions=new();
    private int page;private string localPath="",search="",repository="https://github.com/Xiao-Yanjiu/OmniToolbox.xiaoyanjiu",onlineStatus="";
    private OnlineListing? listing;private bool busy;private uint iconId=60001;private Action<uint>? iconSelection;
    public MainWindow(Configuration config,ModuleManager modules):base("小烟酒 · Yanjiu###YanjiuMain")
    {this.config=config;this.modules=modules;Size=new Vector2(1120,740);SizeCondition=ImGuiCond.FirstUseEver;SizeConstraints=new WindowSizeConstraints{MinimumSize=new Vector2(880,540),MaximumSize=new Vector2(float.MaxValue)};Services.IconPicker=callback=>iconSelection=callback;}
    public override void Draw()
    {
        while(uiActions.TryDequeue(out var action))action();Themes.Selected=config.Theme;Themes.Scale=Math.Clamp(config.Scale,.8f,1.5f);using var theme=new ThemeScope();
        ImGui.BeginChild("sidebar",new Vector2(170*Themes.Scale,0),true);
        ImGui.Spacing();ImGui.TextUnformatted("小烟酒");ImGui.TextDisabled("Yanjiu · 0.1.0");ImGui.Spacing();ImGui.Separator();ImGui.Spacing();
        string[] pages={"首页","本地模块","在线模块","手机支持","插件设置","诊断"};for(int i=0;i<pages.Length;i++){bool active=page==i;if(active)ImGui.PushStyleColor(ImGuiCol.Button,Themes.Current.Accent);if(ImGui.Button(pages[i],new Vector2(-1,40*Themes.Scale)))page=i;if(active)ImGui.PopStyleColor();}
        ImGui.EndChild();ImGui.SameLine();ImGui.BeginChild("content",Vector2.Zero,false);
        switch(page){case 0:DrawHome();break;case 1:DrawModules(false);break;case 2:DrawOnline();break;case 3:DrawPhone();break;case 4:DrawAppearance();break;case 5:DrawDiagnostics();break;}
        DrawIconPicker();ImGui.EndChild();
    }
    private void DrawHome()
    {
        ImGui.TextUnformatted("你好，小烟酒");ImGui.TextDisabled("独立卫月插件 · 0.1.0 测试版");ImGui.Spacing();
        ImGui.TextUnformatted($"{modules.Entries.Count} 个模块    {modules.Entries.Count(e=>e.Instance?.IsEnabled==true)} 个运行中");
        ImGui.Spacing();ImGui.TextWrapped("统一管理 DR / Omni 源码模块、在线仓库和手机连接。模块首次导入默认停用，完整诊断见诊断页。");
        if(ImGui.Button("导入本地模块"))page=1;ImGui.SameLine();if(ImGui.Button("查看在线模块"))page=2;ImGui.SameLine();if(ImGui.Button("手机支持"))page=3;
        ImGui.Spacing();ImGui.TextDisabled("当前主题："+Themes.Current.Name);
    }
    private void DrawModules(bool onlineOnly)
    {
        string[] filters=["全部","收藏","已启用","日常","战斗","物品","界面","自动"];
        for(int i=0;i<filters.Length;i++){bool active=filter==filters[i];if(active)ImGui.PushStyleColor(ImGuiCol.Button,Themes.Current.Accent);if(ImGui.Button(filters[i]))filter=filters[i];if(active)ImGui.PopStyleColor();if(i<filters.Length-1&&ImGui.GetContentRegionAvail().X>100)ImGui.SameLine();}ImGui.Spacing();
        ImGui.SetNextItemWidth(-1);ImGui.InputTextWithHint("##search","搜索名称、作者、类别",ref search,200);
        if(!onlineOnly){ImGui.SetNextItemWidth(-90);ImGui.InputTextWithHint("##localPath","本地 .cs / Yanjiu .dll 模块路径",ref localPath,1000);ImGui.SameLine();if(ImGui.Button("导入"))modules.Import(localPath);}
        ImGui.TextWrapped(modules.Status);
        foreach(var entry in modules.Entries.Where(e=>e.Origin!="内置"&&(!onlineOnly||e.Origin=="在线")&&Matches(e)).ToArray())DrawEntry(entry);
    }
    private string filter="全部";
    private bool Matches(ModuleEntry entry)
    {
        if(search.Length>0&&!(entry.Title+entry.Instance?.Descriptor.Author+entry.Instance?.Descriptor.Category).Contains(search,StringComparison.OrdinalIgnoreCase))return false;
        return filter switch{"收藏"=>entry.Preference.Favorite,"已启用"=>entry.Instance?.IsEnabled==true,"日常"=>entry.Instance?.Descriptor.Category=="Daily","战斗"=>entry.Instance?.Descriptor.Category=="Combat","物品"=>entry.Instance?.Descriptor.Category=="Item","界面"=>entry.Instance?.Descriptor.Category=="Interface","自动"=>entry.Instance?.Descriptor.Category=="Automation",_=>true};
    }
    private void DrawEntry(ModuleEntry entry)
    {
        ImGui.PushID(entry.Path);bool remove=false;
        var cardStart=ImGui.GetCursorScreenPos();var cardWidth=ImGui.GetContentRegionAvail().X;ImGui.BeginGroup();
        if(ImGui.BeginTable("header",3,ImGuiTableFlags.SizingStretchProp)){
            ImGui.TableSetupColumn("toggle",ImGuiTableColumnFlags.WidthFixed,32*Themes.Scale);
            ImGui.TableSetupColumn("title",ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("actions",ImGuiTableColumnFlags.WidthFixed,180*Themes.Scale);
            ImGui.TableNextColumn();bool enabled=entry.Instance?.IsEnabled==true;
            ImGui.BeginDisabled(entry.Instance==null);if(ImGui.Checkbox("##enabled",ref enabled)){ClearSettingsError(entry);modules.Enable(entry,enabled);}ImGui.EndDisabled();
            ImGui.TableNextColumn();if(ImGui.Selectable((selected==entry?"▼ ":"▶ ")+entry.Title,selected==entry))selected=selected==entry?null:entry;
            ImGui.TableNextColumn();ImGui.TextDisabled(entry.Instance?.Descriptor.Author??entry.Origin);
            if(ImGui.SmallButton("操作"))ImGui.OpenPopup("actions");if(ImGui.BeginPopup("actions")){
                bool favorite=entry.Preference.Favorite;if(ImGui.MenuItem("收藏","",favorite)){entry.Preference.Favorite=!favorite;config.Save();}
                if(entry.Compiled!=null&&ImGui.MenuItem("重载"))modules.Import(entry.Path,entry.Origin);
                if(entry.Origin!="内置"&&ImGui.MenuItem("移除"))remove=true;ImGui.EndPopup();}
            ImGui.EndTable();
        }
        if(entry.Error.Length>0){ImGui.TextColored(Themes.Current.Error,entry.Error);}
        if(selected==entry&&entry.Instance!=null&&!remove){
            ImGui.Separator();if(!string.IsNullOrWhiteSpace(entry.Instance.Descriptor.Description))ImGui.TextWrapped(entry.Instance.Descriptor.Description);
            if(!entry.Instance.IsEnabled){ImGui.TextDisabled("启用后可配置模块");if(ImGui.Button("启用并打开")){ClearSettingsError(entry);modules.Enable(entry,true);}}
            else if(entry.Instance.HasSettings){try{if(entry.DrawSettings())config.Save();}catch(Exception e){Services.Log.Error(e,"Settings UI failed for {Module}",entry.Title);modules.Enable(entry,false);entry.Error="设置界面错误："+e.GetBaseException().Message;selected=null;}}
        }
        ImGui.Dummy(new Vector2(0,8*Themes.Scale));ImGui.EndGroup();var cardEnd=ImGui.GetItemRectMax();ImGui.GetWindowDrawList().AddRect(cardStart-new Vector2(3),new Vector2(cardStart.X+cardWidth,cardEnd.Y+3),ImGui.ColorConvertFloat4ToU32(Themes.Current.Border),10*Themes.Scale);ImGui.PopID();ImGui.Spacing();if(remove){if(selected==entry)selected=null;modules.Remove(entry);}
    }
    private static void ClearSettingsError(ModuleEntry entry){if(entry.Error.StartsWith("设置界面错误：",StringComparison.Ordinal))entry.Error="";}
    private ModuleEntry? selected;
    private void DrawPhone(){var entry=modules.Entries.FirstOrDefault(e=>e.Origin=="内置");if(entry!=null){selected=entry;DrawEntry(entry);}}
    private void DrawAppearance()
    {
        ImGui.TextUnformatted("外观");ImGui.Spacing();for(int i=0;i<Themes.Available.Length;i++){ImGui.PushID(i);ImGui.PushStyleColor(ImGuiCol.Button,Themes.Available[i].Accent);if(ImGui.Button(Themes.Available[i].Name,new Vector2(120,50))){config.Theme=i;config.Save();}ImGui.PopStyleColor();ImGui.PopID();if(i%3!=2)ImGui.SameLine();}
        var scale=config.Scale;if(ImGui.SliderFloat("界面缩放",ref scale,.8f,1.5f,"%.2f")){config.Scale=scale;config.Save();}
        var fontSize=config.FontSize;ImGui.SetNextItemWidth(280);if(ImGui.SliderFloat("字号",ref fontSize,16,28,"%.0f"))fontPreview=fontSize;if(ImGui.IsItemDeactivatedAfterEdit()){config.FontSize=MathF.Round(fontPreview);config.Save();}
        int family=config.FontFamily;ImGui.SetNextItemWidth(280);if(ImGui.Combo("字体",ref family,UiFonts.Families,UiFonts.Families.Length)){config.FontFamily=family;config.Save();}
    }
    private float fontPreview=20;
    private void DrawOnline()
    {
        ImGui.SetNextItemWidth(-85);ImGui.InputTextWithHint("##repo","GitHub 仓库 / TreeHouseModules.json",ref repository,1000);ImGui.SameLine();ImGui.BeginDisabled(busy);if(ImGui.Button("读取"))Fetch();ImGui.EndDisabled();
        ImGui.TextWrapped(onlineStatus);
        if(listing!=null)foreach(var item in listing.Modules){ImGui.PushID(item.InternalName);if(ImGui.CollapsingHeader(item.Name+" · "+item.Version)){
            ImGui.TextWrapped(item.Description);ImGui.TextDisabled(item.Author);ImGui.BeginDisabled(busy);if(ImGui.Button("安装（默认停用）"))Install(item);ImGui.EndDisabled();}ImGui.PopID();}
        ImGui.Separator();DrawModules(true);
    }
    private async void Fetch(){busy=true;var input=repository;try{var result=await online.Fetch(input,stop.Token);uiActions.Enqueue(()=>{if(stop.IsCancellationRequested)return;listing=result;onlineStatus="已读取 "+result.Modules.Count+" 个模块";if(!config.Repositories.Contains(input)){config.Repositories.Add(input);config.Save();}});}catch(Exception e){var message=e.GetBaseException().Message;uiActions.Enqueue(()=>onlineStatus=message);}finally{uiActions.Enqueue(()=>busy=false);}}
    private async void Install(OnlineModule item){if(listing==null)return;var source=listing;busy=true;try{var bytes=await online.Download(source,item,stop.Token);var dir=Path.Combine(Services.ConfigDirectory,"online",Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(source.ManifestUri.ToString())))[..12]);Directory.CreateDirectory(dir);var path=Path.Combine(dir,item.InternalName+Path.GetExtension(item.File));File.WriteAllBytes(path+".tmp",bytes);File.Move(path+".tmp",path,true);uiActions.Enqueue(()=>{if(stop.IsCancellationRequested)return;modules.Import(path,"在线");onlineStatus="校验通过，正在编译";});}catch(Exception e){var message=e.GetBaseException().Message;uiActions.Enqueue(()=>onlineStatus=message);}finally{uiActions.Enqueue(()=>busy=false);}}
    private void DrawDiagnostics(){ImGui.TextUnformatted("兼容诊断");ImGui.TextWrapped("源码模块使用独立兼容层，不需要运行原宿主。原宿主私有管理器、付费提供器及针对原宿主编译的 DLL 不保证兼容；请导入源码查看具体缺失 API。");ImGui.TextWrapped(modules.Status);foreach(var entry in modules.Entries.Where(e=>e.Error.Length>0)){ImGui.Separator();ImGui.TextUnformatted(entry.Title);ImGui.TextWrapped(entry.Error);}}
    private void DrawIconPicker(){if(iconSelection==null)return;ImGui.Separator();ImGui.TextUnformatted("选择图标 ID");int id=(int)iconId;if(ImGui.InputInt("##iconId",ref id))iconId=(uint)Math.Max(0,id);var texture=Services.Textures.GetFromGameIcon(new Dalamud.Interface.Textures.GameIconLookup(iconId)).GetWrapOrDefault();if(texture!=null)ImGui.Image(texture.Handle,new Vector2(48));if(ImGui.Button("应用图标")){var callback=iconSelection;iconSelection=null;callback(iconId);}ImGui.SameLine();if(ImGui.Button("取消选择"))iconSelection=null;}
    public void Dispose(){stop.Cancel();online.Dispose();Services.IconPicker=null;}
}
