// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Concurrent;
namespace Yanjiu.Modules;
public sealed class ModuleEntry : IDisposable
{
    public required string Path{get;init;}public required string Origin{get;init;}
    public required ModulePreference Preference{get;init;}public CompiledModule? Compiled{get;set;}
    public ModuleBase? Instance{get;set;}public string Error{get;set;}="";
    public string Title=>Instance?.Descriptor.Title??System.IO.Path.GetFileName(Path);
    public bool DrawSettings()=>Instance?.IsEnabled==true&&Instance.HasSettings&&Instance.DrawSettings();
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
    public string Status{get;private set;}="";
    public ModuleManager(Configuration config,SourceCompiler compiler){this.config=config;this.compiler=compiler;}
    public void AddBuiltIn(ModuleBase module)
    {
        var pref=config.Modules.FirstOrDefault(p=>p.Path=="builtin:phone")??new ModulePreference{Path="builtin:phone",Origin="内置"};
        if(!config.Modules.Contains(pref))config.Modules.Add(pref);Entries.Add(new ModuleEntry{Path=pref.Path,Origin="内置",Instance=module,Preference=pref});
        if(pref.Enabled)Enable(Entries[^1],true);
    }
    public void Restore(){foreach(var pref in config.Modules.Where(p=>!p.Path.StartsWith("builtin:")).ToArray())Import(pref.Path,pref.Origin,pref.Hash);}
    public void Import(string path,string origin="本地",string? expectedHash=null,string? repository=null)
    {
        if(disposed)return;
        try{path=System.IO.Path.GetFullPath(path.Trim());}catch{Status="填写有效模块路径";return;}if(!File.Exists(path)){Status="模块文件不存在";return;}
        if(new FileInfo(path).Length>4*1024*1024){Status="模块不能超过 4 MiB";return;}
        var file=path;var revision=revisions.AddOrUpdate(file,1,(_,previous)=>previous+1);Status="正在编译 "+System.IO.Path.GetFileName(file);
        _=Task.Run(async()=>{
            CompiledModule? compiled=null;string error="";bool acquired=false;
            try{await compileSlots.WaitAsync(stop.Token);acquired=true;stop.Token.ThrowIfCancellationRequested();if(revisions.GetValueOrDefault(file)!=revision)return;compiled=compiler.Compile(File.ReadAllBytes(file),file);}
            catch(Exception e){error=e.GetBaseException().Message;}
            finally{if(acquired)compileSlots.Release();}
            var result=compiled;var failure=error;lock(queueGate){if(disposed){result?.Dispose();return;}
            pending.Enqueue(()=>{if(disposed||revisions.GetValueOrDefault(file)!=revision){result?.Dispose();return;}var old=Entries.FirstOrDefault(e=>e.Path.Equals(file,StringComparison.OrdinalIgnoreCase));if(old!=null){try{old.Dispose();}catch(Exception e){Status="旧模块清理失败："+e.Message;result?.Dispose();return;}Entries.Remove(old);}
                var pref=config.Modules.FirstOrDefault(p=>p.Path.Equals(file,StringComparison.OrdinalIgnoreCase))??new ModulePreference{Path=file,Origin=origin};
                if(!config.Modules.Contains(pref))config.Modules.Add(pref);bool restore=pref.Enabled&&expectedHash!=null&&expectedHash.Equals(result?.Hash,StringComparison.OrdinalIgnoreCase);
                pref.Enabled=false;pref.Origin=origin;if(repository!=null)pref.Repository=repository;var entry=new ModuleEntry{Path=file,Origin=origin,Compiled=result,Preference=pref,Error=failure};
                if(result!=null){try{if(origin=="在线"&&result.ModuleType.Name!=System.IO.Path.GetFileNameWithoutExtension(file))throw new ModuleCompilationException("模块类型与在线清单 InternalName 不一致");entry.Instance=result.Create();pref.Hash=result.Hash;if(entry.Instance is OmniToolbox.Common.Module.Abstractions.ModuleBase omni&&omni.Info.RequiresPrivateProvider)entry.Error="模块要求原宿主私有提供器，不能独立启用";}catch(Exception e){entry.Error="模块构造失败："+e.GetBaseException().Message;}}
                Entries.Add(entry);if(restore&&entry.Error.Length==0)Enable(entry,true);config.Save();Status=entry.Error.Length==0?(entry.Instance?.IsEnabled==true?"已恢复运行":"已导入，默认停用"):entry.Error;
            });}
        },CancellationToken.None);
    }
    public void Pump(){while(pending.TryDequeue(out var action))action();}
    public bool Enable(ModuleEntry entry,bool enabled)
    {
        if(entry.Instance==null||enabled&&entry.Error.Length>0)return false;
        if(enabled&&Entries.Any(e=>e!=entry&&e.Instance?.IsEnabled==true&&e.Instance.ModuleName==entry.Instance.ModuleName)){Status="同名模块已在其他来源运行，请先停用";return false;}
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
