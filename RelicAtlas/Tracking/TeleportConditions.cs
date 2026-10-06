using System.Collections.Generic;
using Dalamud.Game.ClientState.Conditions;

// This helper only compares enum values and is also exercised by cross-platform tests.
#pragma warning disable CA1416

namespace RelicAtlas.Tracking;

internal static class TeleportConditions
{
    public static bool Allow(IReadOnlySet<ConditionFlag> flags)
    {
        if (flags.Count == 0) return false;
        foreach (var flag in flags)
            if (flag is not (ConditionFlag.NormalConditions or ConditionFlag.Mounted or
                ConditionFlag.RidingPillion or ConditionFlag.InFlight)) return false;
        return true;
    }
}
