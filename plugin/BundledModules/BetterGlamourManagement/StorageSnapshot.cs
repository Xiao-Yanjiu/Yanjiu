using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Dalamud.Utility;

namespace OmniToolbox.TreePublic;

// Keep the last observed special-storage state until the next successful load;
// an unopened dresser must not overwrite a known inventory with an empty snapshot.
internal sealed class StorageSnapshot
{
    internal HashSet<uint> Dresser { get; private set; } = [];
    internal HashSet<uint> Armoire { get; private set; } = [];
    internal long Revision { get; private set; }
    private ulong character;
    private long nextScan;
    private readonly Dictionary<uint, uint> cabinetRows = DalamudServices.DataManager
        .GetExcelSheet<Lumina.Excel.Sheets.Cabinet>().Where(x => x.Item.RowId != 0)
        .GroupBy(x => x.Item.RowId).ToDictionary(x => x.Key, x => x.First().RowId);

    internal unsafe void Tick()
    {
        if (Environment.TickCount64 < nextScan) return;
        nextScan = Environment.TickCount64 + 500;
        var current = DalamudServices.PlayerState.IsLoaded ? DalamudServices.PlayerState.ContentId : 0;
        if (character != current) { character = current; Dresser.Clear(); Armoire.Clear(); Revision++; }
        if (current == 0) return;
        var mirage = MirageManager.Instance();
        if (mirage != null && mirage->PrismBoxLoaded)
        {
            HashSet<uint> found = [];
            foreach (var raw in mirage->PrismBoxItemIds)
            {
                var id = ItemUtil.GetBaseId(raw).ItemId;
                if (id != 0) found.Add(id);
            }
            if (!Dresser.SetEquals(found)) { Dresser = found; Revision++; }
        }
        var ui = UIState.Instance();
        if (ui != null && ui->Cabinet.State == Cabinet.CabinetState.Loaded)
        {
            HashSet<uint> found = [];
            foreach (var entry in cabinetRows)
                if (ui->Cabinet.IsItemInCabinet(entry.Value)) found.Add(entry.Key);
            if (!Armoire.SetEquals(found)) { Armoire = found; Revision++; }
        }
    }
}
