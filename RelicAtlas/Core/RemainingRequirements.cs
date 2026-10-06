using System;
using System.Collections.Generic;
using System.Linq;

namespace RelicAtlas.Core;

public sealed record RequirementUse(Series Series, string Job, Stage Stage, Requirement Requirement);
public sealed class RemainingObjective
{
    public required string Key { get; init; }
    public required Requirement Requirement { get; set; }
    public int Required { get; set; }
    public int Recorded { get; set; }
    public int Consumed { get; set; }
    public int SharedConsumed { get; set; }
    public List<RequirementUse> Uses { get; } = [];
    public int Remaining => Math.Max(0, Required - Recorded);
}
public sealed class MaterialNeed
{
    public required string Item { get; init; }
    public bool Hq { get; init; }
    public int Required { get; init; }
    public int Recorded { get; init; }
    public int Consumed { get; init; }
    public int? Bags { get; set; }
    public int? Saddlebag { get; set; }
    public int? Retainers { get; set; }
    public bool IsEstimate => Objectives.Any(o => o.Requirement.Id.StartsWith("shopping-enhancement-", StringComparison.Ordinal));
    public int Owned => (Bags ?? 0) + (Saddlebag ?? 0) + (Retainers ?? 0);
    // A manual material counter may already include held items: never add both credits.
    public int Missing => Math.Max(0, Required - Math.Max(Recorded, Consumed + Owned));
    public List<RemainingObjective> Objectives { get; init; } = [];
}
public sealed record RemainingPlan(int Weapons, List<MaterialNeed> Materials, List<RemainingObjective> Objectives);

public static class RemainingRequirements
{
    public static RemainingPlan Build(Catalog catalog, CharacterProgress character,
        string expansion = "all", string job = "All jobs", bool pinnedOnly = false, bool startedOnly = false,
        IReadOnlyDictionary<string, int>? bags = null, IReadOnlyDictionary<string, int>? saddlebag = null,
        IReadOnlyDictionary<string, int>? retainers = null, string kind = "all")
    {
        var entries = new Dictionary<string, RemainingObjective>();
        var weapons = 0;
        foreach (var series in catalog.Series.Where(s => (expansion == "all" || s.Id == expansion) && (kind == "all" || s.Kind == kind)))
        foreach (var j in series.Jobs.Where(j => job == "All jobs" || j == job))
        {
            var weapon = character.Weapon(series, j);
            var completed = weapon.CompletedIndex(series.Stages.Count);
            if (completed == series.Stages.Count - 1 || pinnedOnly && !weapon.Pinned || startedOnly && completed < 0) continue;
            weapons++;
            foreach (var stage in series.Stages.Skip(completed + 1))
            foreach (var r in stage.Requirements.Concat(AnimaEnhancementMaterials.ForPlanning(character,series,j,stage)).Where(r => r.Applies(j)))
            {
                if (Progress.SharedDone(character, r.Shared)) continue;
                var key = Progress.Key(series, j, stage, r);
                if (!entries.TryGetValue(key, out var entry))
                    entries[key] = entry = new RemainingObjective { Key = key, Requirement = r };
                // Shared cumulative milestones (e.g. 10/20/30 Logos actions) require the maximum, not their sum.
                if (r.Count >= entry.Required) { entry.Required = r.Count; entry.Requirement = r; }
                var sharedCredit = Progress.SharedCredit(character,series,j,stage,r);
                entry.Recorded = Math.Max(entry.Recorded, Math.Max(0,Progress.Status(character, series, j, stage, r, null).Done - (r.Item.Length > 0 ? sharedCredit : 0)));
                entry.Consumed = Math.Max(entry.Consumed, Progress.Consumed(character, series, j, stage, r));
                entry.SharedConsumed = sharedCredit;
                entry.Uses.Add(new(series, j, stage, r));
            }
        }
        var materials = entries.Values.Where(e => e.Requirement.Item.Length > 0)
            .GroupBy(e => (e.Requirement.Item, e.Requirement.Hq))
            .Select(g => new MaterialNeed { Item = g.Key.Item, Hq = g.Key.Hq,
                Required = g.Sum(e => e.Required), Recorded = g.Sum(e => e.Recorded),
                Consumed = g.Sum(e => e.Consumed) + g.GroupBy(e => e.Requirement.Id).Sum(rows => rows.Max(e => e.SharedConsumed)), Objectives = g.ToList() })
            .OrderBy(m => m.Item).ThenByDescending(m => m.Hq).ToList();
        int? Count(IReadOnlyDictionary<string, int>? stock, string key) => stock != null && stock.TryGetValue(key, out var value) ? Math.Max(0, value) : null;
        foreach (var group in materials.GroupBy(m => m.Item))
        {
            var hq = group.FirstOrDefault(m => m.Hq);
            var normal = group.FirstOrDefault(m => !m.Hq);
            var reserve = hq?.Required ?? 0;
            // HQ stock is reserved for HQ requirements first, then the surplus may satisfy general requirements.
            foreach (var source in new[] { bags, saddlebag, retainers }.Select((stock, i) => (stock, i)))
            {
                var all = Count(source.stock, group.Key);
                var high = Count(source.stock, group.Key + "|HQ");
                var reserved = Math.Min(reserve, high ?? 0); reserve -= reserved;
                var general = all.HasValue ? Math.Max(0, all.Value - reserved) : (int?)null;
                if (hq != null) Assign(hq, source.i, high);
                if (normal != null) Assign(normal, source.i, general);
            }
        }
        return new(weapons, materials, entries.Values.Where(e => e.Requirement.Item.Length == 0 && e.Remaining > 0).ToList());
    }
    private static void Assign(MaterialNeed need, int source, int? count)
    { if (source == 0) need.Bags = count; else if (source == 1) need.Saddlebag = count; else need.Retainers = count; }
}
