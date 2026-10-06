using Dalamud.Plugin.Services;
using Dalamud.Memory;
using System.Collections.Generic;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Component.GUI;
using RelicAtlas.Core;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.AtkValueType;

namespace RelicAtlas.Tracking;

internal static class ArrScrollReader
{
    public static unsafe ArrScrollReading? Read(IGameGui gui, InventoryManager* inventory,
        IReadOnlyDictionary<uint,string> names, out string status)
    {
        status = "Open your sphere scroll in game to read its infusion count.";
        var handle = gui.GetAddonByName("RelicSphereScroll");
        if (handle.Address == 0 || !handle.IsVisible) return null;
        var addon = (AtkUnitBase*)handle.Address;
        if (addon->AtkValues == null || addon->AtkValuesCount < 12)
        { status = "Scroll window is open; waiting for infusion data."; return null; }
        // RelicSphereScroll refresh arguments: inventory container/slot, infused count, capacity.
        // Verified against the installed client's refresh routine and its ULD "Infused" row.
        // Types, held item identity and Relic3 capacity are all checked before accepting a count.
        int? Number(int index)
        {
            var value = addon->AtkValues + index;
            return value->Type switch { ValueType.Int => value->Int,
                ValueType.UInt when value->UInt <= int.MaxValue => (int)value->UInt, _ => null };
        }
        if (inventory != null && Number(4) is >= 0 and <= 3 and var containerId && Number(5) is >= 0 and var slot &&
            Number(10) is { } infused && Number(11) is { } limit)
        {
            var container = inventory->GetInventoryContainer((InventoryType)containerId);
            if (container != null && container->IsLoaded && slot < container->Size)
            {
                var item = container->GetInventorySlot(slot);
                var title = addon->AtkValues + 3;
                if (item != null && item->Quantity > 0 && names.TryGetValue(item->ItemId,out var expectedName) &&
                    title->Type is ValueType.String or ValueType.ManagedString or ValueType.ConstString && title->String.Value != null &&
                    GameTracker.Normalize(MemoryHelper.ReadSeStringNullTerminated((nint)title->String.Value).TextValue) == GameTracker.Normalize(expectedName))
                { status = $"Scroll window reading: {infused}/{limit}."; return new(item->ItemId, infused, limit); }
            }
        }
        status = "Scroll window is open, but its infusion data is unavailable.";
        return null;
    }
}
