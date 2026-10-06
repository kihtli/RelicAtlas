using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using RelicAtlas.Core;
using RelicAtlas.Tracking;

namespace RelicAtlas.UI;

public sealed class BookPopoutWindow : Window
{
    private readonly Configuration config;
    private readonly Catalog catalog;
    private readonly GameTracker tracker;
    private readonly IBookTravel travel;
    private readonly Action save;
    private AtlasTheme? theme;
    private BookRoute? route;
    private (ulong Character, uint Relic, uint Book, string Job)? identity;
    private string? selectedRequirementId;
    private long showTravelStatusUntil;
    private Vector2? move;
    private bool compact;
    public Action<string>? OpenChecklist { get; set; }
    private static float Scale => ImGuiHelpers.GlobalScale;
    private static float Line => ImGui.GetTextLineHeight();
    private static float ControlHeight => Math.Max(26 * Scale, Line + 8 * Scale);
    private float FooterHeight => ControlHeight + (compact ? 16 * Scale : Line + 24 * Scale);
    private static readonly Vector4 White = AtlasTheme.Rgb(0xedf0f2);
    private static readonly Vector4 Muted = AtlasTheme.Rgb(0xb0b8c0);
    private static readonly Vector4 Cyan = AtlasTheme.Rgb(0x68d4dc);
    private static readonly Vector4 Violet = AtlasTheme.Rgb(0x81aaff);
    private static readonly Vector4 Green = AtlasTheme.Rgb(0x87c9a1);
    private static readonly ArrBookObjectiveKind[] Kinds =
        [ArrBookObjectiveKind.Monster, ArrBookObjectiveKind.Dungeon, ArrBookObjectiveKind.Fate, ArrBookObjectiveKind.Leve];

    public BookPopoutWindow(Configuration config, Catalog catalog, GameTracker tracker, IBookTravel travel, Action save)
        : base("Zodiac book tracker###RelicAtlasBookPopout", ImGuiWindowFlags.NoTitleBar)
    {
        this.config = config; this.catalog = catalog; this.tracker = tracker; this.travel = travel; this.save = save;
        Size = new Vector2(450, 570); SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(320, 360), MaximumSize = new Vector2(float.MaxValue) };
        RespectCloseHotkey = false; DisableWindowSounds = true;
        AllowClickthrough = false; AllowPinning = false; ShowCloseButton = false;
        IsOpen = config.BookPopoutOpen;
    }

    public void Show() { IsOpen = true; SetOpen(true); BringToFront(); }
    private void SetOpen(bool open)
    {
        if (config.BookPopoutOpen == open) return;
        config.BookPopoutOpen = open; save();
    }
    public override void OnOpen() => SetOpen(true);
    public override void OnClose() => SetOpen(false);
    public override void PreDraw()
    {
        theme = new AtlasTheme();
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(10, 9) * Scale);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(4, 2) * Scale);
        if (config.BookPopoutLocked) Flags |= ImGuiWindowFlags.NoMove;
        else Flags &= ~ImGuiWindowFlags.NoMove;
    }
    public override void PostDraw() { ImGui.PopStyleVar(2); theme?.Dispose(); theme = null; }

    public override void Draw()
    {
        compact = ImGui.GetWindowWidth() < 430 * Scale || ImGui.GetWindowHeight() < 530 * Scale;
        var id = tracker.CurrentId;
        var character = id != 0 ? config.Characters.GetValueOrDefault(id) : null;
        var book = character?.DetectedBook;
        (ulong, uint, uint, string)? current = book == null ? null : (id, book.RelicId, book.BookId, book.Job);
        if (current != identity)
        {
            identity = current;
            selectedRequirementId = null;
            showTravelStatusUntil = 0;
        }
        route = character == null ? null : BookRoute.Build(catalog, character, selectedRequirementId,
            currentTerritoryId: tracker.CurrentTerritoryId);
        // Only an explicit choice stays selected. Suggested steps can change as the player
        // changes zones; finishing a chosen step returns to the current-zone suggestion.
        if (selectedRequirementId != null && route?.Steps.Any(s =>
                s.Requirement.Id == selectedRequirementId && !s.Status.Complete) != true)
            selectedRequirementId = null;
        DrawHeader();
        if (route == null) DrawEmpty(id);
        else
        {
            DrawBook();
            if (!compact) DrawSummary();
            if (route.Next is { } next) DrawNext(id, next);
            else DrawComplete();
            DrawObjectives(id);
        }
        DrawFooter();
        if (move is { } position) { ImGui.SetWindowPos(position); move = null; }
    }

    private void DrawHeader()
    {
        var p = ImGui.GetCursorScreenPos(); var w = ImGui.GetContentRegionAvail().X;
        var h = ControlHeight; var buttonWidth = 32 * Scale;
        var d = ImGui.GetWindowDrawList();
        d.AddRectFilled(p + new Vector2(3 * Scale, 5 * Scale), p + new Vector2(5 * Scale, h - 5 * Scale), Ink(Violet), Scale);
        Label(p + new Vector2(14 * Scale, (h - Line) / 2), Fit(compact ? "Zodiac book" : "ZODIAC BOOK TRACKER", w - 2 * buttonWidth - 20 * Scale), White);
        if (ButtonAt("book-lock", p + new Vector2(w - 2 * buttonWidth, 0), new Vector2(buttonWidth, h)))
        { config.BookPopoutLocked = !config.BookPopoutLocked; save(); }
        var c = p + new Vector2(w - 1.5f * buttonWidth, h / 2);
        var color = Ink(config.BookPopoutLocked ? Cyan : Muted);
        d.AddRect(c + new Vector2(-4, -1) * Scale, c + new Vector2(4, 6) * Scale, color, Scale, ImDrawFlags.None, Scale);
        var shift = config.BookPopoutLocked ? 0 : 3;
        d.AddLine(c + new Vector2(-3 + shift, -1) * Scale, c + new Vector2(-3 + shift, -6) * Scale, color, Scale);
        d.AddLine(c + new Vector2(-3 + shift, -6) * Scale, c + new Vector2(3 + shift, -6) * Scale, color, Scale);
        d.AddLine(c + new Vector2(3 + shift, -6) * Scale, c + new Vector2(3 + shift, -1) * Scale, color, Scale);
        HoverTip(config.BookPopoutLocked ? "Unlock the window position. Book objectives stay clickable while locked." : "Lock the window position.");
        if (ButtonAt("book-close", p + new Vector2(w - buttonWidth, 0), new Vector2(buttonWidth, h)))
        { IsOpen = false; SetOpen(false); }
        c = p + new Vector2(w - buttonWidth / 2, h / 2);
        d.AddLine(c - new Vector2(4) * Scale, c + new Vector2(4) * Scale, Ink(Muted), 1.3f * Scale);
        d.AddLine(c + new Vector2(4, -4) * Scale, c + new Vector2(-4, 4) * Scale, Ink(Muted), 1.3f * Scale);
        HoverTip("Close the Zodiac book tracker. Reopen with /relicatlas book popout.");
        ImGui.SetCursorScreenPos(p);
        ImGui.InvisibleButton("##drag-book", new Vector2(w - 2 * buttonWidth - 4 * Scale, h));
        if (!config.BookPopoutLocked && ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left))
            move = ImGui.GetWindowPos() + ImGui.GetIO().MouseDelta;
        HoverTip(config.BookPopoutLocked ? "Position locked" : "Drag to move · Drag the bottom-right corner to resize · Escape leaves this tracker open");
        d.AddLine(p + new Vector2(0, h + 4 * Scale), p + new Vector2(w, h + 4 * Scale), Ink(AtlasTheme.Rgb(0x41464a)), Scale);
        ImGui.SetCursorScreenPos(p); ImGui.Dummy(new Vector2(w, h + 8 * Scale));
    }

    private void DrawBook()
    {
        var p = ImGui.GetCursorScreenPos(); var w = ImGui.GetContentRegionAvail().X;
        var r = route!;
        if (compact)
        {
            Label(p + new Vector2(4 * Scale, 0), Fit($"{r.Job} · {r.Group}", w - 8 * Scale), tracker.BookIsCurrent ? Cyan : Muted);
            ImGui.Dummy(new Vector2(w, Line + 6 * Scale));
            HoverTip($"{r.Group} · {r.Job}\n{tracker.BookStatus}\n" + string.Join("\n", Kinds.Select(k => $"{KindName(k)}: {r.Steps.Count(s => s.Kind == k && s.Status.Complete)}/{r.Steps.Count(s => s.Kind == k)}")));
            return;
        }
        Label(p + new Vector2(4 * Scale, 0), tracker.BookIsCurrent ? "Picked up · " + r.Job : "Last recorded · " + r.Job, tracker.BookIsCurrent ? Cyan : Muted);
        Label(p + new Vector2(4 * Scale, Line + 5 * Scale), Fit(r.Group, w - 8 * Scale), White);
        ImGui.Dummy(new Vector2(w, 2 * Line + 10 * Scale));
        HoverTip($"{r.Group} · {r.Job}\n\n{tracker.BookStatus}\n\nThe owning job comes from the held book. Equip its Atma weapon when completing objectives. Progress comes from game data, including when the game's book window is closed.");
    }

    private void DrawSummary()
    {
        var p = ImGui.GetCursorScreenPos(); var w = ImGui.GetContentRegionAvail().X;
        var cellWidth = (w - 12 * Scale) / 4; var h = 2 * Line + 12 * Scale;
        var d = ImGui.GetWindowDrawList();
        for (var i = 0; i < Kinds.Length; i++)
        {
            var kind = Kinds[i]; var all = route!.Steps.Where(s => s.Kind == kind).ToArray();
            var done = all.Count(s => s.Status.Complete); var complete = done == all.Length;
            var c = p + new Vector2(i * (cellWidth + 4 * Scale), 0);
            d.AddRectFilled(c, c + new Vector2(cellWidth, h), Ink(AtlasTheme.Rgb(0x222426)), 4 * Scale);
            Label(c + new Vector2(7 * Scale, 4 * Scale), KindName(kind), Muted);
            Label(c + new Vector2(7 * Scale, Line + 7 * Scale), $"{done}/{all.Length}", complete ? Green : White);
        }
        ImGui.Dummy(new Vector2(w, h + 4 * Scale));
        HoverTip($"{route!.Completed}/{route.Steps.Count} objectives complete. Enemy objectives show how many of their required kills have been recorded. Manual checklist overrides are excluded from this tracker.");
    }

    private void DrawNext(ulong id, BookStep step)
    {
        var p = ImGui.GetCursorScreenPos(); var w = ImGui.GetContentRegionAvail().X;
        var pitch = Line + 5 * Scale; var actionY = 10 * Scale + (compact ? 3 : 4) * pitch;
        var d = ImGui.GetWindowDrawList(); var size = new Vector2(w, actionY + ControlHeight + 8 * Scale);
        d.AddRectFilled(p, p + size, Ink(AtlasTheme.Rgb(0x293b58)), 5 * Scale);
        Label(p + new Vector2(10 * Scale, 8 * Scale), "Next step · " + KindName(step.Kind), Cyan);
        var count = $"{step.Status.Done}/{step.Status.Required}";
        Label(p + new Vector2(w - ImGui.CalcTextSize(count).X - 10 * Scale, 8 * Scale), count, Violet);
        Label(p + new Vector2(10 * Scale, 8 * Scale + pitch), Fit(step.Requirement.Label, w - 20 * Scale), White);
        var location = step.Location is { } l ? $"{l.Zone} · {l.X:0.0}, {l.Y:0.0}" : "Location unavailable";
        Label(p + new Vector2(10 * Scale, 8 * Scale + 2 * pitch), Fit(location, w - 20 * Scale), Muted);
        var guidance = step.Kind switch
        {
            ArrBookObjectiveKind.Monster => $"Defeat {step.Status.Remaining} more · equip the {step.Job} Atma weapon.",
            ArrBookObjectiveKind.Dungeon => "Dungeon entrance · Duty Finder also works.",
            ArrBookObjectiveKind.Fate => "Wait for the FATE · equip your Atma weapon.",
            ArrBookObjectiveKind.Leve => "Accept the leve, then start it in your Journal.",
            _ => "Complete with your Atma weapon equipped.",
        };
        if (!compact) Label(p + new Vector2(10 * Scale, 8 * Scale + 3 * pitch), Fit(guidance, w - 20 * Scale), Muted);
        ImGui.SetCursorScreenPos(p); ImGui.Dummy(new Vector2(w, actionY - 4 * Scale));
        HoverTip(guidance + "\n\n" + StepHint(step));
        var check = travel.Check(id, step);
        var flagWidth = ImGui.CalcTextSize("Flag only").X + 24 * Scale;
        var actionWidth = w - flagWidth - 24 * Scale;
        ImGui.BeginDisabled(!check.Allowed);
        if (ButtonAt("book-next", p + new Vector2(8 * Scale, actionY), new Vector2(actionWidth, ControlHeight), true)) Travel(id, step);
        ImGui.EndDisabled();
        Label(p + new Vector2(18 * Scale, actionY + (ControlHeight - Line) / 2), Fit(check.ActionLabel, actionWidth - 20 * Scale), check.Allowed ? Cyan : Muted);
        HoverTip(check.Allowed ? TravelHint(step, check) : check.Reason);
        ImGui.BeginDisabled(!tracker.BookIsCurrent || step.Location == null);
        if (ButtonAt("book-flag", p + new Vector2(w - flagWidth - 8 * Scale, actionY), new Vector2(flagWidth, ControlHeight), true))
        { travel.Flag(id, step); showTravelStatusUntil = Environment.TickCount64 + 10000; }
        ImGui.EndDisabled();
        Label(p + new Vector2(w - flagWidth + 4 * Scale, actionY + (ControlHeight - Line) / 2), "Flag only", tracker.BookIsCurrent && step.Location != null ? Violet : Muted);
        HoverTip("Open the map and flag this objective without teleporting.\n\n" + StepHint(step));
        ImGui.SetCursorScreenPos(p); ImGui.Dummy(size);
    }

    private void DrawComplete()
    {
        var p = ImGui.GetCursorScreenPos(); var w = ImGui.GetContentRegionAvail().X;
        var pitch = Line + 6 * Scale; var h = 3 * pitch + 14 * Scale;
        ImGui.GetWindowDrawList().AddRectFilled(p, p + new Vector2(w, h), Ink(new(.075f, .16f, .14f, 1)), 5 * Scale);
        Label(p + new Vector2(10 * Scale, 10 * Scale), "Book complete", Green);
        Label(p + new Vector2(10 * Scale, 10 * Scale + pitch), "All 19 objectives recorded", White);
        Label(p + new Vector2(10 * Scale, 10 * Scale + 2 * pitch), Fit("Your next book will appear automatically.", w - 20 * Scale), Muted);
        ImGui.Dummy(new Vector2(w, h + 4 * Scale));
    }

    private void DrawObjectives(ulong id)
    {
        var p = ImGui.GetCursorScreenPos(); var w = ImGui.GetContentRegionAvail().X;
        var h = Line + 10 * Scale; var box = Math.Max(16 * Scale, Line);
        var toggleWidth = box + ImGui.CalcTextSize("Hide completed").X + 18 * Scale;
        if (ButtonAt("book-hide-completed", p, new Vector2(toggleWidth, h)))
        { config.BookPopoutHideComplete = !config.BookPopoutHideComplete; save(); }
        var checkPos = p + new Vector2(4 * Scale, (h - box) / 2);
        var d = ImGui.GetWindowDrawList();
        d.AddRectFilled(checkPos, checkPos + new Vector2(box), Ink(config.BookPopoutHideComplete ? AtlasTheme.Rgb(0x293b58) : AtlasTheme.Rgb(0x222426)), 3 * Scale);
        d.AddRect(checkPos, checkPos + new Vector2(box), Ink(config.BookPopoutHideComplete ? Violet : Muted), 3 * Scale, ImDrawFlags.None, Scale);
        if (config.BookPopoutHideComplete)
        {
            d.AddLine(checkPos + new Vector2(box * .22f, box * .52f), checkPos + new Vector2(box * .43f, box * .73f), Ink(White), 2 * Scale);
            d.AddLine(checkPos + new Vector2(box * .43f, box * .73f), checkPos + new Vector2(box * .80f, box * .26f), Ink(White), 2 * Scale);
        }
        Label(p + new Vector2(box + 10 * Scale, (h - Line) / 2), "Hide completed", White);
        HoverTip("Hide completed objectives and empty categories. Book totals always include every objective. This preference is saved.");
        var summary = compact ? $"{route!.Completed}/19" : $"{route!.Completed}/19 complete";
        Label(p + new Vector2(w - ImGui.CalcTextSize(summary).X - 4 * Scale, (h - Line) / 2), summary, White);
        ImGui.SetCursorScreenPos(p); ImGui.Dummy(new Vector2(w, h));
        // Reserve room for the footer as native font sizes grow; every objective remains scrollable.
        var listHeight = Math.Max(40 * Scale, ImGui.GetContentRegionAvail().Y - FooterHeight - 4 * Scale);
        ImGui.BeginChild("book-objectives", new Vector2(0, listHeight), false);
        foreach (var kind in Kinds)
        {
            var rows = route.Steps.Where(s => s.Kind == kind && (!config.BookPopoutHideComplete || !s.Status.Complete)).ToArray();
            if (rows.Length == 0) continue;
            p = ImGui.GetCursorScreenPos(); w = ImGui.GetContentRegionAvail().X;
            if (!compact)
            {
                Label(p + new Vector2(6 * Scale, 4 * Scale), KindName(kind), Violet);
                ImGui.Dummy(new Vector2(w, Line + 10 * Scale));
            }
            foreach (var step in rows) DrawRow(id, step);
        }
        if (config.BookPopoutHideComplete && route.Next == null)
        {
            Label(ImGui.GetCursorScreenPos() + new Vector2(6 * Scale, 6 * Scale), "All objectives complete.", Green);
            ImGui.Dummy(new Vector2(0, Line + 12 * Scale));
        }
        ImGui.EndChild();
    }

    private void DrawRow(ulong id, BookStep step)
    {
        var p = ImGui.GetCursorScreenPos(); var w = ImGui.GetContentRegionAvail().X; var size = new Vector2(w, ControlHeight);
        var selected = step.Requirement.Id == route!.Next?.Requirement.Id;
        var complete = step.Status.Complete;
        if (selected) ImGui.GetWindowDrawList().AddRectFilled(p, p + size, Ink(new(.10f, .25f, .30f, 1)), 4 * Scale);
        // Selecting another unfinished objective is useful even while travel is temporarily blocked.
        ImGui.BeginDisabled(complete);
        if (ButtonAt("book-step-" + step.Requirement.Id, p, size))
        {
            selectedRequirementId = step.Requirement.Id;
            var check = travel.Check(id, step);
            if (check.Allowed) Travel(id, step);
        }
        ImGui.EndDisabled();
        var count = complete ? "Done" : $"{step.Status.Done}/{step.Status.Required}";
        var countWidth = ImGui.CalcTextSize(count).X;
        Label(p + new Vector2(8 * Scale, (ControlHeight - Line) / 2), Fit(step.Requirement.Label, w - countWidth - 28 * Scale), complete ? Muted : selected ? Cyan : White);
        Label(p + new Vector2(w - countWidth - 8 * Scale, (ControlHeight - Line) / 2), count, complete ? Green : Violet);
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            var action = "Completion recorded by the game.";
            if (!complete)
            {
                var travelCheck = travel.Check(id, step);
                action = travelCheck.Allowed ? TravelHint(step, travelCheck) : "Click to select this objective.\n" + travelCheck.Reason;
            }
            HoverTip(StepHint(step) + "\n\n" + action);
        }
        ImGui.SetCursorScreenPos(p); ImGui.Dummy(size);
    }

    private void Travel(ulong id, BookStep step)
    {
        selectedRequirementId = step.Requirement.Id;
        travel.Travel(id, step);
        showTravelStatusUntil = Environment.TickCount64 + 10000;
    }

    private void DrawEmpty(ulong id)
    {
        var p = ImGui.GetCursorScreenPos(); var w = ImGui.GetContentRegionAvail().X;
        Label(p + new Vector2(6 * Scale, 12 * Scale), id == 0 ? "Waiting for your character" : !config.Automatic ? "Book tracking paused" : "No held book detected", White);
        var hint = id == 0 ? "Log in to follow your picked-up book." : !config.Automatic ? "Enable automatic detection in Settings." : "Pick up a Trials of the Braves book to begin.";
        Label(p + new Vector2(6 * Scale, Line + 22 * Scale), Fit(hint, w - 12 * Scale), Muted);
        ImGui.Dummy(new Vector2(w, 2 * Line + 40 * Scale));
        HoverTip(hint);
    }

    private void DrawFooter()
    {
        var p = ImGui.GetCursorScreenPos(); var w = ImGui.GetContentRegionAvail().X;
        ImGui.GetWindowDrawList().AddLine(p + new Vector2(0, 3 * Scale), p + new Vector2(w, 3 * Scale), Ink(AtlasTheme.Rgb(0x41464a)), Scale);
        if (route != null)
        {
            var buttonWidth = ImGui.CalcTextSize("Open checklist").X + 20 * Scale;
            if (ButtonAt("book-open-checklist", p + new Vector2(0, 8 * Scale), new Vector2(buttonWidth, ControlHeight), true)) OpenChecklist?.Invoke(route.Job);
            Label(p + new Vector2(10 * Scale, 8 * Scale + (ControlHeight - Line) / 2), "Open checklist", Violet);
            var hint = !compact ? "Game progress" : Environment.TickCount64 < showTravelStatusUntil ? "Travel status" : !config.Automatic ? "Paused" : tracker.BookIsCurrent ? "Live" : "Recorded";
            if (w > buttonWidth + ImGui.CalcTextSize(hint).X + 16 * Scale)
                Label(p + new Vector2(w - ImGui.CalcTextSize(hint).X - 4 * Scale, 8 * Scale + (ControlHeight - Line) / 2), hint, Muted);
        }
        var note = Environment.TickCount64 < showTravelStatusUntil ? travel.Status :
            !config.Automatic ? "Paused · showing recorded progress" :
            tracker.CurrentId == 0 ? "Waiting for your character" :
            !tracker.BookIsCurrent ? route == null ? tracker.BookStatus : "Waiting for current book data" : "Live · steps advance automatically";
        if (!compact || route == null) Label(p + new Vector2(4 * Scale, route == null ? 11 * Scale : ControlHeight + 16 * Scale), Fit(note, w - 8 * Scale), Muted);
        ImGui.SetCursorScreenPos(p); ImGui.Dummy(new Vector2(w, route == null ? Line + 22 * Scale : FooterHeight));
        HoverTip(note + "\n\n" + tracker.BookStatus + "\n" + travel.Status + "\n\nTravel never marks a step complete. The tracker follows the game and automatically selects the next unfinished objective.");
    }

    private static string KindName(ArrBookObjectiveKind kind) => kind switch
    { ArrBookObjectiveKind.Monster => "Enemies", ArrBookObjectiveKind.Dungeon => "Dungeons", ArrBookObjectiveKind.Fate => "FATEs", ArrBookObjectiveKind.Leve => "Leves", _ => "Objectives" };
    private static string StepHint(BookStep step) => KindName(step.Kind) + " · " + step.Requirement.Label + "\n" +
        (step.Location is { } l ? $"{l.Zone} (X: {l.X:0.0}, Y: {l.Y:0.0})\n{l.Hint}" : "No mapped location is available for this objective.") +
        "\n\n" + step.Requirement.Detail;
    private static string TravelHint(BookStep step, BookTravelCheck check) =>
        (check.Destination is { } destination ? $"{destination.Name} · {destination.GilCost:N0} gil before tickets\n" : "") +
        $"Click to {check.ActionLabel.ToLowerInvariant()}.\n" + (step.Kind == ArrBookObjectiveKind.Dungeon ? "The flag marks the dungeon entrance. Complete the required duty with this book's Atma weapon." : "The flag marks this objective's location. Complete the objective with this book's Atma weapon.");
    private static uint Ink(Vector4 color) { color.W *= ImGui.GetStyle().Alpha; return ImGui.ColorConvertFloat4ToU32(color); }
    private void Label(Vector2 p, string text, Vector4 color) =>
        ImGui.GetWindowDrawList().AddText(new Vector2(MathF.Round(p.X), MathF.Round(p.Y)), Ink(color), text);
    private static string Fit(string text, float width)
    {
        if (ImGui.CalcTextSize(text).X <= width) return text;
        while (text.Length > 0 && ImGui.CalcTextSize(text + "…").X > width) text = text[..^1];
        return text + "…";
    }
    private static bool ButtonAt(string id, Vector2 p, Vector2 size, bool filled = false)
    {
        ImGui.SetCursorScreenPos(p);
        ImGui.PushStyleColor(ImGuiCol.Button, filled ? AtlasTheme.Rgb(0x293b58) : Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, AtlasTheme.Rgb(0x354b6b));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, AtlasTheme.Rgb(0x293b58));
        var clicked = ImGui.Button("###" + id, size);
        ImGui.PopStyleColor(3);
        return clicked;
    }
    private static void HoverTip(string text)
    {
        if (!ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled)) return;
        ImGui.BeginTooltip(); ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + 360 * Scale);
        ImGui.TextUnformatted(text); ImGui.PopTextWrapPos(); ImGui.EndTooltip();
    }
}
