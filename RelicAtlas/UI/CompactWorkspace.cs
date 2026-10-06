using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using RelicAtlas.Core;

namespace RelicAtlas.UI;

public sealed partial class MainWindow
{
    private void DrawCompactCollection(CharacterProgress c, ulong id)
    {
        var series = catalog.Series.Single(s => s.Id == selectedSeries);
        var width = ImGui.GetContentRegionAvail().X;
        var browseWidth = ImGui.CalcTextSize("Browse").X + ImGui.GetStyle().FramePadding.X * 2;
        var comboWidth = (width - browseWidth - ImGui.GetStyle().ItemSpacing.X * 2) / 2;
        ImGui.SetNextItemWidth(comboWidth);
        if (ImGui.BeginCombo("##compact-series",series.Name))
        {
            foreach (var option in VisibleSeries)
                if (ImGui.Selectable(option.Name,option == series))
                    Select(option.Id,option.Jobs.Contains(selectedJob) ? selectedJob : option.Jobs[0]);
            ImGui.EndCombo();
        }
        series = catalog.Series.Single(s => s.Id == selectedSeries);
        ImGui.SameLine(); ImGui.SetNextItemWidth(comboWidth);
        if (ImGui.BeginCombo("##compact-job",JobName(selectedJob)))
        {
            foreach (var job in series.Jobs)
                if (ImGui.Selectable(JobName(job),job == selectedJob)) Select(series.Id,job);
            ImGui.EndCombo();
        }
        ImGui.SameLine();
        if (ImGui.Button("Browse")) ImGui.OpenPopup("compact-collection-browser");
        ImGui.SetNextWindowSize(new Vector2(Math.Min(360 * Scale,ImGui.GetWindowViewport().WorkSize.X - 24 * Scale),Math.Min(450 * Scale,ImGui.GetWindowViewport().WorkSize.Y - 24 * Scale)));
        if (ImGui.BeginPopup("compact-collection-browser")) { DrawCollectionList(c,id); ImGui.EndPopup(); }
        ImGui.BeginChild("compact-detail", Vector2.Zero, false, ImGuiWindowFlags.NoScrollbar);
        DrawDetails(c,id);
        ImGui.EndChild();
    }

    private void DrawCompactOverview(CollectionOverview data)
    {
        Wrap($"{data.Total.Complete} complete · {data.Total.InProgress} in progress · {data.Total.Ready} ready to turn in");
        var width = ImGui.GetContentRegionAvail().X;
        ImGui.SetNextItemWidth(width * .5f);
        ImGui.InputTextWithHint("##overview-search","Search jobs",ref overviewSearch,80);
        ImGui.SameLine();
        DrawExpansionPicker(Math.Max(1,ImGui.GetContentRegionAvail().X));
        ImGui.SetNextItemWidth(width * .5f);
        if (ImGui.BeginCombo("##overview-role", overviewRole))
        {
            foreach (var role in relicKind == "tool" ? new[] { "All roles", "Crafters", "Gatherers" } : new[] { "All roles", "Tanks", "Healers", "Melee", "Physical ranged", "Casters" })
                if (ImGui.Selectable(role, role == overviewRole)) overviewRole = role;
            ImGui.EndCombo();
        }
        if (relicKind == "weapon") { ImGui.SameLine(); if (ImGui.Button("Atma farming")) OpenAtma(null,false); }
        if (ImGui.BeginTable("compact-overview",4,ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY | ImGuiTableFlags.SizingStretchProp,new Vector2(0,Math.Max(1,ImGui.GetContentRegionAvail().Y))))
        {
            ImGui.TableSetupColumn("Job",ImGuiTableColumnFlags.WidthFixed,65 * Scale);
            ImGui.TableSetupColumn("Collection"); ImGui.TableSetupColumn("Next stage");
            ImGui.TableSetupColumn("Acquired",ImGuiTableColumnFlags.WidthFixed,75 * Scale);
            ImGui.TableSetupScrollFreeze(0,1); ImGui.TableHeadersRow();
            var count = 0;
            foreach (var job in data.Jobs.Where(j => (overviewRole == "All roles" || CollectionOverview.Role(j) == overviewRole) && (JobName(j).Contains(overviewSearch,StringComparison.OrdinalIgnoreCase) || j.Contains(overviewSearch,StringComparison.OrdinalIgnoreCase))))
            foreach (var series in VisibleSeries.Where(s => expansion == "all" || s.Id == expansion))
            {
                if (!data.Tracks.TryGetValue((series.Id,job),out var relic)) continue;
                count++; ImGui.PushID(series.Id + job); ImGui.TableNextRow(); ImGui.TableNextColumn();
                if (ImGui.Selectable(job,false,ImGuiSelectableFlags.SpanAllColumns)) { page=AtlasPage.Collection; Select(series.Id,job); }
                Tip(JobName(job)); ImGui.TableNextColumn(); CompactText(series.Name);
                ImGui.TableNextColumn(); CompactText(relic.Complete ? "Complete" : relic.StageName);
                ImGui.TableNextColumn(); ImGui.TextColored(relic.Complete ? Green : Muted,$"{relic.AcquiredStages}/{relic.TotalStages}");
                ImGui.PopID();
            }
            if (count == 0) { ImGui.TableNextRow(); ImGui.TableNextColumn(); Wrap("No matches"); }
            ImGui.EndTable();
        }
    }
}
