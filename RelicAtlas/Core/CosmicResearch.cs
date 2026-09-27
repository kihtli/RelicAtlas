using System;
using System.Collections.Generic;
using System.Linq;

namespace RelicAtlas.Core;

public static class CosmicResearch
{
    // WKSCosmoToolClass 1..11 = ClassJob 8..18. The research module stores
    // seven analysis totals per class in that order, without the sheet's empty row 0.
    public static readonly string[] Jobs = ["CRP", "BSM", "ARM", "GSM", "LTW", "WVR", "ALC", "CUL", "MIN", "BTN", "FSH"];
    public static bool Apply(Catalog catalog, CharacterProgress character, ReadOnlySpan<ushort> analysis)
    {
        if (analysis.Length != Jobs.Length * 7) return false;
        var series = catalog.Series.SingleOrDefault(s => s.Id == "cosmic");
        if (series == null) return false;
        var changed = false;
        for (var job = 0; job < Jobs.Length; job++)
        for (var type = 0; type < 7; type++)
        {
            var key = $"cosmic/{Jobs[job]}/cumulative/research-{type + 1}";
            var value = (int)analysis[job * 7 + type];
            if (character.DetectedCounters.GetValueOrDefault(key) == value) continue;
            character.DetectedCounters[key] = value;
            changed = true;
        }
        // Data thresholds alone don't prove that Researchingway handed over a tool.
        return changed;
    }
}
