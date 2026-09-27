using System;
using System.Collections.Generic;
using System.Linq;

namespace RelicAtlas.Core;

public sealed record AtmaFarmRow(AtmaArea Area, MaterialNeed Need, bool Suggested);

/// <summary>Only shortages for the live character; highlights follow actual ST windows, not early travel.</summary>
public sealed class AtmaFarmView
{
    public AtmaWindow Current { get; }
    public AtmaWindow? NextNeeded { get; }
    public IReadOnlyList<AtmaFarmRow> Rows { get; }
    public bool CurrentNeeded => Rows.Any(r => r.Suggested);

    public AtmaFarmView(AtmaTotals totals, DateTimeOffset now)
    {
        Current = AtmaSchedule.At(now);
        var needs = totals.Materials.ToDictionary(m => m.Item);
        Rows = AtmaSchedule.Areas.Where(a => needs[a.Item].Missing > 0)
            .Select(a => new AtmaFarmRow(a, needs[a.Item], a == Current.Area)).ToArray();
        NextNeeded = Enumerable.Range(1, 12).Select(hours => AtmaSchedule.At(Current.Start.AddHours(hours)))
            .FirstOrDefault(window => needs[window.Area.Item].Missing > 0);
    }
}
