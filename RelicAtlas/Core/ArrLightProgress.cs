using System;
using System.Collections.Generic;
using System.Linq;

namespace RelicAtlas.Core;

public sealed record ArrLightReading(string Job, string SourceStage, int Component, int Value, bool Active);

public static class ArrLightProgress
{
    // The client uses the spiritbond field only when InventoryItem's Relic flag is set.
    // Nexus: 0..2000. Mahatmas: index * 500 + 0..80; catalogue units are 0..40.
    public static bool Apply(Series arr, CharacterProgress character, IEnumerable<ArrLightReading> readings)
    {
        var changed = false;
        foreach (var read in readings)
        {
            if (!arr.Jobs.Contains(read.Job) || read.SourceStage is not ("novus" or "zodiac-braves")) continue;
            var source = arr.Stages.Single(s => s.Id == read.SourceStage);
            if (read.Component < 0 || read.Component >= source.Weapons[read.Job].Count) continue;
            if (read.Active && (read.Value < 0 || (read.SourceStage == "novus" ? read.Value > 2000 :
                read.Value > 5580 || read.Value % 500 > 80))) continue;
            var key = $"{read.Job}/{read.SourceStage}/{read.Component}";
            var raw = read.Active ? read.Value : -1;
            if (character.ArrLight.TryGetValue(key, out var prior) && prior == raw) continue;
            character.ArrLight[key] = raw; changed = true;
        }
        foreach (var job in arr.Jobs)
        foreach (var sourceId in new[] { "novus", "zodiac-braves" })
        {
            var target = arr.Stages.Single(s => s.Id == (sourceId == "novus" ? "nexus" : "zeta"));
            var tier = arr.Stages.IndexOf(target);
            var weapon = character.Weapon(arr, job);
            var components = target.Weapons[job].Count;
            bool Upgraded(int part) => weapon.ObservedStage >= tier || arr.Stages.Skip(tier)
                .Any(s => s.Weapons[job].Count > part && weapon.ObservedWeapons.Contains(s.Weapons[job][part]));
            int? Raw(int part) => Upgraded(part) ? (sourceId == "novus" ? 2000 : 5580) :
                character.ArrLight.TryGetValue($"{job}/{sourceId}/{part}", out var value) ? value : null;
            var raw = Enumerable.Range(0,components).Select(Raw).ToArray();
            if (raw.All(v => !v.HasValue)) continue;
            void Set(Requirement r, int value)
            {
                var key = Progress.Key(arr,job,target,r);
                if (character.DetectedCounters.TryGetValue(key,out var old) && old == value) return;
                character.DetectedCounters[key] = value; changed = true;
            }
            if (sourceId == "novus")
            {
                Set(target.Requirements.Single(r => r.Id == "soulglaze-the-novus-weapon"),raw.All(v => v >= 0) ? 1 : 0);
                Set(target.Requirements.Single(r => r.Id == "soul-attunement-light"),raw.Min(v => Math.Max(0,v ?? 0)));
            }
            else
            {
                for (var index = 0; index < 12; index++)
                {
                    var offset = index * 500;
                    Set(target.Requirements[index],raw.Min(v => Math.Clamp(((v ?? -1) - offset) / 2,0,40)));
                }
            }
        }
        return changed;
    }
}
