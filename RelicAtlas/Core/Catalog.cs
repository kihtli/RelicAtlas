using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace RelicAtlas.Core;

public sealed class Catalog
{
    public string Reviewed { get; set; } = "";
    public List<Series> Series { get; set; } = [];
    public static Catalog Load()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("RelicAtlas.Catalog.json")
            ?? throw new InvalidDataException("Missing relic catalogue.");
        var value = JsonSerializer.Deserialize<Catalog>(stream) ?? throw new InvalidDataException("Empty relic catalogue.");
        value.Validate();
        return value;
    }
    public void Validate()
    {
        var keys = new HashSet<string>();
        foreach (var series in Series)
        {
            if (!keys.Add(series.Id) || series.Stages.Count == 0 || series.Jobs.Count == 0)
                throw new InvalidDataException($"Invalid series: {series.Id}");
            var stages = new HashSet<string>();
            foreach (var stage in series.Stages)
            {
                if (!stages.Add(stage.Id) || stage.Requirements.Count == 0 ||
                    series.Jobs.Any(j => !stage.Weapons.TryGetValue(j, out var names) || names.Count == 0))
                    throw new InvalidDataException($"Invalid stage: {series.Id}/{stage.Id}");
                var requirements = new HashSet<string>();
                foreach (var r in stage.Requirements)
                    if (!requirements.Add(r.Id) || r.Count < 1 || r.Label.Length == 0)
                        throw new InvalidDataException($"Invalid requirement: {stage.Id}/{r.Id}");
            }
        }
    }
}
public sealed class Series
{
    public string Id { get; set; } = "";
    public string Expansion { get; set; } = "";
    public string Name { get; set; } = "";
    public int Level { get; set; }
    public string Source { get; set; } = "";
    public string Unlock { get; set; } = "";
    public List<string> Jobs { get; set; } = [];
    public List<Stage> Stages { get; set; } = [];
}
public sealed class Stage
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Quest { get; set; } = "";
    public string Npc { get; set; } = "";
    public string Notes { get; set; } = "";
    public string Source { get; set; } = "";
    public Dictionary<string, List<string>> Weapons { get; set; } = [];
    public List<Requirement> Requirements { get; set; } = [];
}
public sealed class Requirement
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public int Count { get; set; } = 1;
    public string Detail { get; set; } = "";
    public string Group { get; set; } = "";
    public string Item { get; set; } = "";
    public bool Hq { get; set; }
    public string Quest { get; set; } = "";
    // Shared keys belong to the character, never a job. Only one-time quests use Quest.
    public string Shared { get; set; } = "";
    public List<string> Jobs { get; set; } = [];
    public bool Applies(string job) => Jobs.Count == 0 || Jobs.Contains(job);
}
