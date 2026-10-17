// SPDX-License-Identifier: GPL-3.0-or-later
// Layout matches the MIT public OmenTools MovementInputController fly-input contract.
using System.Runtime.InteropServices;
using OmenTools.Info.Game.AetheryteRecord;
namespace DailyRoutines.Extensions;
[StructLayout(LayoutKind.Explicit,Size=0x18)]
public struct NativeFlyInput
{
    [FieldOffset(0)]public float Forward;
    [FieldOffset(4)]public float Left;
    [FieldOffset(8)]public float Up;
}
public static class YanjiuTeleportExtensions
{
    public static bool TeleportTo(this AetheryteRecord record)=>Yanjiu.Modules.OmniModuleFactory.GetTeleportService().TryTeleport(record);
}
