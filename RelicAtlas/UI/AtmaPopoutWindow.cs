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
    private static readonly Vector4 White = new(.94f, .95f, 1, 1);
    private static readonly Vector4 Muted = new(.63f, .65f, .76f, 1);
    private static readonly Vector4 Cyan = new(.36f, .86f, .94f, 1);
    private static readonly Vector4 Violet = new(.74f, .61f, 1, 1);
    private static readonly Vector4 Green = new(.48f, .82f, .60f, 1);

    public AtmaPopoutWindow(Configuration config, Catalog catalog, GameTracker tracker, IAtmaTravel travel, Action save)
        : base("Atma tracker###RelicAtlasAtmaPopout", ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize |
            ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoScrollbar)
    {
        this.config = config; this.catalog = catalog; this.tracker = tracker; this.travel = travel; this.save = save;
        Size = new Vector2(310, 0); SizeCondition = ImGuiCond.Always;
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
            Label(p + new Vector2(8, 12) * Scale, "Waiting for your character", Muted, .9f);
            ImGui.Dummy(new Vector2(290, 52) * Scale);
        }
        else
        {
            var view = new AtmaFarmView(totals, travel.Now);
            var p = ImGui.GetCursorScreenPos(); var w = ImGui.GetContentRegionAvail().X;
            Label(p + new Vector2(4, 2) * Scale, "ALL ARR JOBS", Muted, .7f);
            var summary = $"{totals.Remaining:N0} left · {view.Rows.Count} types";
            Label(p + new Vector2(w - ImGui.CalcTextSize(summary).X * .8f - 4 * Scale, 0), summary, White, .8f);
            ImGui.Dummy(new Vector2(w, 22 * Scale));
            HoverTip($"Across {totals.Jobs} unfinished ARR Atma sets for your logged-in character, including jobs not started.\n\n" +
                "Uses the same totals as the main Atma page: carried stock, known Allagan Tools storage and recorded checklist credit. Shared stock is counted once; stored Atma must be withdrawn for turn-ins.");
            DrawSuggestion(view);
            if (view.Rows.Count > 0)
            {
                p = ImGui.GetCursorScreenPos();
                Label(p + new Vector2(10, 2) * Scale, "ATMA", Muted, .65f);
                Label(p + new Vector2(w - 145 * Scale, 2 * Scale), "STARTS · ST", Muted, .65f);
                Label(p + new Vector2(w - 34 * Scale, 2 * Scale), "LEFT", Muted, .65f);
                ImGui.Dummy(new Vector2(w, 19 * Scale));
                foreach (var row in view.Rows) DrawRow(id, row);
            }
        }
        DrawFooter();
        if (move is { } position) { ImGui.SetWindowPos(position); move = null; }
    }

    private void DrawHeader()
    {
        var p = ImGui.GetCursorScreenPos(); var w = ImGui.GetContentRegionAvail().X;
        var d = ImGui.GetWindowDrawList();
        d.AddRectFilled(p + new Vector2(3, 5) * Scale, p + new Vector2(5, 23) * Scale, Ink(Violet), Scale);
        Label(p + new Vector2(14, 2) * Scale, "ATMA", White, 1.05f);
        Label(p + new Vector2(72, 7) * Scale, "FIELD TRACKER", Muted, .65f);
        if (ButtonAt("lock", p + new Vector2(w - 56 * Scale, 0), new Vector2(26, 28) * Scale))
        { config.AtmaPopoutLocked = !config.AtmaPopoutLocked; save(); }
        var c = p + new Vector2(w - 43 * Scale, 15 * Scale);
        var color = Ink(config.AtmaPopoutLocked ? Cyan : Muted);
        d.AddRect(c + new Vector2(-4, -1) * Scale, c + new Vector2(4, 6) * Scale, color, Scale, ImDrawFlags.None, Scale);
        var shift = config.AtmaPopoutLocked ? 0 : 3;
        d.AddLine(c + new Vector2(-3 + shift, -1) * Scale, c + new Vector2(-3 + shift, -6) * Scale, color, Scale);
        d.AddLine(c + new Vector2(-3 + shift, -6) * Scale, c + new Vector2(3 + shift, -6) * Scale, color, Scale);
        d.AddLine(c + new Vector2(3 + shift, -6) * Scale, c + new Vector2(3 + shift, -1) * Scale, color, Scale);
        HoverTip(config.AtmaPopoutLocked ? "Unlock the window position. Atma rows remain clickable while locked." : "Lock the window position.");
        if (ButtonAt("close", p + new Vector2(w - 26 * Scale, 0), new Vector2(26, 28) * Scale))
        { IsOpen = false; SetOpen(false); }
        c = p + new Vector2(w - 13 * Scale, 14 * Scale);
        d.AddLine(c - new Vector2(4) * Scale, c + new Vector2(4) * Scale, Ink(Muted), 1.3f * Scale);
        d.AddLine(c + new Vector2(4, -4) * Scale, c + new Vector2(-4, 4) * Scale, Ink(Muted), 1.3f * Scale);
        HoverTip("Close the Atma tracker. Reopen with /relicatlas atma popout.");
        ImGui.SetCursorScreenPos(p);
        ImGui.InvisibleButton("##drag-atma", new Vector2(w - 64 * Scale, 28 * Scale));
        if (!config.AtmaPopoutLocked && ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left))
            move = ImGui.GetWindowPos() + ImGui.GetIO().MouseDelta;
        HoverTip(config.AtmaPopoutLocked ? "Position locked" : "Drag to move · Escape leaves this tracker open");
        d.AddLine(p + new Vector2(0, 32) * Scale, p + new Vector2(w, 32 * Scale), Ink(new(.23f, .20f, .34f, 1)), Scale);
        ImGui.SetCursorScreenPos(p); ImGui.Dummy(new Vector2(w, 34 * Scale));
    }

    private void DrawSuggestion(AtmaFarmView view)
    {
        var p = ImGui.GetCursorScreenPos(); var w = ImGui.GetContentRegionAvail().X;
        var d = ImGui.GetWindowDrawList(); var size = new Vector2(w, 50 * Scale);
        d.AddRectFilled(p, p + size, Ink(new(.075f, .09f, .145f, 1)), 5 * Scale);
        var target = view.CurrentNeeded ? view.Current : view.NextNeeded;
        var tint = view.CurrentNeeded ? Cyan : target != null ? Violet : Green;
        var caption = view.CurrentNeeded ? "SUGGESTED NOW" : target != null ? $"NEXT NEEDED · {target.Start:HH:mm}" : "SET REQUIREMENTS MET";
        Label(p + new Vector2(10, 8) * Scale, caption, tint, .65f);
        var clock = travel.Now.ToString("HH:mm") + (travel.HasServerTime ? " ST" : " UTC");
        Label(p + new Vector2(w - ImGui.CalcTextSize(clock).X * .65f - 10 * Scale, 8 * Scale), clock, Muted, .65f);
        Label(p + new Vector2(10, 29) * Scale, target?.Area.Atma ?? "All Atma covered", target != null ? White : Green, 1.05f);
        if (target != null)
        {
            var seconds = Math.Max(0, (int)Math.Ceiling(((view.CurrentNeeded ? target.End : target.Start) - travel.Now).TotalSeconds));
            var duration = TimeSpan.FromSeconds(seconds);
            var countdown = duration.TotalHours >= 1 ? $"{(int)duration.TotalHours}:{duration.Minutes:00}:{duration.Seconds:00}" : $"{duration.Minutes:00}:{duration.Seconds:00}";
            Label(p + new Vector2(w - ImGui.CalcTextSize(countdown).X - 10 * Scale, 28 * Scale), countdown, tint);
        }
        ImGui.Dummy(size);
        HoverTip(target == null ? "No more Atma are required across your unfinished ARR relics. Completed types are hidden." :
            $"{target.Area.Zone}\n{target.Start:HH:mm}–{target.End:HH:mm} ST\n" +
            (view.CurrentNeeded ? "Countdown to the end of this suggested window." : "The current window is covered. Countdown to the next type you still need.") +
            "\n\nThis is the existing unverified player theory, assuming JST. Atma can drop outside these windows. The highlight uses the actual hour, regardless of the early-travel setting.");
    }

    private void DrawRow(ulong id, AtmaFarmRow row)
    {
        var p = ImGui.GetCursorScreenPos(); var w = ImGui.GetContentRegionAvail().X; var size = new Vector2(w, 24 * Scale);
        var d = ImGui.GetWindowDrawList();
        if (row.Suggested)
        {
            d.AddRectFilled(p, p + size, Ink(new(.07f, .17f, .22f, 1)), 4 * Scale);
            d.AddRectFilled(p + new Vector2(0, 4) * Scale, p + new Vector2(2, 20) * Scale, Ink(Cyan), Scale);
        }
        var check = travel.Check(id, row.Area);
        ImGui.BeginDisabled(!check.Allowed);
        var clicked = ButtonAt("atma-" + row.Area.Hour, p, size);
        ImGui.EndDisabled();
        if (clicked) { travel.Travel(id, row.Area); showTravelStatusUntil = Environment.TickCount64 + 10000; }
        var badge = p + new Vector2(w - 42 * Scale, 1 * Scale);
        d.AddRectFilled(badge, badge + new Vector2(37, 21) * Scale, Ink(row.Suggested ? new(.12f, .30f, .35f, 1) : new(.17f, .13f, .26f, 1)), 4 * Scale);
        Label(p + new Vector2(10, 3) * Scale, row.Area.Atma, row.Suggested ? Cyan : White, .85f);
        Label(p + new Vector2(w - 145 * Scale, 5 * Scale), row.Area.Hours, Muted, .7f);
        var count = row.Need.Missing.ToString("N0");
        Label(badge + new Vector2((37 * Scale - ImGui.CalcTextSize(count).X * .9f) / 2, 1 * Scale), count, row.Suggested ? Cyan : Violet, .9f);
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
        ImGui.GetWindowDrawList().AddLine(p + new Vector2(0, 3) * Scale, p + new Vector2(w, 3 * Scale), Ink(new(.23f, .20f, .34f, 1)), Scale);
        if (ButtonAt("open-farming", p + new Vector2(0, 7) * Scale, new Vector2(105, 23) * Scale)) OpenFarmingPage?.Invoke();
        Label(p + new Vector2(4, 10) * Scale, "Farming page", Violet, .75f);
        if (travel.Active)
        {
            if (ButtonAt("stop-auto", p + new Vector2(w - 115 * Scale, 7 * Scale), new Vector2(115, 23) * Scale)) travel.Stop();
            Label(p + new Vector2(w - 107 * Scale, 10 * Scale), "Stop auto travel", Cyan, .75f);
        }
        else Label(p + new Vector2(w - 112 * Scale, 10 * Scale), travel.Available ? "Click Atma to travel" : "Teleporter offline", Muted, .65f);
        var note = Environment.TickCount64 < showTravelStatusUntil ? travel.Status :
            !config.Automatic ? "Tracking paused · recorded counts" : totals != null && !totals.BagsKnown ? "Inventory unavailable · recorded counts" :
            !travel.HasServerTime ? "Computer UTC · unverified schedule" : "ST schedule · unverified player theory";
        Label(p + new Vector2(4, 32) * Scale, Fit(note, w - 8 * Scale, .65f), Muted, .65f);
        ImGui.SetCursorScreenPos(p); ImGui.Dummy(new Vector2(w, 46 * Scale));
        HoverTip(note + "\n\nCounts follow the logged-in character only. Known Allagan Tools storage is cached; unavailable stock is excluded. Manual corrections still apply.\n\n" + travel.Status);
    }

    private static uint Ink(Vector4 color) { color.W *= ImGui.GetStyle().Alpha; return ImGui.ColorConvertFloat4ToU32(color); }
    private void Label(Vector2 p, string text, Vector4 color, float factor = 1) =>
        ImGui.GetWindowDrawList().AddText(ImGui.GetFont(), ImGui.GetFontSize() * factor, p, Ink(color), text);
    private static string Fit(string text, float width, float factor)
    {
        if (ImGui.CalcTextSize(text).X * factor <= width) return text;
        while (text.Length > 0 && ImGui.CalcTextSize(text + "…").X * factor > width) text = text[..^1];
        return text + "…";
    }
    private static bool ButtonAt(string id, Vector2 p, Vector2 size)
    {
        ImGui.SetCursorScreenPos(p);
        ImGui.PushStyleColor(ImGuiCol.Button, Vector4.Zero);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(.22f, .18f, .32f, 1));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(.27f, .23f, .39f, 1));
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
