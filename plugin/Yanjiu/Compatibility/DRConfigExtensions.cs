// SPDX-License-Identifier: GPL-3.0-or-later
using DailyRoutines.Common.Module.Abstractions;
using DailyRoutines.Common.Module.Models;
namespace DailyRoutines.Extensions;

/// <summary>Configuration bridges use Yanjiu's actual per-module store, never the DR host.</summary>
public static class ConfigExtensions
{
    extension<T>(T config) where T:ModuleConfig
    {
        public static T? Load(ModuleBase owner)=>owner.LoadConfig<T>();
        public void Save(ModuleBase owner)=>owner.SaveConfig(config);
    }
}

/// <summary>Holding Shift aborts the real task queue; it never injects game input.</summary>
public static class TaskCancellationExtensions
{
    public static bool AbortByConflictKey(this OmenTools.Threading.TaskHelper.TaskHelper tasks,ModuleBase owner)
    {
        if(!tasks.IsBusy||Yanjiu.Services.Keys==null||!Yanjiu.Services.Keys[Dalamud.Game.ClientState.Keys.VirtualKey.SHIFT])return false;
        tasks.Abort();return true;
    }
    extension(global::OmenTools.ImGuiOm.ImGuiOm)
    {
        public static void ConflictKeyText()=>Dalamud.Bindings.ImGui.ImGui.TextDisabled("按住 Shift 取消当前自动化任务");
    }
}
