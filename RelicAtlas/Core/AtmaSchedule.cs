using System;
using System.Collections.Generic;
using System.Linq;

namespace RelicAtlas.Core;

public sealed record AtmaArea(int Hour, string Atma, string Zone, uint TerritoryId)
{
    public string Item => "Atma of the " + Atma;
    public string Hours => $"{Hour:00}:00 / {Hour + 12:00}:00";
}

public sealed record AtmaWindow(AtmaArea Area, DateTimeOffset Start)
{
    public DateTimeOffset End => Start.AddHours(1);
    public long Key => Start.ToUnixTimeSeconds();
}

/// <summary>The player's unverified real-world schedule, assuming JST (UTC+9).</summary>
public static class AtmaSchedule
{
    public const string TheoryUrl = "https://jp.finalfantasyxiv.com/lodestone/character/5360731/blog/1049228/";
    public const string ClockUrl = "https://forum.square-enix.com/ffxiv/threads/143918";
    public static IReadOnlyList<AtmaArea> Areas { get; } = Array.AsReadOnly<AtmaArea>([
        new(0, "Fish", "Lower La Noscea", 135),
        new(1, "Archer", "North Shroud", 154),
        new(2, "Scales", "Central Thanalan", 141),
        new(3, "Crab", "Western La Noscea", 138),
        new(4, "Maiden", "Central Shroud", 148),
        new(5, "Scorpion", "Southern Thanalan", 146),
        new(6, "Water-bearer", "Upper La Noscea", 139),
        new(7, "Goat", "East Shroud", 152),
        new(8, "Bull", "Eastern Thanalan", 145),
        new(9, "Ram", "Middle La Noscea", 134),
        new(10, "Twins", "Western Thanalan", 140),
        new(11, "Lion", "Outer La Noscea", 180),
    ]);

    public static AtmaWindow At(DateTimeOffset time)
    {
        var utc = time.ToUniversalTime();
        return new(Areas[utc.Hour % 12], new(utc.Year, utc.Month, utc.Day, utc.Hour, 0, 0, TimeSpan.Zero));
    }

    public static AtmaWindow Target(DateTimeOffset time, bool early) => At(time.AddMinutes(early ? 15 : 0));

    public static HashSet<string> Missing(Catalog catalog, CharacterProgress character, string job,
        IReadOnlyDictionary<string, int>? inventory)
    {
        var series = catalog.Series.Single(s => s.Id == "arr");
        if (!series.Jobs.Contains(job)) return [];
        var stage = series.Stages.Single(s => s.Id == "atma");
        if (character.Weapon(series, job).CompletedIndex(series.Stages.Count) >= series.Stages.IndexOf(stage)) return [];
        return stage.Requirements.Where(r => r.Applies(job) && !Progress.Status(character, series, job, stage, r, inventory).Complete)
            .Select(r => r.Item).ToHashSet();
    }
}

public sealed record AtmaDestination(uint AetheryteId, byte SubIndex, string Name, uint GilCost);
public sealed record AtmaTravelResult(bool Accepted, string Message);
public sealed record AtmaTravelCheck(bool Allowed, string Reason, AtmaDestination? Destination = null);

public interface IAtmaTravel
{
    DateTimeOffset Now { get; }
    bool HasServerTime { get; }
    bool Available { get; }
    bool Active { get; }
    string Job { get; }
    string Status { get; }
    AtmaTravelCheck Check(ulong characterId, AtmaArea area);
    void Travel(ulong characterId, AtmaArea area);
    void Start(ulong characterId, string job, bool early, bool skipCollected);
    void Stop();
}
