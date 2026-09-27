using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Interface.Windowing;
using RelicAtlas.Core;
using RelicAtlas.Tracking;

namespace RelicAtlas.UI;

public sealed partial class MainWindow : Window
{
    private readonly Configuration config;
    private readonly Catalog catalog;
    private readonly GameTracker tracker;
    private readonly Action save;
    private readonly IFontHandle? headingFont;
    private readonly IAtlasArtwork? artwork;
    private readonly IAtmaTravel? atmaTravel;
    private readonly CharacterProgress preview = new();
    private ulong selectedCharacter;
    private string expansion = "all";
    private string relicKind = "weapon";
    private System.Collections.Generic.IEnumerable<Series> VisibleSeries => catalog.Series.Where(s => s.Kind == relicKind);
    private string jobFilter = "All jobs";
    private string search = "";
    private string selectedSeries = "arr";
    private string selectedJob = "PLD";
    private int viewedStage = -1;
    private string selectedKey = "";
    private bool hideReady = true;
    private bool inProgressOnly;
    private bool resetDetailScroll;
    private bool expandGroups;
    private bool? groupExpansion;
    private string checklistKey = "";
    private bool readOnly;
    private static float Scale => ImGuiHelpers.GlobalScale;
    private static readonly Vector4 Accent = new(0.68f, 0.53f, 0.96f, 1);
    private static readonly Vector4 Lavender = new(0.77f, 0.65f, 1.0f, 1);
    private static readonly Vector4 Green = new(0.48f, 0.82f, 0.60f, 1);
    private static readonly Vector4 Muted = new(0.65f, 0.66f, 0.76f, 1);
    private static readonly Vector4 Cyan = new(0.36f, 0.86f, 0.94f, 1);
    private AtlasTheme? theme;
    private enum AtlasPage { Overview, Collection, Shopping, Atma }
    private AtlasPage page = AtlasPage.Overview;
    private Vector2? headerMove;
    private bool guideView;
    private string objectiveSummary = "";
    private readonly Dictionary<string, bool> openGroups = new();
    private sealed record WeaponRow(Series Series, string Job, WeaponProgress Weapon, int Completed, Stage? Next, Requirement? Objective);

    public MainWindow(Configuration config, Catalog catalog, GameTracker tracker, Action save, IFontHandle? headingFont = null, IAtlasArtwork? artwork = null, IAtmaTravel? atmaTravel = null)
        : base("Relic Atlas###RelicAtlas", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoTitleBar)
    {
        this.config = config; this.catalog = catalog; this.tracker = tracker; this.save = save; this.headingFont = headingFont; this.artwork = artwork;
        this.atmaTravel = atmaTravel;
        Size = new Vector2(1160, 760); SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(900, 640), MaximumSize = new Vector2(float.MaxValue) };
    }

    private static void Wrap(string text)
    { ImGui.PushTextWrapPos(0); ImGui.TextUnformatted(text); ImGui.PopTextWrapPos(); }

    private static void Tip(string text)
    {
        if (!ImGui.IsItemHovered()) return;
        ImGui.BeginTooltip(); ImGui.PushTextWrapPos(420 * Scale);
        ImGui.TextUnformatted(text); ImGui.PopTextWrapPos(); ImGui.EndTooltip();
    }

    private static string ShortName(string id) => id switch
    { "arr" => "ARR", "hw" => "Heavensward", "sb" => "Stormblood", "shb" => "Shadowbringers", "ew" => "Endwalker", "dt" => "Dawntrail", _ => id };

    private static Vector4 JobColor(string job) => job switch
    {
        "PLD" or "WAR" or "DRK" or "GNB" => new(0.53f, 0.70f, 1f, 1),
        "WHM" or "SCH" or "AST" or "SGE" => Green,
        "CRP" or "BSM" or "ARM" or "GSM" or "LTW" or "WVR" or "ALC" or "CUL" => Lavender,
        "MIN" or "BTN" or "FSH" => Cyan,
        _ => new(0.94f, 0.62f, 0.57f, 1),
    };

    public override void PreDraw() => theme = new AtlasTheme();
    public override void PostDraw() { theme?.Dispose(); theme = null; }

    public override void Draw()
    {
        var id = selectedCharacter == 0 ? tracker.CurrentId : selectedCharacter;
        var c = config.Characters.GetValueOrDefault(id) ?? preview;
        readOnly = ReferenceEquals(c, preview);
        if (openWeaponView) { page = AtlasPage.Collection; openWeaponView = false; }
        DrawMasthead(c, id);
        if (page != AtlasPage.Atma) DrawRelicKindPicker();
        if (atmaTravel?.Active == true && id != tracker.CurrentId) atmaTravel.Stop();
        if (page == AtlasPage.Overview) DrawCollectionOverview(c, id);
        else if (page == AtlasPage.Shopping) DrawRemainingRequirements(c, id);
        else if (page == AtlasPage.Atma) DrawAtmaFarming(c, id);
        else DrawWeaponWorkspace(c, id);
        // Move after drawing so header and body use the same origin throughout this frame.
        if (headerMove is { } position) { ImGui.SetWindowPos(position); headerMove = null; }
    }

    private static void CompactText(string text)
    {
        var width = ImGui.GetContentRegionAvail().X;
        var label = text;
        if (ImGui.CalcTextSize(label).X > width)
        {
            var length = label.Length;
            while (length > 0 && ImGui.CalcTextSize(label[..length] + "...").X > width) length--;
            label = label[..length] + "...";
        }
        ImGui.TextUnformatted(label);
        Tip(text);
    }

    private void DrawWeaponWorkspace(CharacterProgress c, ulong id)
    {
        var available = ImGui.GetContentRegionAvail();
        // The action pane gets most of the space, including at the minimum width.
        var leftWidth = Math.Clamp(available.X * 0.27f, 262 * Scale, 310 * Scale);
        ImGui.BeginChild("weapon-panel", new Vector2(leftWidth, available.Y), true, ImGuiWindowFlags.NoScrollbar);
        DrawCollectionList(c, id);
        ImGui.EndChild();
        ImGui.SameLine();
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(22, 16) * Scale);
        ImGui.BeginChild("action-panel", new Vector2(0, available.Y), true, ImGuiWindowFlags.NoScrollbar);
        DrawDetails(c, id);
        ImGui.EndChild();
        ImGui.PopStyleVar();
    }


    private void DrawToolbar(CharacterProgress c, ulong id, float width)
    {
        ImGui.SetNextItemWidth(width * Scale);
        var characterLabel = readOnly ? "Browse catalogue" : c.Name;
        if (ImGui.BeginCombo("##character", characterLabel))
        {
            if (ImGui.Selectable("Follow current character", selectedCharacter == 0))
            { selectedCharacter = 0; viewedStage = -1; resetDetailScroll = true; }
            foreach (var (characterId, profile) in config.Characters.OrderBy(p => p.Value.Name))
                if (ImGui.Selectable(profile.Name + "##" + characterId, characterId == selectedCharacter))
                { selectedCharacter = characterId; viewedStage = -1; resetDetailScroll = true; }
            ImGui.EndCombo();
        }
        var current = id != 0 && id == tracker.CurrentId;
        var atmaActive = atmaTravel?.Active == true;
        var status = atmaActive ? "Atma auto · " + atmaTravel!.Job : readOnly ? "Preview" : !current ? "Saved profile" : config.Automatic ? "Auto tracking" : "Paused";
        var p = ImGui.GetCursorScreenPos();
        ImGui.GetWindowDrawList().AddCircleFilled(p + new Vector2(4, 11) * Scale, 3 * Scale, Ink(current && config.Automatic ? Cyan : Muted));
        Label(p + new Vector2(15, 3) * Scale, status, Muted, .9f);
        if (atmaActive)
        {
            if (CanvasButton("atma-active-status", new Vector2(width - 87, 24) * Scale, out _, out _)) OpenAtma(atmaTravel!.Job, false);
        }
        else ImGui.Dummy(new Vector2(width - 87, 24) * Scale);
        Tip(atmaActive ? "Atma automatic travel is active. Click to review or stop it." : current ? tracker.Status : readOnly ? "Log in to save progress. You can browse every checklist now." : "Showing saved progress. Live inventory belongs to your logged-in character.");
        ImGui.SameLine();
        if (ActionButton("Settings", false, 77, 24, true)) ImGui.OpenPopup("settings");
        if (ImGui.BeginPopup("settings"))
        {
            ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + 410 * Scale);
            var locked = IsPinned;
            if (ImGui.Checkbox("Lock window position", ref locked)) IsPinned = locked;
            ImGui.Separator();
            ImGui.TextColored(Accent, "TRACKING");
            var automatic = config.Automatic;
            if (ImGui.Checkbox("Automatic detection", ref automatic)) { config.Automatic = automatic; tracker.Rescan(); save(); }
            ImGui.BeginDisabled(!config.Automatic || tracker.CurrentId == 0);
            if (ImGui.Button("Scan now")) tracker.Rescan();
            ImGui.EndDisabled();
            ImGui.TextUnformatted(tracker.Status);
            if (atmaTravel?.Active == true)
            {
                ImGui.Separator();
                ImGui.TextColored(Cyan, "Atma automatic travel: " + atmaTravel.Job);
                if (ImGui.Button("Stop Atma automatic travel")) atmaTravel.Stop();
            }
            ImGui.Separator();
            ImGui.TextUnformatted("Reads carried weapons and tools, materials, shared quests and supported achievements. Open the in-game Achievements window to load history. Open an ARR book to read its objectives.");
            ImGui.TextUnformatted("The Shopping list tab optionally reads saddlebag and retainer storage through Allagan Tools. Its cache updates when you visit storage. Light and other unobservable objectives can be entered manually. Bag counts are shared stock, not reserved per relic.");
            ImGui.TextColored(Muted, tracker.AchievementsLoaded ? "Achievement history loaded" : "Achievement history not loaded");
            if (tracker.Unresolved.Count > 0 && ImGui.TreeNode($"Detection notices ({tracker.Unresolved.Count})"))
            { foreach (var name in tracker.Unresolved) ImGui.TextUnformatted(name); ImGui.TreePop(); }
            ImGui.TextColored(Muted, "Catalogue reviewed " + catalog.Reviewed);
            ImGui.PopTextWrapPos(); ImGui.EndPopup();
        }
    }

    private void Select(string series, string job)
    {
        selectedSeries = series; selectedJob = job; viewedStage = -1; resetDetailScroll = true;
        relicKind = catalog.Series.Single(s => s.Id == series).Kind;
    }

    public void OpenRelic(string seriesId, string job)
    {
        if (!catalog.Series.Any(s => s.Id == seriesId && s.Jobs.Contains(job))) return;
        selectedCharacter = 0; page = AtlasPage.Collection; guideView = false;
        expansion = seriesId; jobFilter = "All jobs"; search = ""; inProgressOnly = false;
        Select(seriesId, job);
        IsOpen = true;
        BringToFront();
    }

    private void DrawCollectionList(CharacterProgress c, ulong id)
    {
        SectionLabel("Your collection", "");
        DrawExpansionPicker();
        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##search", "Search relics or objectives", ref search, 160);
        var filterCount = (config.PinnedOnly ? 1 : 0) + (config.HideComplete ? 1 : 0) + (inProgressOnly ? 1 : 0) + (jobFilter != "All jobs" ? 1 : 0);
        if (ActionButton(filterCount == 0 ? "Filters" : $"Filters ({filterCount})", false, 100, 28)) ImGui.OpenPopup("collection-filters");
        if (ImGui.BeginPopup("collection-filters"))
        {
            ImGui.SetNextItemWidth(210 * Scale);
            if (ImGui.BeginCombo("Job", jobFilter))
            {
                foreach (var job in new[] { "All jobs" }.Concat(VisibleSeries.SelectMany(s => s.Jobs).Distinct().Order()))
                    if (ImGui.Selectable(job, job == jobFilter)) jobFilter = job;
                ImGui.EndCombo();
            }
            ImGui.BeginDisabled(tracker.CurrentJob.Length == 0);
            if (ImGui.Button("Use my current job"))
            {
                var current = catalog.Series.FirstOrDefault(s => s.Jobs.Contains(tracker.CurrentJob));
                if (current != null) ChangeRelicKind(current.Kind);
                jobFilter = tracker.CurrentJob;
            }
            ImGui.EndDisabled();
            ImGui.Separator();
            var pinned = config.PinnedOnly;
            if (ImGui.Checkbox("Pinned only", ref pinned)) { config.PinnedOnly = pinned; save(); }
            ImGui.Checkbox("Started only", ref inProgressOnly);
            var hide = config.HideComplete;
            if (ImGui.Checkbox("Hide completed relics", ref hide)) { config.HideComplete = hide; save(); }
            ImGui.EndPopup();
        }
        var inventory = tracker.InventoryFor(id);
        var rows = new List<WeaponRow>();
        foreach (var series in VisibleSeries.Where(s => expansion == "all" || expansion == s.Id))
        foreach (var job in series.Jobs)
        {
            if (jobFilter != "All jobs" && job != jobFilter) continue;
            var weapon = c.Weapon(series, job);
            var index = weapon.CompletedIndex(series.Stages.Count);
            var (next, objective) = Progress.Next(c, series, job, inventory);
            if (config.HideComplete && next == null || config.PinnedOnly && !weapon.Pinned || inProgressOnly && (index < 0 || next == null)) continue;
            if (search.Length > 0)
            {
                var words = $"{job} {series.Name} {series.Expansion} " + string.Join(' ', series.Stages.SelectMany(s => s.Weapons[job])) + " " +
                    string.Join(' ', series.Stages.Select(s => s.Name)) + " " + string.Join(' ', series.Stages.SelectMany(s => s.Requirements).Where(r => r.Applies(job)).Select(r => r.Label));
                if (!words.Contains(search, StringComparison.OrdinalIgnoreCase)) continue;
            }
            rows.Add(new(series, job, weapon, index, next, objective));
        }
        rows = rows.OrderByDescending(r => r.Weapon.Pinned).ThenByDescending(r => r.Completed >= 0 && r.Next != null).ToList();
        ImGui.SameLine();
        var countLabel = $"{rows.Count} relics";
        var countPos = ImGui.GetCursorScreenPos();
        Label(countPos + new Vector2(ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize(countLabel).X * .9f, 5 * Scale), countLabel, Muted, .9f);
        ImGui.Dummy(new Vector2(ImGui.GetContentRegionAvail().X, 28 * Scale));
        ImGui.Spacing();
        ImGui.BeginChild("weapon-list", new Vector2(0, 0), false);
        foreach (var row in rows) DrawWeaponRow(row);
        if (rows.Count == 0)
        {
            Wrap("No relics match these filters.");
            if (ImGui.Button("Clear filters"))
            { search = ""; jobFilter = "All jobs"; inProgressOnly = false; config.PinnedOnly = false; config.HideComplete = false; save(); }
        }
        ImGui.EndChild();
    }

    private void DrawWeaponRow(WeaponRow row) => DrawRelicCard(row);

    private void DrawDetails(CharacterProgress c, ulong id)
    {
        var series = catalog.Series.First(s => s.Id == selectedSeries);
        var job = selectedJob;
        var weapon = c.Weapon(series, job);
        var complete = weapon.CompletedIndex(series.Stages.Count);
        var key = $"{id}/{series.Id}/{job}";
        if (key != selectedKey) { selectedKey = key; viewedStage = -1; resetDetailScroll = true; }
        ImGui.PushID(key);
        var stageIndex = viewedStage < 0 ? Math.Min(complete + 1, series.Stages.Count - 1) : Math.Clamp(viewedStage, 0, series.Stages.Count - 1);
        var stage = series.Stages[stageIndex];
        var inventory = tracker.InventoryFor(id);
        var heading = ImGui.GetCursorScreenPos();
        var headingWidth = ImGui.GetContentRegionAvail().X;
        DrawRelicHeading(series, job, complete, stageIndex);
        var afterHeading = ImGui.GetCursorScreenPos();
        ImGui.SetCursorScreenPos(heading + new Vector2(headingWidth - 110 * Scale, 13 * Scale));
        ImGui.BeginDisabled(readOnly);
        if (ActionButton("Manage", false, 92, 28)) ImGui.OpenPopup("progress");
        if (ImGui.BeginPopup("progress"))
        {
            var pinned = weapon.Pinned;
            if (ImGui.Checkbox("Pin this relic", ref pinned)) { weapon.Pinned = pinned; save(); }
            ImGui.Separator();
            ImGui.BeginDisabled(stageIndex <= complete);
            if (ImGui.Button("Record this relic received")) { SetStage(c, series, weapon, stageIndex); ImGui.CloseCurrentPopup(); }
            ImGui.EndDisabled();
            Tip("Use after the actual relic turn-in. Choose an earlier stage below to undo.");
            ImGui.Separator();
            ImGui.TextColored(Accent, "Last relic stage received");
            if (ImGui.Selectable("Not started", complete == -1)) SetStage(c, series, weapon, -1);
            for (int i = 0; i < series.Stages.Count; i++)
                if (ImGui.Selectable(series.Stages[i].Name, complete == i)) SetStage(c, series, weapon, i);
            ImGui.Separator();
            if (ImGui.Selectable("Use automatic detection", !weapon.ManualStage.HasValue))
            { weapon.ManualStage = null; viewedStage = -1; resetDetailScroll = true; save(); }
            ImGui.EndPopup();
        }
        ImGui.EndDisabled();
        Tip(weapon.ManualStage.HasValue ? "Manual stage correction active" : "Automatic stage detection active");
        ImGui.SetCursorScreenPos(afterHeading);
        DrawStagePath(series, complete, stageIndex);
        var requirements = stage.Requirements.Where(r => r.Applies(job)).ToArray();
        var done = requirements.Count(r => Progress.Status(c, series, job, stage, r, inventory).Complete);
        ImGui.BeginChild("detail-body", new Vector2(0, 0), false);
        var freshKey = key + "/" + stage.Id;
        var fresh = checklistKey != freshKey;
        if (resetDetailScroll || fresh) { ImGui.SetScrollY(0); resetDetailScroll = false; checklistKey = freshKey; }
        var upNext = Progress.Next(c, series, job, inventory);
        ImGui.PushID(stage.Id);
        if (series.Id == "arr" && stage.Id == "atma")
        {
            if (ActionButton("Atma farming route", false, 190, 30)) OpenAtma(job, false);
            ImGui.Spacing();
        }
        if (stageIndex == complete + 1 && upNext.Requirement != null)
            DrawNextAction(c, series, job, stage, upNext.Requirement, id);
        else if (stageIndex <= complete) { ImGui.TextColored(Green, "Relic acquired"); Wrap("Historic objectives may not have been recorded."); ImGui.Spacing(); }
        else if (done == requirements.Length) { ImGui.TextColored(Green, "Ready for turn-in"); Wrap(stage.Npc); }
        if (stageIndex == complete + 1 && upNext.Requirement == null)
        {
            ImGui.BeginDisabled(readOnly);
            if (ActionButton("Record relic received", true, 215, 32)) SetStage(c, series, weapon, stageIndex);
            ImGui.EndDisabled();
        }
        objectiveSummary = $"{done}/{requirements.Length} ready";
        if (viewedStage >= 0) { if (ImGui.SmallButton("Return to current stage")) { viewedStage = -1; resetDetailScroll = true; } }
        ImGui.PopID();
        ImGui.Spacing();
        // The mission body scrolls below the persistent weapon and stage controls.
        DrawMissionSwitch();
        {
            if (!guideView)
            {
                ImGui.PushID(stage.Id);
                var next = Progress.Next(c, series, job, inventory);
                DrawChecklist(c, series, job, stage, requirements, id, fresh, next.Requirement?.Group);
                ImGui.PopID();
                groupExpansion = null;

            }
            else
            {
                ImGui.TextColored(Accent, "WEAPON"); Wrap(string.Join(" + ", stage.Weapons[job]));
                ImGui.Spacing(); ImGui.TextColored(Accent, "WHERE TO GO"); Wrap(stage.Npc);
                if (stage.Quest.Length > 0) Wrap("Quest: " + stage.Quest);
                ImGui.Spacing(); ImGui.TextColored(Accent, "GETTING STARTED"); Wrap(series.Unlock);
                if (stage.Notes.Length > 0) { ImGui.Spacing(); Wrap(stage.Notes); }
                if (ImGui.Button("Copy guide link")) ImGui.SetClipboardText(stage.Source);
                ImGui.Spacing(); ImGui.Separator(); ImGui.TextColored(Accent, "YOUR NOTES");
                ImGui.BeginDisabled(readOnly);
                var notes = weapon.Notes;
                if (ImGui.InputTextMultiline("##notes", ref notes, 3000, new Vector2(-1, 130 * Scale))) { weapon.Notes = notes; save(); }
                ImGui.EndDisabled();

            }

        }
        ImGui.EndChild();
        ImGui.PopID();
    }

    private void DrawChecklist(CharacterProgress c, Series series, string job, Stage stage, Requirement[] requirements, ulong id, bool fresh, string? nextGroup)
    {
        var inventory = tracker.InventoryFor(id);
        var visibleCount = 0;
        foreach (var group in requirements.GroupBy(r => r.Group))
        {
            var all = group.ToArray();
            var finished = all.Count(r => Progress.Status(c, series, job, stage, r, inventory).Complete);
            var visible = hideReady ? all.Where(r => !Progress.Status(c, series, job, stage, r, inventory).Complete).ToArray() : all;
            if (visible.Length == 0) continue;
            visibleCount += visible.Length;
            ImGui.PushID(group.Key);
            if (group.Key.Length > 0)
            {
                var groupId = $"{selectedKey}/{stage.Id}/{group.Key}";
                if (groupExpansion.HasValue) openGroups[groupId] = groupExpansion.Value;
                else if (fresh || !openGroups.ContainsKey(groupId)) openGroups[groupId] = group.Key == nextGroup;
                var isOpen = openGroups.GetValueOrDefault(groupId);
                var isBook = series.Id == "arr" && stage.Id == "animus";
                var groupPos = ImGui.GetCursorScreenPos();
                var groupWidth = ImGui.GetContentRegionAvail().X;
                if (MissionGroup(group.Key, finished, all.Length, isOpen, isBook ? 108 : 0))
                    openGroups[groupId] = isOpen = !isOpen;
                if (isBook)
                {
                    ImGui.SetCursorScreenPos(groupPos + new Vector2(groupWidth - 101 * Scale, 3 * Scale));
                    if (c.ActiveBooks.GetValueOrDefault(job) == group.Key)
                    {
                        Label(ImGui.GetCursorScreenPos() + new Vector2(7, 6) * Scale, "Active book", Cyan, .9f);
                        ImGui.Dummy(new Vector2(97, 28) * Scale);
                    }
                    else
                    {
                        ImGui.BeginDisabled(readOnly);
                        if (ActionButton("Set active", false, 97, 28, true))
                        { c.ActiveBooks[job] = group.Key; openGroups[groupId] = isOpen = true; save(); }
                        Tip("Make this book your next objective group.");
                        ImGui.EndDisabled();
                    }
                    ImGui.SetCursorScreenPos(groupPos);
                    ImGui.Dummy(new Vector2(groupWidth, 38 * Scale));
                }
                if (!isOpen) { ImGui.PopID(); continue; }
            }
            if (ImGui.BeginTable("objectives", 4, ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.PadOuterX))
            {
                ImGui.TableSetupColumn("Done", ImGuiTableColumnFlags.WidthFixed, 22 * Scale);
                ImGui.TableSetupColumn("Objective", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("Progress", ImGuiTableColumnFlags.WidthFixed, 102 * Scale);
                ImGui.TableSetupColumn("Details", ImGuiTableColumnFlags.WidthFixed, 30 * Scale);
                foreach (var r in visible) DrawRequirement(c, series, job, stage, r, id);
                ImGui.EndTable();
            }
            ImGui.PopID(); ImGui.Spacing();
        }
        if (visibleCount == 0)
        {
            Wrap("No remaining objectives in this checklist.");
            if (hideReady && ImGui.SmallButton("Show ready objectives")) hideReady = false;
        }
    }

    private void DrawQuickAction(CharacterProgress c, Series series, string job, Stage stage, Requirement r, ulong id)
    {
        var status = Progress.Status(c, series, job, stage, r, tracker.InventoryFor(id));
        if (r.Count > 1)
        {
            var value = status.Done;
            ImGui.SetNextItemWidth(110 * Scale);
            if (ImGui.InputInt("##quick-count", ref value, 0, 0)) SetCount(c, series, job, stage, r, value);
            ImGui.SameLine(); ImGui.TextUnformatted($"/ {r.Count:N0}"); ImGui.SameLine();
        }
        if (ActionButton(r.Count > 1 ? "Mark ready" : "Mark done", true, 100, 30)) SetCount(c, series, job, stage, r, r.Count);
        Tip(r.Shared.Length > 0 ? "Shared across jobs" : r.Item.Length > 0 ? "Materials for this step" : "Objective for this relic");
    }

    private void DrawRequirement(CharacterProgress c, Series series, string job, Stage stage, Requirement r, ulong id)
    {
        var key = Progress.Key(series, job, stage, r);
        var status = Progress.Status(c, series, job, stage, r, tracker.InventoryFor(id));
        var hasOverride = c.Counters.ContainsKey(key) || r.Quest.Length > 0 && c.SharedOverrides.ContainsKey(r.Shared);
        ImGui.PushID(key); ImGui.TableNextRow(); ImGui.TableNextColumn();
        ImGui.BeginDisabled(readOnly);
        var complete = status.Complete;
        if (ObjectiveCheck(ref complete)) SetCount(c, series, job, stage, r, complete ? r.Count : 0);
        ImGui.EndDisabled();
        ImGui.TableNextColumn();
        ImGui.PushStyleColor(ImGuiCol.Text, status.Complete ? Muted : ImGui.GetStyle().Colors[(int)ImGuiCol.Text]);
        Wrap(r.Label + (r.Hq ? " (HQ)" : ""));
        ImGui.PopStyleColor();
        Tip((r.Shared.Length > 0 ? "Shared across jobs. " : "") + r.Detail);
        ImGui.TableNextColumn();
        if (r.Count > 1)
        {
            ImGui.BeginDisabled(readOnly); ImGui.SetNextItemWidth(63 * Scale);
            var value = status.Done;
            if (ImGui.InputInt("##count", ref value, 0, 0)) SetCount(c, series, job, stage, r, value);
            ImGui.EndDisabled(); ImGui.SameLine(0, 3 * Scale); ImGui.TextColored(Muted, $"/ {r.Count}");
        }
        else ImGui.TextColored(status.Complete ? Green : Muted, status.Complete ? "Ready" : "To do");
        ImGui.TableNextColumn();
        if (ActionButton("···", false, 28, 23, true)) ImGui.OpenPopup("objective-info");
        Tip("Objective details and manual correction");
        if (ImGui.BeginPopup("objective-info"))
        {
            ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + 360 * Scale);
            ImGui.TextColored(Accent, r.Label); ImGui.Separator();
            if (r.Detail.Length > 0) ImGui.TextUnformatted(r.Detail);
            if (r.Shared.Length > 0) ImGui.TextUnformatted("Shared across this character's jobs.");
            if (status.SharedComplete) ImGui.TextColored(Green, "Character unlock complete");
            else if (status.InBags.HasValue) ImGui.TextUnformatted($"On hand: {status.InBags.Value:N0}. This stock is shared between relics.");
            else if (c.DetectedCounters.ContainsKey(key)) ImGui.TextUnformatted("Recorded from game data.");
            else ImGui.TextUnformatted("No automatic progress available; record this objective manually.");
            if (hasOverride)
            {
                ImGui.TextColored(Lavender, "Using your manual entry");
                ImGui.BeginDisabled(readOnly);
                if (ImGui.Button("Clear manual entry"))
                { c.Counters.Remove(key); if (r.Quest.Length > 0) c.SharedOverrides.Remove(r.Shared); save(); ImGui.CloseCurrentPopup(); }
                ImGui.EndDisabled();
            }
            ImGui.PopTextWrapPos(); ImGui.EndPopup();
        }
        ImGui.PopID();
    }

    private void SetCount(CharacterProgress c, Series series, string job, Stage stage, Requirement r, int value)
    {
        if (readOnly) return;
        value = Math.Clamp(value, 0, r.Count);
        if (r.Quest.Length > 0 && r.Shared.Length > 0) c.SharedOverrides[r.Shared] = value == r.Count;
        else c.Counters[Progress.Key(series, job, stage, r)] = value;
        save();
    }

    private void SetStage(CharacterProgress c, Series series, WeaponProgress weapon, int index)
    {
        if (readOnly) return;
        weapon.ManualStage = index;
        foreach (var r in series.Stages.Take(index + 1).SelectMany(s => s.Requirements).Where(r => r.Applies(selectedJob)))
            if (r.Quest.Length > 0 && r.Shared.Length > 0) c.SharedOverrides[r.Shared] = true;
        viewedStage = -1; resetDetailScroll = true; save();
    }
}
