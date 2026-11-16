using OmenTools.Info.Game.Packets.Upstream;
using OmenTools;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Interface.ImGuiNotification;
using OmniToolbox.Notifications;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using NativeObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;
namespace OmniToolbox.TreePublic;
public sealed partial class ToolbarRenderer
{
    private static readonly HashSet<uint> BellIds = [196630,2000072,2000401,2000403,2000439,2000441,2000661,2001271,2001358,2006565,2010284,2014985,2015131];
    private long lastBellInteraction, bellDue;
    private ulong bellObject;
    private uint bellEvent, bellTerritory;
    private unsafe void InteractWithRetainerBell()
    {
        var services = DService.Instance();
        var player = services.ObjectTable.LocalPlayer;
        if (player == null || services.Condition[ConditionFlag.InCombat] || services.Condition[ConditionFlag.OccupiedInEvent]) return;
        if (Environment.TickCount64 - lastBellInteraction < 8000) return;
        var bell = services.ObjectTable.Where(x => x.IsTargetable)
            .Where(x => BellIds.Contains(x.DataID) || x.Name.ToString() is "雇员铃" or "Summoning Bell" or "呼び鈴" or "Sonnette" or "Krämerklingel")
            .Where(x => Vector2.Distance(new(player.Position.X, player.Position.Z), new(x.Position.X, x.Position.Z)) <= 100f)
            .OrderBy(x => Vector3.DistanceSquared(player.Position, x.Position)).FirstOrDefault();
        if (bell == null) { OmniNotifier.Popup("雇员铃", "100 米内没有已加载且可交互的雇员铃。", NotificationType.Info); return; }
        var eventId = bell.EventID.Id != 0 ? bell.EventID.Id : bell.EventHandler?.EventID.Id ?? 0;
        if (eventId == 0 || UpstreamOpcode.PositionUpdateOpcode == 0 || UpstreamOpcode.EventStartOpcode == 0)
        { OmniNotifier.Popup("雇员铃", "交互事件或网络接口尚未就绪。", NotificationType.Warning); return; }
        bellObject = bell.GameObjectID; bellEvent = eventId; bellTerritory = services.ClientState.TerritoryType;
        PositionUpdatePacket.Send(player.Rotation, bell.Position + new Vector3(0,-4,0));
        lastBellInteraction = Environment.TickCount64; bellDue = lastBellInteraction + 1400;
    }
    private void TickRetainerBell()
    {
        if (bellDue == 0) return;
        var services = DService.Instance();
        if (runtimeLifetime is null || runtimeLifetime.IsStopping || services.ObjectTable.LocalPlayer == null ||
            services.ClientState.TerritoryType != bellTerritory || services.Condition[ConditionFlag.InCombat]) { bellDue = 0; return; }
        if (Environment.TickCount64 < bellDue) return;
        bellDue = 0;
        var bell = services.ObjectTable.SearchByID(bellObject);
        if (bell == null || !bell.IsTargetable || !BellIds.Contains(bell.DataID)) return;
        var eventId = bell.EventID.Id != 0 ? bell.EventID.Id : bell.EventHandler?.EventID.Id ?? 0;
        if (eventId != bellEvent || UpstreamOpcode.EventStartOpcode == 0) return;
        new EventStartPackt(bellObject, bellEvent).Send();
    }
}
