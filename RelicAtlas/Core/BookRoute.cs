using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RelicAtlas.Core;

public sealed record BookLocation(uint TerritoryId, uint MapId, float X, float Y, string Zone,
    ArrBookObjectiveKind Kind, string Hint)
{
    public bool Valid => TerritoryId > 0 && MapId > 0 && float.IsFinite(X) && float.IsFinite(Y) &&
        X is >= 1 and <= 45 && Y is >= 1 and <= 45 && !string.IsNullOrWhiteSpace(Zone);
}

public sealed record BookStep(string Job, uint BookId, uint RelicId, Requirement Requirement,
    BookLocation? Location, RequirementStatus Status)
{
    public ArrBookObjectiveKind Kind => BookLocations.KindOf(Requirement);
}

/// <summary>A snapshot of the held book. Choosing a step never writes progress or travels.</summary>
public sealed class BookRoute
{
    public string Job { get; }
    public string Group { get; }
    public uint BookId { get; }
    public uint RelicId { get; }
    public IReadOnlyList<BookStep> Steps { get; }
    public int Completed => Steps.Count(s => s.Status.Complete);
    public BookStep? Next { get; }

    private BookRoute(ArrBookObservation book, IReadOnlyList<BookStep> steps, string? selectedRequirementId,
        uint currentTerritoryId)
    {
        Job = book.Job;
        Group = book.Group;
        BookId = book.BookId;
        RelicId = book.RelicId;
        Steps = steps;
        Next = steps.FirstOrDefault(s => s.Requirement.Id == selectedRequirementId && !s.Status.Complete)
            ?? steps.FirstOrDefault(s => currentTerritoryId != 0 && !s.Status.Complete &&
                s.Location is { Valid: true } location && location.TerritoryId == currentTerritoryId)
            ?? steps.FirstOrDefault(s => !s.Status.Complete);
    }

    public static BookRoute? Build(Catalog catalog, CharacterProgress character,
        string? selectedRequirementId = null, IReadOnlyDictionary<string, BookLocation>? locations = null,
        uint currentTerritoryId = 0)
    {
        var book = character.DetectedBook;
        var series = catalog.Series.FirstOrDefault(s => s.Id == "arr");
        var stage = series?.Stages.FirstOrDefault(s => s.Id == "animus");
        if (book == null || book.BookId == 0 || book.RelicId == 0 || series == null || stage == null ||
            !series.Jobs.Contains(book.Job)) return null;
        var objectives = stage.Requirements.Where(r => r.Group == book.Group && r.Applies(book.Job) &&
            BookLocations.KindOf(r) != ArrBookObjectiveKind.Purchase).ToArray();
        if (objectives.Length != 19) return null;

        locations ??= BookLocations.Load();
        var steps = objectives.Select(r =>
        {
            var location = locations.GetValueOrDefault(r.Id);
            if (location is not { Valid: true } || location.Kind != BookLocations.KindOf(r)) location = null;
            // The walkthrough follows the held in-game book even if an older manual checklist
            // has ticks for the same job. Reading this snapshot never clears those overrides.
            var done = character.DetectedCounters.GetValueOrDefault(Progress.Key(series, book.Job, stage, r));
            return new BookStep(book.Job, book.BookId, book.RelicId, r, location,
                new RequirementStatus(Math.Clamp(done, 0, r.Count), r.Count, null, false));
        }).ToArray();
        return new BookRoute(book, steps, selectedRequirementId, currentTerritoryId);
    }
}

/// <summary>
/// Monster/FATE positions preserve the existing catalogue. Dungeon entrance markers and leve
/// issuers were resolved from the installed game's MapMarker and Leve/Level sheets (2026-09-30).
/// </summary>
public static class BookLocations
{
    private static readonly Lazy<IReadOnlyDictionary<string, BookLocation>> Locations = new(Read);
    public static IReadOnlyDictionary<string, BookLocation> Load() => Locations.Value;

    public static ArrBookObjectiveKind KindOf(Requirement requirement) =>
        requirement.Label.StartsWith("Purchase ", StringComparison.Ordinal) ? ArrBookObjectiveKind.Purchase :
        requirement.Count > 1 ? ArrBookObjectiveKind.Monster :
        requirement.Detail.StartsWith("Defeat ", StringComparison.Ordinal) ? ArrBookObjectiveKind.Dungeon :
        requirement.Detail.StartsWith("General — ", StringComparison.Ordinal) ||
        requirement.Detail.Contains("Grand Company — ", StringComparison.Ordinal) ? ArrBookObjectiveKind.Leve :
        ArrBookObjectiveKind.Fate;

    private static IReadOnlyDictionary<string, BookLocation> Read()
    {
        try
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("RelicAtlas.BookLocations.json");
            if (stream == null) return new Dictionary<string, BookLocation>();
            return JsonSerializer.Deserialize<Dictionary<string, BookLocation>>(stream,
                new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } })
                ?? new Dictionary<string, BookLocation>();
        }
        catch (Exception ex) when (ex is JsonException or IOException or NotSupportedException)
        {
            // Guidance remains usable if an optional destination cannot be loaded.
            return new Dictionary<string, BookLocation>();
        }
    }

    public static void Validate(Catalog catalog)
    {
        var requirements = catalog.Series.Single(s => s.Id == "arr").Stages.Single(s => s.Id == "animus").Requirements;
        var locations = Load();
        if (requirements.Count != locations.Count)
            throw new InvalidDataException("Book destination coverage does not match the catalogue.");
        foreach (var requirement in requirements)
            if (!locations.TryGetValue(requirement.Id, out var location) || !location.Valid ||
                location.Kind != KindOf(requirement) || string.IsNullOrWhiteSpace(location.Hint))
                throw new InvalidDataException("Invalid book destination: " + requirement.Id);
    }
}
