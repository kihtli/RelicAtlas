using System;
using System.Collections.Generic;
using System.Linq;

namespace RelicAtlas.Core;

public sealed record ArrScrollDefinition(uint ItemId, string Job, int Component, int Limit);
public sealed record ArrScrollReading(uint ItemId, int Infused, int Limit);

// Counts are read from the game's infusion window, never inferred from materia or stone losses.
public sealed class ArrScrollTracking(Series series, IReadOnlyDictionary<uint, ArrScrollDefinition> definitions)
{
    private ulong owner;
    private HashSet<uint> previousHeld = [];
    public void Reset() { owner = 0; previousHeld.Clear(); }

    public bool Update(ulong characterId, CharacterProgress character, IReadOnlySet<uint> held, ArrScrollReading? reading)
    {
        if (characterId == 0) { Reset(); return false; }
        if (owner != characterId) { Reset(); owner = characterId; }
        var changed = false;
        var confirmed = held.Where(previousHeld.Contains).ToHashSet();
        previousHeld = held.ToHashSet();
        foreach (var item in confirmed.Where(definitions.ContainsKey))
            changed |= character.ObtainedScrolls.Add(item);
        if (reading is { } value && confirmed.Contains(value.ItemId) &&
            definitions.TryGetValue(value.ItemId, out var definition) && value.Limit == definition.Limit &&
            value.Infused >= 0 && value.Infused <= value.Limit)
        {
            if (!character.ScrollInfusions.TryGetValue(value.ItemId, out var old) || old != value.Infused)
            { character.ScrollInfusions[value.ItemId] = value.Infused; changed = true; }
        }
        var stage = series.Stages.Single(s => s.Id == "novus");
        var tier = series.Stages.IndexOf(stage);
        foreach (var group in definitions.Values.GroupBy(d => d.Job))
        {
            var weapon = character.Weapon(series, group.Key);
            bool Upgraded(ArrScrollDefinition d) => weapon.ObservedStage >= tier || series.Stages.Skip(tier)
                .Any(s => s.Weapons[d.Job].Count > d.Component && weapon.ObservedWeapons.Contains(s.Weapons[d.Job][d.Component]));
            var obtained = group.All(d => Upgraded(d) || character.ObtainedScrolls.Contains(d.ItemId));
            var count = group.Sum(d => Upgraded(d) ? d.Limit : Math.Clamp(character.ScrollInfusions.GetValueOrDefault(d.ItemId), 0, d.Limit));
            void Set(string id, int amount, bool known = false)
            {
                var key = Progress.Key(series, group.Key, stage, stage.Requirements.Single(r => r.Id == id));
                if (character.DetectedCounters.TryGetValue(key, out var previous) ? previous == amount : amount == 0 && !known) return;
                character.DetectedCounters[key] = amount; changed = true;
            }
            Set("obtain-the-sphere-scroll", obtained ? 1 : 0);
            Set("successful-materia-infusions", count, group.Any(d => Upgraded(d) || character.ScrollInfusions.ContainsKey(d.ItemId)));
        }
        return changed;
    }
}
