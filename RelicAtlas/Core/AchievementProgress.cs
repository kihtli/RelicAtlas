using System;
using System.Collections.Generic;
using System.Linq;

namespace RelicAtlas.Core;

public readonly record struct AchievementProgressReading(uint Id, uint Current, uint Maximum, bool Requested, bool Loaded);

/// <summary>Serializes objective queries against the game's single shared achievement progress slot.</summary>
public sealed class AchievementProgress
{
    private sealed record Objective(Series Series, Stage Stage, Requirement Requirement, string Job)
    {
        public string Key => Progress.Key(Series, Job, Stage, Requirement);
    }

    private readonly Dictionary<uint, Objective[]> objectives;
    private readonly Dictionary<uint, DateTime> nextDue = [];
    private ulong owner;
    private uint pending;
    private bool sawRequested;
    private DateTime deadline;
    private DateTime nextRequest;

    public AchievementProgress(IEnumerable<(Series Series, Stage Stage, Requirement Requirement)> bindings)
    {
        objectives = bindings.Where(b => b.Requirement.Achievement != 0)
            .SelectMany(b => b.Series.Jobs.Where(b.Requirement.Applies)
                .Select(job => new Objective(b.Series, b.Stage, b.Requirement, job)))
            .GroupBy(o => o.Requirement.Achievement).ToDictionary(g => g.Key, g => g.ToArray());
    }

    public void Reset(DateTime now)
    {
        if (owner == 0) return;
        owner = 0;
        pending = 0;
        sawRequested = false;
        nextDue.Clear();
        nextRequest = now.AddSeconds(10);
    }

    // Observe immediately after the native request as a reply can arrive before the next scan.
    public void ObserveRequest(bool requested) => sawRequested |= pending != 0 && requested;

    public bool Update(ulong characterId, CharacterProgress character, DateTime now,
        AchievementProgressReading reading, bool canRequest, out uint request)
    {
        request = 0;
        if (characterId == 0) { Reset(now); return false; }
        if (owner != characterId)
        {
            Reset(now);
            owner = characterId;
            nextRequest = nextRequest > now.AddSeconds(5) ? nextRequest : now.AddSeconds(5);
        }
        if (pending != 0)
        {
            if (now >= deadline) { pending = 0; sawRequested = false; return false; }
            ObserveRequest(reading.Requested);
            // Cached data, replies for other achievements, and invalid states are not evidence.
            if (!sawRequested || !reading.Loaded || reading.Id != pending) return false;
            pending = 0;
            sawRequested = false;
            return Apply(character, reading);
        }
        if (!canRequest || reading.Requested || now < nextRequest) return false;
        foreach (var (id, targets) in objectives)
        {
            if (nextDue.GetValueOrDefault(id) > now || !targets.Any(o =>
                    character.DetectedCounters.GetValueOrDefault(o.Key) < o.Requirement.Count &&
                    character.Weapon(o.Series, o.Job).CompletedIndex(o.Series.Stages.Count) < o.Series.Stages.IndexOf(o.Stage)))
                continue;
            pending = request = id;
            sawRequested = false;
            deadline = now.AddSeconds(10);
            nextRequest = now.AddSeconds(5);
            nextDue[id] = now.AddMinutes(1);
            break;
        }
        return false;
    }

    private bool Apply(CharacterProgress character, AchievementProgressReading reading)
    {
        var changed = false;
        foreach (var objective in objectives[reading.Id])
        {
            var requirement = objective.Requirement;
            if (requirement.Count <= 0 || reading.Maximum != (uint)requirement.Count) continue;
            var count = (int)Math.Min(reading.Current, reading.Maximum);
            // Unique discoveries are cumulative. A stale/zero reply must not undo known completion.
            if (character.DetectedCounters.TryGetValue(objective.Key, out var previous) && previous >= count) continue;
            character.DetectedCounters[objective.Key] = count;
            changed = true;
        }
        // Completing the achievement does not establish that the reward tool has been claimed.
        return changed;
    }
}
