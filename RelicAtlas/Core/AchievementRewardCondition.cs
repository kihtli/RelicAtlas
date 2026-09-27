namespace RelicAtlas.Core;

public readonly record struct AchievementRewardCondition(uint Bit, bool ClaimedWhenSet)
{
    // Achievement.Unknown1/Unknown2 describe the unclaimed-reward condition.
    // 255 is absent. The native map has 26 bytes; ambiguous rows are unsupported.
    public static AchievementRewardCondition? FromSheet(byte unclaimedWhenSet, byte unclaimedWhenClear) =>
        (unclaimedWhenSet, unclaimedWhenClear) switch
        {
            (< 208, 255) => new(unclaimedWhenSet, false),
            (255, < 208) => new(unclaimedWhenClear, true),
            _ => null,
        };
}
