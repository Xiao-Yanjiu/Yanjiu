// SPDX-License-Identifier: GPL-3.0-or-later
using Yanjiu.Modules;
using Yanjiu.NativeUI;
namespace Yanjiu.UI;

internal static class NativeUiService
{
    private sealed record OpenWindow(ModuleEntry Entry,ModuleBase Adapter,NativePanelWindow Window);
    private static readonly Dictionary<object,Dictionary<string,OpenWindow>> windows=new(ReferenceEqualityComparer.Instance);
    private static ModuleManager? manager;
    private static int generation;
    internal static void Initialize(ModuleManager modules)
    {
        if(manager!=null)throw new InvalidOperationException("原生UI服务已经初始化。");
        manager=modules;generation++;
        NativeUi.OpenHandler=OpenAsync;NativeUi.CloseHandler=CloseAsync;
    }
    private static ModuleEntry? Find(object owner)=>manager?.Entries.FirstOrDefault(entry=>
        ReferenceEquals(entry.Instance,owner)||entry.Instance is Api1ModuleAdapter api&&api.OwnsNativeUi(owner));
    private static async Task OpenAsync(object owner,NativePanel panel)
    {
        int session=generation;
        Task? initialization=null;
        await Services.Framework.RunOnFrameworkThread(()=>{
            if(session!=generation||manager==null)throw new ObjectDisposedException("Yanjiu原生UI服务");
            initialization=DRNativeRuntime.EnsureReady();
        });
        await initialization!;
        await Services.Framework.RunOnFrameworkThread(()=>{
            if(session!=generation||manager==null)throw new ObjectDisposedException("Yanjiu原生UI服务");
            var entry=Find(owner)??throw new InvalidOperationException("窗口所属模块已卸载。");
            if(entry.Instance?.IsEnabled!=true||entry.Transitioning)throw new InvalidOperationException("请等待模块启用完成后打开窗口。");
            if(!windows.TryGetValue(owner,out var owned))windows[owner]=owned=new(StringComparer.Ordinal);
            if(!owned.ContainsKey(panel.Id)&&(owned.Count>=16||windows.Values.Sum(items=>items.Count)>=64))
                throw new InvalidOperationException("原生模块窗口数量已达上限，请先关闭其他面板。");
            var next=new NativePanelWindow(entry,manager,panel);
            try{next.Open();if(!next.IsAllocated)throw new InvalidOperationException("原生窗口分配失败。");}catch{next.Dispose();throw;}
            if(owned.Remove(panel.Id,out var previous))previous.Window.Dispose();
            owned[panel.Id]=new(entry,entry.Instance,next);
        });
    }
    private static Task CloseAsync(object owner,string id)=>Services.Framework.RunOnFrameworkThread(()=>{
        if(windows.TryGetValue(owner,out var owned)&&owned.Remove(id,out var window))window.Window.Dispose();
        if(owned?.Count==0)windows.Remove(owner);
    });
    internal static void Pump()
    {
        if(manager==null)return;
        foreach(var pair in windows.ToArray()){
            foreach(var item in pair.Value.ToArray()){
                var window=item.Value;
                if(!manager.Entries.Contains(window.Entry)||!ReferenceEquals(window.Entry.Instance,window.Adapter)||!window.Adapter.IsEnabled||window.Window.Finalized){window.Window.Dispose();pair.Value.Remove(item.Key);}
            }
            if(pair.Value.Count==0)windows.Remove(pair.Key);
        }
    }
    internal static void Dispose()
    {
        generation++;NativeUi.OpenHandler=null;NativeUi.CloseHandler=null;manager=null;
        foreach(var owned in windows.Values)foreach(var window in owned.Values)window.Window.Dispose();
        windows.Clear();
    }
}
