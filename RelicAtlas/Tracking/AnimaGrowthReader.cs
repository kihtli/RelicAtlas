using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;
using RelicAtlas.Core;

namespace RelicAtlas.Tracking;

internal static class AnimaGrowthReader
{
    public static unsafe bool Read(IGameGui gui, CharacterProgress character, Catalog catalog,
        IReadOnlyDictionary<byte,string> jobs, string currentJob, IReadOnlySet<string> owned)
    {
        var handle = gui.GetAddonByName("AWMakingSpiritGrow");
        if (handle.Address == 0 || !handle.IsVisible) return false;
        var addon = (AtkUnitBase*)handle.Address;
        if (addon->AtkValues == null || addon->AtkValuesCount != 111) return false;
        var values = addon->AtkValues;
        int? Number(int i) => values[i].Type switch
        {
            AtkValueType.Int => values[i].Int,
            AtkValueType.UInt when values[i].UInt <= int.MaxValue => (int)values[i].UInt,
            _ => null,
        };
        // Initial enhancement only; exclude the later stat-reallocation modes.
        if (Number(0) != 0 || Number(27) != 15840 || Number(28) != 15841 || Number(31) != 180 ||
            values[2].Type != AtkValueType.Bool) return false;
        var quests = QuestManager.Instance();
        if (quests == null) return false;
        var work = quests->GetQuestById((ushort)(67864 & 0xffff));
        if (work == null || work->Sequence != 2 || !jobs.TryGetValue(work->AcceptClassJob,out var owner) || owner != currentJob) return false;
        var stage = catalog.Series.Single(s => s.Id == "hw").Stages.Single(s => s.Id == "hyperconductive");
        if (!stage.Weapons.TryGetValue(owner,out var weapons) || !weapons.All(owned.Contains) || values[2].Bool != (owner == "PLD")) return false;
        if (Number(29) is not { } proposed || Number(30) is not { } committed) return false;
        var draftStats = new List<int>(); var savedStats = new List<int>();
        // Five stats, seven values each. Paladin has a second group for the shield.
        for (var component = 0; component < (owner == "PLD" ? 2 : 1); component++)
        for (var stat = 0; stat < 5; stat++)
        {
            var index = 42 + component * 35 + stat * 7;
            if (Number(index) is not { } draft || Number(index+1) is not { } saved) return false;
            draftStats.Add(draft); savedStats.Add(saved);
        }
        var points = AnimaProgress.CommittedEnhancement(proposed,committed,180,draftStats,savedStats);
        return points.HasValue && AnimaProgress.RecordEnhancement(character,owner,points.Value);
    }
}
