using System;
using System.Collections.Generic;
using System.Linq;

namespace RelicAtlas.Core;

public sealed record AnimaExchange(string Product, string Stage, string[] Ingredients, int[] Quantities);
public static class AnimaExchangeProgress
{
    public static readonly AnimaExchange[] Exchanges =
    [
        new("Astral Nodule","animated",["luminous-wind-crystal","luminous-fire-crystal","luminous-lightning-crystal"],[1,1,1]),
        new("Umbral Nodule","animated",["luminous-ice-crystal","luminous-earth-crystal","luminous-water-crystal"],[1,1,1]),
        new("Enchanted Rubber","anima",["unidentifiable-bone","adamantite-francesca"],[10,4]),
        new("Fast-drying Carboncoat","anima",["unidentifiable-shell","titanium-alloy-mirror"],[10,4]),
        new("Divine Water","anima",["unidentifiable-ore","dispelling-arrow"],[10,4]),
        new("Fast-acting Allagan Catalyst","anima",["unidentifiable-seeds","kingcake"],[10,4]),
        new("Newborn Soulstone","complete",["pneumite"],[15]),
    ];

    // Exchanged ingredients remain shared stock until their products are handed in.
    // Recompute only from a complete inventory snapshot, never from quest history.
    public static bool Update(CharacterProgress c, IReadOnlyDictionary<string,int> inventory)
    {
        if (Exchanges.Any(e => !inventory.ContainsKey(e.Product))) return false;
        var credits = new Dictionary<string,int>();
        foreach (var exchange in Exchanges)
            for (var i = 0; i < exchange.Ingredients.Length; i++)
                credits[$"{exchange.Stage}/{exchange.Ingredients[i]}"] = (int)Math.Min(int.MaxValue,
                    (long)Math.Max(0,inventory[exchange.Product]) * exchange.Quantities[i]);
        foreach (var (stage, task) in new[] { ("animated","exchange-crystals-for-both-nodules"),
            ("anima","exchange-for-the-four-special-materials"), ("complete","receive-the-newborn-soulstone") })
            credits[$"{stage}/{task}"] = Exchanges.Where(e => e.Stage == stage).All(e => inventory[e.Product] > 0) ? 1 : 0;
        if (credits.Count == c.AnimaExchangeCredits.Count && credits.All(p => c.AnimaExchangeCredits.GetValueOrDefault(p.Key) == p.Value)) return false;
        c.AnimaExchangeCredits = credits;
        return true;
    }

    public static int Credit(CharacterProgress c, Series series, string job, Stage stage, Requirement r) =>
        series.Id == "hw" && !c.Counters.ContainsKey(Progress.Key(series,job,stage,r))
            ? c.AnimaExchangeCredits.GetValueOrDefault($"{stage.Id}/{r.Id}") : 0;
}
