using Lumina.Excel;
using Lumina.Excel.Sheets;

namespace RelicAtlas.Tracking;

// Relic sheets contain empty/unused references, including nested monster names.
// Missing rows are normal data: never dereference RowRef.Value unconditionally.
public static class BookRowNames
{
    public static string Item(RowRef<Item> reference) =>
        reference.TryGetValue(out var row) ? row.Name.ExtractText() : "";

    public static string EventItem(RowRef<EventItem> reference) =>
        reference.TryGetValue(out var row) ? row.Name.ExtractText() : "";

    public static string Monster(RowRef<MonsterNoteTarget> reference) =>
        reference.TryGetValue(out var target) ? MonsterName(target.BNpcName) : "";

    public static string MonsterName(RowRef<BNpcName> reference) =>
        reference.TryGetValue(out var row) ? row.Singular.ExtractText() : "";

    public static string Fate(RowRef<Fate> reference) =>
        reference.TryGetValue(out var row) ? row.Name.ExtractText() : "";

    public static string Leve(RowRef<Leve> reference) =>
        reference.TryGetValue(out var row) ? row.Name.ExtractText() : "";
}
