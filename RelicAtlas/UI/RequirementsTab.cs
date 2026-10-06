using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using RelicAtlas.Core;

namespace RelicAtlas.UI;

public sealed partial class MainWindow
{
    private bool openWeaponView;
    private bool dutiesView;
    private string planningSearch = "";
    private string planningJob = "All jobs";
    private bool planningPinned;
    private bool planningStarted;
    private bool includeStorage = true;
    private RemainingPlan? planningCache;
    private string planningCacheKey = "";
    private DateTime planningRefresh;

    private void DrawRemainingRequirements(CharacterProgress c, ulong id)
    {
        var controlWidth = ImGui.GetContentRegionAvail().X;
        DrawExpansionPicker(compactLayout ? controlWidth * .58f : 195 * Scale);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(compactLayout ? Math.Max(1,ImGui.GetContentRegionAvail().X) : 112 * Scale);
        if (ImGui.BeginCombo("##planning-job", planningJob))
        {
            foreach (var job in new[] { "All jobs" }.Concat(VisibleSeries.SelectMany(s => s.Jobs).Distinct().Order()))
                if (ImGui.Selectable(job, job == planningJob)) planningJob = job;
            ImGui.EndCombo();
        }
        if (!compactLayout) ImGui.SameLine();
        ImGui.SetNextItemWidth(Math.Max(100 * Scale, ImGui.GetContentRegionAvail().X - 194 * Scale));
        ImGui.InputTextWithHint("##planning-search", "Search materials, duties, sources...", ref planningSearch, 160);
        ImGui.SameLine();
        var filters = (planningPinned ? 1 : 0) + (planningStarted ? 1 : 0) + (config.ShoppingHideComplete ? 1 : 0);
        if (ActionButton($"Filters ({filters})", false, 94, 28)) ImGui.OpenPopup("shopping-filters");
        var hideComplete = config.ShoppingHideComplete;
        var showSources = config.ShoppingShowSources;
        if (ImGui.BeginPopup("shopping-filters"))
        {
            ImGui.TextColored(Accent, "SHOW IN SHOPPING LIST");
            ImGui.Checkbox("Pinned only", ref planningPinned);
            ImGui.Checkbox("Started only", ref planningStarted);
            if (ImGui.Checkbox("Hide complete", ref hideComplete)) { config.ShoppingHideComplete = hideComplete; save(); }
            Tip("Hide materials with zero shortage. Also applies to the copied shopping list.");
            ImGui.Separator();
            ImGui.Checkbox("Include stored items", ref includeStorage);
            if (ImGui.Checkbox("Show currency / sources", ref showSources)) { config.ShoppingShowSources = showSources; save(); }
            ImGui.EndPopup();
        }
        ImGui.SameLine();
        if (ActionButton("Refresh", false, 80, 28)) { tracker.Rescan(); planningRefresh = DateTime.MinValue; }
        var stock = includeStorage ? tracker.StorageFor(id) : null;
        var bags = tracker.InventoryFor(id);
        var cacheKey = $"{id}/{relicKind}/{expansion}/{planningJob}/{planningPinned}/{planningStarted}/{includeStorage}/{config.Automatic}/{tracker.CurrentId}";
        if (planningCache == null || planningCacheKey != cacheKey || DateTime.UtcNow >= planningRefresh)
        {
            planningCache = RemainingRequirements.Build(catalog, c, expansion, planningJob, planningPinned, planningStarted,
                bags, stock?.SaddlebagRecorded == true ? stock.Saddlebag : null,
                stock?.RecordedRetainers > 0 ? stock.Retainers : null, relicKind);
            planningCacheKey = cacheKey;
            planningRefresh = DateTime.UtcNow.AddMilliseconds(500);
        }
        var plan = planningCache;
        DrawSupplyStats(plan);
        var stockPosition = ImGui.GetCursorScreenPos();
        var stockWidth = ImGui.GetContentRegionAvail().X;
        var stockLabel = id == 0 || id != tracker.CurrentId ? "Stock unavailable for this profile" :
            !config.Automatic ? "Tracking paused" : !includeStorage ? "Carried stock" :
            stock == null ? "Carried stock / stored stock unavailable" : "Carried stock + cached storage";
        ImGui.GetWindowDrawList().AddCircleFilled(stockPosition + new Vector2(5, 13) * Scale, 3 * Scale, Ink(bags != null ? Cyan : Muted));
        Label(stockPosition + new Vector2(17, 5) * Scale, Fit(stockLabel,(stockWidth - 150 * Scale) / .9f), Muted, .9f);
        ImGui.SetCursorScreenPos(stockPosition + new Vector2(stockWidth - 137 * Scale, 0));
        if (ActionButton("Inventory status", false, 137, 28, true)) ImGui.OpenPopup("inventory-status");
        if (ImGui.BeginPopup("inventory-status"))
        {
            ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + 470 * Scale);
            if (id != tracker.CurrentId || id == 0)
                Wrap("Log into this character to compare stock. No live stock from another character is used.");
            else if (!config.Automatic) Wrap("Automatic detection is paused. Enable it in Settings to compare stock.");
            else
            {
                Wrap(includeStorage ? tracker.StorageStatus : "Carried stock only; stored items excluded");
                if (stock != null) Wrap($"Queried at {stock.QueriedAt.ToLocalTime():HH:mm:ss}. Cached observations may be older.");
                if (includeStorage) Wrap("Open your saddlebags and each retainer in Allagan Tools to refresh stored counts. Unvisited storage may be missing.");
            }
            Wrap("? means unknown. Shortages use known stock only. Stored counts are last-seen data.");
            ImGui.PopTextWrapPos(); ImGui.EndPopup();
        }
        ImGui.SetCursorScreenPos(stockPosition); ImGui.Dummy(new Vector2(stockWidth, 28 * Scale));
        if (Navigation("materials-view", "Materials", !dutiesView, 117, true)) dutiesView = false;
        ImGui.SameLine();
        if (Navigation("duties-view", "Duties & unlocks", dutiesView, 164, true)) dutiesView = true;
        ImGui.Spacing();
        if (!dutiesView)
        {
            var currency = ShoppingList.CurrencyFilters.Contains(config.ShoppingCurrency)
                ? config.ShoppingCurrency : ShoppingList.AllCurrencies;
            var controlsPosition = ImGui.GetCursorScreenPos();
            var controlsWidth = ImGui.GetContentRegionAvail().X;
            ImGui.SetNextItemWidth(compactLayout ? Math.Min(240 * Scale,controlsWidth * .55f) : 240 * Scale);
            if (ImGui.BeginCombo("##currency", currency))
            {
                foreach (var option in ShoppingList.CurrencyFilters)
                    if (ImGui.Selectable(option, option == currency))
                    { config.ShoppingCurrency = currency = option; save(); }
                ImGui.EndCombo();
            }
            Tip("Filter by a known purchase currency, including alternative routes. The budget uses the selected currency. No fixed currency shows materials without a listed fixed-price exchange. This filter also applies to the copied list.");
            var visible = ShoppingList.Visible(plan.Materials, hideComplete, planningSearch, showSources, currency);
            ImGui.SameLine(); ImGui.AlignTextToFramePadding(); ImGui.TextColored(Muted, $"{visible.Length} materials");
            if (compactLayout) ImGui.SetCursorScreenPos(controlsPosition + new Vector2(0,ImGui.GetFrameHeightWithSpacing()));
            else ImGui.SetCursorScreenPos(controlsPosition + new Vector2(controlsWidth - 234 * Scale, 0));
            if (ActionButton("Budget", false, 80, 28)) ImGui.OpenPopup("material-budget");
            // Wrapped text cannot establish an auto-sized popup's width. Give it a
            // readable width first, and cap its height so small viewports can scroll.
            var budgetViewport = ImGui.GetWindowViewport().WorkSize;
            var budgetWidth = Math.Min(560 * Scale, Math.Max(1, budgetViewport.X - 32 * Scale));
            var budgetHeight = Math.Max(1, budgetViewport.Y - 32 * Scale);
            ImGui.SetNextWindowSize(new Vector2(budgetWidth, 0));
            ImGui.SetNextWindowSizeConstraints(new Vector2(budgetWidth, 0), new Vector2(budgetWidth, budgetHeight));
            if (ImGui.BeginPopup("material-budget"))
            {
                ImGui.TextColored(Accent, "MATERIAL BUDGET");
                ImGui.Spacing();
                var budget = ShoppingList.Budget(visible, currency);
                if (budget.Count == 0) Wrap("No fixed-price purchases in the visible material list.");
                foreach (var (name, amount) in budget) Wrap($"{amount:N0} {name}");
                ImGui.Spacing(); ImGui.Separator(); ImGui.Spacing();
                Wrap(ShoppingList.BudgetNote);
                ImGui.Spacing();
                Wrap("This subtotal covers the visible material rows. Book purchases, Mahatmas, sphere-scroll ink and costs without a fixed estimate are excluded.");
                ImGui.EndPopup();
            }
            ImGui.SameLine();
            if (ActionButton("Copy shopping list", false, 144, 28))
                ImGui.SetClipboardText(ShoppingList.Export(visible, showSources, currency));
            if (ImGui.BeginTable("materials", compactLayout ? 3 : 7, ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY | ImGuiTableFlags.Resizable,
                new Vector2(0, ImGui.GetContentRegionAvail().Y)))
            {
                ImGui.TableSetupColumn("Material / details", ImGuiTableColumnFlags.WidthStretch);
                foreach (var label in compactLayout ? new[] { "Owned", "Still need" } : new[] { "Required", "Recorded", "Bags", "Saddlebag", "Retainers", "Still need" })
                    ImGui.TableSetupColumn(label, ImGuiTableColumnFlags.WidthFixed, 76 * Scale);
                ImGui.TableSetupScrollFreeze(0, 1);
                ImGui.TableNextRow(ImGuiTableRowFlags.Headers);
                var headings = compactLayout ? new[] { "Material / source", "Owned", "Still need" } : new[] { "Material / source", "Required", "Recorded", "Bags", "Saddlebag", "Retainers", "Still need" };
                for (var column = 0; column < headings.Length; column++)
                {
                    ImGui.TableSetColumnIndex(column);
                    if (column > 0) ImGui.SetCursorPosX(ImGui.GetCursorPosX() + Math.Max(0, ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize(headings[column]).X));
                    ImGui.PushStyleColor(ImGuiCol.Text, column == 6 ? Cyan : Muted);
                    ImGui.TableHeader(headings[column]); ImGui.PopStyleColor();
                }
                foreach (var m in visible)
                {
                    ImGui.PushID(m.Item + m.Hq); ImGui.TableNextRow(); ImGui.TableNextColumn();
                    var label = m.Item + (m.Hq ? " (HQ)" : "") + (m.IsEstimate ? " (estimate)" : "");
                    if (ImGui.Selectable(Fit(label, ImGui.GetContentRegionAvail().X) + "###material")) ImGui.OpenPopup("material-uses");
                    Tip(label + "\n" + (compactLayout && showSources ? ShoppingList.Source(m) + "\n" : "") + "Click for stock, costs and the jobs that need this material.");
                    if (showSources && !compactLayout)
                    {
                        MaterialDetail(ShoppingList.Source(m), Muted);
                        var cost = ShoppingList.Cost(m, currency);
                        if (!cost.StartsWith("No fixed", StringComparison.Ordinal))
                        { MaterialDetail(cost, new(.48f, .74f, .82f, 1)); }
                    }
                    if (ImGui.BeginPopup("material-uses"))
                    {
                        ImGui.TextColored(Accent, label);
                        Wrap($"Required {m.Required:N0}; recorded {m.Recorded:N0}; known stock {m.Owned:N0}; still need {m.Missing:N0}");
                        Wrap($"Bags: {Number(m.Bags)} · Saddlebag: {Number(m.Saddlebag)} · Retainers: {Number(m.Retainers)}");
                        ImGui.BeginChild("uses", new Vector2(Math.Min(560 * Scale,ImGui.GetWindowViewport().WorkSize.X - 60 * Scale),Math.Min(300 * Scale,ImGui.GetWindowViewport().WorkSize.Y - 140 * Scale)), false);
                        Wrap("Purchase remaining: " + ShoppingList.Cost(m, currency));
                        foreach (var objective in m.Objectives)
                        {
                            ImGui.PushID(objective.Key);
                            ImGui.TextColored(Muted, objective.Requirement.Shared.Length > 0 ? "One-time shared requirement" : "Weapon requirement");
                            Wrap(objective.Requirement.Detail);
                            foreach (var use in objective.Uses) DrawRequirementLink(use, objective.Recorded, true);
                            ImGui.Separator(); ImGui.PopID();
                        }
                        ImGui.EndChild(); ImGui.EndPopup();
                    }
                    if (compactLayout) Cell(m.Bags.HasValue || m.Saddlebag.HasValue || m.Retainers.HasValue ? m.Owned : null);
                    else { Cell(m.Required, White); Cell(m.Recorded); Cell(m.Bags); Cell(m.Saddlebag); Cell(m.Retainers); }
                    Cell(m.Missing, m.Missing == 0 ? Green : Cyan);
                    ImGui.PopID();
                }
                if (visible.Length == 0) { ImGui.TableNextRow(); ImGui.TableNextColumn(); Wrap("No materials match this view."); }
                ImGui.EndTable();
            }

        }
        else
        {
            Wrap("Remaining non-item objectives, including future stages. Shared milestones count once.");
            ImGui.BeginChild("other-requirements", new Vector2(0, 0), false);
            foreach (var group in plan.Objectives.Where(o => Matches(o.Requirement.Label) || o.Uses.Any(u => Matches(u.Stage.Name)))
                .GroupBy(o => o.Uses[^1].Series.Name + " / " + o.Uses[^1].Stage.Name))
            {
                if (!ImGui.CollapsingHeader($"{group.Key} ({group.Count()})")) continue;
                foreach (var objective in group)
                {
                    ImGui.PushID(objective.Key);
                    Wrap($"{objective.Requirement.Label}   {objective.Recorded:N0}/{objective.Required:N0}");
                    if (objective.Requirement.Shared.Length > 0) ImGui.TextColored(Lavender, "Shared across jobs");
                    if (ImGui.TreeNode("Details / open checklist"))
                    {
                        Wrap(objective.Requirement.Detail);
                        foreach (var use in objective.Uses) DrawRequirementLink(use, objective.Recorded);
                        ImGui.TreePop();
                    }
                    ImGui.Separator(); ImGui.PopID();
                }
            }
            if (plan.Objectives.Count == 0) Wrap("No remaining duties or unlocks in this scope.");
            ImGui.EndChild();
        }

    }
    private bool Matches(string value) => planningSearch.Length == 0 || value.Contains(planningSearch, StringComparison.OrdinalIgnoreCase);
    private static string Number(int? value) => value?.ToString("N0") ?? "?";
    private static void Cell(int? value, Vector4? color = null)
    {
        ImGui.TableNextColumn();
        var text = Number(value);
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + Math.Max(0, ImGui.GetContentRegionAvail().X - ImGui.CalcTextSize(text).X));
        ImGui.TextColored(color ?? Muted, text);
    }
    private void MaterialDetail(string text, Vector4 color)
    {
        var width = ImGui.GetContentRegionAvail().X;
        Label(ImGui.GetCursorScreenPos(), Fit(text, width / .9f), color, .9f);
        ImGui.Dummy(new Vector2(width, ImGui.GetFontSize() * .9f));
        Tip(text);
    }
    private void DrawRequirementLink(RequirementUse use, int recorded, bool closePopup = false)
    {
        if (ImGui.Selectable($"{use.Job} / {use.Series.Name} / {use.Stage.Name}  ({Math.Min(recorded, use.Requirement.Count):N0}/{use.Requirement.Count:N0})"))
        {
            Select(use.Series.Id, use.Job);
            // DrawDetails resets stage when a different weapon is selected; prime the key so the linked stage survives.
            var id = selectedCharacter == 0 ? tracker.CurrentId : selectedCharacter;
            selectedKey = $"{id}/{use.Series.Id}/{use.Job}";
            viewedStage = use.Series.Stages.IndexOf(use.Stage);
            openWeaponView = true;
            if (closePopup) ImGui.CloseCurrentPopup();
        }
    }
}
