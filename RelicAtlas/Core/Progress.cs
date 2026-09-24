using System;
using System.Collections.Generic;
using System.Linq;

namespace RelicAtlas.Core;

public sealed class CharacterProgress
{
    public string Name { get; set; } = "";
    public Dictionary<string, WeaponProgress> Weapons { get; set; } = [];
    public Dictionary<string, int> Counters { get; set; } = [];
    public Dictionary<string, int> DetectedCounters { get; set; } = [];
    public Dictionary<string, string> ActiveBooks { get; set; } = [];
    public HashSet<string> CompletedShared { get; set; } = [];
    public Dictionary<string, bool> SharedOverrides { get; set; } = [];
    public WeaponProgress Weapon(Series series, string job)
    {
        var key = $"{series.Id}/{job}";
        if (!Weapons.TryGetValue(key, out var value)) Weapons[key] = value = new();
        return value;
    }
}
public sealed class WeaponProgress
{
    public int ObservedStage { get; set; } = -1;
    public int? ManualStage { get; set; }
    public bool Pinned { get; set; }
    public string Notes { get; set; } = "";
    // Separate PLD sword/shield evidence. One half must never complete the pair.
    public HashSet<string> ObservedWeapons { get; set; } = [];
    public int CompletedIndex(int count) => Math.Clamp(ManualStage ?? ObservedStage, -1, count - 1);
}
public readonly record struct RequirementStatus(int Done, int Required, int? InBags, bool SharedComplete)
{
    public bool Complete => Done >= Required;
    public int Remaining => Math.Max(0, Required - Done);
}
public static class Progress
{
    public static string Key(Series series, string job, Stage stage, Requirement r) =>
        r.Shared.Length > 0 ? $"shared/{r.Shared}/{r.Id}" : $"{series.Id}/{job}/{stage.Id}/{r.Id}";

    public static bool SharedDone(CharacterProgress c, string key) =>
        key.Length > 0 && (c.SharedOverrides.TryGetValue(key, out var manual) ? manual : c.CompletedShared.Contains(key));

    public static RequirementStatus Status(CharacterProgress c, Series series, string job, Stage stage,
        Requirement r, IReadOnlyDictionary<string, int>? inventory)
    {
        if (SharedDone(c, r.Shared)) return new(r.Count, r.Count, null, true);
        var key = Key(series, job, stage, r);
        int? bags = r.Item.Length > 0 && inventory != null && inventory.TryGetValue(r.Item + (r.Hq ? "|HQ" : ""), out var held) ? held : null;
        // A recorded objective is authoritative; inventory is a live hint, never a permanent completion event.
        var done = c.Counters.TryGetValue(key, out var manual) ? manual : bags ?? c.DetectedCounters.GetValueOrDefault(key);
        return new(Math.Clamp(done, 0, r.Count), r.Count, bags, false);
    }

    public static (Stage? Stage, Requirement? Requirement) Next(CharacterProgress c, Series series, string job,
        IReadOnlyDictionary<string, int>? inventory)
    {
        var index = c.Weapon(series, job).CompletedIndex(series.Stages.Count) + 1;
        if (index >= series.Stages.Count) return (null, null);
        var stage = series.Stages[index];
        if (series.Id == "arr" && stage.Id == "animus" && c.ActiveBooks.TryGetValue(job, out var book))
        {
            var active = stage.Requirements.FirstOrDefault(r => r.Group == book && r.Applies(job) &&
                !Status(c, series, job, stage, r, inventory).Complete);
            if (active != null) return (stage, active);
        }
        return (stage, stage.Requirements.FirstOrDefault(r => r.Applies(job) && !Status(c, series, job, stage, r, inventory).Complete));
    }

    public static bool Observe(CharacterProgress c, Series series, string job, IReadOnlySet<string> owned)
    {
        var weapon = c.Weapon(series, job);
        bool changed = false;
        foreach (var stage in series.Stages)
            foreach (var name in stage.Weapons[job])
                if (owned.Contains(name)) changed |= weapon.ObservedWeapons.Add(name);

        // Each component may be at a different stage. An upgraded sword proves its earlier sword stages,
        // but doesn't prove shield progress. All components must have been observed at this tier or above.
        for (int i = weapon.ObservedStage + 1; i < series.Stages.Count; i++)
        {
            var components = series.Stages[i].Weapons[job].Count;
            bool all = true;
            for (int part = 0; part < components; part++)
                all &= series.Stages.Skip(i).Any(s => s.Weapons[job].Count > part && weapon.ObservedWeapons.Contains(s.Weapons[job][part]));
            if (all) { weapon.ObservedStage = i; changed = true; }
        }
        return changed;
    }
}
