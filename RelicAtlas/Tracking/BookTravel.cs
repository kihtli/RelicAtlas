using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Game;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using Lumina.Excel.Sheets;
using RelicAtlas.Core;

namespace RelicAtlas.Tracking;

/// <summary>One user request per step; never teleports automatically when an objective completes.</summary>
public sealed class BookTravel : IBookTravel, IDisposable
{
    private readonly Configuration config;
    private readonly Catalog catalog;
    private readonly GameTracker tracker;
    private readonly IAtmaTravel atmaTravel;
    private readonly IClientState client;
    private readonly ICondition condition;
    private readonly IFramework framework;
    private readonly IObjectTable objects;
    private readonly IAetheryteList aetherytes;
    private readonly IDataManager data;
    private readonly IGameGui gameGui;
    private readonly IPluginLog log;
    private readonly ICallGateSubscriber<uint, byte, bool> teleport;
    private readonly Dictionary<uint, BookDestination> destinations = [];
    private readonly List<BookDestination> unlocked = [];
    private (ulong Character, BookStep Step, bool FlagOnly)? pending;
    private (ulong Character, BookStep Step, long Deadline)? arrival;
    private Vector3? lastPosition;
    private long movedAt = Environment.TickCount64;
    private long nextRequest;
    private long nextRefresh;
    private ulong unlockedOwner;
    public string Status { get; private set; } = "Choose an unfinished step to travel and flag its location.";

    public BookTravel(Configuration config, Catalog catalog, GameTracker tracker, IAtmaTravel atmaTravel,
        IDalamudPluginInterface pi, IClientState client, ICondition condition, IFramework framework,
        IObjectTable objects, IAetheryteList aetherytes, IDataManager data, IGameGui gameGui, IPluginLog log)
    {
        this.config = config; this.catalog = catalog; this.tracker = tracker; this.atmaTravel = atmaTravel;
        this.client = client; this.condition = condition; this.framework = framework; this.objects = objects;
        this.aetherytes = aetherytes; this.data = data; this.gameGui = gameGui; this.log = log;
        teleport = pi.GetIpcSubscriber<uint, byte, bool>("Teleport");
        ResolveDestinations();
        framework.Update += Update;
    }

    private void ResolveDestinations()
    {
        try
        {
            var markers = data.GetSubrowExcelSheet<MapMarker>(ClientLanguage.English).SelectMany(r => r)
                .Where(m => m.DataType == 3).GroupBy(m => m.DataKey.RowId).ToDictionary(g => g.Key, g => g.ToArray());
            foreach (var row in data.GetExcelSheet<Aetheryte>(ClientLanguage.English))
            {
                if (!row.IsAetheryte || !row.Map.TryGetValue(out var map) || map.SizeFactor == 0 ||
                    !row.PlaceName.TryGetValue(out var place)) continue;
                Vector2 position;
                if (row.Level.Count > 0 && row.Level[0].TryGetValue(out var level) && level.RowId != 0 &&
                    level.Territory.RowId == row.Territory.RowId && level.Map.RowId == map.RowId)
                    position = MapUtil.WorldToMap(new Vector2(level.X, level.Z), map);
                else if (markers.TryGetValue(row.RowId, out var matches) && matches.Length == 1)
                    // MapMarker coordinates are map-texture pixels, not world positions.
                    // https://github.com/xivapi/ffxiv-datamining/blob/master/docs/MapCoordinates.md
                    position = new Vector2(matches[0].X, matches[0].Y) / map.SizeFactor * 2f + Vector2.One;
                else continue;
                if (!float.IsFinite(position.X) || !float.IsFinite(position.Y)) continue;
                destinations[row.RowId] = new(row.RowId, 0, place.Name.ExtractText(), 0,
                    row.Territory.RowId, map.RowId, position.X, position.Y);
            }
        }
        catch (Exception ex) { log.Warning(ex, "Could not resolve optional Zodiac book teleport destinations."); }
    }

    private void RefreshDestinations(bool force = false)
    {
        if (!force && unlockedOwner == tracker.CurrentId && Environment.TickCount64 < nextRefresh) return;
        nextRefresh = Environment.TickCount64 + 5000;
        unlocked.Clear(); unlockedOwner = tracker.CurrentId;
        if (unlockedOwner == 0) return;
        foreach (var entry in aetherytes)
            if (destinations.TryGetValue(entry.AetheryteId, out var destination))
                unlocked.Add(destination with { SubIndex = entry.SubIndex, GilCost = entry.GilCost });
    }

    private string Validate(ulong characterId, BookStep requested, out BookStep? current)
    {
        current = null;
        if (!client.IsLoggedIn || characterId == 0 || characterId != tracker.CurrentId || objects.LocalPlayer == null)
            return "Travel is only available for your logged-in character.";
        if (!tracker.BookIsCurrent) return "Waiting for the game's current book progress.";
        if (!config.Characters.TryGetValue(characterId, out var character)) return "Waiting for your character's book.";
        current = BookTravelRules.CurrentStep(BookRoute.Build(catalog, character), requested);
        if (current == null) return "The book changed or this step is complete. Choose an unfinished step.";
        if (current.Location is not { } location) return "No verified location is available for this step.";
        if (!data.GetExcelSheet<TerritoryType>(ClientLanguage.English).TryGetRow(location.TerritoryId, out var territory) ||
            !territory.PlaceName.TryGetValue(out var place) || place.Name.ExtractText() != location.Zone ||
            !data.GetExcelSheet<Map>(ClientLanguage.English).TryGetRow(location.MapId, out var map) ||
            map.TerritoryType.RowId != territory.RowId)
            return "This step's map could not be verified with the game data.";
        if (condition[ConditionFlag.BetweenAreas] || condition[ConditionFlag.BetweenAreas51]) return "Wait until the area has loaded.";
        return "";
    }

    private unsafe string BusyReason()
    {
        var player = objects.LocalPlayer;
        if (player == null) return "Waiting for your character.";
        if (client.IsPvP || player.IsDead || player.IsCasting || !TeleportConditions.Allow(condition.AsReadOnlySet()))
            return "Finish your current activity before teleporting.";
        var fate = FFXIVClientStructs.FFXIV.Client.Game.Fate.FateManager.Instance();
        if (fate != null && fate->GetCurrentFateId() != 0) return "Finish or leave the current FATE before teleporting.";
        if (Environment.TickCount64 - movedAt < 2500) return "Stand still for a moment before teleporting.";
        if (Environment.TickCount64 < nextRequest) return "A teleport was just requested; wait before trying again.";
        return "";
    }

    public BookTravelCheck Check(ulong characterId, BookStep step)
    {
        try
        {
            var reason = Validate(characterId, step, out var current);
            if (reason.Length > 0) return new(false, reason);
            var location = current!.Location!;
            var destination = unlockedOwner == characterId ? BookTravelRules.Nearest(location, unlocked) : null;
            (float X, float Y)? playerPosition = null;
            if (client.MapId == location.MapId && objects.LocalPlayer is { } player)
            {
                var map = data.GetExcelSheet<Map>().GetRow(location.MapId);
                var position = MapUtil.WorldToMap(new Vector2(player.Position.X, player.Position.Z), map);
                playerPosition = (position.X, position.Y);
            }
            if (!BookTravelRules.NeedsTeleport(location, destination, client.TerritoryType, client.MapId, playerPosition))
                return new(true, "You're near enough to travel directly; flag the step's location.", "Flag location");
            if (!teleport.HasFunction) return new(false, "Enable the Teleporter plugin to travel. You can still flag the location.");
            if (destination == null) return new(false, "No unlocked aetheryte with a verified position was found on this map. You can still flag the location.");
            reason = BusyReason();
            return new(reason.Length == 0, reason, "Teleport + flag", destination);
        }
        catch (Exception ex)
        { log.Warning(ex, "Zodiac book travel readiness check failed."); return new(false, "Book travel is unavailable; check the plugin log."); }
    }

    public void Travel(ulong characterId, BookStep step) => Queue(characterId, step, false);
    public void Flag(ulong characterId, BookStep step) => Queue(characterId, step, true);

    private void Queue(ulong characterId, BookStep step, bool flagOnly)
    {
        atmaTravel.Stop();
        arrival = null;
        pending = (characterId, step, flagOnly);
        Status = flagOnly ? "Checking map location…" : "Checking teleport and map location…";
    }

    private bool PlaceFlag(BookLocation location) => gameGui.OpenMapWithMapLink(
        new MapLinkPayload(location.TerritoryId, location.MapId, location.X, location.Y));

    private void Send(ulong characterId, BookStep step, bool flagOnly)
    {
        RefreshDestinations(true);
        var reason = Validate(characterId, step, out var current);
        if (reason.Length > 0) { Status = reason; return; }
        var location = current!.Location!;
        var check = flagOnly ? new BookTravelCheck(true, "", "Flag location") : Check(characterId, current);
        if (!check.Allowed) { Status = check.Reason; return; }
        if (!PlaceFlag(location)) { Status = "The map could not be opened. Try flagging the location again."; return; }
        if (flagOnly || check.Destination == null)
        { Status = $"Flagged {location.Zone} ({location.X:F1}, {location.Y:F1})."; return; }
        nextRequest = Environment.TickCount64 + 10000;
        try
        {
            if (!teleport.InvokeFunc(check.Destination.AetheryteId, check.Destination.SubIndex))
            { Status = "Location flagged. Teleporter declined the request; check your teleport availability and gil."; return; }
            if (client.TerritoryType != location.TerritoryId)
                arrival = (characterId, current, Environment.TickCount64 + 90000);
            Status = $"Teleport requested: {check.Destination.Name}. Location flagged; retry manually if the cast is interrupted.";
        }
        catch (Exception ex)
        { log.Warning(ex, "Zodiac book Teleporter request failed."); Status = "Location flagged. Teleporter is unavailable or was reloaded."; }
    }

    private void Update(IFramework _)
    {
        var tick = Environment.TickCount64;
        var position = objects.LocalPlayer?.Position;
        if (position == null || lastPosition == null || Vector3.DistanceSquared(position.Value, lastPosition.Value) > .0001f) movedAt = tick;
        lastPosition = position;
        try
        {
            RefreshDestinations();
            if (pending is { } request)
            { pending = null; Send(request.Character, request.Step, request.FlagOnly); }
            if (arrival is not { } target) return;
            if (!client.IsLoggedIn || tracker.CurrentId != target.Character || tick > target.Deadline)
            { arrival = null; return; }
            if (!tracker.BookIsCurrent || condition[ConditionFlag.BetweenAreas] || condition[ConditionFlag.BetweenAreas51]) return;
            var reason = Validate(target.Character, target.Step, out var current);
            if (reason.Length > 0) { arrival = null; return; }
            if (client.TerritoryType != current!.Location!.TerritoryId) return;
            arrival = null;
            Status = PlaceFlag(current.Location)
                ? $"Arrived in {current.Location.Zone}. Follow the flag to the step's location."
                : "Arrived. Click Flag location to reopen the destination map.";
        }
        catch (Exception ex)
        {
            pending = null; arrival = null;
            Status = "Book travel is unavailable; check the plugin log before trying again.";
            log.Warning(ex, "Zodiac book travel update failed.");
        }
    }

    public void Dispose() { framework.Update -= Update; pending = null; arrival = null; }
}
