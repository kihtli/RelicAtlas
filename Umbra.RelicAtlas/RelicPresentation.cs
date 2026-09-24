using RelicAtlas.Interop;

namespace Umbra.RelicAtlas;

public sealed record RelicDisplay(string Text, string SubText, string Tooltip, int Progress, uint IconId, bool Visible);

public static class RelicPresentation
{
    public static RelicTrackSnapshot? Select(RelicSnapshot snapshot, string series, string configuredJob)
    {
        if (snapshot.State != "ready") return null;
        var job = configuredJob == "current" ? snapshot.CurrentJob : configuredJob;
        var eligible = snapshot.Relics.Where(r => r.Job == job);
        if (series != "auto") return eligible.FirstOrDefault(r => r.SeriesId == series);
        // Keep the selection stable: current job, unfinished, pinned, started, newest series.
        return eligible.OrderBy(r => r.Complete).ThenByDescending(r => r.Pinned)
            .ThenByDescending(r => r.AcquiredStages > 0).ThenByDescending(r => SeriesOrder(r.SeriesId)).FirstOrDefault();
    }

    public static RelicDisplay Create(RelicSnapshot? snapshot, RelicTrackSnapshot? relic, string series,
        string job, string labelMode, string progressMode, bool hideComplete)
    {
        if (snapshot == null)
            return new("Relic Atlas unavailable", "Enable Relic Atlas", "Enable Relic Atlas 0.1.0.16 or newer. The widget reconnects automatically.", 0, 0, true);
        if (snapshot.State == "logged-out")
            return new("Relic Atlas", "Log in to track progress", "Log into a character to show their relic progress.", 0, 0, true);
        if (snapshot.State != "ready")
            return new("Relic Atlas", "Waiting for progress", "Relic Atlas is waiting for character data. The widget will refresh automatically.", 0, 0, true);
        if (relic == null)
        {
            var target = job == "current" ? snapshot.CurrentJob : job;
            return new("Relic Atlas", "No matching combat relic", $"No {(series == "auto" ? "combat" : series.ToUpperInvariant())} relic is available for {target}. Choose another job or series in this widget's settings.", 0, 0, true);
        }

        var stages = $"{relic.AcquiredStages}/{relic.TotalStages}";
        var text = labelMode switch
        {
            "compact" => $"{relic.SeriesName} {stages}",
            "objective" => $"{relic.Job} · {Shorten(relic.NextLabel, 48)}",
            _ => $"{relic.Job} · {relic.SeriesName} · {stages}",
        };
        var next = relic.NextRequired > 1 ? $"{relic.NextLabel} ({relic.NextDone:N0}/{relic.NextRequired:N0})" : relic.NextLabel;
        var sub = relic.Complete ? "Relic complete" : relic.ReadyForTurnIn ? $"{relic.StageName} · Ready for turn-in" : next;
        if (!snapshot.Automatic) sub = "Paused · " + sub;
        var progress = progressMode switch
        {
            "stages" => relic.TotalStages > 0 ? (int)(10000L * relic.AcquiredStages / relic.TotalStages) : 0,
            "objective" => relic.Complete || relic.ReadyForTurnIn ? 10000 : relic.NextRequired > 0 ? (int)(10000L * relic.NextDone / relic.NextRequired) : 0,
            _ => relic.StageProgress,
        };
        var status = relic.Complete ? "All weapon stages acquired." : relic.ReadyForTurnIn ?
            "Objectives ready. Receive the weapon and record the turn-in in Relic Atlas." :
            $"{relic.ReadyObjectives}/{relic.TotalObjectives} objectives ready for this stage.\nNext: {next}\n{relic.NextDetail}";
        var tooltip = $"{relic.SeriesName} / {relic.Expansion} / {relic.Job}\n{stages} weapon stages acquired\n{relic.StageName}: {relic.WeaponName}\n\n{status}";
        if (!snapshot.Automatic) tooltip += "\n\nAutomatic tracking is paused; displaying saved progress.";
        else if (!snapshot.LiveInventory) tooltip += "\n\nLive bag counts are unavailable; displaying recorded progress.";
        tooltip += "\n\nClick to open this relic in Relic Atlas.";
        return new(text, Shorten(sub, 64), tooltip, Math.Clamp(progress, 0, 10000), relic.IconId, !hideComplete || !relic.Complete);
    }

    private static string Shorten(string value, int max) => value.Length <= max ? value : value[..(max - 1)] + "…";
    private static int SeriesOrder(string id) => id switch { "arr" => 0, "hw" => 1, "sb" => 2, "shb" => 3, "ew" => 4, "dt" => 5, _ => -1 };
}
