using System;
using System.Collections.Generic;
using System.Linq;
using RelicAtlas.Interop;

namespace RelicAtlas.Core;

public static class RelicBarProgress
{
    public static RelicSnapshot Build(Catalog catalog, CharacterProgress? character, ulong characterId,
        string job, bool automatic, IReadOnlyDictionary<string, int>? inventory)
    {
        var result = new RelicSnapshot
        {
            State = characterId == 0 ? "logged-out" : character == null ? "waiting" : "ready",
            CharacterId = characterId, CurrentJob = job, Automatic = automatic,
            LiveInventory = characterId != 0 && automatic && inventory != null, GeneratedAtUtc = DateTime.UtcNow,
        };
        if (characterId == 0 || character == null) return result;
        result.Relics = BuildTracks(catalog, character, automatic ? inventory : null);
        return result;
    }

    public static List<RelicTrackSnapshot> BuildTracks(Catalog catalog, CharacterProgress character,
        IReadOnlyDictionary<string, int>? stock)
    {
        var result = new List<RelicTrackSnapshot>();
        foreach (var series in catalog.Series)
        foreach (var relicJob in series.Jobs)
        {
            var weapon = character.Weapon(series, relicJob);
            var acquired = weapon.CompletedIndex(series.Stages.Count) + 1;
            var (nextStage, next) = Progress.Next(character, series, relicJob, stock);
            var stage = nextStage ?? series.Stages[^1];
            var statuses = nextStage == null ? [] : stage.Requirements.Where(r => r.Applies(relicJob))
                .Select(r => Progress.Status(character, series, relicJob, stage, r, stock)).ToArray();
            var nextStatus = next == null ? default : Progress.Status(character, series, relicJob, stage, next, stock);
            result.Add(new()
            {
                SeriesId = series.Id, SeriesName = series.Name, Expansion = series.Expansion, Job = relicJob,
                Pinned = weapon.Pinned, AcquiredStages = acquired, TotalStages = series.Stages.Count,
                Complete = nextStage == null, ReadyForTurnIn = nextStage != null && next == null,
                StageName = stage.Name, WeaponName = string.Join(" + ", stage.Weapons[relicJob]), Npc = stage.Npc,
                ReadyObjectives = statuses.Count(s => s.Complete), TotalObjectives = statuses.Length,
                StageProgress = nextStage == null || statuses.Length == 0 ? 10000 :
                    (int)Math.Clamp(Math.Floor(statuses.Average(s => (double)s.Done / s.Required) * 10000), 0, 10000),
                NextLabel = next?.Label ?? (nextStage == null ? "Relic complete" : "Receive the weapon"),
                NextDetail = next?.Detail ?? (nextStage == null ? "All weapon stages acquired." : stage.Npc),
                NextDone = nextStatus.Done, NextRequired = nextStatus.Required,
            });
        }
        return result;
    }
}
