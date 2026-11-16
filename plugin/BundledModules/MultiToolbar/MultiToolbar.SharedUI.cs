// SPDX-License-Identifier: GPL-3.0-or-later
using System.Globalization;
using Yanjiu.NativeUI;

namespace OmniToolbox.TreePublic;

public sealed partial class MultiToolbar
{
    // Keep the original multi-column settings until a layout-equivalent native view is ready.
    public NativePanel? CreateNativePanel() => null;
}

public sealed partial class ToolbarRenderer
{
    private static string NativeClipboard(Func<string> operation)
    {
        string result = ""; Exception? failure = null;
        var thread = new System.Threading.Thread(() => { try { result = operation(); } catch(Exception error) { failure = error; } });
        thread.SetApartmentState(System.Threading.ApartmentState.STA);thread.IsBackground=true;thread.Start();
        if(!thread.Join(TimeSpan.FromSeconds(2))) throw new InvalidOperationException("剪贴板访问超时，请稍后重试。");
        if (failure != null) throw new InvalidOperationException("无法访问剪贴板，请稍后重试。", failure);
        return result;
    }

    public NativePanel CreateNativeConfigPanel()
    {
        MigrateMultiDockPlugins();
        var root = CollectionConfig;
        MultiToolbarBarConfig bar = root.AuxiliaryBars.FirstOrDefault(item => item.ID == selectedBarID) as MultiToolbarBarConfig ?? root;
        MultiToolbarWidgetConfig? widget = null;
        MultiToolbarWidgetConfig? command = null;
        string plugin = "";
        string dtr = "";
        var controls = new List<NativeControl>();
        void Changed() => saveConfig();
        void SelectBar(string id)
        {
            selectedBarID=id;
            bar = id == "main" ? root : root.AuxiliaryBars.First(item => item.ID == id);
            widget = null; command = null; plugin = ""; dtr = "";
        }
        void Number(string label, Func<double> read, Action<double> write, double min, double max) =>
            controls.Add(new NativeNumber(label, read, value => { write(value); Changed(); }, min, max));
        void Toggle(string label, Func<bool> read, Action<bool> write) =>
            controls.Add(new NativeToggle(label, read, value => { write(value); Changed(); }));
        void Text(string label, Func<string> read, Action<string> write) =>
            controls.Add(new NativeText(label, read, value => { write(value); Changed(); }));
        controls.Add(new NativeSelect("工具栏", () => new[] { new NativeOption("main", "主工具栏") }
            .Concat(root.AuxiliaryBars.Select(item => new NativeOption(item.ID, item.Name))).ToArray(),
            () => bar is MultiToolbarAuxiliaryBarConfig auxiliary ? auxiliary.ID : "main", SelectBar));
        controls.Add(new NativeButton("添加副工具栏", () => {
            var added = new MultiToolbarAuxiliaryBarConfig { Name = "副工具栏 " + (root.AuxiliaryBars.Count + 1), Widgets = [],
                BarVerticalOffset = root.BarVerticalOffset + 40 * (root.AuxiliaryBars.Count + 1),
                MultiDockMigrated = true, MultiDockPluginsMigrated = true, TextEntryDefaultsApplied = true };
            root.AuxiliaryBars.Add(added); SelectBar(added.ID); Changed();
        }));
        controls.Add(new NativeText("工具栏名称", () => (bar as MultiToolbarAuxiliaryBarConfig)?.Name ?? "主工具栏", value => {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("名称不能为空。");
            if (bar is MultiToolbarAuxiliaryBarConfig auxiliary) auxiliary.Name = value;
            Changed();
        }) { Enabled = () => bar is MultiToolbarAuxiliaryBarConfig });
        controls.Add(new NativeButton("删除当前副工具栏", () => {
            if (bar is MultiToolbarAuxiliaryBarConfig auxiliary) root.AuxiliaryBars.Remove(auxiliary);
            SelectBar("main"); Changed();
        }) { Enabled = () => bar is MultiToolbarAuxiliaryBarConfig });
        Toggle("组件自动展开", () => bar.AutoExpandOnHover, value => bar.AutoExpandOnHover = value);
        Toggle("锁定工具栏", () => bar.IsLocked, value => bar.IsLocked = value);
        controls.Add(new NativeSelect("位置", () => [new("Top", "顶部"), new("Bottom", "底部")],
            () => bar.Alignment.ToString(), value => { bar.Alignment = Enum.Parse<MultiToolbarAlignment>(value); Changed(); }));
        Number("垂直偏移", () => bar.BarVerticalOffset, value => bar.BarVerticalOffset = (float)value, 0, 2160);
        Number("工具栏缩放", () => bar.ToolbarScale, value => bar.ToolbarScale = (float)value, .6, 1);
        Number("图标大小", () => bar.RowIconSize, value => bar.RowIconSize = (float)value, 20, 60);
        Number("组件间距", () => bar.ComponentSpacing, value => bar.ComponentSpacing = (float)value, 0, 40);
        Number("工具栏背景透明度", () => bar.BarBackgroundOpacity, value => bar.BarBackgroundOpacity = (float)value, 0, 1);
        Number("按钮背景透明度", () => bar.ButtonBackgroundOpacity, value => bar.ButtonBackgroundOpacity = (float)value, 0, 1);
        controls.Add(new NativeSelect("字体", () => new[] { new NativeOption("", "默认字体") }.Concat(
            GetInstalledFonts().Select(pair => new NativeOption(pair.Key, pair.Value))).ToArray(), () => bar.FontFileName,
            value => { bar.FontFileName = value; Changed(); }));
        AddColor("背景颜色", () => bar.BackgroundColor, value => bar.BackgroundColor = value);
        AddColor("文本颜色", () => bar.TextColor, value => bar.TextColor = value);
        AddColor("强调颜色", () => bar.AccentColor, value => bar.AccentColor = value);
        AddColor("边框颜色", () => bar.BorderColor, value => bar.BorderColor = value);
        Text("配装插件命令", () => bar.EquipmentPlannerCommand, value => bar.EquipmentPlannerCommand = value);
        Text("ID 信息插件命令", () => bar.IDInformationCommand, value => bar.IDInformationCommand = value);
        AddEditor("工具栏组件", () => bar.Widgets, () => widget, value => widget = value, false);
        AddEditor("指令收纳", () => bar.Commands, () => command, value => command = value, true);
        controls.Add(new NativeSelect("插件收纳", () => {
            RefreshPluginCache();
            return bar.SelectedPlugins.Concat(loadedPlugins.Select(item => item.InternalName)).Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(name => new NativeOption(name, installedPlugins.TryGetValue(name, out var info) ? info.Name : name)).ToArray();
        }, () => plugin, value => plugin = value));
        controls.Add(new NativeToggle("收纳所选插件", () => bar.SelectedPlugins.Contains(plugin), value => {
            if (value) { if (!bar.SelectedPlugins.Contains(plugin)) bar.SelectedPlugins.Add(plugin); }
            else bar.SelectedPlugins.Remove(plugin);
            bar.MultiDockPluginsMigrated = true; pluginCacheValid = false; Changed();
        }) { Enabled = () => plugin.Length > 0 });
        AddMove("插件", () => bar.SelectedPlugins, () => plugin);
        controls.Add(new NativeSelect("服务器信息栏条目", () => OmenTools.DService.Instance().DTRBar.Entries
            .Select(item => new NativeOption(item.Title, item.Title)).ToArray(), () => dtr, value => dtr = value));
        controls.Add(new NativeToggle("收纳所选信息栏条目", () => bar.CollapsedDtrTitles.Contains(dtr), value => {
            if (value) { if (!bar.CollapsedDtrTitles.Contains(dtr)) bar.CollapsedDtrTitles.Add(dtr); }
            else bar.CollapsedDtrTitles.Remove(dtr);
            if (!bar.DtrOrder.Contains(dtr)) bar.DtrOrder.Add(dtr);
            Changed();
        }) { Enabled = () => dtr.Length > 0 });
        AddMove("信息栏条目", () => bar.DtrOrder, () => dtr);
        controls.Add(new NativeButton("导出配置到剪贴板", () => {
            var value = new Newtonsoft.Json.Linq.JObject {
                ["Format"] = "OmniToolbox.ToolbarRenderer", ["Version"] = 2,
                ["Config"] = Newtonsoft.Json.Linq.JObject.FromObject(root, Newtonsoft.Json.JsonSerializer.Create(TransferSettings))
            }.ToString(Newtonsoft.Json.Formatting.Indented);
            NativeClipboard(() => { System.Windows.Forms.Clipboard.SetText(value); return ""; });
        }));
        controls.Add(new NativeButton("从剪贴板导入配置", () => {
            var value = NativeClipboard(() => System.Windows.Forms.Clipboard.GetText());
            ImportConfiguration(value); SelectBar("main"); Changed();
        }));
        return new NativePanel("MultiToolbar", "多功能工具栏", controls);

        void AddColor(string label, Func<Vector4?> read, Action<Vector4?> write)
        {
            controls.Add(new NativeColor(label, () => read() ?? label switch {
                "背景颜色" => OmniToolbox.UI.Theme.OmniTheme.Tokens.Background, "文本颜色" => Vector4.One,
                "强调颜色" => OmniToolbox.UI.Theme.OmniTheme.Tokens.Accent, _ => OmniToolbox.UI.Theme.OmniTheme.Tokens.Border
            }, value => { write(value); Changed(); }));
            controls.Add(new NativeButton(label + "跟随主题", () => { write(null); Changed(); }));
        }

        void AddMove<T>(string label, Func<List<T>> list, Func<T> selected)
        {
            foreach (var delta in new[] {-1,1}) {
                int direction = delta;
                controls.Add(new NativeButton(label + (direction < 0 ? "上移" : "下移"), () => {
                    var items=list(); int index=items.IndexOf(selected()); int target=index+direction;
                    if(index<0||target<0||target>=items.Count)return;
                    (items[index],items[target])=(items[target],items[index]); Changed();
                }) { Enabled=()=> {var items=list();int index=items.IndexOf(selected());return index>=0&&index+direction>=0&&index+direction<items.Count;} });
            }
        }
        void AddEditor(string label, Func<List<MultiToolbarWidgetConfig>> list, Func<MultiToolbarWidgetConfig?> read,
            Action<MultiToolbarWidgetConfig?> select, bool isCommand)
        {
            bool Valid() => read() is { } selected && list().Contains(selected);
            MultiToolbarWidgetConfig Current() => Valid() ? read()! : throw new InvalidOperationException("请先选择条目。");
            controls.Add(new NativeSelect(label, () => list().Select((item,index) => new NativeOption(index.ToString(),
                $"{index+1}. " + (isCommand ? item.Name : WidgetTypeLabel(item.Type)))).ToArray(),
                () => list().IndexOf(read()!).ToString(), value => select(list()[int.Parse(value)])));
            controls.Add(new NativeButton("添加" + label, () => {var added=CreateDefaultWidget(MultiToolbarWidgetType.CustomButton);list().Add(added);select(added);Changed();}));
            controls.Add(new NativeButton("删除所选" + label, () => {list().Remove(Current());select(null);Changed();}) { Enabled=Valid });
            AddMove(label, list, () => read()!);
            controls.Add(new NativeToggle("启用条目", () => Valid() && Current().Enabled, value => {Current().Enabled=value;Changed();}) {Enabled=Valid});
            if (!isCommand) {
                controls.Add(new NativeSelect("组件类型", () => SelectableWidgetTypes.Select(type=>new NativeOption(type.ToString(),WidgetTypeLabel(type))).ToArray(),
                    () => Valid()?Current().Type.ToString():"", value=>{Current().Type=Enum.Parse<MultiToolbarWidgetType>(value);Changed();}) {Enabled=Valid});
                controls.Add(new NativeSelect("组件位置", () => [new("Left","左侧"),new("Center","中间"),new("Right","右侧")],
                    () => Valid()?Current().Side.ToString():"", value=>{Current().Side=Enum.Parse<MultiToolbarWidgetSide>(value);Changed();}) {Enabled=Valid});
            }
            Field("名称", item=>item.Name, (item,value)=>item.Name=value);
            controls.Add(new NativeToggle("使用组件默认显示名称", () => !Valid()||Current().DisplayName is null,
                value=>{Current().DisplayName=value?null:"";Changed();}) {Enabled=Valid});
            Field("显示名称（空白为仅图标）", item=>item.DisplayName??"", (item,value)=>item.DisplayName=value);
            Field("左键指令", item=>item.Command, (item,value)=>item.Command=value);
            Field("右键指令", item=>item.RightCommand, (item,value)=>item.RightCommand=value);
            controls.Add(new NativeToggle("显示图标", () => Valid()&&Current().ShowIcon, value=>{Current().ShowIcon=value;Changed();}) {Enabled=Valid});
            controls.Add(new NativeNumber("游戏图标编号", () => Valid()?Current().GameIconID:0, value=>{
                if(value!=Math.Truncate(value))throw new ArgumentException("图标编号必须为整数。");
                Current().GameIconID=(uint)value;Changed();},0,uint.MaxValue) {Enabled=Valid});
            Field("图标文件路径", item=>item.IconFilePath, (item,value)=>item.IconFilePath=value);
            Field("图标字符", item=>item.IconGlyph, (item,value)=>item.IconGlyph=value);
            if(!isCommand) Field("单条信息栏名称", item=>item.DtrTitle, (item,value)=>item.DtrTitle=value);
            void Field(string title,Func<MultiToolbarWidgetConfig,string> get,Action<MultiToolbarWidgetConfig,string> set) =>
                controls.Add(new NativeText(title,()=>Valid()?get(Current()):"",value=>{set(Current(),value);Changed();}) {Enabled=Valid});
        }
    }
}
