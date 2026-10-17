// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Concurrent;
namespace Yanjiu.Modules;
public sealed class ModuleEntry : IDisposable
{
    public required string Path{get;init;}public required string Origin{get;init;}
    public required ModulePreference Preference{get;init;}public CompiledModule? Compiled{get;set;}
    public string DependencyNotice{get;set;}="";public DRLocalModule? Catalog{get;set;}public ModuleBase? Instance{get;set;}public string Error{get;set;}="";
    public string Source=>Instance is DailyRoutines.Common.Module.Abstractions.ModuleBase?"DR":Instance is OmniToolbox.Common.Module.Abstractions.ModuleBase?"Omni":Origin is "DR" or "Omni"?Origin:"Yanjiu";
    public string Title=>(Instance?.Descriptor.Title??Catalog?.Title??System.IO.Path.GetFileNameWithoutExtension(Path))+" ["+Source+"]";
    public bool DrawSettings(){if(Instance?.IsEnabled!=true||!Instance.HasSettings)return false;bool changed=Instance.DrawSettings();if(changed)Instance.SaveImportedConfiguration();return changed;}
    public void Dispose(){try{Instance?.Dispose();}finally{Instance=null;Compiled?.Dispose();Compiled=null;}}
}
public sealed class ModuleManager : IDisposable
{
    public readonly List<ModuleEntry> Entries=new();
    private readonly Configuration config;
    private readonly SourceCompiler compiler;
    private readonly ConcurrentQueue<Action> pending=new();
    private readonly ConcurrentDictionary<string,long> revisions=new(StringComparer.OrdinalIgnoreCase);
    private readonly CancellationTokenSource stop=new();private readonly object queueGate=new();private readonly SemaphoreSlim compileSlots=new(2);private volatile bool disposed;
    private readonly HashSet<string> requestedEnable=new(StringComparer.OrdinalIgnoreCase);
    public string Status{get;private set;}="";
    public ModuleManager(Configuration config,SourceCompiler compiler){this.config=config;this.compiler=compiler;}
    public void Restore(){
        DRLocalModule[] bundled;
        try{bundled=DRCatalog.Load().ToArray();}catch(Exception e) when(e is IOException or System.Text.Json.JsonException or ArgumentException){bundled=[];Status="内置模块包校验失败："+e.Message;Services.Log.Error(e,"Bundled module catalog rejected; continuing with local and online modules");}
        var paths=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var item in bundled){
            var path=System.IO.Path.Combine(DRCatalog.RootFor(item.Source),item.Id+".dll");paths.Add(path);if(Entries.Any(e=>e.Path.Equals(path,StringComparison.OrdinalIgnoreCase)))continue;
            var pref=config.Modules.FirstOrDefault(p=>p.Path.Equals(path,StringComparison.OrdinalIgnoreCase))??new ModulePreference{Path=path,Origin=item.Source};
            if(!config.Modules.Contains(pref))config.Modules.Add(pref);
            Entries.Add(new ModuleEntry{Path=path,Origin=item.Source,Preference=pref,Catalog=item,Error=item.Compiles&&item.Importable?"":"待适配：需要补齐原宿主接口"});
            if(item.Compiles&&item.Importable){try{Import(DRCatalog.VerifiedPath(item),item.Source,pref.Hash);}catch(Exception e){Entries.Last().Error=e.Message;Services.Log.Error(e,"Bundled module rejected: "+item.Id);}}else pref.Enabled=false;
        }
        config.Modules.RemoveAll(pref=>pref.Origin=="Omni"&&!paths.Contains(pref.Path));
        foreach(var pref in config.Modules.Where(p=>p.Origin is not ("DR" or "Omni")&&!p.Path.StartsWith("builtin:")&&!paths.Contains(p.Path)).ToArray())Import(pref.Path,pref.Origin,pref.Hash);
    }
    public void Import(string path,string origin="本地",string? expectedHash=null,string? repository=null)
    {
        if(disposed)return;
        try{path=System.IO.Path.GetFullPath(path.Trim());}catch{Status="填写有效模块路径";return;}if(!File.Exists(path)){Status="模块文件不存在";return;}
        if(new FileInfo(path).Length>4*1024*1024){Status="模块不能超过 4 MiB";return;}
        Task readiness;try{readiness=origin is "DR" or "Omni"?DRNativeRuntime.EnsureReady():Task.CompletedTask;}catch(Exception e){Status=e.Message;return;}
        var file=path;var revision=revisions.AddOrUpdate(file,1,(_,previous)=>previous+1);Status="正在编译 "+System.IO.Path.GetFileName(file);
        _=Task.Run(async()=>{
            CompiledModule? compiled=null;string error="";bool acquired=false;
            try{await readiness.WaitAsync(stop.Token);await compileSlots.WaitAsync(stop.Token);acquired=true;stop.Token.ThrowIfCancellationRequested();if(revisions.GetValueOrDefault(file)!=revision)return;compiled=compiler.Compile(File.ReadAllBytes(file),file);}
            catch(Exception e){error=e.GetBaseException().Message;}
            finally{if(acquired)compileSlots.Release();}
            var result=compiled;var failure=error;lock(queueGate){if(disposed){result?.Dispose();return;}
            pending.Enqueue(()=>{if(disposed||revisions.GetValueOrDefault(file)!=revision){result?.Dispose();return;}var old=Entries.FirstOrDefault(e=>e.Path.Equals(file,StringComparison.OrdinalIgnoreCase));if(old!=null){try{old.Dispose();}catch(Exception e){Status="旧模块清理失败："+e.Message;result?.Dispose();return;}Entries.Remove(old);}
                var pref=config.Modules.FirstOrDefault(p=>p.Path.Equals(file,StringComparison.OrdinalIgnoreCase))??new ModulePreference{Path=file,Origin=origin};
                if(!config.Modules.Contains(pref))config.Modules.Add(pref);bool restore=pref.Enabled&&expectedHash!=null&&expectedHash.Equals(result?.Hash,StringComparison.OrdinalIgnoreCase);
                pref.Enabled=false;pref.Origin=origin;if(repository!=null)pref.Repository=repository;var entry=new ModuleEntry{Path=file,Origin=origin,Compiled=result,Preference=pref,Catalog=old?.Catalog,Error=failure};
                if(result!=null){try{if(origin=="在线"&&result.ModuleType.Name!=System.IO.Path.GetFileNameWithoutExtension(file))throw new ModuleCompilationException("模块类型与在线清单 InternalName 不一致");entry.Instance=result.Create();pref.Hash=result.Hash;/* Actual provider requirements are checked by the constructor binder. */}catch(Exception e){entry.Error="模块构造失败："+e.GetBaseException().Message;Services.Log?.Error(e,"Bundled module construction failed: {Module}",System.IO.Path.GetFileName(file));}}
                Entries.Add(entry);if(entry.Instance is DailyRoutines.Common.Module.Abstractions.ModuleBase constructedDr){var missing=constructedDr.Info.ModulesPrerequisite.Where(name=>!Entries.Any(e=>e.Source=="DR"&&(e.Instance?.ModuleName??e.Catalog?.Id)==name)).ToArray();if(missing.Length>0)entry.DependencyNotice="缺少前置模块："+string.Join("、",missing); }if(restore&&entry.Error.Length==0)Enable(entry,true);config.Save();Status=entry.Error.Length==0?(entry.Instance?.IsEnabled==true?"已恢复运行":"已导入，默认停用"):entry.Error;
            });}
        },CancellationToken.None);
    }
    public void Pump(){
        for(int i=0;i<2&&pending.TryDequeue(out var action);i++)action();
        foreach(var path in requestedEnable.ToArray()){
            var entry=Entries.FirstOrDefault(e=>e.Path.Equals(path,StringComparison.OrdinalIgnoreCase));
            if(entry==null){requestedEnable.Remove(path);continue;}
            try{if(EnableWithDependencies(entry,new HashSet<string>(StringComparer.OrdinalIgnoreCase)))requestedEnable.Remove(path);}
            catch(Exception e){Status=e.Message;requestedEnable.Remove(path);}
        }
    }
    private bool EnableWithDependencies(ModuleEntry entry,HashSet<string> visiting){
        if(entry.Error.Length>0)throw new InvalidOperationException(entry.Title+"："+entry.Error);
        if(entry.Instance==null)return false;
        if(entry.Instance.IsEnabled)return true;
        if(!visiting.Add(entry.Path))throw new InvalidOperationException("模块前置依赖存在循环："+entry.Title);
        try{
            if(entry.Instance is DailyRoutines.Common.Module.Abstractions.ModuleBase dr){
                bool ready=true;
                foreach(var name in dr.Info.ModulesPrerequisite){
                    var dependency=Entries.FirstOrDefault(e=>e.Source=="DR"&&(e.Instance?.ModuleName??e.Catalog?.Id)==name)
                        ??throw new InvalidOperationException("未打包所需前置模块："+name);
                    if(!EnableWithDependencies(dependency,visiting))ready=false;
                }
                if(!ready)return false;
            }
            if(!EnableCore(entry,true))throw new InvalidOperationException(Status);
            return true;
        }finally{visiting.Remove(entry.Path);}
    }
    public bool Enable(ModuleEntry entry,bool enabled){
        if(!enabled){requestedEnable.Remove(entry.Path);return EnableCore(entry,false);}
        requestedEnable.Add(entry.Path);
        try{if(EnableWithDependencies(entry,new HashSet<string>(StringComparer.OrdinalIgnoreCase))){requestedEnable.Remove(entry.Path);return true;}Status="正在加载模块及前置依赖："+entry.Title;return false;}
        catch(Exception e){requestedEnable.Remove(entry.Path);Status=e.Message;return false;}
    }
    private bool EnableCore(ModuleEntry entry,bool enabled)
    {
        if(entry.Instance==null||enabled&&entry.Error.Length>0)return false;
        if(enabled&&Entries.Any(e=>e!=entry&&e.Instance?.IsEnabled==true&&e.Instance.ModuleName==entry.Instance.ModuleName)){Status="同名模块已在其他来源运行，请先停用";return false;}
        if(enabled){var overlap=Entries.FirstOrDefault(other=>other!=entry&&other.Instance?.IsEnabled==true&&ModuleOverlaps.Conflicts(entry,other));if(overlap!=null){Status="存在重复或重叠功能，请先停用："+overlap.Title;return false;}}
        if(entry.Instance is DailyRoutines.Common.Module.Abstractions.ModuleBase dr){
            if(enabled){
                var missing=dr.Info.ModulesPrerequisite.Where(name=>!Entries.Any(e=>e.Source=="DR"&&e.Instance?.ModuleName==name&&e.Instance.IsEnabled)).ToArray();
                if(missing.Length>0){Status="请先启用前置模块："+string.Join("、",missing);return false;}
                var conflicts=Entries.Where(e=>e!=entry&&e.Instance?.IsEnabled==true).Where(e=>dr.Info.ModulesConflict.Contains(e.Instance!.ModuleName)||e.Instance is DailyRoutines.Common.Module.Abstractions.ModuleBase other&&other.Info.ModulesConflict.Contains(dr.ModuleName)).Select(e=>e.Title).ToArray();
                if(conflicts.Length>0){Status="请先停用冲突模块："+string.Join("、",conflicts);return false;}
            }else{
                var dependents=Entries.Where(e=>e.Instance is DailyRoutines.Common.Module.Abstractions.ModuleBase dependent&&dependent.IsEnabled&&dependent.Info.ModulesPrerequisite.Contains(dr.ModuleName)).Select(e=>e.Title).ToArray();
                if(dependents.Length>0){Status="请先停用依赖此模块的："+string.Join("、",dependents);return false;}
            }
        }
        try{entry.Instance.SetEnabled(enabled);entry.Preference.Enabled=enabled;config.Save();return true;}
        catch(Exception e){entry.Preference.Enabled=false;entry.Error=e.GetBaseException().Message;Status=entry.Error;config.Save();Services.Log?.Error(e,"Yanjiu module lifecycle failure");return false;}
    }
    public void Remove(ModuleEntry entry)=>Forget([entry.Path]);
    public void Forget(IEnumerable<string> paths)
    {
        var removed=new HashSet<string>(paths.Select(NormalizePath),StringComparer.OrdinalIgnoreCase);
        foreach(var path in removed)revisions.AddOrUpdate(path,1,(_,previous)=>previous+1);
        foreach(var entry in Entries.Where(e=>removed.Contains(e.Path)).ToArray()){if(entry.Instance!=null&&!config.HiddenAvailableModules.Contains(entry.Instance.ModuleName))config.HiddenAvailableModules.Add(entry.Instance.ModuleName);try{entry.Dispose();}catch(Exception e){Status="模块清理失败："+e.Message;Services.Log?.Error(e,"Module removal failed");}Entries.Remove(entry);}
        config.Modules.RemoveAll(pref=>removed.Contains(NormalizePath(pref.Path)));config.Save();
    }
    public static string NormalizePath(string path){if(path.StartsWith("builtin:",StringComparison.Ordinal))return path;try{return Path.GetFullPath(path);}catch{return path;}}
    public void Dispose(){lock(queueGate){if(disposed)return;disposed=true;stop.Cancel();}foreach(var entry in Entries){try{entry.Dispose();}catch(Exception e){Services.Log?.Error(e,"Module dispose failed");}}Entries.Clear();Pump();}
}













