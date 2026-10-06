using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using RelicAtlas.Core;

namespace RelicAtlas.UI;

public sealed partial class MainWindow
{
    private static readonly Vector4 Surface = AtlasTheme.Rgb(0x222426);
    private static readonly Vector4 Edge = AtlasTheme.Rgb(0x41464a);
    private static readonly Vector4 White = AtlasTheme.Rgb(0xedf0f2);
    private static readonly Vector4 Violet = AtlasTheme.Rgb(0x81aaff);
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
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 0f);
        var click = ImGui.Button("###" + id, size);
        ImGui.PopStyleVar();
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
        "BRD" => "Bard", "MCH" => "Machinist", "DNC" => "Dancer", "BLM" => "Black Mage", "SMN" => "Summoner", "RDM" => "Red Mage", "PCT" => "Pictomancer",
        "CRP" => "Carpenter", "BSM" => "Blacksmith", "ARM" => "Armorer", "GSM" => "Goldsmith", "LTW" => "Leatherworker",
        "WVR" => "Weaver", "ALC" => "Alchemist", "CUL" => "Culinarian", "MIN" => "Miner", "BTN" => "Botanist", "FSH" => "Fisher", _ => job,
    };
    private static string ExpansionName(string id) => id == "arr" ? "A Realm Reborn" : ShortName(id);

    private void ChangeRelicKind(string kind)
    {
        if (relicKind == kind) return;
        relicKind = kind; expansion = "all"; jobFilter = planningJob = "All jobs";
        overviewRole = "All roles"; overviewSearch = search = planningSearch = "";
        overviewRefresh = planningRefresh = DateTime.MinValue;
        var series = VisibleSeries.LastOrDefault(s => s.Jobs.Contains(tracker.CurrentJob)) ?? VisibleSeries.First();
        Select(series.Id, series.Jobs.Contains(tracker.CurrentJob) ? tracker.CurrentJob : series.Jobs[0]);
    }

    private void DrawRelicKindPicker()
    {
        if (Navigation("weapons-kind","Weapons",relicKind == "weapon",0,true)) ChangeRelicKind("weapon");
        ImGui.SameLine();
        if (Navigation("tools-kind","Crafting & gathering",relicKind == "tool",0,true)) ChangeRelicKind("tool");
        ImGui.Spacing();
    }

    private void DrawMasthead(CharacterProgress c, ulong id)
    {
        if (compactLayout) DrawToolbar(c,id,ImGui.GetContentRegionAvail().X / Scale);
        else
        {
        ImGui.BeginGroup();
        ImGui.TextColored(Accent, "Relic Atlas");
        ImGui.TextColored(Muted, "Relic progress");
        ImGui.EndGroup();
        ImGui.SameLine();
        var profileWidth = Math.Min(260 * Scale, ImGui.GetWindowWidth() * .52f);
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X - profileWidth);
        ImGui.BeginGroup(); DrawToolbar(c,id,profileWidth / Scale); ImGui.EndGroup();
        }
        foreach (var tab in new[] { (AtlasPage.Overview,"Overview"), (AtlasPage.Collection,"Collection"), (AtlasPage.Shopping,"Shopping list") })
        {
            if (tab.Item1 != AtlasPage.Overview) ImGui.SameLine();
            if (Navigation(tab.Item1.ToString(),tab.Item2,page == tab.Item1 || page == AtlasPage.Atma && tab.Item1 == AtlasPage.Collection,0,true))
            { page = tab.Item1; overviewRefresh = DateTime.MinValue; }
        }
        ImGui.Separator();
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
        ImGui.PushID(id);
        ImGui.PushStyleColor(ImGuiCol.Button,Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.Text,selected ? White : Muted);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize,0);
        var clicked = ImGui.Button(title);
        ImGui.PopStyleVar();
        var min = ImGui.GetItemRectMin(); var max = ImGui.GetItemRectMax();
        if (selected) Line(new(min.X,max.Y),max,Accent,2);
        ImGui.PopStyleColor(2); ImGui.PopID();
        return clicked;
    }
    private void DrawExpansionPicker(float width = -1)
    {
        ImGui.SetNextItemWidth(width);
        var selected = catalog.Series.FirstOrDefault(s => s.Id == expansion);
        if (ImGui.BeginCombo("##expansion", selected == null ? "All collections" : selected.IsTool ? selected.Name + " · " + selected.Expansion : ExpansionName(selected.ExpansionKey)))
        {
            foreach (var value in new[] { "all" }.Concat(VisibleSeries.Select(s => s.Id)))
            {
                var option = catalog.Series.FirstOrDefault(s => s.Id == value);
                var label = option == null ? "All collections" : option.IsTool ? option.Name + " · " + option.Expansion : ExpansionName(option.ExpansionKey);
                if (!ImGui.Selectable(label, value == expansion)) continue;
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
        if (selected || hover) d.AddRectFilled(p, p + size, Ink(selected ? AtlasTheme.Rgb(0x293b58) : Surface), 7 * Scale);
        if (selected) d.AddRect(p, p + size, Ink(Edge), 7 * Scale);
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
        var width = Math.Max(1,ImGui.GetContentRegionAvail().X - 114 * Scale);
        ImGui.TextColored(Accent,Fit($"{JobName(job)} · {series.Name}",width));
        var names = string.Join(" + ",series.Stages[current].Weapons[job]);
        ImGui.TextUnformatted(Fit(names,width)); Tip(names);
        ImGui.Spacing();
    }
    private void DrawStagePath(Series series, int complete, int current)
    {
        if (compactLayout || series.Stages.Count > 12)
        {
            ImGui.SetNextItemWidth(-1);
            if (ImGui.BeginCombo("##long-stage-path", $"Stage {current + 1:D2} / {series.Stages.Count}   ·   {series.Stages[current].Name}"))
            {
                for (var i = 0; i < series.Stages.Count; i++)
                    if (ImGui.Selectable($"{i + 1:D2} · {series.Stages[i].Name}" + (i <= complete ? "   ✓" : i == complete + 1 ? "   · Next" : ""), i == current))
                    { viewedStage = i; resetDetailScroll = true; }
                ImGui.EndCombo();
            }
            Tip("Browse all tool stages. Selecting a stage does not change recorded progress.");
            ImGui.Spacing();
            return;
        }
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
        if (quiet) ImGui.PushStyleColor(ImGuiCol.Button,Vector4.Zero);
        if (primary) ImGui.PushStyleColor(ImGuiCol.Button,new Vector4(.16f,.23f,.35f,1));
        var clicked = ImGui.Button(title,new Vector2(Math.Max(width * Scale,ImGui.CalcTextSize(title).X + ImGui.GetStyle().FramePadding.X * 2),ImGui.GetFrameHeight()));
        if (primary) ImGui.PopStyleColor();
        if (quiet) ImGui.PopStyleColor();
        return clicked;
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
        Wrap("Next: " + r.Label);
        ImGui.BeginDisabled(readOnly); ImGui.PushID("next-action"); ImGui.PushID(r.Id);
        DrawQuickAction(c,series,job,stage,r,id);
        ImGui.PopID(); ImGui.PopID(); ImGui.EndDisabled();
        if (r.Detail.Length > 0) { ImGui.TextColored(Muted,"Step guidance"); Tip(r.Detail); }
        ImGui.Separator();
    }
    private void DrawMissionSwitch()
    {
        if (Navigation("objectives","Objectives",!guideView,0,true)) guideView = false;
        ImGui.SameLine();
        if (Navigation("guide","Guide & notes",guideView,0,true)) guideView = true;
        if (!guideView)
        {
            ImGui.SameLine();
            if (ImGui.Button("View")) ImGui.OpenPopup("objective-view");
            if (ImGui.BeginPopup("objective-view"))
            {
                ImGui.Checkbox("Hide ready objectives",ref hideReady);
                if (ImGui.Button(expandGroups ? "Collapse all groups" : "Expand all groups"))
                { expandGroups = !expandGroups; groupExpansion = expandGroups; ImGui.CloseCurrentPopup(); }
                ImGui.EndPopup();
            }
            ImGui.TextColored(Muted,objectiveSummary);
        }
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
        Wrap($"{plan.Weapons} relics · {plan.Materials.Count(m => m.Missing > 0)} materials needed · {plan.Objectives.Count} duties & unlocks");
    }
}
