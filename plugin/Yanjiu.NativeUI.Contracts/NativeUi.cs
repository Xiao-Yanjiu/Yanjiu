// SPDX-License-Identifier: GPL-3.0-or-later
using System.Runtime.CompilerServices;
[assembly:InternalsVisibleTo("Yanjiu")]
namespace Yanjiu.NativeUI;

/// <summary>Creates native module windows through the host. No native toolkit dependency is required.</summary>
public static class NativeUi
{
    internal static Func<object,NativePanel,Task>? OpenHandler;
    internal static Func<object,string,Task>? CloseHandler;
    public static Task OpenAsync(object module,NativePanel panel)
    {
        ArgumentNullException.ThrowIfNull(module);ArgumentNullException.ThrowIfNull(panel);panel.Validate();
        return (OpenHandler??throw new InvalidOperationException("Yanjiu原生UI服务尚未就绪。"))(module,panel);
    }
    public static Task CloseAsync(object module,string panelId)
    {
        ArgumentNullException.ThrowIfNull(module);ArgumentException.ThrowIfNullOrWhiteSpace(panelId);
        return CloseHandler?.Invoke(module,panelId)??Task.CompletedTask;
    }
}
