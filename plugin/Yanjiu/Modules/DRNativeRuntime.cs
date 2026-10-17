// SPDX-License-Identifier: GPL-3.0-or-later
namespace Yanjiu.Modules;
/// <summary>Initializes native UI only when a DR module is explicitly imported.</summary>
internal static class DRNativeRuntime
{
    private static Task? initialization;private static bool stopping;
    public static Task EnsureReady()
    {
        if(stopping)throw new ObjectDisposedException(nameof(DRNativeRuntime));
        return initialization??=KamiToolKit.KamiToolKitLibrary.InitializeAsync(Services.PluginInterface,"Yanjiu");
    }
    public static void Stop()
    {
        stopping=true;
        if(initialization==null)return;
        _=Cleanup(initialization);
    }
    private static async Task Cleanup(Task task)
    {
        try{await task;await Services.Framework.Run(KamiToolKit.KamiToolKitLibrary.Dispose);}
        catch(Exception e){Services.Log?.Error(e,"DR native UI initialization/cleanup failed");}
    }
}
