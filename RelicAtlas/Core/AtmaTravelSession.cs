using System;
using System.Collections.Generic;

namespace RelicAtlas.Core;

public sealed record AtmaTravelContext(ulong CharacterId, string Job, DateTimeOffset? ServerTime,
    uint TerritoryId, bool Tracking, bool InventoryReady, bool ZenithEquipped, string BusyReason,
    IReadOnlySet<string> Missing);

/// <summary>Session-only travel. No timer survives logout, a job change or plugin reload.</summary>
public sealed class AtmaTravelSession
{
    public bool Active { get; private set; }
    public ulong CharacterId { get; private set; }
    public string Job { get; private set; } = "";
    public string Status { get; private set; } = "Automatic travel is off.";
    private bool early;
    private bool skipCollected;
    private readonly HashSet<long> handled = [];

    public void Start(ulong characterId, string job, bool arriveEarly, bool skip)
    {
        if (characterId == 0 || string.IsNullOrEmpty(job)) return;
        CharacterId = characterId; Job = job; early = arriveEarly; skipCollected = skip;
        handled.Clear(); Active = true; Status = "Automatic travel started; waiting for the next check.";
    }

    public void Stop(string message = "Automatic travel stopped.")
    { Active = false; Status = message; }

    public void Tick(AtmaTravelContext context, Func<AtmaArea, AtmaTravelResult> travel)
    {
        if (!Active) return;
        if (context.CharacterId != CharacterId || context.Job != Job)
        { Stop("Automatic travel stopped: character or job changed."); return; }
        if (!context.Tracking) { Stop("Automatic travel stopped: tracking is paused."); return; }
        if (context.ServerTime == null) { Status = "Waiting for the game's ST clock."; return; }
        if (!context.InventoryReady) { Status = "Waiting for current inventory."; return; }
        if (skipCollected && context.Missing.Count == 0)
        { Stop("This job's Atma set is ready or already acquired. Automatic travel stopped."); return; }
        if (!context.ZenithEquipped) { Status = "Equip a Zenith weapon for this job to farm Atma."; return; }
        var target = AtmaSchedule.Target(context.ServerTime.Value, early);
        if (handled.Contains(target.Key)) return;
        if (skipCollected && !context.Missing.Contains(target.Area.Item))
        { Status = $"{target.Area.Atma} is collected; waiting for another window."; return; }
        if (context.TerritoryId == target.Area.TerritoryId)
        {
            handled.Add(target.Key);
            Status = $"Already in {target.Area.Zone}. Next change at {target.End.AddMinutes(early ? -15 : 0):HH:mm} ST.";
            return;
        }
        if (context.BusyReason.Length > 0) { Status = context.BusyReason; return; }
        // Mark before dispatch: a rejected/cancelled cast must never create an automatic retry loop.
        handled.Add(target.Key);
        try
        {
            var result = travel(target.Area);
            Status = result.Message;
            if (!result.Accepted) Stop(result.Message + " Automatic travel stopped; retry manually or start a new session.");
        }
        catch
        { Stop("Teleporter became unavailable. Automatic travel stopped."); }
    }
}
