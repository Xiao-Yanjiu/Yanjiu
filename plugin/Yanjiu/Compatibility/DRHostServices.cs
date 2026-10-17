// SPDX-License-Identifier: GPL-3.0-or-later
using DailyRoutines.Common.Module.Abstractions;
namespace DailyRoutines.Internal
{
    public sealed class ConflictKey
    {
        public Dalamud.Game.ClientState.Keys.VirtualKey Keyboard{get;set;}=Dalamud.Game.ClientState.Keys.VirtualKey.SHIFT;
        public bool IsPressed()=>Yanjiu.Services.Keys!=null&&Yanjiu.Services.Keys[Keyboard];
        public override string ToString()=>Keyboard.ToString();
    }
    public sealed class PluginConfig
    {
        private static readonly PluginConfig current=new();
        public static PluginConfig Instance()=>current;
        public ConflictKey ConflictKeyBinding{get;}=new();
    }
}
namespace DailyRoutines.Manager
{
    public sealed class ModuleManager
    {
        private static readonly ModuleManager current=new();private Yanjiu.Modules.ModuleManager? manager;
        public static ModuleManager Instance()=>current;
        public void Bind(Yanjiu.Modules.ModuleManager value)=>manager=value;
        public T GetModule<T>() where T:ModuleBase=>manager?.Entries.Select(e=>e.Instance).OfType<T>().FirstOrDefault()??throw new InvalidOperationException("模块尚未加载："+typeof(T).Name);
        public bool? IsModuleEnabled(string name)=>manager?.Entries.FirstOrDefault(e=>e.Instance?.ModuleName==name)?.Instance?.IsEnabled;
        public bool IsModuleEnabled(Type type)=>manager?.Entries.Any(e=>e.Instance?.IsEnabled==true&&type.IsInstanceOfType(e.Instance))==true;
    }
}
