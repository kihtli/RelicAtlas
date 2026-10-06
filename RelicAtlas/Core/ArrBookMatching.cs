using System.Globalization;

namespace RelicAtlas.Core;

public static class ArrBookMatching
{
    public static string Normalize(string value) => value.Replace('’', '\'').Trim().ToLowerInvariant();

    public static bool MonsterMatches(Requirement requirement, string monsterName)
    {
        var name = Normalize(monsterName);
        if (name.Length == 0 || requirement.Count < 1) return false;
        var label = Normalize(requirement.Label);
        // Some catalogue labels include the kill count. Keep their IDs intact for saved progress.
        return label == name || label == name + " x" + requirement.Count.ToString(CultureInfo.InvariantCulture);
    }
}
