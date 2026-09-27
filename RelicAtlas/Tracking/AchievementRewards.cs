using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Dalamud.Game;
using Dalamud.Plugin.Services;
using RelicAtlas.Core;
using NativeAchievement = FFXIVClientStructs.FFXIV.Client.Game.UI.Achievement;
using SheetAchievement = Lumina.Excel.Sheets.Achievement;

namespace RelicAtlas.Tracking;

/// <summary>Reads the reward condition used by the Achievements window; never claims rewards.</summary>
public sealed class AchievementRewards
{
    // MatchesAgentState, verified against the 2026.09.15 client and both AgentAchievement
    // call sites. Match the full getter so a changed layout disables this optional reader.
    private const string GetterSignature = "4C 8B C9 8B C2 48 C1 E8 03 8B CA 83 E1 07 41 BA 01 00 00 00 41 D3 E2 42 0F B6 8C 08 14 02 00 00 44 23 D1 41 80 F8 01 75 07 45 85 D2 0F 95 C0 C3 45 85 D2 0F 94 C0 C3";
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate byte MatchesRewardState(nint achievement, uint bit, byte expectedSet);
    private sealed record Binding(uint Achievement, string Tool, AchievementRewardCondition Condition);
    private readonly List<Binding> bindings = [];
    private readonly IPluginLog log;
    private MatchesRewardState? matches;

    public AchievementRewards(Catalog catalog, IDataManager data, ISigScanner scanner, IPluginLog log)
    {
        this.log = log;
        try
        {
            var tools = catalog.Series.Where(s => s.IsTool).SelectMany(s => s.Stages)
                .SelectMany(s => s.Weapons.Values).SelectMany(x => x).ToHashSet();
            foreach (var row in data.GetExcelSheet<SheetAchievement>(ClientLanguage.English))
            {
                if (row.Item.RowId == 0 || !row.Item.TryGetValue(out var item)) continue;
                var name = item.Name.ExtractText();
                if (!tools.Contains(name)) continue;
                var condition = AchievementRewardCondition.FromSheet(row.Unknown1, row.Unknown2);
                if (condition.HasValue) bindings.Add(new(row.RowId, name, condition.Value));
            }
            if (bindings.Count == 0 || !scanner.TryScanText(GetterSignature, out var address))
            {
                log.Warning("Relic Atlas achievement reward checks are unavailable; inventory and manual tracking remain available.");
                return;
            }
            matches = Marshal.GetDelegateForFunctionPointer<MatchesRewardState>(address);
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Relic Atlas could not initialise optional achievement reward detection.");
        }
    }

    public unsafe void AddClaimedTools(HashSet<string> owned)
    {
        if (matches == null) return;
        try
        {
            var achievement = NativeAchievement.Instance();
            if (achievement == null || !achievement->IsLoaded()) return;
            foreach (var binding in bindings)
            {
                if (!achievement->IsComplete((int)binding.Achievement)) continue;
                if (matches((nint)achievement, binding.Condition.Bit,
                        binding.Condition.ClaimedWhenSet ? (byte)1 : (byte)0) != 0)
                    owned.Add(binding.Tool);
            }
        }
        catch (Exception ex)
        {
            matches = null;
            log.Warning(ex, "Relic Atlas disabled optional achievement reward detection after an error.");
        }
    }
}
