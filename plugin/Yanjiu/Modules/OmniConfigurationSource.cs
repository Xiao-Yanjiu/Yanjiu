// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections;
using System.Reflection;
using OmniToolbox.Common.Module.Abstractions;
using OmniToolbox.Common.Module.Models;
namespace Yanjiu.Modules;
internal sealed class OmniConfigurationSource(ModuleManager manager):IAutoSwitchConfigurationSource
{
    private static object Profiles=>OmniToolbox.Host.DalamudServices.GetInternalService("Dalamud.Plugin.Internal.Profiles.ProfileManager")??throw new InvalidOperationException("当前卫月未提供配置集合管理器");
    private static IEnumerable<object> EnumerateProfiles(){
        var host=Profiles;var values=host.GetType().GetProperty("Profiles")?.GetValue(host) as IEnumerable??throw new MissingMemberException("卫月配置集合接口已变化");
        return values.Cast<object>().Where(p=>p.GetType().GetProperty("IsDefaultProfile")?.GetValue(p) is false).ToArray();
    }
    public IReadOnlyList<PluginCollectionOption> GetPluginCollections()=>EnumerateProfiles().Select(p=>new PluginCollectionOption((Guid)(p.GetType().GetProperty("Guid")?.GetValue(p)??throw new MissingMemberException("Profile.Guid")),(string)(p.GetType().GetProperty("Name")?.GetValue(p)??""))).ToArray();
    public IReadOnlyList<ToolbarCommandOption> GetToolbarCommands()=>manager.Entries.Where(e=>e.Instance is OmniToolbox.Common.Module.Abstractions.ModuleBase).SelectMany(e=>((OmniToolbox.Common.Module.Abstractions.ModuleBase)e.Instance!).Info.Commands??[]).Select(c=>new ToolbarCommandOption(OmniToolbox.UI.OmniLoc.Get(c.DescriptionKey),c.ClipboardText)).Distinct().ToArray();
    public async Task<bool> SetPluginCollectionEnabledAsync(Guid profileID,bool enabled,CancellationToken cancellationToken){
        cancellationToken.ThrowIfCancellationRequested();
        var profile=EnumerateProfiles().SingleOrDefault(p=>p.GetType().GetProperty("Guid")?.GetValue(p) is Guid id&&id==profileID)??throw new InvalidOperationException("配置集合已不存在");
        var method=profile.GetType().GetMethod("SetStateAsync",[typeof(bool),typeof(bool)])??throw new MissingMethodException("Profile.SetStateAsync");
        var task=method.Invoke(profile,[enabled,true]) as Task??throw new InvalidOperationException("卫月配置集合返回了无效结果");
        await task.WaitAsync(cancellationToken);
        return profile.GetType().GetProperty("IsEnabled")?.GetValue(profile) is bool actual&&actual==enabled;
    }
}
