using System.Collections.Generic;
using System.Linq;

namespace RelicAtlas.Core;

public sealed record ZodiacMaterialQuest(string Name, string Prefix, string Reward, int CommonDelivery,
    IReadOnlyDictionary<string,int> Deliveries);

public static class ArrZodiacMaterials
{
    public static readonly ZodiacMaterialQuest[] Quests =
    [
        new("A Ponze of Flesh","a-ponze-of-flesh-","Book of Skylight",1,new Dictionary<string,int>
        { ["bronze-lake-crystal"]=1,["horn-of-the-beast"]=3,["gobmachine-bangplate"]=3,["narasimha-hide"]=5,["sickle-fang"]=5,["perfect-firewood"]=255,["furnace-ring"]=255 }),
        new("Labor of Love","labor-of-love-","Zodium",255,new Dictionary<string,int>
        { ["allagan-resin"]=1,["vale-bubo"]=3,["voidweave"]=3,["amdapor-vellum"]=5,["indigo-pearl"]=5,["perfect-mortar"]=6,["perfect-pestle"]=6 }),
        new("Method in His Malice","method-in-his-malice-","Zodiac Scroll",255,new Dictionary<string,int>
        { ["furite-sand"]=255,["tonberry-king-blood"]=2,["royal-gigant-blood"]=4,["kraken-blood"]=6,["vicegerent-blood"]=8,["perfect-vellum"]=9,["perfect-pounce"]=9 }),
        new("A Treasured Mother","a-treasured-mother-","Flawless Alexandrite",1,new Dictionary<string,int>
        { ["brass-kettle"]=1,["lost-treasure-of-amdapor"]=4,["lost-treasure-of-pharos-sirius"]=4,["lost-treasure-of-the-tam-tara-deepcroft"]=4,["lost-treasure-of-the-stone-vigil"]=6,["perfect-cloth"]=255,["tailor-made-eel-pie"]=255 }),
    ];

    // These quests produce transferable ingredients, not job-bound progress. Credit the current
    // batch once at character level; never use their historical completion bits for every job.
    public static bool Update(CharacterProgress character, IReadOnlyDictionary<string,int> active,
        IReadOnlyDictionary<string,int> inventory)
    {
        var credits = new Dictionary<string,int>();
        var common = 0;
        foreach (var quest in Quests)
        {
            var reward = inventory.GetValueOrDefault(quest.Reward) > 0;
            var sequence = active.GetValueOrDefault(quest.Name);
            if (reward || sequence > quest.CommonDelivery) common++;
            foreach (var (id, after) in quest.Deliveries) credits[quest.Prefix + id] = reward || sequence > after ? 1 : 0;
        }
        credits["all-1-per-quest-bombard-core"] = common;
        credits["all-1-per-quest-sacred-spring-water"] = common;
        credits["begin-the-four-material-quests"] = Quests.All(q => active.ContainsKey(q.Name) || inventory.GetValueOrDefault(q.Reward) > 0) ? 1 : 0;
        credits["finish-all-four-material-quests"] = Quests.All(q => inventory.GetValueOrDefault(q.Reward) > 0) ? 1 : 0;
        if (credits.Count == character.ZodiacMaterialCredits.Count && credits.All(p => character.ZodiacMaterialCredits.GetValueOrDefault(p.Key) == p.Value)) return false;
        character.ZodiacMaterialCredits = credits;
        return true;
    }

    public static int Credit(CharacterProgress c, Series series, string job, Stage stage, Requirement r) =>
        series.Id == "arr" && stage.Id == "zodiac-braves" && !c.Counters.ContainsKey(Progress.Key(series,job,stage,r))
            ? c.ZodiacMaterialCredits.GetValueOrDefault(r.Id) : 0;
}
