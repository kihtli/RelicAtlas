using System.Collections.Generic;
using Dalamud.Memory;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;
using RelicAtlas.Core;

namespace RelicAtlas.Tracking;

internal sealed record AnimaGlassWeapon(string Job, string LocalizedName);
internal static class AnimaGlassReader
{
    public static unsafe bool Read(IGameGui gui, CharacterProgress character,
        IReadOnlyDictionary<uint,AnimaGlassWeapon> weapons, IReadOnlyDictionary<byte,string> jobs)
    {
        var handle = gui.GetAddonByName("Relic2Glass");
        if (handle.Address == 0 || !handle.IsVisible) return false;
        var addon = (AtkUnitBase*)handle.Address;
        if (addon->AtkValues == null || addon->AtkValuesCount < 9) return false;
        var values = addon->AtkValues;
        // The native refresh hides its weapon display when argument 0 is true.
        if (values[0].Type != AtkValueType.Bool || values[0].Bool) return false;
        int? Number(int index) => values[index].Type switch
        {
            AtkValueType.UInt when values[index].UInt <= int.MaxValue => (int)values[index].UInt,
            AtkValueType.Int => values[index].Int, _ => null,
        };
        if (Number(1) is not (>= 0 and <= 2000 and var light) || Number(7) is not { } containerId || Number(8) is not (>= 0 and var slot)) return false;
        var type = (InventoryType)containerId;
        if (type is not (InventoryType.Inventory1 or InventoryType.Inventory2 or InventoryType.Inventory3 or InventoryType.Inventory4 or
            InventoryType.EquippedItems or InventoryType.ArmoryMainHand or InventoryType.ArmoryOffHand)) return false;
        var manager = InventoryManager.Instance(); var quests = QuestManager.Instance();
        if (manager == null || quests == null) return false;
        var container = manager->GetInventoryContainer(type);
        if (container == null || !container->IsLoaded || slot >= container->Size) return false;
        var item = container->GetInventorySlot(slot);
        var work = quests->GetQuestById((ushort)(67932 & 0xffff));
        if (item == null || item->Quantity < 1 || !weapons.TryGetValue(item->ItemId,out var weapon) || work == null ||
            work->Sequence != 5 || !jobs.TryGetValue(work->AcceptClassJob,out var owner) || owner != weapon.Job) return false;
        var title = values + 4;
        if (title->Type is not (AtkValueType.String or AtkValueType.ManagedString or AtkValueType.ConstString) || title->String.Value == null ||
            GameTracker.Normalize(MemoryHelper.ReadSeStringNullTerminated((nint)title->String.Value).TextValue) != GameTracker.Normalize(weapon.LocalizedName)) return false;
        return AnimaProgress.RecordDensity(character,owner,light);
    }
}
