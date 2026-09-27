using System.Collections.Generic;
using System.Linq;
using RelicAtlas.Interop;

namespace RelicAtlas.Core;

public enum RelicCollectionStatus { NotStarted, InProgress, Ready, Complete }

public sealed record RelicTotals(int NotStarted, int InProgress, int Ready, int Complete)
{
    public int Total => NotStarted + InProgress + Ready + Complete;
    public static RelicTotals Count(IEnumerable<RelicCollectionStatus> values)
    {
        var statuses = values.ToArray();
        return new(statuses.Count(s => s == RelicCollectionStatus.NotStarted),
            statuses.Count(s => s == RelicCollectionStatus.InProgress),
            statuses.Count(s => s == RelicCollectionStatus.Ready),
            statuses.Count(s => s == RelicCollectionStatus.Complete));
    }
}

public sealed class CollectionOverview
{
    private static readonly string[] JobOrder = ["PLD", "WAR", "DRK", "GNB", "WHM", "SCH", "AST", "SGE",
        "MNK", "DRG", "NIN", "SAM", "RPR", "VPR", "BRD", "MCH", "DNC", "BLM", "SMN", "RDM", "PCT",
        "CRP", "BSM", "ARM", "GSM", "LTW", "WVR", "ALC", "CUL", "MIN", "BTN", "FSH"];
    public Dictionary<(string Series, string Job), RelicTrackSnapshot> Tracks { get; }
    public string[] Jobs { get; }
    public RelicTotals Total { get; }
    public Dictionary<string, RelicTotals> ByJob { get; }
    public Dictionary<string, RelicTotals> ByExpansion { get; }
    private readonly HashSet<(string Series, string Job)> started = [];

    public CollectionOverview(Catalog catalog, CharacterProgress character, IReadOnlyDictionary<string, int>? inventory, string kind = "all")
    {
        var tracks = RelicBarProgress.BuildTracks(catalog, character, inventory).Where(r => kind == "all" || r.Kind == kind).ToList();
        Tracks = tracks.ToDictionary(r => (r.SeriesId, r.Job));
        foreach (var series in catalog.Series)
        foreach (var job in series.Jobs)
        {
            var first = series.Stages[0];
            // Shared unlocks and unreserved bag stock do not prove this job has begun a relic.
            if (first.Requirements.Any(r => r.Applies(job) && r.Shared.Length == 0 &&
                Progress.Status(character, series, job, first, r, null).Done > 0))
                started.Add((series.Id, job));
        }
        var eligible = tracks.Select(r => r.Job).Distinct().ToArray();
        Jobs = JobOrder.Where(eligible.Contains).Concat(eligible.Except(JobOrder)).ToArray();
        Total = RelicTotals.Count(tracks.Select(Status));
        ByJob = tracks.GroupBy(r => r.Job).ToDictionary(g => g.Key, g => RelicTotals.Count(g.Select(Status)));
        ByExpansion = tracks.GroupBy(r => r.SeriesId).ToDictionary(g => g.Key, g => RelicTotals.Count(g.Select(Status)));
    }

    public RelicCollectionStatus Status(RelicTrackSnapshot relic) => relic.Complete ? RelicCollectionStatus.Complete :
        relic.ReadyForTurnIn ? RelicCollectionStatus.Ready : relic.AcquiredStages > 0 || started.Contains((relic.SeriesId, relic.Job)) ?
        RelicCollectionStatus.InProgress : RelicCollectionStatus.NotStarted;

    public static string Role(string job) => job switch
    {
        "PLD" or "WAR" or "DRK" or "GNB" => "Tanks",
        "WHM" or "SCH" or "AST" or "SGE" => "Healers",
        "MNK" or "DRG" or "NIN" or "SAM" or "RPR" or "VPR" => "Melee",
        "BRD" or "MCH" or "DNC" => "Physical ranged",
        "BLM" or "SMN" or "RDM" or "PCT" => "Casters",
        "CRP" or "BSM" or "ARM" or "GSM" or "LTW" or "WVR" or "ALC" or "CUL" => "Crafters",
        "MIN" or "BTN" or "FSH" => "Gatherers",
        _ => "Other",
    };
}
