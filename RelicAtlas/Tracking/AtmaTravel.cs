using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Game;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.Sheets;
using RelicAtlas.Core;

namespace RelicAtlas.Tracking;

public sealed class AtmaTravel : IAtmaTravel, IDisposable
{
    private readonly Configuration config;
    private readonly Catalog catalog;
    private readonly GameTracker tracker;
    private readonly IClientState client;
    private readonly ICondition condition;
    private readonly IFramework framework;
    private readonly IObjectTable objects;
    private readonly IAetheryteList aetherytes;
    private readonly IPluginLog log;
    private readonly ICallGateSubscriber<uint, byte, bool> teleport;
    private readonly AtmaTravelSession session = new();
    private readonly Dictionary<uint, (uint Territory, string Name)> destinations = [];
    private readonly Dictionary<uint, string> zenithJobs = [];
    private readonly Dictionary<uint, AtmaDestination> unlocked = [];
    private ulong unlockedOwner;
    private Vector3? lastPosition;
    private long movedAt = Environment.TickCount64;
    private long nextUpdate;
    private long nextRequest;
    private long nextDestinationRefresh;
    private string fault = "";
    private (ulong Character, AtmaArea Area)? pending;
    public DateTimeOffset Now { get; private set; } = DateTimeOffset.UtcNow;
    public bool HasServerTime { get; private set; }
    public bool Available => teleport.HasFunction;
    public bool Active => session.Active;
    public string Job => session.Job;
    public string Status => session.Status;

    public AtmaTravel(Configuration config, Catalog catalog, GameTracker tracker, IDalamudPluginInterface pi,
        IClientState client, ICondition condition, IFramework framework, IObjectTable objects,
        IAetheryteList aetherytes, IDataManager data, IPluginLog log)
    {
        this.config = config; this.catalog = catalog; this.tracker = tracker; this.client = client;
        this.condition = condition; this.framework = framework; this.objects = objects; this.aetherytes = aetherytes; this.log = log;
        teleport = pi.GetIpcSubscriber<uint, byte, bool>("Teleport");
        try
        {
            // Resolve from English sheets, independent of the client's display language. Fail closed on missing rows.
            var territories = data.GetExcelSheet<TerritoryType>(ClientLanguage.English);
            var verified = AtmaSchedule.Areas.Where(a => territories.TryGetRow(a.TerritoryId, out var row) &&
                row.PlaceName.TryGetValue(out var place) && place.Name.ExtractText() == a.Zone).Select(a => a.TerritoryId).ToHashSet();
            foreach (var row in data.GetExcelSheet<Aetheryte>(ClientLanguage.English))
                if (row.IsAetheryte && verified.Contains(row.Territory.RowId) && row.PlaceName.TryGetValue(out var name))
                    destinations[row.RowId] = (row.Territory.RowId, name.Name.ExtractText());
            var zenith = catalog.Series.Single(s => s.Id == "arr").Stages.Single(s => s.Id == "zenith");
            var names = zenith.Weapons.ToDictionary(p => p.Value[0], p => p.Key);
            foreach (var item in data.GetExcelSheet<Item>(ClientLanguage.English))
                if (names.TryGetValue(item.Name.ExtractText(), out var job)) zenithJobs[item.RowId] = job;
        }
        catch (Exception ex) { log.Warning(ex, "Could not resolve optional Atma travel data."); }
        framework.Update += Update;
    }

    private unsafe string BusyReason()
    {
        var player = objects.LocalPlayer;
        if (!client.IsLoggedIn || player == null) return "Waiting for your character.";
        var flags = condition.AsReadOnlySet();
        if (client.IsPvP || player.IsDead || player.IsCasting || !TeleportConditions.Allow(flags))
            return "Waiting until you're free to teleport. Finish your current activity.";
        var fate = FFXIVClientStructs.FFXIV.Client.Game.Fate.FateManager.Instance();
        if (fate != null && fate->GetCurrentFateId() != 0) return "Finish or leave the current FATE before teleporting.";
        if (Environment.TickCount64 - movedAt < 2500) return "Waiting until you've stood still for a moment.";
        if (Environment.TickCount64 < nextRequest) return "A teleport was just requested; wait before trying again.";
        return "";
    }

    public AtmaTravelCheck Check(ulong characterId, AtmaArea area)
    {
        if (fault.Length > 0) return new(false, fault);
        if (characterId == 0 || characterId != tracker.CurrentId) return new(false, "Travel is only available for your logged-in character.");
        if (!Available) return new(false, "Enable the Teleporter plugin to travel.");
        if (client.TerritoryType == area.TerritoryId) return new(false, "You're already in this zone.");
        if (unlockedOwner != characterId || !unlocked.TryGetValue(area.TerritoryId, out var destination))
            return new(false, "No unlocked aetheryte was found for this zone.");
        try
        {
            var busy = BusyReason();
            return new(busy.Length == 0, busy, destination);
        }
        catch (Exception ex)
        {
            fault = "Atma travel data is unavailable. Reload Relic Atlas after checking the log.";
            session.Stop(fault); log.Warning(ex, "Atma travel readiness check failed.");
            return new(false, fault);
        }
    }

    public void Travel(ulong characterId, AtmaArea area)
    {
        // UI input queues exactly one request. Revalidate on the framework thread, including character identity.
        session.Stop("Checking teleport request…");
        pending = (characterId, area);
    }

    private AtmaTravelResult Send(ulong characterId, AtmaArea area)
    {
        RefreshDestinations(true);
        var check = Check(characterId, area);
        if (!check.Allowed || check.Destination == null) return new(false, check.Reason);
        nextRequest = Environment.TickCount64 + 10000;
        try
        {
            var destination = check.Destination;
            return teleport.InvokeFunc(destination.AetheryteId, destination.SubIndex)
                ? new(true, $"Teleport requested: {destination.Name}. An interrupted cast can be retried manually.")
                : new(false, "Teleporter declined the request. Check your teleport availability and gil.");
        }
        catch (Exception ex)
        { log.Warning(ex, "Atma Teleporter request failed."); return new(false, "Teleporter is unavailable or was reloaded."); }
    }

    public void Start(ulong characterId, string job, bool early, bool skipCollected)
    {
        pending = null;
        if (characterId == 0 || characterId != tracker.CurrentId || job != tracker.CurrentJob)
        { session.Stop("Choose your logged-in character and current ARR job before starting automatic travel."); return; }
        if (!catalog.Series.Single(s => s.Id == "arr").Jobs.Contains(job))
        { session.Stop("This job has no ARR relic."); return; }
        if (fault.Length > 0) { session.Stop(fault); return; }
        if (!Available) { session.Stop("Enable Teleporter before starting automatic travel."); return; }
        session.Start(characterId, job, early, skipCollected);
        nextUpdate = 0;
    }

    public void Stop() { pending = null; session.Stop(); }

    private void RefreshDestinations(bool force = false)
    {
        if (!force && unlockedOwner == tracker.CurrentId && Environment.TickCount64 < nextDestinationRefresh) return;
        nextDestinationRefresh = Environment.TickCount64 + 5000;
        unlocked.Clear(); unlockedOwner = tracker.CurrentId;
        if (unlockedOwner == 0) return;
        foreach (var entry in aetherytes)
        {
            if (!destinations.TryGetValue(entry.AetheryteId, out var data)) continue;
            if (unlocked.TryGetValue(data.Territory, out var old) && (old.GilCost < entry.GilCost ||
                old.GilCost == entry.GilCost && old.AetheryteId <= entry.AetheryteId)) continue;
            unlocked[data.Territory] = new(entry.AetheryteId, entry.SubIndex, data.Name, entry.GilCost);
        }
    }

    private unsafe bool ZenithEquipped(string job)
    {
        var inventory = InventoryManager.Instance();
        if (inventory == null) return false;
        var equipped = inventory->GetInventoryContainer(InventoryType.EquippedItems);
        if (equipped == null || !equipped->IsLoaded) return false;
        var mainHand = equipped->GetInventorySlot(0);
        return mainHand != null && zenithJobs.TryGetValue(mainHand->ItemId % 1000000, out var found) && found == job;
    }

    private void Update(IFramework _)
    {
        var tick = Environment.TickCount64;
        var position = objects.LocalPlayer?.Position;
        if (position == null || lastPosition == null || Vector3.DistanceSquared(position.Value, lastPosition.Value) > .0001f) movedAt = tick;
        lastPosition = position;
        if (tick < nextUpdate && pending == null) return;
        nextUpdate = tick + 500;
        try
        {
            HasServerTime = false; Now = DateTimeOffset.UtcNow;
            RefreshDestinations();
            if (client.IsLoggedIn)
            {
                var seconds = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework.GetServerTime();
                if (seconds > 1262304000 && seconds < 4102444800)
                { Now = DateTimeOffset.FromUnixTimeSeconds(seconds); HasServerTime = true; }
            }
            if (pending is { } request)
            { pending = null; session.Stop(Send(request.Character, request.Area).Message); }
            if (!Active) return;
            if (!client.IsLoggedIn) { session.Stop("Automatic travel stopped: logged out."); return; }
            if (!Available) { session.Stop("Automatic travel stopped: Teleporter is unavailable."); return; }
            // Loading screens temporarily remove player objects; don't treat a normal teleport as a job change.
            if (condition[ConditionFlag.BetweenAreas] || condition[ConditionFlag.BetweenAreas51]) return;
            var id = tracker.CurrentId; var job = tracker.CurrentJob;
            var inventory = tracker.InventoryFor(id);
            var character = config.Characters.GetValueOrDefault(id);
            var fresh = inventory != null && tracker.LastScan is { } scan && DateTime.UtcNow - scan < TimeSpan.FromSeconds(6);
            var missing = character == null ? [] : AtmaSchedule.Missing(catalog, character, job, inventory);
            session.Tick(new(id, job, HasServerTime ? Now : null, client.TerritoryType, config.Automatic,
                fresh, ZenithEquipped(job), BusyReason(), missing), area => Send(id, area));
        }
        catch (Exception ex)
        {
            pending = null; HasServerTime = false;
            session.Stop("Atma travel is unavailable; check the plugin log before starting another session.");
            log.Warning(ex, "Atma travel update failed.");
            nextUpdate = tick + 30000;
        }
    }

    public void Dispose() { framework.Update -= Update; Stop(); }
}
