using System.Linq;
using System.Collections.Generic;

namespace RelicAtlas.Core;

public static class ArrRelicQuestProgress
{
    // All ten job-specific, non-repeatable A Relic Reborn quests use these sequences.
    // A sequence is credited only once the journal has advanced beyond its objective.
    public static int CompletedAfter(Requirement r) => r.Id switch
    {
        var id when id.StartsWith("recover-the-broken-weapon-") => 1,
        var id when id.StartsWith("deliver-") => 3,
        "defeat-the-dhorme-chimera" => 4,
        "obtain-the-amdapor-glyph" => 7,
        var _ when r.Count == 8 && r.Jobs.Count == 1 => 10,
        "defeat-the-hydra" => 12,
        "the-bowl-of-embers-hard" => 15,
        "the-howling-eye-hard" => 16,
        "the-navel-hard" => 17,
        _ => 0,
    };

    public static bool Apply(Series arr, CharacterProgress character, string job, byte sequence, bool complete)
    {
        if (!arr.Jobs.Contains(job)) return false;
        var stage = arr.Stages.Single(s => s.Id == "relic");
        var changed = false;
        foreach (var r in stage.Requirements.Where(r => r.Applies(job) && CompletedAfter(r) > 0))
        {
            var count = complete || sequence > CompletedAfter(r) ? r.Count : 0;
            var key = Progress.Key(arr,job,stage,r);
            if (character.DetectedCounters.GetValueOrDefault(key) == count) continue;
            character.DetectedCounters[key] = count; changed = true;
        }
        return changed;
    }
}
