using System.Collections.Generic;
using System.Linq;
namespace RelicAtlas.Core;
public static class AnimaProgress
{
    public static int? CommittedEnhancement(int proposed, int committed, int limit,
        IReadOnlyList<int> draftStats, IReadOnlyList<int> savedStats)
    {
        if (limit != 180 || committed is < 0 or > 180 || proposed != committed ||
            draftStats.Count is not (5 or 10) || savedStats.Count != draftStats.Count ||
            draftStats.Any(n => n is < 0 or > 180) || savedStats.Any(n => n is < 0 or > 180) ||
            !draftStats.SequenceEqual(savedStats) || savedStats.Sum() != committed) return null;
        return committed;
    }

    public static bool RecordEnhancement(CharacterProgress character, string job, int value)
    {
        if (value is < 0 or > 180 || job.Length == 0) return false;
        var key = $"hw/{job}/reconditioned/allocated-enhancement-points";
        if (character.DetectedCounters.TryGetValue(key,out var old) && old == value) return false;
        character.DetectedCounters[key] = value; return true;
    }

    public static bool RecordLiveDensity(CharacterProgress character, string owner, int questSequence, bool hasSharpenedWeapon, int value)
    {
        // Density belongs to Born Again Anima's accepted job, even after switching jobs.
        if (questSequence != 5 || !hasSharpenedWeapon) return false;
        return RecordDensity(character, owner, value);
    }

    public static bool RecordDensity(CharacterProgress character, string job, int value)
    {
        if (value is < 0 or > 2000 || job.Length == 0) return false;
        var key = $"hw/{job}/complete/aetheric-density";
        if (character.DetectedCounters.TryGetValue(key,out var old) && old == value) return false;
        character.DetectedCounters[key] = value; return true;
    }
}
