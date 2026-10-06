using System.Collections.Generic;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using RelicAtlas.Core;

namespace RelicAtlas.Tracking;

internal static class QuestObjectiveReader
{
    public static unsafe bool Read(Catalog catalog, CharacterProgress character, IEnumerable<RelicQuestBinding> bindings,
        IReadOnlyDictionary<byte,string> jobs, IObjectTable objects)
    {
        var manager = QuestManager.Instance();
        if (manager == null) return false;
        var events = EventFramework.Instance();
        var player = (BattleChara*)(objects.LocalPlayer?.Address ?? 0);
        var changed = false;
        foreach (var binding in bindings)
        {
            var work = manager->GetQuestById((ushort)(binding.QuestId & 0xffff));
            if (work == null || !jobs.TryGetValue(work->AcceptClassJob,out var job)) continue;
            var checkedTodos = new HashSet<byte>();
            if (events == null || player == null) continue;
            var handler = (QuestEventHandler*)events->GetEventHandlerById(binding.QuestId);
            if (handler == null) continue;
            foreach (var objective in binding.Objectives)
                if (work->Sequence == objective.Sequence && handler->IsTodoChecked(player,objective.Todo))
                    checkedTodos.Add(objective.Todo);
            changed |= QuestObjectiveProgress.Apply(catalog,character,binding,job,work->Sequence,checkedTodos);
        }
        return changed;
    }
}
