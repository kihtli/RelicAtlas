using System.Linq;

namespace RelicAtlas.Core;

public static class AnimaEnhancementMaterials
{
    public static readonly string[] Items = ["Crystal Sand", "Umbrite"];
    public const string Note = "Anima enhancement materials are upper estimates: one Crystal Sand and one Umbrite yield at least three points. Bonuses can reduce purchases. Estimates use recorded allocated points; unallocated treated sand is not deducted. Buy in batches and update your enhancement count.";

    // Planning inputs, not extra completion objectives: bonuses make a fixed
    // sixty-item checklist incorrect even though sixty is a safe starting budget.
    public static Requirement[] ForPlanning(CharacterProgress character, Series series, string job, Stage stage)
    {
        if (series.Id != "hw" || stage.Id != "reconditioned") return [];
        var points = stage.Requirements.Single(r => r.Id == "allocated-enhancement-points");
        var remaining = Progress.Status(character,series,job,stage,points,null).Remaining;
        if (remaining == 0) return [];
        var count = (remaining + 2) / 3;
        return Items.Select(item => new Requirement
        {
            Id = "shopping-enhancement-" + item.ToLowerInvariant().Replace(' ','-'),
            Label = item, Item = item, Count = count,
            Detail = (item == "Crystal Sand" ? "Exchange materials with Ulan in Idyllshire; several exchange routes are available. " :
                "Buy Umbrite for 75 Poetics each from Hismena in Idyllshire. ") + Note,
            Currencies = ["Poetics"],
        }).ToArray();
    }
}
