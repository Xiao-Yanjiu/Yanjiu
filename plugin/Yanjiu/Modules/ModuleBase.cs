// SPDX-License-Identifier: GPL-3.0-or-later
using System.Text.Json;
namespace Yanjiu.Modules;
public sealed record ModuleDescriptor(string Title,string Description,string Author,string Category);
public abstract class ModuleBase : IDisposable
{
    private bool disposed;
    public abstract ModuleDescriptor Descriptor { get; }
    public bool IsEnabled { get; private set; }
    public string ModuleName=>GetType().Name;
    public virtual bool HasSettings=>false;
    public virtual bool DrawSettings()=>false;
    public virtual bool ResetSettings()=>false;
    public virtual bool TryHandleCommand(string arguments)=>false;
    public virtual bool TryHandleCommand(string command,string arguments)=>TryHandleCommand(arguments);
    public void SetEnabled(bool enabled)
    {
        ObjectDisposedException.ThrowIf(disposed,this);if(enabled==IsEnabled)return;
        if(enabled){try{OnEnable();IsEnabled=true;}catch{try{OnDisable();}finally{IsEnabled=false;}throw;}}
        else{try{OnDisable();}finally{IsEnabled=false;}}
    }
    protected virtual void OnEnable(){}
    protected virtual void OnDisable(){}
    protected virtual void OnDispose(){}
    protected virtual bool OnInterruptAutomation()=>false;
    public bool InterruptAutomation()=>!disposed&&OnInterruptAutomation();
    protected void OpenIconBrowser(Action<uint> select){if(Services.IconPicker==null)throw new InvalidOperationException("图标选择器未就绪");Services.IconPicker(select);}
    protected T? ReadConfiguration<T>(){var path=ConfigPath;return File.Exists(path)?JsonSerializer.Deserialize<T>(File.ReadAllText(path)):default;}
    protected void WriteConfiguration<T>(T data){Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);File.WriteAllText(ConfigPath+".tmp",JsonSerializer.Serialize(data));File.Move(ConfigPath+".tmp",ConfigPath,true);}
    private string ConfigPath=>Path.Combine(Services.ConfigDirectory,"modules",GetType().FullName!.Replace(':','_')+".json");
    public void Dispose(){if(disposed)return;try{if(IsEnabled)OnDisable();}finally{try{OnDispose();}finally{IsEnabled=false;disposed=true;}}}
}
