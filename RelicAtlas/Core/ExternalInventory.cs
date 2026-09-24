using System;
using System.Collections.Generic;

namespace RelicAtlas.Core;

public sealed class ExternalInventory
{
    public ulong Owner { get; init; }
    public DateTime QueriedAt { get; init; }
    public Dictionary<string, int> Saddlebag { get; } = [];
    public Dictionary<string, int> Retainers { get; } = [];
    public bool SaddlebagRecorded { get; private set; }
    public int RecordedRetainers { get; private set; }

    // Allagan Tools GetCharacterItems -> CriticalCommonLib InventoryItem.ToNumeric.
    // Only consume this character's saddlebags and its retainers' storage bags.
    public static ExternalInventory Read(ulong activeId, IReadOnlyDictionary<ulong, HashSet<ulong[]>> owners,
        IReadOnlyDictionary<uint, string> materials)
    {
        var result = new ExternalInventory { Owner = activeId, QueriedAt = DateTime.UtcNow };
        foreach (var name in materials.Values)
        {
            result.Saddlebag.TryAdd(name, 0); result.Saddlebag.TryAdd(name + "|HQ", 0);
            result.Retainers.TryAdd(name, 0); result.Retainers.TryAdd(name + "|HQ", 0);
        }
        foreach (var (owner, items) in owners)
        {
            var retainerSeen = false;
            var slots = new HashSet<(ulong, ulong)>();
            foreach (var row in items)
            {
                if (row.Length < 25) throw new InvalidOperationException("Unsupported Allagan Tools inventory record format.");
                if (row[23] != owner) continue;
                var container = row[20];
                var saddle = owner == activeId && container is 4000 or 4001 or 4100 or 4101;
                var retainer = owner != activeId && container is >= 10000 and <= 10006;
                if (!saddle && !retainer) continue;
                if (saddle) result.SaddlebagRecorded = true;
                if (retainer) retainerSeen = true;
                if (!slots.Add((container, row[22]))) continue;
                if (row[2] > uint.MaxValue || row[3] > int.MaxValue) throw new InvalidOperationException("Invalid Allagan Tools item/count.");
                if (row[3] == 0 || !materials.TryGetValue((uint)row[2], out var name)) continue;
                var stock = saddle ? result.Saddlebag : result.Retainers;
                stock[name] = checked(stock[name] + (int)row[3]);
                if ((row[6] & 1) != 0) stock[name + "|HQ"] = checked(stock[name + "|HQ"] + (int)row[3]);
            }
            if (retainerSeen) result.RecordedRetainers++;
        }
        return result;
    }
}
