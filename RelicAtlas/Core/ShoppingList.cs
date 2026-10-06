using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RelicAtlas.Core;

public sealed record PurchasePrice(string Currency, int Each);
public static class ShoppingList
{
    // Explicit per-item prices, not amounts parsed from prose (which can contain batch totals).
    // First route is used for the estimate; alternative routes are never added to it.
    private static readonly Dictionary<string, PurchasePrice[]> Prices = new()
    {
        ["Radz-at-Han Quenching Oil"] = [new("Poetics", 15)],
        ["Thavnairian Mist"] = [new("Poetics", 20)],
        ["Alexandrite"] = [new("Allied Seals", 50)],
        ["Bombard Core"] = [new("Company Seals", 20000)],
        ["Sacred Spring Water"] = [new("Poetics", 200)],
        ["Furite Sand"] = [new("Gil", 100000)],
        ["Allagan Resin"] = [new("Gil", 100000)],
        ["Bronze Lake Crystal"] = [new("Gil", 100000)],
        ["Brass Kettle"] = [new("Gil", 100000)],
        ["Unidentifiable Bone"] = [new("Poetics", 150)],
        ["Unidentifiable Shell"] = [new("Poetics", 150)],
        ["Unidentifiable Ore"] = [new("Poetics", 150)],
        ["Unidentifiable Seeds"] = [new("Poetics", 150)],
        ["Adamantite Francesca"] = [new("Company Seals", 5000)],
        ["Titanium Alloy Mirror"] = [new("Company Seals", 5000)],
        ["Dispelling Arrow"] = [new("Company Seals", 5000)],
        ["Kingcake"] = [new("Company Seals", 5000)],
        ["Umbrite"] = [new("Poetics", 75)],
        ["Aether Oil"] = [new("Poetics", 350)],
        ["Singing Cluster"] = [new("Poetics", 40)],
        ["Pneumite"] = [new("Poetics", 100), new("Company Seals", 4000)],
        ["Archaic Enchanted Ink"] = [new("Poetics", 500)],
        ["Thavnairian Scalepowder"] = [new("Poetics", 250)],
        ["Manderium Meteorite"] = [new("Poetics", 500)],
        ["Complementary Chondrite"] = [new("Poetics", 500)],
        ["Amplifying Achondrite"] = [new("Poetics", 500)],
        ["Cosmic Crystallite"] = [new("Poetics", 500)],
        ["Arcanite"] = [new("Mathematics", 500)],
        ["Waxing Arcanite"] = [new("Mathematics", 500)],
        ["Waning Arcanite"] = [new("Mathematics", 500)],
        ["Ecliptic Arcanite"] = [new("Mathematics", 500)],
        ["Rroneek Glue"] = [new("Gil", 300000)],
        ["Ut'ohmu Siderite"] = [new("Bicolor Gemstones", 600)],
        ["Umbral Clay"] = [new("Gil", 500000)],
        ["Monarch Whetstone"] = [new("Gil", 500000)],
        ["Moonstone"] = [new("Company Seals", 4000)],
    };
    public const string AllCurrencies = "All currencies";
    public const string NoFixedCurrency = "No fixed currency";
    public static readonly string[] CurrencyFilters = new[] { AllCurrencies }
        .Concat(Prices.Values.SelectMany(p => p).Select(p => p.Currency)
            .Concat(["Purple Crafters' Scrips", "Skybuilders' Scrips"]).Distinct().Order())
        .Append(NoFixedCurrency).ToArray();
    public static bool MatchesCurrency(MaterialNeed material, string currency) => currency == AllCurrencies ||
        (Prices.TryGetValue(material.Item, out var prices) && prices.Any(p => p.Currency == currency)) ||
        material.Objectives.SelectMany(o => o.Uses).Any(u => u.Requirement.Currencies.Contains(currency)) ||
        currency == NoFixedCurrency && !Prices.ContainsKey(material.Item);
    public static string Source(MaterialNeed material) => string.Join(" / ", material.Objectives
        .SelectMany(o => o.Uses).Select(u => u.Requirement.Detail).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct());
    public static string Cost(MaterialNeed material, string currency = AllCurrencies)
    {
        if (material.Missing == 0) return "No purchase needed";
        return Prices.TryGetValue(material.Item, out var prices)
            ? string.Join(" OR ", prices.Where(p => currency == AllCurrencies || p.Currency == currency).Select(p => $"{(long)p.Each * material.Missing:N0} {p.Currency} ({p.Each:N0} each)"))
            : material.Objectives.SelectMany(o => o.Uses).Any(u => u.Requirement.Currencies.Count > 0)
                ? "Craft / exchange; variable inputs — see source" : "No fixed currency estimate; see source";
    }
    public static SortedDictionary<string, long> Budget(IEnumerable<MaterialNeed> materials, string currency = AllCurrencies)
    {
        var result = new SortedDictionary<string, long>();
        foreach (var material in materials.Where(m => m.Missing > 0))
            if (Prices.TryGetValue(material.Item, out var prices))
            {
                var price = prices.FirstOrDefault(p => currency == AllCurrencies || p.Currency == currency);
                if (price != null)
                    result[price.Currency] = result.GetValueOrDefault(price.Currency) + (long)price.Each * material.Missing;
            }
        return result;
    }
    public static MaterialNeed[] Visible(IEnumerable<MaterialNeed> materials, bool hideComplete, string search, bool searchSources, string currency = AllCurrencies) =>
        materials.Where(m => MatchesCurrency(m, currency) && (!hideComplete || m.Missing > 0) &&
            (search.Length == 0 || m.Item.Contains(search, StringComparison.OrdinalIgnoreCase) ||
             searchSources && (Source(m) + " " + Cost(m)).Contains(search, StringComparison.OrdinalIgnoreCase))).ToArray();
    public const string BudgetNote = "Estimate for missing materials using the selected currency, or the first listed purchase route when showing all currencies. Alternatives are not added together. Drops, crafting, Market Board prices and variable costs are excluded; currency already owned is not deducted. The first Resistance weapon is free: record it before budgeting additional weapons.";
    public static string Export(IEnumerable<MaterialNeed> materials, bool showSources, string currency = AllCurrencies)
    {
        var rows = materials.Where(m => MatchesCurrency(m, currency)).ToArray();
        var text = new StringBuilder();
        if (currency != AllCurrencies) text.AppendLine("Currency filter: " + currency);
        if (showSources)
        {
            text.AppendLine("Material purchase budget");
            foreach (var (name, amount) in Budget(rows, currency)) text.AppendLine($"{name}: {amount:N0}");
            text.AppendLine(BudgetNote).AppendLine();
        }
        if (rows.Any(m => m.IsEstimate)) text.AppendLine(AnimaEnhancementMaterials.Note).AppendLine();
        foreach (var m in rows)
        {
            text.AppendLine($"{m.Item}{(m.Hq ? " (HQ)" : "")}{(m.IsEstimate ? " (estimate)" : "")}: need {m.Missing:N0}; required {m.Required:N0}; recorded {m.Recorded:N0}; bags {Number(m.Bags)}; saddlebag {Number(m.Saddlebag)}; retainers {Number(m.Retainers)}");
            if (!showSources) continue;
            text.AppendLine("  Purchase: " + Cost(m, currency));
            text.AppendLine("  Source: " + Source(m));
        }
        return text.ToString().TrimEnd();
    }
    private static string Number(int? value) => value?.ToString("N0") ?? "?";
}
