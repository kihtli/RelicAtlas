using System;
using System.Collections.Generic;
using System.Linq;

namespace RelicAtlas.Core;

/// <summary>One shared stock pool for every ARR job still awaiting its Atma weapon.</summary>
public sealed class AtmaTotals
{
    public int Jobs { get; }
    public IReadOnlyList<MaterialNeed> Materials { get; }
    public int Required => Materials.Sum(m => m.Required);
    public int Remaining => Materials.Sum(m => m.Missing);
    public int? Owned => Materials.Any(m => m.Bags.HasValue || m.Saddlebag.HasValue || m.Retainers.HasValue)
        ? Materials.Sum(m => m.Owned) : null;
    public bool BagsKnown => Materials.All(m => m.Bags.HasValue);
    public bool StorageKnown => Materials.Any(m => m.Saddlebag.HasValue || m.Retainers.HasValue);

    public AtmaTotals(Catalog catalog, CharacterProgress character,
        IReadOnlyDictionary<string, int>? bags = null, IReadOnlyDictionary<string, int>? saddlebag = null,
        IReadOnlyDictionary<string, int>? retainers = null)
    {
        var arr = catalog.Series.Single(s => s.Id == "arr");
        var stage = arr.Stages.Single(s => s.Id == "atma");
        var index = arr.Stages.IndexOf(stage);
        var jobs = arr.Jobs.Where(j => character.Weapon(arr, j).CompletedIndex(arr.Stages.Count) < index).ToArray();
        Jobs = jobs.Length;
        // Keep all twelve types even when every job is finished, so surplus stock remains visible.
        Materials = stage.Requirements.Where(r => r.Item.Length > 0).Select(r =>
        {
            var required = 0; var recorded = 0;
            foreach (var job in jobs.Where(r.Applies))
            {
                var key = Progress.Key(arr, job, stage, r);
                required += r.Count;
                recorded += Math.Clamp(character.Counters.TryGetValue(key, out var manual)
                    ? manual : character.DetectedCounters.GetValueOrDefault(key), 0, r.Count);
            }
            // MaterialNeed credits max(recorded, owned), matching the shopping list without double-counting.
            return new MaterialNeed { Item = r.Item, Required = required, Recorded = recorded,
                Bags = Count(bags, r.Item), Saddlebag = Count(saddlebag, r.Item), Retainers = Count(retainers, r.Item) };
        }).ToArray();
    }

    private static int? Count(IReadOnlyDictionary<string, int>? stock, string item) =>
        stock != null && stock.TryGetValue(item, out var count) ? Math.Max(0, count) : null;
}
