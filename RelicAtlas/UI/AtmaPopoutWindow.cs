using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using RelicAtlas.Core;
using RelicAtlas.Tracking;

namespace RelicAtlas.UI;

public sealed class AtmaPopoutWindow : Window
{
    private readonly Configuration config;
    private readonly Catalog catalog;
    private readonly GameTracker tracker;
    private readonly IAtmaTravel travel;
    private readonly Action save;
    private AtlasTheme? theme;
    private AtmaTotals? totals;
    private ulong owner;
    private long nextRefresh;
    private long showTravelStatusUntil;
    private Vector2? move;
    public Action? OpenFarmingPage { get; set; }
    private static float Scale => ImGuiHelpers.GlobalScale;
    private static float LineHeight => ImGui.GetTextLineHeight();
    private static float RowHeight => Math.Max(26 * Scale, LineHeight + 8 * Scale);
    private static float FooterHeight => Math.Max(28 * Scale, LineHeight + 10 * Scale) + LineHeight + 18 * Scale;
    private static readonly Vector4 White = AtlasTheme.Rgb(0xedf0f2);
    private static readonly Vector4 Muted = AtlasTheme.Rgb(0xb0b8c0);
    private static readonly Vector4 Cyan = AtlasTheme.Rgb(0x68d4dc);
    private static readonly Vector4 Violet = AtlasTheme.Rgb(0x81aaff);
    private static readonly Vector4 Green = AtlasTheme.Rgb(0x87c9a1);

    public AtmaPopoutWindow(Configuration config, Catalog catalog, GameTracker tracker, IAtmaTravel travel, Action save)
        : base("Atma tracker###RelicAtlasAtmaPopout", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize |
            ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoScrollbar)
    {
        this.config = config; this.catalog = catalog; this.tracker = tracker; this.travel = travel; this.save = save;
        Size = new Vector2(420, 0); SizeCondition = ImGuiCond.Always;
        RespectCloseHotkey = false; DisableWindowSounds = true;
        AllowClickthrough = false; AllowPinning = false; ShowCloseButton = false;
        IsOpen = config.AtmaPopoutOpen;
    }

    public void Show() { nextRefresh = 0; IsOpen = true; SetOpen(true); BringToFront(); }
    private void SetOpen(bool open)
    {
        if (config.AtmaPopoutOpen == open) return;
        config.AtmaPopoutOpen = open; save();
    }
    public override void OnOpen() => SetOpen(true);
    public override void OnClose() => SetOpen(false);

    public override void PreDraw()
    {
        theme = new AtlasTheme();
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(10, 9) * Scale);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(4, 2) * Scale);
        var columns = ImGui.CalcTextSize("Water-bearer").X + ImGui.CalcTextSize("00:00 / 12:00").X +
            Math.Max(ImGui.CalcTextSize("LEFT").X, ImGui.CalcTextSize("999").X) + 94 * Scale;
        Size = new Vector2(Math.Max(420, columns / Scale), 0);
        if (config.AtmaPopoutLocked) Flags |= ImGuiWindowFlags.NoMove;
        else Flags &= ~ImGuiWindowFlags.NoMove;
    }
    public override void PostDraw() { ImGui.PopStyleVar(2); theme?.Dispose(); theme = null; }

    public override void Draw()
    {
        var id = tracker.CurrentId;
        if (id == 0 || !config.Characters.TryGetValue(id, out var character)) { owner = 0; totals = null; }
        else if (id != owner || totals == null || Environment.TickCount64 >= nextRefresh)
        {
            var stock = tracker.StorageFor(id);
            totals = new AtmaTotals(catalog, character, tracker.InventoryFor(id),
                stock?.SaddlebagRecorded == true ? stock.Saddlebag : null,
                stock?.RecordedRetainers > 0 ? stock.Retainers : null);
            owner = id; nextRefresh = Environment.TickCount64 + 500;
        }
        DrawHeader();
        if (totals == null)
        {
            var p = ImGui.GetCursorScreenPos();
            Label(p + new Vector2(8, 12) * Scale, "Waiting for your character", Muted);
            ImGui.Dummy(new Vector2(ImGui.GetContentRegionAvail().X, LineHeight + 30 * Scale));
        }
        else
        {
            var view = new AtmaFarmView(totals, travel.Now);
            var p = ImGui.GetCursorScreenPos(); var w = ImGui.GetContentRegionAvail().X;
            Label(p + new Vector2(4, 2) * Scale, "ALL ARR JOBS", Muted);
            var summary = $"{totals.Remaining:N0} left · {view.Rows.Count} types";
            Label(p + new Vector2(w - ImGui.CalcTextSize(summary).X - 4 * Scale, 2 * Scale), summary, White);
            ImGui.Dummy(new Vector2(w, LineHeight + 5 * Scale));
            HoverTip($"Across {totals.Jobs} unfinished ARR Atma sets for your logged-in character, including jobs not started.\n\n" +
                "Uses the same totals as the main Atma page: carried stock, known Allagan Tools storage and recorded checklist credit. Shared stock is counted once; stored Atma must be withdrawn for turn-ins.");
            DrawSuggestion(view);
            if (view.Rows.Count > 0)
            {
                var spacing = ImGui.GetStyle().ItemSpacing.Y;
                var fullHeight = LineHeight + 7 * Scale + spacing + view.Rows.Count * (RowHeight + spacing);
                var availableHeight = ImGui.GetIO().DisplaySize.Y - ImGui.GetCursorScreenPos().Y - FooterHeight -
                    ImGui.GetStyle().WindowPadding.Y - spacing * 2 - 10 * Scale;
                var height = Math.Min(fullHeight, Math.Max(LineHeight + RowHeight + 10 * Scale, availableHeight));
                ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
                ImGui.BeginChild("atma-rows", new Vector2(0, height), false, ImGuiWindowFlags.NoBackground);
                p = ImGui.GetCursorScreenPos(); w = ImGui.GetContentRegionAvail().X;
                var columns = Columns(w);
                Label(p + new Vector2(10, 2) * Scale, "ATMA", Muted);
                Label(p + new Vector2(columns.Times, 2 * Scale), "STARTS · ST", Muted);
                Label(p + new Vector2(columns.Count + (columns.CountWidth - ImGui.CalcTextSize("LEFT").X) / 2, 2 * Scale), "LEFT", Muted);
                ImGui.Dummy(new Vector2(w, LineHeight + 7 * Scale));
                foreach (var row in view.Rows) DrawRow(id, row);
                ImGui.EndChild();
                ImGui.PopStyleVar();
            }
        }
        DrawFooter();
        if (move is { } position) { ImGui.SetWindowPos(position); move = null; }
    }

    private void DrawHeader()
    {
        var p = ImGui.GetCursorScreenPos(); var w = ImGui.GetContentRegionAvail().X;
        var d = ImGui.GetWindowDrawList();
        var height = Math.Max(32 * Scale, LineHeight + 10 * Scale);
        var textY = (height - LineHeight) / 2;
        d.AddRectFilled(p + new Vector2(3 * Scale, textY), p + new Vector2(5 * Scale, textY + LineHeight), Ink(Violet), Scale);
        Label(p + new Vector2(14 * Scale, textY), "ATMA", White);
        Label(p + new Vector2(22 * Scale + ImGui.CalcTextSize("ATMA").X, textY), "FIELD TRACKER", Muted);
        if (ButtonAt("lock", p + new Vector2(w - height * 2 - 4 * Scale, 0), new Vector2(height)))
        { config.AtmaPopoutLocked = !config.AtmaPopoutLocked; save(); }
        var c = p + new Vector2(w - height * 1.5f - 4 * Scale, height / 2);
        var color = Ink(config.AtmaPopoutLocked ? Cyan : Muted);
        d.AddRect(c + new Vector2(-4, -1) * Scale, c + new Vector2(4, 6) * Scale, color, Scale, ImDrawFlags.None, Scale);
        var shift = config.AtmaPopoutLocked ? 0 : 3;
        d.AddLine(c + new Vector2(-3 + shift, -1) * Scale, c + new Vector2(-3 + shift, -6) * Scale, color, Scale);
        d.AddLine(c + new Vector2(-3 + shift, -6) * Scale, c + new Vector2(3 + shift, -6) * Scale, color, Scale);
        d.AddLine(c + new Vector2(3 + shift, -6) * Scale, c + new Vector2(3 + shift, -1) * Scale, color, Scale);
        HoverTip(config.AtmaPopoutLocked ? "Unlock the window position. Atma rows remain clickable while locked." : "Lock the window position.");
        if (ButtonAt("close", p + new Vector2(w - height, 0), new Vector2(height)))
        { IsOpen = false; SetOpen(false); }
        c = p + new Vector2(w - height / 2, height / 2);
        d.AddLine(c - new Vector2(4) * Scale, c + new Vector2(4) * Scale, Ink(Muted), 1.3f * Scale);
        d.AddLine(c + new Vector2(4, -4) * Scale, c + new Vector2(-4, 4) * Scale, Ink(Muted), 1.3f * Scale);
        HoverTip("Close the Atma tracker. Reopen with /relicatlas atma popout.");
        ImGui.SetCursorScreenPos(p);
        ImGui.InvisibleButton("##drag-atma", new Vector2(w - height * 2 - 12 * Scale, height));
        if (!config.AtmaPopoutLocked && ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left))
            move = ImGui.GetWindowPos() + ImGui.GetIO().MouseDelta;
        HoverTip(config.AtmaPopoutLocked ? "Position locked" : "Drag to move · Escape leaves this tracker open");
        d.AddLine(p + new Vector2(0, height + 4 * Scale), p + new Vector2(w, height + 4 * Scale), Ink(AtlasTheme.Rgb(0x41464a)), Scale);
        ImGui.SetCursorScreenPos(p); ImGui.Dummy(new Vector2(w, height + 8 * Scale));
    }

    private void DrawSuggestion(AtmaFarmView view)
    {
        var p = ImGui.GetCursorScreenPos(); var w = ImGui.GetContentRegionAvail().X;
        var d = ImGui.GetWindowDrawList(); var size = new Vector2(w, LineHeight * 2 + 22 * Scale);
        d.AddRectFilled(p, p + size, Ink(AtlasTheme.Rgb(0x222426)), 5 * Scale);
        var target = view.CurrentNeeded ? view.Current : view.NextNeeded;
        var tint = view.CurrentNeeded ? Cyan : target != null ? Violet : Green;
        var caption = view.CurrentNeeded ? "SUGGESTED NOW" : target != null ? $"NEXT NEEDED · {target.Start:HH:mm}" : "SET REQUIREMENTS MET";
        var clock = travel.Now.ToString("HH:mm") + (travel.HasServerTime ? " ST" : " UTC");
        Label(p + new Vector2(10, 8) * Scale, Fit(caption, w - ImGui.CalcTextSize(clock).X - 30 * Scale), tint);
        Label(p + new Vector2(w - ImGui.CalcTextSize(clock).X - 10 * Scale, 8 * Scale), clock, Muted);
        var secondY = LineHeight + 14 * Scale;
        Label(p + new Vector2(10 * Scale, secondY), target?.Area.Atma ?? "All Atma covered", target != null ? White : Green);
        if (target != null)
        {
            var seconds = Math.Max(0, (int)Math.Ceiling(((view.CurrentNeeded ? target.End : target.Start) - travel.Now).TotalSeconds));
            var duration = TimeSpan.FromSeconds(seconds);
            var countdown = duration.TotalHours >= 1 ? $"{(int)duration.TotalHours}:{duration.Minutes:00}:{duration.Seconds:00}" : $"{duration.Minutes:00}:{duration.Seconds:00}";
            Label(p + new Vector2(w - ImGui.CalcTextSize(countdown).X - 10 * Scale, secondY), countdown, tint);
        }
        ImGui.Dummy(size);
        HoverTip(target == null ? "No more Atma are required across your unfinished ARR relics. Completed types are hidden." :
            $"{target.Area.Zone}\n{target.Start:HH:mm}–{target.End:HH:mm} ST\n" +
            (view.CurrentNeeded ? "Countdown to the end of this suggested window." : "The current window is covered. Countdown to the next type you still need.") +
            "\n\nThis is the existing unverified player theory, assuming JST. Atma can drop outside these windows. The highlight uses the actual hour, regardless of the early-travel setting.");
    }

    private void DrawRow(ulong id, AtmaFarmRow row)
    {
        var p = ImGui.GetCursorScreenPos(); var w = ImGui.GetContentRegionAvail().X; var size = new Vector2(w, RowHeight);
        var d = ImGui.GetWindowDrawList();
        if (row.Suggested)
        {
            d.AddRectFilled(p, p + size, Ink(new(.07f, .17f, .22f, 1)), 4 * Scale);
            d.AddRectFilled(p + new Vector2(0, 4) * Scale, p + new Vector2(2 * Scale, size.Y - 4 * Scale), Ink(Cyan), Scale);
        }
        var check = travel.Check(id, row.Area);
        ImGui.BeginDisabled(!check.Allowed);
        var clicked = ButtonAt("atma-" + row.Area.Hour, p, size);
        ImGui.EndDisabled();
        if (clicked) { travel.Travel(id, row.Area); showTravelStatusUntil = Environment.TickCount64 + 10000; }
        var columns = Columns(w);
        var badge = p + new Vector2(columns.Count, 2 * Scale);
        d.AddRectFilled(badge, badge + new Vector2(columns.CountWidth, size.Y - 4 * Scale), Ink(row.Suggested ? new(.12f, .30f, .35f, 1) : AtlasTheme.Rgb(0x293b58)), 4 * Scale);
        var textY = (size.Y - LineHeight) / 2;
        Label(p + new Vector2(10 * Scale, textY), row.Area.Atma, row.Suggested ? Cyan : White);
        Label(p + new Vector2(columns.Times, textY), row.Area.Hours, Muted);
        var count = row.Need.Missing.ToString("N0");
        Label(p + new Vector2(columns.Count + (columns.CountWidth - ImGui.CalcTextSize(count).X) / 2, textY), count, row.Suggested ? Cyan : Violet);
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            string Count(int? value) => value?.ToString("N0") ?? "unknown";
            HoverTip($"{row.Area.Item} · {row.Area.Zone}\nSuggested starts: {row.Area.Hours} ST\n\n" +
                $"Required: {row.Need.Required} · Remaining: {row.Need.Missing}\nBags: {Count(row.Need.Bags)} · Saddlebag: {Count(row.Need.Saddlebag)} · Retainers: {Count(row.Need.Retainers)}\n" +
                $"Recorded checklist credit: {row.Need.Recorded}\n\n" +
                (check.Destination is { } target ? $"{target.Name} · {target.GilCost:N0} gil before tickets\n" : "") +
                (check.Allowed ? "Click to teleport. This stops any automatic Atma travel session." : check.Reason) +
                "\nEquip a Zenith weapon to farm Atma. Storage counts may be cached.");
        }
    }

    private void DrawFooter()
    {
        var p = ImGui.GetCursorScreenPos(); var w = ImGui.GetContentRegionAvail().X;
        var actionHeight = Math.Max(28 * Scale, LineHeight + 10 * Scale);
        var textY = 7 * Scale + (actionHeight - LineHeight) / 2;
        ImGui.GetWindowDrawList().AddLine(p + new Vector2(0, 3) * Scale, p + new Vector2(w, 3 * Scale), Ink(AtlasTheme.Rgb(0x41464a)), Scale);
        var openWidth = ImGui.CalcTextSize("Farming page").X + 16 * Scale;
        if (ButtonAt("open-farming", p + new Vector2(0, 7) * Scale, new Vector2(openWidth, actionHeight))) OpenFarmingPage?.Invoke();
        Label(p + new Vector2(8 * Scale, textY), "Farming page", Violet);
        if (travel.Active)
        {
            var stopWidth = ImGui.CalcTextSize("Stop auto travel").X + 16 * Scale;
            if (ButtonAt("stop-auto", p + new Vector2(w - stopWidth, 7 * Scale), new Vector2(stopWidth, actionHeight))) travel.Stop();
            Label(p + new Vector2(w - stopWidth + 8 * Scale, textY), "Stop auto travel", Cyan);
        }
        else
        {
            var hint = travel.Available ? "Click Atma to travel" : "Teleporter offline";
            Label(p + new Vector2(w - ImGui.CalcTextSize(hint).X - 4 * Scale, textY), hint, Muted);
        }
        var note = Environment.TickCount64 < showTravelStatusUntil ? travel.Status :
            !config.Automatic ? "Tracking paused · recorded counts" : totals != null && !totals.BagsKnown ? "Inventory unavailable · recorded counts" :
            !travel.HasServerTime ? "Computer UTC · unverified schedule" : "ST schedule · unverified player theory";
        Label(p + new Vector2(4 * Scale, actionHeight + 13 * Scale), Fit(note, w - 8 * Scale), Muted);
        ImGui.SetCursorScreenPos(p); ImGui.Dummy(new Vector2(w, FooterHeight));
        HoverTip(note + "\n\nCounts follow the logged-in character only. Known Allagan Tools storage is cached; unavailable stock is excluded. Manual corrections still apply.\n\n" + travel.Status);
    }

    private static uint Ink(Vector4 color) { color.W *= ImGui.GetStyle().Alpha; return ImGui.ColorConvertFloat4ToU32(color); }
    private static (float Times, float Count, float CountWidth) Columns(float width)
    {
        var countWidth = Math.Max(ImGui.CalcTextSize("LEFT").X, ImGui.CalcTextSize("999").X) + 12 * Scale;
        var count = width - countWidth - 5 * Scale;
        var timesWidth = Math.Max(ImGui.CalcTextSize("00:00 / 12:00").X, ImGui.CalcTextSize("STARTS · ST").X);
        return (count - 16 * Scale - timesWidth, count, countWidth);
    }
    private static void Label(Vector2 p, string text, Vector4 color) =>
        ImGui.GetWindowDrawList().AddText(p, Ink(color), text);
    private static string Fit(string text, float width)
    {
        if (ImGui.CalcTextSize(text).X <= width) return text;
        while (text.Length > 0 && ImGui.CalcTextSize(text + "…").X > width) text = text[..^1];
        return text + "…";
    }
    private static bool ButtonAt(string id, Vector2 p, Vector2 size)
    {
        ImGui.SetCursorScreenPos(p);
        ImGui.PushStyleColor(ImGuiCol.Button, Vector4.Zero);
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
