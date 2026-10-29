// SPDX-License-Identifier: GPL-3.0-or-later
using System.Reflection;
using System.Text.Json;
namespace Yanjiu.Modules;

public sealed class Api1ModuleAdapter:ModuleBase,IAsyncDisposable
{
    private const BindingFlags Members=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
    private readonly Func<Task> ensureReady;
    private readonly Func<Action,Task> dispatcher;
    private readonly object module;
    private readonly Type contract;
    private readonly FieldInfo? config;
    private readonly string configPath;
    private readonly IDisposable registration;
    private readonly SemaphoreSlim operations=new(1,1);
    private Task? disposal;
    private volatile bool closing;
    public bool Transitioning{get;private set;}
    public Func<bool,bool,CancellationToken,Task>? StateRequest{get;set;}
    public override string ModuleName=>module.GetType().Name;
    private object? Get(string name)=>contract.GetProperty(name,Members)!.GetValue(module);
    private void Set(string name,object? value)=>contract.GetProperty(name,Members)!.SetValue(module,value);
    public override ModuleDescriptor Descriptor
    {
        get{var info=Get("Info")!;return new((string)Get("Title")!,(string)Get("Description")!,(string?)info.GetType().GetProperty("Author")!.GetValue(info)??"",info.GetType().GetProperty("Category")!.GetValue(info)?.ToString()??"General");}
    }
    public Api1ModuleAdapter(Type type):this(type,Api1Runtime.EnsureReady,Api1Runtime.Dispatch){}
    internal Api1ModuleAdapter(Type type,Func<Task> ready,Func<Action,Task> dispatch)
    {
        ensureReady=ready;dispatcher=dispatch;
        contract=Api1Runtime.Type("OmniToolbox.Common.Module.Abstractions.OmniModuleBase");
        Api1Runtime.Invoke(Api1Runtime.Type("OmniToolbox.Common.Module.Runtime.ModuleContract"),null,"ValidateType",type);
        module=Activator.CreateInstance(type)??throw new InvalidDataException("API 1 模块没有公共无参构造");
        config=type.GetField("config",Members);
        configPath=Path.Combine(Services.ConfigDirectory,"modules","api1",type.Name+".json");
        if(config!=null&&File.Exists(configPath)){
            config.SetValue(module,JsonSerializer.Deserialize(Api1SettingsStore.Read(configPath,type.Name),config.FieldType,new JsonSerializerOptions{IncludeFields=true,MaxDepth=64})??throw new InvalidDataException("模块配置为空"));
        }
        Set("Dispatch",dispatcher);
        Set("SaveHostConfig",(Action)Save);
        Set("ErrorReporter",(Action<Exception>)(e=>Services.Log?.Error(e,"API 1 module error: "+ModuleName)));
        var requestProperty=contract.GetProperty("StateChangeRequest",Members)!;
        var create=typeof(Api1ModuleAdapter).GetMethod(nameof(CreateRequest),BindingFlags.Instance|BindingFlags.NonPublic)!.MakeGenericMethod(requestProperty.PropertyType.GenericTypeArguments[3].GenericTypeArguments[0]);
        requestProperty.SetValue(module,create.Invoke(this,null));
        registration=(IDisposable)Api1Runtime.Invoke(Api1Runtime.Type("OmniToolbox.UI.OmniLoc"),null,"RegisterModule",module)!;
        PersistImportedConfiguration=Save;
    }
    private Func<bool,bool,CancellationToken,Task<T>> CreateRequest<T>()=>async(enabled,affectConfig,token)=>{
        token.ThrowIfCancellationRequested();if(StateRequest is {} request)await request(enabled,affectConfig,token).ConfigureAwait(false);else{await ChangeEnabledAsync(enabled).ConfigureAwait(false);if(affectConfig)await dispatcher(Save).ConfigureAwait(false);}
        return (T)typeof(T).GetProperty("Success",Members)!.GetValue(null)!;
    };
    public void Save(){if(config?.GetValue(module) is {} value)Api1SettingsStore.Save(configPath,ModuleName,value,config.FieldType);}
    public override bool HasSettings=>(bool)Get("HasSettings")!;
    public IEnumerable<KeyValuePair<string,string>> MacroCommands()
    {
        var info=Get("Info")!;
        if(info.GetType().GetProperty("Commands")?.GetValue(info) is not System.Collections.IEnumerable commands)yield break;
        foreach(var command in commands){
            var type=command!.GetType();var key=(string)type.GetProperty("DescriptionKey")!.GetValue(command)!;
            var text=(string)type.GetProperty("ClipboardText")!.GetValue(command)!;
            if(text.StartsWith("/omni ",StringComparison.OrdinalIgnoreCase))text="/yanjiu "+text[6..];
            var label=(string)Api1Runtime.Invoke(Api1Runtime.Type("OmniToolbox.UI.OmniLoc"),null,"Get",module,key)!;
            yield return new(label,text);
        }
    }
    public override bool DrawSettings(){var changed=(bool)Api1Runtime.Invoke(contract,module,"DrawSettings")!;if(changed)Save();return changed;}
    public override bool ResetSettings(){var changed=(bool)Api1Runtime.Invoke(contract,module,"ResetSettings")!;if(changed)Save();return changed;}
    public override bool TryHandleCommand(string args)=>!closing&&!Transitioning&&IsEnabled&&(bool)Get("CanHandleCommands")!&&(bool)Api1Runtime.Invoke(contract,module,"TryHandleCommand",args)!;
    public override bool TryHandleCommand(string command,string args)=>!closing&&!Transitioning&&IsEnabled&&(bool)Get("CanHandleCommands")!&&(bool)Api1Runtime.Invoke(contract,module,"TryHandleCommand",command,args)!;
    protected override bool OnInterruptAutomation()=>(bool)Api1Runtime.Invoke(contract,module,"InterruptAutomation")!;
    public async Task ChangeEnabledAsync(bool enabled)
    {
        if(closing)throw new ObjectDisposedException(ModuleName);
        await operations.WaitAsync().ConfigureAwait(false);
        try{
            if(closing)throw new ObjectDisposedException(ModuleName);
            Transitioning=true;
            if(enabled){await ensureReady().ConfigureAwait(false);Set("HostIconBrowser",Api1Runtime.Icons);}
            await (Task)Api1Runtime.Invoke(contract,module,"SetEnabledAsync",enabled,dispatcher)!;
            MarkEnabled(!closing&&(bool)Get("IsEnabled")!);
            await dispatcher(Save).ConfigureAwait(false);
        }catch(Exception startupError){
            // A failed settings save must not leave an enabled module hidden
            // behind an error/disabled checkbox in the host UI.
            if(enabled&&(bool)Get("IsEnabled")!){
                try{await (Task)Api1Runtime.Invoke(contract,module,"SetEnabledAsync",false,dispatcher)!;}
                catch(Exception cleanupError){throw new AggregateException(startupError,cleanupError);}
                finally{MarkEnabled((bool)Get("IsEnabled")!);}
            }
            throw;
        }finally{Transitioning=false;operations.Release();}
    }
    public ValueTask DisposeAsync(){lock(operations)return new(disposal??=DisposeCore());}
    private async Task DisposeCore()
    {
        closing=true;
        try{await (Task)Api1Runtime.Invoke(contract,module,"DisposeAsync",dispatcher)!;await operations.WaitAsync().ConfigureAwait(false);operations.Release();await dispatcher(Save).ConfigureAwait(false);}
        finally{MarkEnabled(false);await dispatcher(registration.Dispose).ConfigureAwait(false);}
    }
    public override void Dispose()=>throw new InvalidOperationException("API 1 模块必须异步释放；宿主不能同步阻塞框架线程。");
}
