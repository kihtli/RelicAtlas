using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace RelicAtlas.Core;

public sealed record QuestObjectiveBinding(string RequirementId, byte Todo, byte Sequence, bool CompletionOnly = false);
public sealed record RelicQuestBinding(uint QuestId, string Name, string Series, string Stage, QuestObjectiveBinding[] Objectives);

public static class QuestObjectiveProgress
{
    public static string NormalizeName(string value) => ArrBookMatching.Normalize(new string(value
        .Where(c => char.GetUnicodeCategory(c) != UnicodeCategory.PrivateUse).ToArray()));

    public static RelicQuestBinding[] Bindings(Catalog catalog)
    {
        var hw = catalog.Series.Single(s => s.Id == "hw");
        QuestObjectiveBinding[] Map(string stage, int[] todos, int[] sequences) => hw.Stages.Single(s => s.Id == stage)
            .Requirements.Where(r => r.Item.Length == 0 && r.Shared.Length == 0 && r.Count == 1)
            .Take(todos.Length).Select((r,i) => new QuestObjectiveBinding(r.Id,(byte)todos[i],(byte)sequences[i])).ToArray();
        return [
            new(67749,"Toughening Up","hw","awoken",Map("awoken",[0,1,2,4,5,6,8,9,10,11],[1,2,3,5,6,7,9,10,11,12])),
            new(67932,"Born Again Anima","hw","complete",Map("complete",[0,1,2],[1,1,1]).Concat([new QuestObjectiveBinding("aetheric-density",6,5,true)]).ToArray()),
            new(67864,"A Dream Fulfilled","hw","reconditioned",[new("allocated-enhancement-points",1,2,true)]),
            new(67940,"Best Friends Forever","hw","lux",Map("lux",[0,1,2,3,4,5,6,7,8,9,10,11],[1,1,1,2,2,2,2,3,3,4,4,4])),
        ];
    }

    public static bool Apply(Catalog catalog, CharacterProgress character, RelicQuestBinding binding,
        string acceptedJob, byte sequence, IReadOnlySet<byte> checkedTodos)
    {
        var series = catalog.Series.Single(s => s.Id == binding.Series);
        if (!series.Jobs.Contains(acceptedJob) || sequence == 0) return false;
        var stage = series.Stages.Single(s => s.Id == binding.Stage);
        var changed = false;
        foreach (var objective in binding.Objectives)
        {
            var r = stage.Requirements.Single(r => r.Id == objective.RequirementId);
            var done = sequence > objective.Sequence || sequence == objective.Sequence && checkedTodos.Contains(objective.Todo);
            if (objective.CompletionOnly && !done) continue;
            var key = Progress.Key(series,acceptedJob,stage,r);
            var count = done ? r.Count : 0;
            if (character.DetectedCounters.TryGetValue(key,out var old) && old == count) continue;
            character.DetectedCounters[key] = count; changed = true;
        }
        return changed;
    }
}
