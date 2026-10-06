using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using RelicAtlas.Core;
using RelicAtlas.Interop;

namespace RelicAtlas.UI;

public sealed partial class MainWindow
{
    private CollectionOverview? overview;
    private CharacterProgress? overviewCharacter;
    private ulong overviewCharacterId;
    private bool overviewAutomatic;
    private string overviewKind = "";
    private DateTime overviewRefresh;
    private string overviewSearch = "";
    private string overviewRole = "All roles";

    private void DrawCollectionOverview(CharacterProgress character, ulong id)
    {
        if (overview == null || !ReferenceEquals(character, overviewCharacter) || id != overviewCharacterId ||
            config.Automatic != overviewAutomatic || overviewKind != relicKind || DateTime.UtcNow >= overviewRefresh)
        {
            overview = new(catalog, character, tracker.InventoryFor(id), relicKind);
            overviewKind = relicKind;
            overviewCharacter = character; overviewCharacterId = id; overviewAutomatic = config.Automatic;
            overviewRefresh = DateTime.UtcNow.AddMilliseconds(500);
        }
        var data = overview;
        if (compactLayout) { DrawCompactOverview(data); return; }
        var collections = VisibleSeries.ToArray();
        var compact = ImGui.GetContentRegionAvail().X < 1030 * Scale;
        DrawOverviewSummary(data, id);
        var controls = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        ImGui.SetNextItemWidth((compact ? 200 : 250) * Scale);
        ImGui.InputTextWithHint("##overview-search", "Search jobs…", ref overviewSearch, 80);
        ImGui.SameLine(); ImGui.SetNextItemWidth(160 * Scale);
        if (ImGui.BeginCombo("##overview-role", overviewRole))
        {
            foreach (var role in relicKind == "tool" ? new[] { "All roles", "Crafters", "Gatherers" } : new[] { "All roles", "Tanks", "Healers", "Melee", "Physical ranged", "Casters" })
                if (ImGui.Selectable(role, role == overviewRole)) overviewRole = role;
            ImGui.EndCombo();
        }
        var jobs = data.Jobs.Where(job => (overviewRole == "All roles" || CollectionOverview.Role(job) == overviewRole) &&
            (JobName(job).Contains(overviewSearch, StringComparison.OrdinalIgnoreCase) || job.Contains(overviewSearch, StringComparison.OrdinalIgnoreCase))).ToArray();
        ImGui.SameLine(); ImGui.AlignTextToFramePadding(); ImGui.TextColored(Muted, $"{jobs.Length} / {data.Jobs.Length} jobs");
        if (relicKind == "weapon")
        {
            ImGui.SameLine();
            ImGui.SetCursorScreenPos(controls + new Vector2(width - 142 * Scale, 0));
            if (ActionButton("Atma farming", false, 142, 28)) OpenAtma(null, false);
        }
        ImGui.Spacing();

        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new Vector2(5, 4) * Scale);
        if (ImGui.BeginTable("relic-overview-grid-" + relicKind, collections.Length + 1,
            ImGuiTableFlags.ScrollY | ImGuiTableFlags.SizingStretchSame | ImGuiTableFlags.NoSavedSettings,
            new Vector2(0, ImGui.GetContentRegionAvail().Y)))
        {
            ImGui.TableSetupColumn("Job", ImGuiTableColumnFlags.WidthFixed, (compact ? 120 : 172) * Scale);
            foreach (var series in collections) ImGui.TableSetupColumn(series.Id, ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupScrollFreeze(1, 1);
            ImGui.TableNextRow(ImGuiTableRowFlags.Headers);
            ImGui.TableNextColumn();
            var p = ImGui.GetCursorScreenPos();
            Label(p + new Vector2(7, 7) * Scale, "Job", White);
            Label(p + new Vector2(7, 32) * Scale, "Relics complete", Muted, .8f);
            ImGui.Dummy(new Vector2(ImGui.GetContentRegionAvail().X, 64 * Scale));
            foreach (var series in collections)
            {
                ImGui.TableNextColumn();
                DrawExpansionSummary(series, data.ByExpansion[series.Id], compact);
            }
            foreach (var job in jobs)
            {
                ImGui.PushID(job); ImGui.TableNextRow(); ImGui.TableNextColumn();
                DrawOverviewJob(job, data.ByJob[job], compact);
                foreach (var series in collections)
                {
                    ImGui.TableNextColumn(); ImGui.PushID(series.Id);
                    if (data.Tracks.TryGetValue((series.Id, job), out var relic)) DrawOverviewRelic(relic, data.Status(relic));
                    else
                    {
                        var at = ImGui.GetCursorScreenPos(); var cellWidth = ImGui.GetContentRegionAvail().X;
                        Label(at + new Vector2((cellWidth - ImGui.CalcTextSize("—").X) / 2, 15 * Scale), "—", Edge);
                        ImGui.Dummy(new Vector2(cellWidth, 50 * Scale));
                        Tip($"{JobName(job)} has no {series.Name} relic in {series.Expansion}.");
                    }
                    ImGui.PopID();
                }
                ImGui.PopID();
            }
            if (jobs.Length == 0)
            {
                ImGui.TableNextRow(); ImGui.TableNextColumn();
                Wrap("No jobs match.");
                if (ImGui.Button("Clear filters")) { overviewSearch = ""; overviewRole = "All roles"; }
            }
            ImGui.EndTable();
        }
        ImGui.PopStyleVar();
    }

    private void DrawOverviewSummary(CollectionOverview data, ulong id)
    {
        var p = ImGui.GetCursorScreenPos(); var width = ImGui.GetContentRegionAvail().X;
        var size = new Vector2(width, 96 * Scale); var d = ImGui.GetWindowDrawList();
        d.AddRectFilled(p, p + size, Ink(Surface), 9 * Scale);

        d.AddRectFilled(p, p + size, Ink(Surface), 9 * Scale);
        Label(p + new Vector2(20, 18) * Scale, relicKind == "tool" ? "Tool collection" : "Collection overview", White, 1.55f);
        var note = readOnly ? "Catalogue preview · log in to save" : id != tracker.CurrentId ? "Saved profile · live inventory unavailable" :
            !config.Automatic ? "Automatic tracking paused" : $"{data.Total.Total} relics · {data.Jobs.Length} jobs · {VisibleSeries.Select(s => s.ExpansionKey).Distinct().Count()} expansions";
        var statWidth = Math.Min(125 * Scale, width * .135f);
        Label(p + new Vector2(20, 58) * Scale, Fit(note, (width - statWidth * 4 - 44 * Scale) / .85f), Muted, .85f);
        var stats = new[] { (data.Total.Complete, "Complete", Green), (data.Total.InProgress, "In progress", Lavender),
            (data.Total.Ready, "Ready to turn in", Cyan), (data.Total.NotStarted, "Not started", Muted) };
        for (var i = 0; i < stats.Length; i++)
        {
            var start = p + new Vector2(width - statWidth * (4 - i) + 12 * Scale, 16 * Scale);
            Label(start, stats[i].Item1.ToString(), stats[i].Item3, 1.8f);
            Label(start + new Vector2(0, 43) * Scale, stats[i].Item2, Muted, .8f);
            if (i == 0) Line(start + new Vector2(-16, 1) * Scale, start + new Vector2(-16, 62) * Scale, Edge);
        }
        ImGui.Dummy(new Vector2(width, 103 * Scale));
    }

    private void DrawExpansionSummary(Series series, RelicTotals totals, bool compact)
    {
        var p = ImGui.GetCursorScreenPos(); var width = ImGui.GetContentRegionAvail().X;
        var title = compact ? series.ExpansionKey switch { "arr" => "ARR", "hw" => "HW", "sb" => "SB", "shb" => "ShB", "ew" => "EW", _ => "DT" } : series.Expansion;
        Label(p + new Vector2(8, 6) * Scale, Fit(title, (width - 16 * Scale) / .92f), White, .92f);
        Label(p + new Vector2(8, 27) * Scale, Fit(series.Name, (width - 16 * Scale) / .8f), Lavender, .8f);
        Label(p + new Vector2(8, 47) * Scale, $"{totals.Complete} / {totals.Total} complete", Muted, .75f);
        ImGui.Dummy(new Vector2(width, 64 * Scale));
        Tip($"{series.Expansion} / {series.Name}\n{totals.Complete} complete · {totals.InProgress} in progress · {totals.Ready} ready for turn-in · {totals.NotStarted} not started");
    }

    private void DrawOverviewJob(string job, RelicTotals totals, bool compact)
    {
        var p = ImGui.GetCursorScreenPos(); var width = ImGui.GetContentRegionAvail().X;
        var hasIcon = artwork?.Job(job, p + new Vector2(5, 8) * Scale, new Vector2(28) * Scale) == true;
        var x = hasIcon ? 41 : 7;
        if (!hasIcon) Line(p + new Vector2(1, 10) * Scale, p + new Vector2(1, 39) * Scale, JobColor(job), 2);
        Label(p + new Vector2(x, 5) * Scale, Fit(compact ? job : JobName(job), (width - (x + 4) * Scale) / .95f), White, .95f);
        Label(p + new Vector2(x, 28) * Scale, $"{totals.Complete} / {totals.Total}" + (compact ? "" : " complete"), Muted, .8f);
        ImGui.Dummy(new Vector2(width, 50 * Scale));
        Tip($"{JobName(job)} / {CollectionOverview.Role(job)}\n{totals.Complete} of {totals.Total} eligible relics complete\n{totals.InProgress} in progress · {totals.Ready} ready for turn-in · {totals.NotStarted} not started");
    }

    private void DrawOverviewRelic(RelicTrackSnapshot relic, RelicCollectionStatus status)
    {
        var p = ImGui.GetCursorScreenPos(); var width = ImGui.GetContentRegionAvail().X;
        var size = new Vector2(width, 50 * Scale);
        var color = status switch { RelicCollectionStatus.Complete => Green, RelicCollectionStatus.Ready => Cyan,
            RelicCollectionStatus.InProgress => Lavender, _ => Muted };
        var title = status switch { RelicCollectionStatus.Complete => "Complete", RelicCollectionStatus.Ready => "Ready",
            RelicCollectionStatus.InProgress => "In progress", _ => "Not started" };
        if (CanvasButton("relic", size, out var hover, out var focus))
        {
            // Preserve the overview's selected character, including saved/offline profiles.
            page = AtlasPage.Collection; guideView = false; expansion = relic.SeriesId;
            jobFilter = "All jobs"; search = ""; inProgressOnly = false;
            Select(relic.SeriesId, relic.Job);
        }
        var d = ImGui.GetWindowDrawList();
        var surface = status == RelicCollectionStatus.NotStarted ? AtlasTheme.Rgb(0x181a1c) : AtlasTheme.Rgb(0x293b58);
        d.AddRectFilled(p, p + size, Ink(hover ? AtlasTheme.Rgb(0x354b6b) : surface), 6 * Scale);
        if (focus || hover) d.AddRect(p, p + size, Ink(Cyan), 6 * Scale);
        var stages = $"{relic.AcquiredStages}/{relic.TotalStages}";
        var countWidth = ImGui.CalcTextSize(stages).X * .75f;
        Label(p + new Vector2(8, 5) * Scale, Fit(title, (width - countWidth - 23 * Scale) / .75f), color, .75f);
        Label(p + new Vector2(width - countWidth - 8 * Scale, 5 * Scale), stages, color, .75f);
        Label(p + new Vector2(8, 24) * Scale, Fit(relic.StageName, (width - 16 * Scale) / .85f), status == RelicCollectionStatus.NotStarted ? Muted : White, .85f);
        var bar = p + new Vector2(8, 44) * Scale;
        d.AddRectFilled(bar, bar + new Vector2(width - 16 * Scale, 2 * Scale), Ink(Edge), Scale);
        if (relic.AcquiredStages > 0)
            d.AddRectFilled(bar, bar + new Vector2((width - 16 * Scale) * relic.AcquiredStages / relic.TotalStages, 2 * Scale), Ink(color), Scale);
        var next = relic.NextRequired > 1 ? $"{relic.NextLabel} ({relic.NextDone:N0}/{relic.NextRequired:N0})" : relic.NextLabel;
        var detail = relic.Complete ? "All relic stages acquired." : relic.ReadyForTurnIn ? "Ready for turn-in. Receive the relic and record it in Collection. Available stock is shared across relics." :
            $"{relic.ReadyObjectives}/{relic.TotalObjectives} objectives ready\nNext: {next}\n{relic.NextDetail}";
        Tip($"{JobName(relic.Job)} / {relic.Expansion}\n{relic.AcquiredStages}/{relic.TotalStages} stages acquired\n{relic.StageName} · {relic.WeaponName}\n\n{detail}\n\nClick to open this relic.");
    }
}
