using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace RelicAtlas.Core;

public sealed class AchievementEvidence
{
    public uint Id { get; set; }
    public string Name { get; set; } = "";
    public string Series { get; set; } = "";
    public string Job { get; set; } = "";
    public int Stage { get; set; }
    public static List<AchievementEvidence> Load()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("RelicAtlas.Achievements.json")
            ?? throw new InvalidDataException("Missing achievement catalogue.");
        return JsonSerializer.Deserialize<List<AchievementEvidence>>(stream) ?? [];
    }
}
