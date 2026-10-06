using System;
using System.Collections.Generic;
using System.Linq;

namespace RelicAtlas.Core;

public sealed class ArrBookObservation
{
    public string Job { get; set; } = "";
    public string Group { get; set; } = "";
    public uint RelicId { get; set; }
    public uint BookId { get; set; }
    public DateTime LastSeenUtc { get; set; }
}

public enum ArrBookObjectiveKind { Purchase, Monster, Dungeon, Fate, Leve }
public sealed record ArrBookObjective(Requirement Requirement, ArrBookObjectiveKind Kind, int Index);
public sealed record ArrBookDefinition(uint Id, uint EventItemId, string Group, IReadOnlyList<ArrBookObjective> Objectives);

// A copy of the game's global RelicNote state. No addon or equipped-job state is needed.
public sealed record ArrBookReading(uint RelicId, uint BookId, IReadOnlyList<int> Monsters, int Objectives)
{
    public int Value(ArrBookObjective binding) => binding.Kind switch
    {
        ArrBookObjectiveKind.Purchase => 1,
        ArrBookObjectiveKind.Monster => Monsters[binding.Index],
        ArrBookObjectiveKind.Dungeon => (Objectives >> binding.Index) & 1,
        ArrBookObjectiveKind.Fate => (Objectives >> (binding.Index + 4)) & 1,
        ArrBookObjectiveKind.Leve => (Objectives >> (binding.Index + 7)) & 1,
        _ => 0,
    };
}

/// <summary>Validates held-book identity before saving its job-specific objectives.</summary>
public sealed class ArrBookTracking(
    Series series,
    IReadOnlyDictionary<uint, string> relicJobs,
    IReadOnlyDictionary<uint, ArrBookDefinition> books,
    IReadOnlySet<uint> bookItemIds)
{
    private readonly Stage stage = series.Stages.Single(s => s.Id == "animus");
    private ulong owner;
    private (uint Relic, uint Book)? candidate;
    private bool missingBook;
    public string Status { get; private set; } = "Waiting for ARR book data";
    public bool IsCurrent { get; private set; }

    public void Reset()
    {
        owner = 0;
        candidate = null;
        missingBook = false;
        IsCurrent = false;
        Status = "Waiting for ARR book data";
    }

    public bool Update(ulong characterId, CharacterProgress character, ArrBookReading? reading,
        IReadOnlySet<uint>? keyItems, DateTime now)
    {
        IsCurrent = false;
        if (characterId == 0) { Reset(); return false; }
        if (owner != characterId) { Reset(); owner = characterId; }
        if (books.Count == 0 || relicJobs.Count == 0)
            return Wait("ARR book data unavailable; recorded progress is retained");
        if (keyItems == null)
            return Wait("Waiting for key items; recorded book progress is retained");

        var heldBooks = bookItemIds.Where(keyItems.Contains).ToArray();
        if (heldBooks.Length == 0)
        {
            candidate = null;
            // Two loaded scans avoid clearing the held book during inventory refreshes.
            if (!missingBook) { missingBook = true; Status = "Checking held ARR book"; return false; }
            Status = "No ARR book held; recorded objectives are retained";
            if (character.DetectedBook == null) return false;
            character.DetectedBook = null;
            return true;
        }
        missingBook = false;
        if (heldBooks.Length != 1 || reading == null ||
            !relicJobs.TryGetValue(reading.RelicId, out var job) ||
            !books.TryGetValue(reading.BookId, out var book) || book.EventItemId != heldBooks[0] ||
            reading.Monsters.Count != 10 || reading.Monsters.Any(n => n is < 0 or > 3))
            return Wait("Waiting for matching ARR book data; recorded progress is retained");

        var identity = (reading.RelicId, reading.BookId);
        if (candidate != identity)
        {
            candidate = identity;
            Status = "Confirming " + book.Group + " · " + job;
            return false;
        }

        var changed = false;
        IsCurrent = true;
        var observed = character.DetectedBook;
        if (observed == null || observed.Job != job || observed.BookId != book.Id || observed.RelicId != reading.RelicId || observed.Group != book.Group)
        {
            character.DetectedBook = observed = new ArrBookObservation
            { Job = job, Group = book.Group, RelicId = reading.RelicId, BookId = book.Id, LastSeenUtc = now };
            changed = true;
        }
        foreach (var binding in book.Objectives)
        {
            var key = Progress.Key(series, job, stage, binding.Requirement);
            var value = Math.Clamp(reading.Value(binding), 0, binding.Requirement.Count);
            // Include observed zeros: the UI can distinguish unfinished from never observed.
            // Exact values also handle an abandoned book being purchased again.
            if (character.DetectedCounters.TryGetValue(key, out var previous) && previous == value) continue;
            character.DetectedCounters[key] = value;
            changed = true;
        }
        if (changed) observed.LastSeenUtc = now;
        var completed = book.Objectives.Count(b => b.Kind != ArrBookObjectiveKind.Purchase && reading.Value(b) >= b.Requirement.Count);
        Status = $"{book.Group} · {job} · {completed}/19 objectives complete";
        return changed;
    }

    private bool Wait(string status)
    {
        candidate = null;
        missingBook = false;
        Status = status;
        return false;
    }
}
