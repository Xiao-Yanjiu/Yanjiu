// SPDX-License-Identifier: GPL-3.0-or-later
using System.Reflection;
using Dalamud.Plugin;
namespace Yanjiu.Modules;

// The public libraries own a separate service configuration folder. Framework,
// command and UI services still come from the actual Yanjiu plugin interface.
public class Api1PluginInterface:DispatchProxy
{
    private IDalamudPluginInterface target=null!;
    public static IDalamudPluginInterface Create(IDalamudPluginInterface target)
    {
        var proxy=Create<IDalamudPluginInterface,Api1PluginInterface>();
        ((Api1PluginInterface)(object)proxy).target=target;return proxy;
    }
    protected override object? Invoke(MethodInfo? method,object?[]? args)
    {
        if(method==null)throw new MissingMethodException();
        if(method.Name=="GetPluginConfigDirectory"){
            var path=Path.Combine(target.GetPluginConfigDirectory(),"api1-runtime");Directory.CreateDirectory(path);return path;
        }
        try{return method.Invoke(target,args);}catch(TargetInvocationException e)when(e.InnerException!=null){System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e.InnerException).Throw();throw;}
    }
}
