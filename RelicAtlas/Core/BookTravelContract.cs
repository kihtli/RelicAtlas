using System;
using System.Collections.Generic;
using System.Linq;

namespace RelicAtlas.Core;

public sealed record BookDestination(uint AetheryteId, byte SubIndex, string Name, uint GilCost,
    uint TerritoryId, uint MapId, float X, float Y);

public sealed record BookTravelCheck(bool Allowed, string Reason, string ActionLabel = "Teleport + flag",
    BookDestination? Destination = null);

public interface IBookTravel
{
    string Status { get; }
    BookTravelCheck Check(ulong characterId, BookStep step);
    void Travel(ulong characterId, BookStep step);
    void Flag(ulong characterId, BookStep step);
}

public static class BookTravelRules
{
    // Resolve from the current route instead of trusting a UI snapshot after a book/job change.
    public static BookStep? CurrentStep(BookRoute? route, BookStep requested) =>
        route != null && route.Job == requested.Job && route.BookId == requested.BookId && route.RelicId == requested.RelicId
            ? route.Steps.FirstOrDefault(s => s.Requirement.Id == requested.Requirement.Id && !s.Status.Complete)
            : null;

    public static BookDestination? Nearest(BookLocation location, IEnumerable<BookDestination> destinations) =>
        destinations.Where(d => d.TerritoryId == location.TerritoryId && d.MapId == location.MapId &&
                float.IsFinite(d.X) && float.IsFinite(d.Y))
            .OrderBy(d => Math.Pow(d.X - location.X, 2) + Math.Pow(d.Y - location.Y, 2))
            .ThenBy(d => d.GilCost).ThenBy(d => d.AetheryteId).FirstOrDefault();

    public static bool NeedsTeleport(BookLocation location, BookDestination? destination, uint currentTerritory,
        uint currentMap, (float X, float Y)? playerPosition)
    {
        if (currentTerritory != location.TerritoryId) return true;
        if (destination == null || currentMap != location.MapId || playerPosition is not { } player ||
            !float.IsFinite(player.X) || !float.IsFinite(player.Y)) return false;
        var direct = Math.Sqrt(Math.Pow(player.X - location.X, 2) + Math.Pow(player.Y - location.Y, 2));
        var afterTeleport = Math.Sqrt(Math.Pow(destination.X - location.X, 2) + Math.Pow(destination.Y - location.Y, 2));
        // Avoid paying for a teleport when travelling directly is already as close.
        return direct > afterTeleport + 1;
    }
}
