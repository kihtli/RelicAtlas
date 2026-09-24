using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using RelicAtlas.Core;

namespace RelicAtlas.UI;

public sealed partial class MainWindow
{
    private string atmaJob = "PLD";

    public void OpenAtma(string? job = null, bool followCurrent = true)
    {
        if (followCurrent) selectedCharacter = 0;
        var arr = catalog.Series.Single(s => s.Id == "arr");
        var candidate = job ?? tracker.CurrentJob;
        if (arr.Jobs.Contains(candidate)) atmaJob = candidate;
        page = AtlasPage.Atma; IsOpen = true; BringToFront();
    }

    private void DrawAtmaFarming(CharacterProgress character, ulong id)
    {
        var now = atmaTravel?.Now ?? DateTimeOffset.UtcNow;
        var current = AtmaSchedule.At(now); var next = AtmaSchedule.At(current.End);
        var bags = tracker.InventoryFor(id);
        var stock = tracker.StorageFor(id);
        var totals = new AtmaTotals(catalog, character, bags,
            stock?.SaddlebagRecorded == true ? stock.Saddlebag : null,
            stock?.RecordedRetainers > 0 ? stock.Retainers : null);
        var missing = AtmaSchedule.Missing(catalog, character, atmaJob, bags);
        var arr = catalog.Series.Single(s => s.Id == "arr");
        SectionLabel("Atma farming", $"{now:HH:mm:ss} ST / UTC");
        ImGui.TextColored(Muted, "Unverified player theory · assumes JST. No confirmed time-based drop bonus.");
        if (ActionButton("Back to checklist", false, 155, 28))
        {
            page = AtlasPage.Collection; expansion = "arr"; Select("arr", atmaJob);
            selectedKey = $"{id}/arr/{atmaJob}"; viewedStage = arr.Stages.FindIndex(s => s.Id == "atma");
        }
        ImGui.SameLine(); ImGui.SetNextItemWidth(170 * Scale);
        if (ImGui.BeginCombo("##atma-job", JobName(atmaJob)))
        {
            foreach (var job in arr.Jobs)
                if (ImGui.Selectable(JobName(job), job == atmaJob))
                { if (atmaJob != job) atmaTravel?.Stop(); atmaJob = job; }
            ImGui.EndCombo();
        }
        ImGui.SameLine(); ImGui.AlignTextToFramePadding(); ImGui.TextColored(Muted, $"{12 - missing.Count}/12 collected");
        ImGui.SameLine();
        if (ActionButton("About this theory", false, 147, 28, true)) ImGui.OpenPopup("atma-theory");
        if (ImGui.BeginPopup("atma-theory"))
        {
            ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + 440 * Scale);
            ImGui.TextColored(Cyan, "A farming experiment, not a drop-rate prediction");
            ImGui.TextUnformatted("The author confirmed real-world time but did not explicitly specify JST. This route assumes JST (UTC+9), subtracting nine hours to show Server Time. ST is UTC on every World. ET and your local timezone are not used.");
            ImGui.Spacing();
            ImGui.TextUnformatted("Each suggested window lasts one hour and repeats after twelve hours. The optional early setting changes travel at :45 for the next window. It does not change the displayed drop window.");
            ImGui.Spacing();
            ImGui.TextUnformatted("Equip a Zenith weapon and complete FATEs in the listed zone. Atma can drop outside these proposed windows. Collected counts follow this job's checklist and carried inventory; shared stock is not reserved. To test the theory, count completed FATEs and drops both inside and outside the window.");
            ImGui.Spacing();
            if (ImGui.Button("Copy original post URL")) ImGui.SetClipboardText(AtmaSchedule.TheoryUrl);
            if (ImGui.Button("Copy official ST explanation URL")) ImGui.SetClipboardText(AtmaSchedule.ClockUrl);
            ImGui.PopTextWrapPos(); ImGui.EndPopup();
        }
        ImGui.Spacing();
        var available = ImGui.GetContentRegionAvail();
        var side = available.X < 1030 * Scale ? 256 * Scale : 292 * Scale;
        ImGui.BeginChild("atma-route-controls", new Vector2(side, available.Y), false);
        DrawAtmaWindow("CURRENT WINDOW", current, now, id, missing, true);
        ImGui.Spacing();
        DrawAtmaWindow("UP NEXT", next, now, id, missing, false);
        ImGui.Spacing();
        ImGui.TextColored(White, "Travel via Teleporter");
        var active = atmaTravel?.Active == true;
        ImGui.BeginDisabled(active);
        var early = config.AtmaArriveEarly; var skip = config.AtmaSkipCollected;
        if (ImGui.Checkbox("Travel 15 minutes early", ref early)) { config.AtmaArriveEarly = early; save(); }
        if (ImGui.Checkbox("Skip collected Atma", ref skip)) { config.AtmaSkipCollected = skip; save(); }
        ImGui.EndDisabled();
        var canStart = !readOnly && id == tracker.CurrentId && atmaJob == tracker.CurrentJob && config.Automatic && atmaTravel?.Available == true;
        ImGui.BeginDisabled(!active && !canStart);
        if (ActionButton(active ? "Stop automatic travel" : "Start automatic travel", active, side / Scale - 12, 33))
        {
            if (active) atmaTravel?.Stop();
            else atmaTravel?.Start(id, atmaJob, config.AtmaArriveEarly, config.AtmaSkipCollected);
        }
        ImGui.EndDisabled();
        ImGui.Spacing();
        var help = atmaTravel?.Status ?? "Travel is unavailable in this preview.";
        if (!active && !canStart) help = readOnly || id != tracker.CurrentId ? "Select your logged-in character to travel." :
            atmaTravel?.Available != true ? "Enable Teleporter to use travel." : !config.Automatic ? "Enable automatic detection to follow collected Atma." :
            $"Switch to {JobName(atmaJob)} in game, or choose your current ARR job above.";
        if (config.AtmaArriveEarly) help = "Travel target: " + AtmaSchedule.Target(now, true).Area.Zone + ". " + help;
        ImGui.PushStyleColor(ImGuiCol.Text, active ? Cyan : Muted); Wrap(help); ImGui.PopStyleColor();
        ImGui.Spacing();
        ImGui.PushStyleColor(ImGuiCol.Text, Muted);
        Wrap("Equip a Zenith weapon. Normal teleport fees apply. A session can keep running with this window closed; logout, job changes or plugin reload stop it.");
        if (atmaTravel?.HasServerTime != true) { ImGui.Spacing(); Wrap("Using computer UTC for display. Automatic travel waits for the game's ST clock."); }
        ImGui.PopStyleColor();
        ImGui.EndChild();
        ImGui.SameLine();
        ImGui.BeginChild("atma-schedule", new Vector2(0, available.Y), true, ImGuiWindowFlags.NoScrollbar);
        DrawAtmaTotals(totals);
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new Vector2(8, 7) * Scale);
        if (ImGui.BeginTable("atma-windows", 6, ImGuiTableFlags.ScrollY | ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoSavedSettings,
                new Vector2(0, ImGui.GetContentRegionAvail().Y)))
        {
            ImGui.TableSetupColumn("Window (ST)", ImGuiTableColumnFlags.WidthFixed, 107 * Scale);
            ImGui.TableSetupColumn("Atma / zone", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Required", ImGuiTableColumnFlags.WidthFixed, 55 * Scale);
            ImGui.TableSetupColumn("Owned", ImGuiTableColumnFlags.WidthFixed, 44 * Scale);
            ImGui.TableSetupColumn("Remaining", ImGuiTableColumnFlags.WidthFixed, 68 * Scale);
            ImGui.TableSetupColumn("Travel", ImGuiTableColumnFlags.WidthFixed, 50 * Scale);
            ImGui.TableSetupScrollFreeze(0, 1);
            ImGui.TableNextRow(ImGuiTableRowFlags.Headers);
            var headings = new[] { "Window (ST)", "Atma / zone", "Required", "Owned", "Remaining", "Travel" };
            for (var column = 0; column < headings.Length; column++)
            {
                ImGui.TableSetColumnIndex(column);
                if (column is >= 2 and <= 4)
                    ImGui.SetCursorPosX(ImGui.GetCursorPosX() + Math.Max(0, ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize(headings[column]).X));
                ImGui.PushStyleColor(ImGuiCol.Text, column == 4 ? Cyan : Muted);
                ImGui.TableHeader(headings[column]); ImGui.PopStyleColor();
            }
            foreach (var area in AtmaSchedule.Areas)
            {
                var need = totals.Materials.Single(m => m.Item == area.Item);
                int? owned = need.Bags.HasValue || need.Saddlebag.HasValue || need.Retainers.HasValue ? need.Owned : null;
                ImGui.PushID(area.Hour); ImGui.TableNextRow();
                var isCurrent = area == current.Area;
                if (isCurrent) ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, Ink(new(.15f, .12f, .24f, 1)));
                ImGui.TableNextColumn(); ImGui.TextColored(isCurrent ? Cyan : Muted, area.Hours);
                Tip("Suggested one-hour windows, repeated twelve hours apart. This is the unverified player theory shown above, not a confirmed drop bonus.");
                ImGui.TableNextColumn(); ImGui.BeginGroup();
                ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(ImGui.GetStyle().ItemSpacing.X, 2 * Scale));
                ImGui.TextColored(isCurrent ? White : need.Missing == 0 ? Green : Lavender, area.Atma);
                MaterialDetail(area.Zone, Muted);
                ImGui.PopStyleVar();
                ImGui.EndGroup(); DrawAtmaMaterialTip(area, need);
                DrawAtmaQuantity(need.Required, Muted); DrawAtmaMaterialTip(area, need);
                DrawAtmaQuantity(owned, White); DrawAtmaMaterialTip(area, need);
                DrawAtmaQuantity(need.Missing, need.Missing == 0 ? Green : Cyan); DrawAtmaMaterialTip(area, need);
                ImGui.TableNextColumn(); DrawAtmaTeleport(area, id, "Go", 48);
                ImGui.PopID();
            }
            ImGui.EndTable();
        }
        ImGui.PopStyleVar(); ImGui.EndChild();
    }

    private void DrawAtmaTotals(AtmaTotals totals)
    {
        SectionLabel("Atma requirements", "All ARR jobs");
        ImGui.TextColored(Muted, $"{totals.Jobs} sets · {totals.Required:N0} required · {totals.Owned?.ToString("N0") ?? "—"} owned · {totals.Remaining:N0} remaining");
        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + 420 * Scale);
            ImGui.TextColored(Cyan, "All ARR jobs · selected character");
            ImGui.TextUnformatted("Includes jobs not yet started. Jobs that acquired their Atma weapon or a later stage no longer need a set. The job selector only changes the individual checklist and travel controls.");
            ImGui.Spacing();
            ImGui.TextUnformatted("Owned counts physical Atma once across carried inventory and any recorded saddlebag/retainer stock from Allagan Tools. Storage is cached; unrecorded storage is excluded. Manual checklist entries are not physical stock.");
            ImGui.Spacing();
            ImGui.TextUnformatted("Remaining is calculated separately for each Atma type, using the greater of recorded checklist progress or known stock. Surplus of one type cannot cover another. Withdraw stored Atma to use it for a turn-in; travel still follows the selected job's carried items and checklist.");
            if (!totals.BagsKnown) { ImGui.Spacing(); ImGui.TextColored(Lavender, "Carried inventory is unavailable or incomplete. Remaining uses recorded progress and known stock only."); }
            ImGui.PopTextWrapPos(); ImGui.EndTooltip();
        }
        ImGui.Spacing();
    }

    private static void DrawAtmaQuantity(int? value, Vector4 color)
    {
        ImGui.TableNextColumn();
        var text = value?.ToString("N0") ?? "—";
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + Math.Max(0, ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize(text).X));
        ImGui.TextColored(color, text);
    }

    private static void DrawAtmaMaterialTip(AtmaArea area, MaterialNeed need)
    {
        if (!ImGui.IsItemHovered()) return;
        string Count(int? n) => n?.ToString("N0") ?? "unavailable";
        Tip($"{area.Item} · {area.Zone}\nSuggested starts: {area.Hours} ST (one-hour windows)\n\n" +
            $"Required across unfinished ARR Atma stages: {need.Required:N0}\n" +
            $"Bags: {Count(need.Bags)}\nSaddlebag: {Count(need.Saddlebag)}\nRetainers: {Count(need.Retainers)}\n" +
            $"Recorded checklist credit: {need.Recorded:N0}\nRemaining to farm: {need.Missing:N0}\n\n" +
            "Shared stock is counted once. Remaining uses the greater of known stock or recorded progress. Allagan Tools storage is cached; unavailable stock is excluded. The selected job does not limit these totals.");
    }

    private void DrawAtmaWindow(string label, AtmaWindow window, DateTimeOffset now, ulong id, HashSet<string> missing, bool current)
    {
        var p = ImGui.GetCursorScreenPos(); var width = ImGui.GetContentRegionAvail().X;
        var d = ImGui.GetWindowDrawList(); var size = new Vector2(width, (current ? 132 : 82) * Scale);
        d.AddRectFilled(p, p + size, Ink(Surface), 8 * Scale);
        if (current) Line(p + new Vector2(1, 15) * Scale, p + new Vector2(1, 60) * Scale, Cyan, 2);
        var remaining = (current ? window.End : window.Start) - now;
        var minutes = Math.Max(0, (int)Math.Ceiling(remaining.TotalMinutes));
        Label(p + new Vector2(14, 9) * Scale, current ? label : $"NEXT · {window.Start:HH:mm} ST · in {minutes}m", current ? Cyan : Lavender, .8f);
        Label(p + new Vector2(14, 29) * Scale, Fit(window.Area.Zone, (width - 28 * Scale) / 1.15f), White, 1.15f);
        Label(p + new Vector2(14, 52) * Scale, Fit(window.Area.Item, (width - (current ? 28 : 91) * Scale) / .85f), missing.Contains(window.Area.Item) ? Muted : Green, .85f);
        if (current) Label(p + new Vector2(14, 73) * Scale, $"{window.Start:HH:mm}–{window.End:HH:mm} ST · ends in {minutes}m", Muted, .8f);
        ImGui.SetCursorScreenPos(p + (current ? new Vector2(14, 96) * Scale : new Vector2(width - 74 * Scale, 48 * Scale)));
        ImGui.PushID(label); DrawAtmaTeleport(window.Area, id, current ? "Teleport to zone" : "Go", current ? (width / Scale) - 28 : 62); ImGui.PopID();
        ImGui.SetCursorScreenPos(p); ImGui.Dummy(size);
    }

    private void DrawAtmaTeleport(AtmaArea area, ulong id, string label, float width)
    {
        var check = atmaTravel?.Check(id, area) ?? new AtmaTravelCheck(false, "Teleporter integration unavailable.");
        ImGui.BeginDisabled(!check.Allowed);
        if (ActionButton(label, false, width, 28)) atmaTravel?.Travel(id, area);
        ImGui.EndDisabled();
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            ImGui.BeginTooltip();
            ImGui.TextUnformatted(check.Destination is { } target ? $"{target.Name} · {target.GilCost:N0} gil before tickets" : check.Reason);
            if (check.Destination != null && !check.Allowed) ImGui.TextUnformatted(check.Reason);
            ImGui.EndTooltip();
        }
    }
}
