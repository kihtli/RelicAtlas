using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using RelicAtlas.Core;

namespace RelicAtlas.UI;

public sealed partial class MainWindow
{
    private static readonly Vector4 Surface = new(.075f, .082f, .13f, 1);
    private static readonly Vector4 Edge = new(.21f, .23f, .34f, 1);
    private static readonly Vector4 White = new(.94f, .95f, 1, 1);
    private static readonly Vector4 Violet = new(.64f, .48f, .99f, 1);
    private static uint Ink(Vector4 color)
    {
        color.W *= ImGui.GetStyle().Alpha;
        return ImGui.ColorConvertFloat4ToU32(color);
    }
    private void Label(Vector2 p, string text, Vector4 color, float factor = 1)
    {
        var size = ImGui.GetFontSize() * factor;
        using var font = factor > 1.05f ? headingFont?.Push() : null;
        ImGui.GetWindowDrawList().AddText(ImGui.GetFont(), size, p, Ink(color), text);
    }
    private static string Fit(string text, float width)
    {
        if (ImGui.CalcTextSize(text).X <= width) return text;
        var n = text.Length;
        while (n > 0 && ImGui.CalcTextSize(text[..n] + "…").X > width) n--;
        return text[..n] + "…";
    }
    private static void Line(Vector2 a, Vector2 b, Vector4 color, float thickness = 1)
        => ImGui.GetWindowDrawList().AddLine(a, b, Ink(color), thickness * Scale);
    private static bool CanvasButton(string id, Vector2 size, out bool hover, out bool focus)
    {
        ImGui.PushStyleColor(ImGuiCol.Button, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, Vector4.Zero);
        // Keep ImGui's keyboard navigation and hit testing without drawing a second label.
        var click = ImGui.Button("###" + id, size);
        hover = ImGui.IsItemHovered(); focus = ImGui.IsItemFocused();
        ImGui.PopStyleColor(3);
        return click;
    }
    private void SectionLabel(string title, string trailing)
    {
        var p = ImGui.GetCursorScreenPos(); var w = ImGui.GetContentRegionAvail().X;
        Label(p, title, White, 1.2f);
        Label(p + new Vector2(w - ImGui.CalcTextSize(trailing).X, 4 * Scale), trailing, Muted);
        ImGui.Dummy(new Vector2(w, 31 * Scale));
    }
    private static string JobName(string job) => job switch
    {
        "PLD" => "Paladin", "WAR" => "Warrior", "DRK" => "Dark Knight", "GNB" => "Gunbreaker",
        "WHM" => "White Mage", "SCH" => "Scholar", "AST" => "Astrologian", "SGE" => "Sage",
        "MNK" => "Monk", "DRG" => "Dragoon", "NIN" => "Ninja", "SAM" => "Samurai", "RPR" => "Reaper", "VPR" => "Viper",
        "BRD" => "Bard", "MCH" => "Machinist", "DNC" => "Dancer", "BLM" => "Black Mage", "SMN" => "Summoner", "RDM" => "Red Mage", "PCT" => "Pictomancer", _ => job,
    };
    private static string ExpansionName(string id) => id == "arr" ? "A Realm Reborn" : ShortName(id);

    private void DrawMasthead(CharacterProgress c, ulong id)
    {
        var p = ImGui.GetCursorScreenPos(); var w = ImGui.GetContentRegionAvail().X; var d = ImGui.GetWindowDrawList();
        if (artwork?.Logo(p + new Vector2(-3, -1) * Scale, new Vector2(215, 66) * Scale) != true)
            Label(p + new Vector2(3, 21) * Scale, "Relic Atlas", White, 1.55f);
        var tabWidth = w < 1000 * Scale ? 104f : 124f;
        var navSize = new Vector2(tabWidth * 3 + 16, 46) * Scale;
        var nav = p + new Vector2((w - navSize.X) / 2, 10 * Scale);
        d.AddRectFilled(nav, nav + navSize, Ink(new(.075f, .075f, .12f, 1)), 9 * Scale);
        d.AddRect(nav, nav + navSize, Ink(new(.22f, .20f, .31f, .8f)), 9 * Scale);
        var pages = new[] { (AtlasPage.Overview, "Overview"), (AtlasPage.Collection, "Collection"), (AtlasPage.Shopping, "Shopping list") };
        for (var i = 0; i < pages.Length; i++)
        {
            ImGui.SetCursorScreenPos(nav + new Vector2(4 + i * (tabWidth + 4), 4) * Scale);
            if (HeaderTab(pages[i].Item1.ToString(), pages[i].Item2, page == pages[i].Item1 || page == AtlasPage.Atma && pages[i].Item1 == AtlasPage.Collection, tabWidth))
            { page = pages[i].Item1; if (page == AtlasPage.Overview) overviewRefresh = DateTime.MinValue; }
        }
        var profile = p + new Vector2(w - 258 * Scale, 1 * Scale);
        ImGui.SetCursorScreenPos(profile);
        ImGui.BeginGroup(); DrawToolbar(c, id, 220); ImGui.EndGroup();
        var close = p + new Vector2(w - 28 * Scale, 17 * Scale);
        ImGui.SetCursorScreenPos(close);
        if (CanvasButton("close-window", new Vector2(28, 32) * Scale, out var hover, out var focus)) IsOpen = false;
        if (hover || focus) d.AddRectFilled(close, close + new Vector2(28, 32) * Scale, Ink(Surface), 5 * Scale);
        if (focus) d.AddRect(close, close + new Vector2(28, 32) * Scale, Ink(Cyan), 5 * Scale);
        var center = close + new Vector2(14, 16) * Scale;
        Line(center + new Vector2(-4, -4) * Scale, center + new Vector2(4, 4) * Scale, hover || focus ? White : Muted, 1.5f);
        Line(center + new Vector2(4, -4) * Scale, center + new Vector2(-4, 4) * Scale, hover || focus ? White : Muted, 1.5f);
        Tip("Close Relic Atlas");
        // Explicit drag targets keep the header movable even with title-bar-only dragging enabled.
        HeaderDrag("logo", p, new Vector2(nav.X - p.X - 8 * Scale, 66 * Scale));
        HeaderDrag("gap", new Vector2(nav.X + navSize.X + 8 * Scale, p.Y), new Vector2(profile.X - nav.X - navSize.X - 16 * Scale, 66 * Scale));
        Line(p + new Vector2(0, 66) * Scale, p + new Vector2(w, 66 * Scale), new(.17f, .18f, .28f, 1));
        ImGui.SetCursorScreenPos(p); ImGui.Dummy(new Vector2(w, 77 * Scale));
    }
    private bool HeaderTab(string id, string title, bool selected, float width)
    {
        var p = ImGui.GetCursorScreenPos(); var size = new Vector2(width, 38) * Scale;
        var clicked = CanvasButton(id, size, out var hover, out var focus);
        var d = ImGui.GetWindowDrawList();
        if (selected || hover) d.AddRectFilled(p, p + size, Ink(selected ? new(.20f, .16f, .32f, 1) : Surface), 6 * Scale);
        if (focus) d.AddRect(p, p + size, Ink(Cyan), 6 * Scale);
        Label(p + (size - ImGui.CalcTextSize(title)) / 2, title, selected || hover ? White : Muted);
        return clicked;
    }
    private void HeaderDrag(string id, Vector2 position, Vector2 size)
    {
        if (size.X <= 0 || IsPinned || (Flags & ImGuiWindowFlags.NoMove) != 0) return;
        ImGui.SetCursorScreenPos(position);
        ImGui.InvisibleButton("##move-header-" + id, size);
        if (ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left))
            headerMove = ImGui.GetWindowPos() + ImGui.GetIO().MouseDelta;
        Tip("Drag to move Relic Atlas");
    }
    private bool Navigation(string id, string title, bool selected, float width, bool compact = false)
    {
        var p = ImGui.GetCursorScreenPos(); var size = new Vector2(width, compact ? 34 : 41) * Scale;
        var clicked = CanvasButton("##" + id, size, out var hover, out var focus);
        var d = ImGui.GetWindowDrawList();
        if (hover || selected && !compact) d.AddRectFilled(p, p + size, Ink(selected ? new(.20f, .16f, .33f, 1) : Surface), 8 * Scale);
        if (focus) d.AddRect(p, p + size, Ink(Cyan), 8 * Scale);
        Label(p + (size - ImGui.CalcTextSize(title)) / 2, title, selected ? White : Muted);
        if (selected) Line(p + new Vector2((compact ? 12 : 22) * Scale, size.Y), p + new Vector2(size.X - (compact ? 12 : 22) * Scale, size.Y), compact ? Violet : Cyan, 2);
        return clicked;
    }
    private void DrawExpansionPicker(float width = -1)
    {
        ImGui.SetNextItemWidth(width);
        if (ImGui.BeginCombo("##expansion", expansion == "all" ? "All expansions" : ExpansionName(expansion)))
        {
            foreach (var value in new[] { "all" }.Concat(catalog.Series.Select(s => s.Id)))
            {
                if (!ImGui.Selectable(value == "all" ? "All expansions" : ExpansionName(value), value == expansion)) continue;
                expansion = value;
                if (value == "all") continue;
                var series = catalog.Series.Single(s => s.Id == value);
                Select(value, series.Jobs.Contains(selectedJob) ? selectedJob : series.Jobs[0]);
            }
            ImGui.EndCombo();
        }
    }
    private void DrawRelicCard(WeaponRow row)
    {
        ImGui.PushID(row.Series.Id + row.Job);
        var p = ImGui.GetCursorScreenPos(); var w = ImGui.GetContentRegionAvail().X; var size = new Vector2(w, 73 * Scale);
        var selected = selectedSeries == row.Series.Id && selectedJob == row.Job;
        if (CanvasButton("##relic", size, out var hover, out var focus)) Select(row.Series.Id, row.Job);
        Tip($"{JobName(row.Job)} / {row.Series.Name}\nNext stage: {row.Next?.Name ?? "Complete"}\n{row.Objective?.Label ?? ""}");
        var d = ImGui.GetWindowDrawList();
        if (selected || hover) d.AddRectFilled(p, p + size, Ink(selected ? new(.18f, .145f, .285f, 1) : Surface), 7 * Scale);
        if (selected) d.AddRect(p, p + size, Ink(new(.39f, .31f, .56f, .6f)), 7 * Scale);
        if (selected) d.AddRectFilled(p + new Vector2(0, 16) * Scale, p + new Vector2(3, 57) * Scale, Ink(Cyan), 2 * Scale);
        if (focus) d.AddRect(p, p + size, Ink(Cyan), 7 * Scale);
        var badge = p + new Vector2(11, 17) * Scale;
        if (artwork?.Job(row.Job, badge, new Vector2(34) * Scale) != true)
            Label(badge + new Vector2(0, 9) * Scale, row.Job, JobColor(row.Job), .9f);
        Label(p + new Vector2(57, 9) * Scale, Fit(JobName(row.Job), (w - 105 * Scale) / 1.1f), White, 1.1f);
        Label(p + new Vector2(w - 35 * Scale, 12 * Scale), $"{row.Completed + 1}/{row.Series.Stages.Count}", Muted, .9f);
        var subtitle = $"{row.Series.Name} · {row.Next?.Name ?? "Complete"}";
        Label(p + new Vector2(57, 34) * Scale, Fit(subtitle, (w - 72 * Scale) / .9f), selected ? Lavender : Muted, .9f);
        var barStart = p + new Vector2(57, 59) * Scale; var barWidth = w - 74 * Scale;
        d.AddRectFilled(barStart, barStart + new Vector2(barWidth, 2 * Scale), Ink(Edge));
        var progress = Math.Clamp((row.Completed + 1f) / row.Series.Stages.Count, 0, 1);
        if (progress > 0) d.AddRectFilled(barStart, barStart + new Vector2(barWidth * progress, 2 * Scale), Ink(selected ? Cyan : Violet));
        if (row.Weapon.Pinned) d.AddCircleFilled(p + new Vector2(w - 8 * Scale, 6 * Scale), 2.5f * Scale, Ink(Cyan));
        ImGui.PopID();
    }
    private void DrawRelicHeading(Series series, string job, int complete, int current)
    {
        var p = ImGui.GetCursorScreenPos(); var w = ImGui.GetContentRegionAvail().X; var d = ImGui.GetWindowDrawList();
        var compact = w < 640 * Scale;
        var height = compact ? 130f : 154f;
        var size = new Vector2(w, height * Scale);
        d.AddRectFilled(p, p + size, Ink(new(.12f, .075f, .23f, 1)), 10 * Scale);
        artwork?.Backdrop(p, size);
        d.AddRectFilledMultiColor(p, p + size, Ink(new(.035f, .03f, .08f, .30f)), Ink(new(.035f, .03f, .08f, .06f)), Ink(new(.035f, .03f, .08f, .35f)), Ink(new(.035f, .03f, .08f, .5f)));
        Label(p + new Vector2(21, 14) * Scale, $"{JobName(job)}  /  {ExpansionName(series.Id)}", Lavender, .95f);
        Label(p + new Vector2(19, compact ? 30 : 36) * Scale, Fit(series.Name, (w * .62f - 30 * Scale) / (compact ? 1.9f : 2.25f)), White, compact ? 1.9f : 2.25f);
        var weapon = series.Stages[current].Weapons[job].FirstOrDefault() ?? "";
        var hasIcon = artwork?.Weapon(weapon, p + new Vector2(21, compact ? 72 : 82) * Scale, new Vector2(compact ? 28 : 33) * Scale) == true;
        var textX = (hasIcon ? 65 : 21) * Scale;
        var weaponNames = string.Join(" + ", series.Stages[current].Weapons[job]);
        Label(p + new Vector2(textX, (compact ? 74 : 85) * Scale), Fit(weaponNames, (w * .63f - textX) / .95f), White, .95f);
        if (ImGui.IsMouseHoveringRect(p + new Vector2(textX, (compact ? 72 : 82) * Scale), p + new Vector2(w * .64f, (compact ? 102 : 116) * Scale)))
            ImGui.SetTooltip(weaponNames);
        Label(p + new Vector2(21, height - 26) * Scale, $"Stage {current + 1:D2}  ·  {series.Stages[current].Name}", Cyan, .9f);
        var progress = $"{complete + 1} / {series.Stages.Count} acquired";
        Label(p + new Vector2(w - ImGui.CalcTextSize(progress).X * .9f - 19 * Scale, (height - 26) * Scale), progress, White, .9f);
        ImGui.Dummy(size);
    }
    private void DrawStagePath(Series series, int complete, int current)
    {
        var p = ImGui.GetCursorScreenPos(); var w = ImGui.GetContentRegionAvail().X; var cell = w / series.Stages.Count; var d = ImGui.GetWindowDrawList();
        for (int i = 0; i < series.Stages.Count; i++)
        {
            var start = p + new Vector2(i * cell, 0); ImGui.SetCursorScreenPos(start);
            if (CanvasButton("##stage-node" + i, new Vector2(cell - 3 * Scale, 29 * Scale), out var hover, out var focus)) { viewedStage = i; resetDetailScroll = true; }
            Tip($"{i + 1}. {series.Stages[i].Name} — {(i <= complete ? "acquired" : i == complete + 1 ? "current stage" : "future stage")} — click to view");
            var color = i == current ? White : i <= complete ? Cyan : hover || focus ? Lavender : Muted;
            var number = (i + 1).ToString("D2");
            Label(start + new Vector2((cell - 4 * Scale - ImGui.CalcTextSize(number).X * .75f) / 2, 0), number, color, .75f);
            d.AddRectFilled(start + new Vector2(0, 21) * Scale, start + new Vector2(cell - 4 * Scale, 24 * Scale), Ink(i <= complete ? Cyan : i == current ? Violet : hover || focus ? Lavender : Edge), 2 * Scale);
            if (i == current) d.AddCircleFilled(start + new Vector2((cell - 4 * Scale) / 2, 22.5f * Scale), 3.5f * Scale, Ink(White));
        }
        ImGui.SetCursorScreenPos(p); ImGui.Dummy(new Vector2(w, 33 * Scale));
    }
    private bool ActionButton(string title, bool primary, float width, float height, bool quiet = false)
    {
        var p = ImGui.GetCursorScreenPos(); var size = new Vector2(width, height) * Scale;
        var click = CanvasButton(title, size, out var hover, out var focus); var d = ImGui.GetWindowDrawList();
        if (!quiet || hover || focus)
            d.AddRectFilled(p, p + size, Ink(primary ? (hover ? new(.62f,.94f,.98f,1) : Cyan) : (hover ? new(.22f,.18f,.33f,1) : new(.13f,.12f,.22f,.96f))), 5 * Scale);
        if (!primary && !quiet) d.AddRect(p, p + size, Ink(new(.34f,.30f,.45f,.55f)), 5 * Scale);
        if (focus) d.AddRect(p - new Vector2(2) * Scale, p + size + new Vector2(2) * Scale, Ink(Lavender), 6 * Scale);
        Label(p + (size - ImGui.CalcTextSize(title) * .94f) / 2, title, primary ? new(.025f,.09f,.12f,1) : quiet && !hover ? Muted : White, .94f);
        return click;
    }
    private static bool ObjectiveCheck(ref bool value)
    {
        var p = ImGui.GetCursorScreenPos(); var d = ImGui.GetWindowDrawList();
        var click = CanvasButton("##done", new Vector2(23) * Scale, out var hover, out var focus);
        if (click) value = !value;
        var center = p + new Vector2(11.5f) * Scale;
        d.AddCircle(center, 8.5f * Scale, Ink(value || hover || focus ? Cyan : new(.35f,.36f,.48f,1)), 24, 1.5f * Scale);
        if (value) { d.AddCircleFilled(center, 8.5f * Scale, Ink(Cyan), 24); Line(center + new Vector2(-4,0)*Scale, center + new Vector2(-1,3)*Scale, Surface,1.5f); Line(center + new Vector2(-1,3)*Scale, center + new Vector2(5,-3)*Scale, Surface,1.5f); }
        return click;
    }
    private void DrawNextAction(CharacterProgress c, Series series, string job, Stage stage, Requirement r, ulong id)
    {
        var p = ImGui.GetCursorScreenPos(); var w = ImGui.GetContentRegionAvail().X;
        var textWidth = w - 34 * Scale;
        var d = ImGui.GetWindowDrawList();
        // Measure real content first, then paint its background in a lower draw channel.
        d.ChannelsSplit(2); d.ChannelsSetCurrent(1);
        ImGui.SetCursorScreenPos(p + new Vector2(17, 12) * Scale);
        ImGui.BeginGroup();
        Label(ImGui.GetCursorScreenPos(), "Up next", Cyan, .95f);
        ImGui.Dummy(new Vector2(textWidth, 24 * Scale));
        Label(ImGui.GetCursorScreenPos(), Fit(r.Label, textWidth / 1.2f), White, 1.2f);
        ImGui.Dummy(new Vector2(textWidth, 22 * Scale)); Tip(r.Label);
        if (r.Detail.Length > 0)
        {
            ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + textWidth);
            ImGui.PushStyleColor(ImGuiCol.Text, Muted); ImGui.TextUnformatted(r.Detail); ImGui.PopStyleColor(); ImGui.PopTextWrapPos();
        }
        ImGui.EndGroup();
        var height = ImGui.GetItemRectMax().Y - p.Y + 14 * Scale;
        ImGui.SetCursorScreenPos(p + new Vector2(w - (r.Count > 1 ? 290 : 115) * Scale, 10 * Scale));
        ImGui.BeginDisabled(readOnly); ImGui.PushID("next-action"); ImGui.PushID(r.Id);
        DrawQuickAction(c, series, job, stage, r, id);
        ImGui.PopID(); ImGui.PopID(); ImGui.EndDisabled();
        d.ChannelsSetCurrent(0);
        d.AddRectFilled(p, p + new Vector2(w, height), Ink(new(.105f, .13f, .195f, 1)), 9 * Scale);
        Line(p + new Vector2(17, 0) * Scale, p + new Vector2(81, 0) * Scale, Cyan, 2);
        d.ChannelsMerge();
        ImGui.SetCursorScreenPos(p); ImGui.Dummy(new Vector2(w, height + 3 * Scale));
    }
    private void DrawMissionSwitch()
    {
        var p = ImGui.GetCursorScreenPos(); var w = ImGui.GetContentRegionAvail().X;
        for (int i = 0; i < 2; i++)
        {
            var start = p + new Vector2(i * 124, 0) * Scale; ImGui.SetCursorScreenPos(start);
            var selected = guideView == (i == 1);
            if (CanvasButton("##mission-mode" + i, new Vector2(117, 35) * Scale, out var hover, out var focus)) guideView = i == 1;
            Label(start + new Vector2(0, 5) * Scale, i == 0 ? "Objectives" : "Guide & notes", selected || hover ? White : Muted);
            if (selected || focus) Line(start + new Vector2(0, 33) * Scale, start + new Vector2(100, 33) * Scale, Violet, 2);
        }
        if (!guideView)
        {
            Label(p + new Vector2(w - 180 * Scale, 5 * Scale), objectiveSummary, Muted, .9f);
            ImGui.SetCursorScreenPos(p + new Vector2(w - 54 * Scale, 3 * Scale));
            if (ActionButton("View", false, 51, 26)) ImGui.OpenPopup("objective-view");
            if (ImGui.BeginPopup("objective-view"))
            {
                ImGui.Checkbox("Hide ready objectives", ref hideReady);
                if (ImGui.Button(expandGroups ? "Collapse all groups" : "Expand all groups"))
                { expandGroups = !expandGroups; groupExpansion = expandGroups; ImGui.CloseCurrentPopup(); }
                ImGui.EndPopup();
            }
        }
        ImGui.SetCursorScreenPos(p); ImGui.Dummy(new Vector2(w, 42 * Scale));
    }
    private bool MissionGroup(string title, int done, int total, bool open, float trailing = 0)
    {
        var p = ImGui.GetCursorScreenPos(); var w = ImGui.GetContentRegionAvail().X; var size = new Vector2(w, 38 * Scale);
        var click = CanvasButton("##group", size - new Vector2(trailing * Scale, 0), out var hover, out var focus); var d = ImGui.GetWindowDrawList();
        if (hover || focus) d.AddRectFilled(p, p + size, Ink(Surface), 5 * Scale);
        var arrow = p + new Vector2(8, 18) * Scale;
        if (open) { Line(arrow + new Vector2(-3, -2) * Scale, arrow + new Vector2(1, 2) * Scale, Muted); Line(arrow + new Vector2(1, 2) * Scale, arrow + new Vector2(5, -2) * Scale, Muted); }
        else { Line(arrow + new Vector2(0, -4) * Scale, arrow + new Vector2(4, 0) * Scale, Muted); Line(arrow + new Vector2(4, 0) * Scale, arrow + new Vector2(0, 4) * Scale, Muted); }
        Label(p + new Vector2(24, 8) * Scale, Fit(title, w - (93 + trailing) * Scale), White);
        var count = $"{done}/{total}"; Label(p + new Vector2(w - ImGui.CalcTextSize(count).X - (8 + trailing) * Scale, 8 * Scale), count, Muted);
        Line(p + new Vector2(0, 37) * Scale, p + new Vector2(w, 37 * Scale), new(.16f, .18f, .26f, 1));
        return click;
    }
    private void DrawSupplyStats(RemainingPlan plan)
    {
        var p = ImGui.GetCursorScreenPos(); var w = ImGui.GetContentRegionAvail().X; var cell = w / 3; var d = ImGui.GetWindowDrawList();
        var values = new[] { (plan.Weapons, "Relics in scope"), (plan.Materials.Count(m => m.Missing > 0), "Materials to collect"), (plan.Objectives.Count, "Duties & unlocks") };
        var size = new Vector2(w, 88 * Scale);
        d.AddRectFilled(p, p + size, Ink(Surface), 10 * Scale);
        artwork?.Backdrop(p, size);
        d.AddRectFilled(p, p + size, Ink(new(.04f, .035f, .095f, .7f)), 9 * Scale);
        for (int i = 0; i < 3; i++)
        {
            var start = p + new Vector2(i * cell + 20 * Scale, 12 * Scale);
            Label(start, values[i].Item1.ToString("N0"), i == 1 ? Cyan : White, 1.8f);
            Label(start + new Vector2(0, 41) * Scale, values[i].Item2, Muted);
            if (i > 0) Line(start + new Vector2(-20, 5) * Scale, start + new Vector2(-20, 62) * Scale, Edge);
        }
        ImGui.Dummy(new Vector2(w, 94 * Scale));
    }
}
