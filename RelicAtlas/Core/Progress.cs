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
    public ArrBookObservation? DetectedBook { get; set; }
    public HashSet<uint> ObtainedScrolls { get; set; } = [];
    public Dictionary<uint, int> ScrollInfusions { get; set; } = [];
    public Dictionary<string, int> ArrLight { get; set; } = [];
    public Dictionary<string, int> AnimaExchangeCredits { get; set; } = [];
    public Dictionary<string, int> ZodiacMaterialCredits { get; set; } = [];
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
        r.Shared.Length > 0 ? $"shared/{r.Shared}/{r.Id}" : r.CumulativeKey.Length > 0 ?
            $"{series.Id}/{job}/cumulative/{r.CumulativeKey}" : $"{series.Id}/{job}/{stage.Id}/{r.Id}";

    public static bool SharedDone(CharacterProgress c, string key) =>
        key.Length > 0 && (c.SharedOverrides.TryGetValue(key, out var manual) ? manual : c.CompletedShared.Contains(key));

    public static RequirementStatus Status(CharacterProgress c, Series series, string job, Stage stage,
        Requirement r, IReadOnlyDictionary<string, int>? inventory)
    {
        if (SharedDone(c, r.Shared)) return new(r.Count, r.Count, null, true);
        var key = Key(series, job, stage, r);
        int? bags = r.Item.Length > 0 && inventory != null && inventory.TryGetValue(r.Item + (r.Hq ? "|HQ" : ""), out var held) ? held : null;
        // A recorded objective is authoritative; inventory is a live hint, never a permanent completion event.
        var done = c.Counters.TryGetValue(key, out var manual) ? manual :
            Consumed(c, series, job, stage, r) + SharedCredit(c,series,job,stage,r) + (bags ?? c.DetectedCounters.GetValueOrDefault(key));
        // An acquired tier proves its prerequisites, including materials already handed in.
        // Explicit objective/shared corrections and a manually selected earlier tier still win.
        if (!c.Counters.ContainsKey(key) && !c.SharedOverrides.ContainsKey(r.Shared) &&
            series.Stages.IndexOf(stage) is var index && index >= 0 &&
            index <= c.Weapon(series, job).CompletedIndex(series.Stages.Count)) done = r.Count;
        if (r.CumulativeKey.Length > 0 && !c.Counters.ContainsKey(key))
        {
            var completed = c.Weapon(series, job).CompletedIndex(series.Stages.Count);
            var minimum = series.Stages.Take(completed + 1).SelectMany(s => s.Requirements)
                .Where(p => p.Applies(job) && p.CumulativeKey == r.CumulativeKey).Select(p => p.Count).DefaultIfEmpty(0).Max();
            done = Math.Max(done, minimum);
        }
        return new(Math.Clamp(done, 0, r.Count), r.Count, bags, false);
    }

    public static int SharedCredit(CharacterProgress c, Series series, string job, Stage stage, Requirement r) =>
        ArrZodiacMaterials.Credit(c,series,job,stage,r) + AnimaExchangeProgress.Credit(c,series,job,stage,r);

    public static int Consumed(CharacterProgress c, Series series, string job, Stage stage, Requirement r) =>
        series.Id == "arr" && stage.Id == "novus" && r.Id == "alexandrite" &&
        !c.Counters.ContainsKey(Key(series, job, stage, r))
            ? Math.Clamp(c.DetectedCounters.GetValueOrDefault($"arr/{job}/novus/successful-materia-infusions"), 0, r.Count) : 0;

    public static (Stage? Stage, Requirement? Requirement) Next(CharacterProgress c, Series series, string job,
        IReadOnlyDictionary<string, int>? inventory)
    {
        var index = c.Weapon(series, job).CompletedIndex(series.Stages.Count) + 1;
        if (index >= series.Stages.Count) return (null, null);
        var stage = series.Stages[index];
        var book = c.DetectedBook?.Job == job ? c.DetectedBook.Group : c.ActiveBooks.GetValueOrDefault(job);
        if (series.Id == "arr" && stage.Id == "animus" && book != null)
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
