using System;
using System.Collections.Generic;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using RelicAtlas.Core;

namespace RelicAtlas.Tracking;

public sealed class AllaganToolsInventory
{
    private readonly ICallGateSubscriber<bool> initialized;
    private readonly ICallGateSubscriber<ulong> currentCharacter;
    private readonly ICallGateSubscriber<bool, HashSet<ulong>> owners;
    private readonly ICallGateSubscriber<ulong, HashSet<ulong[]>> items;
    private DateTime nextQuery;
    private ulong queriedOwner;
    public ExternalInventory? Snapshot { get; private set; }
    public string Status { get; private set; } = "Allagan Tools: waiting for character";
    public AllaganToolsInventory(IDalamudPluginInterface pi)
    {
        initialized = pi.GetIpcSubscriber<bool>("AllaganTools.IsInitialized");
        currentCharacter = pi.GetIpcSubscriber<ulong>("AllaganTools.CurrentCharacter");
        owners = pi.GetIpcSubscriber<bool, HashSet<ulong>>("AllaganTools.GetCharactersOwnedByActive");
        items = pi.GetIpcSubscriber<ulong, HashSet<ulong[]>>("AllaganTools.GetCharacterItems");
    }
    public void Reset()
    { Snapshot = null; queriedOwner = 0; nextQuery = DateTime.MinValue; Status = "Allagan Tools: waiting for character"; }
    public void RefreshSoon() => nextQuery = DateTime.MinValue;
    public void Update(ulong id, IReadOnlyDictionary<uint, string> materials)
    {
        if (id != queriedOwner) { Reset(); queriedOwner = id; }
        if (DateTime.UtcNow < nextQuery) return;
        nextQuery = DateTime.UtcNow.AddSeconds(10);
        Snapshot = null;
        try
        {
            if (!initialized.InvokeFunc()) { Status = "Allagan Tools is still loading"; return; }
            if (currentCharacter.InvokeFunc() != id) { Status = "Allagan Tools: waiting for matching character"; return; }
            var data = new Dictionary<ulong, HashSet<ulong[]>>();
            var ids = owners.InvokeFunc(true); ids = new HashSet<ulong>(ids) { id };
            foreach (var owner in ids) data[owner] = items.InvokeFunc(owner);
            // Do not publish a snapshot spanning a character change.
            if (currentCharacter.InvokeFunc() != id) { Status = "Allagan Tools: character changed; retrying"; return; }
            Snapshot = ExternalInventory.Read(id, data, materials);
            Status = $"Allagan Tools cache: {Snapshot.RecordedRetainers} recorded retainers; saddlebag {(Snapshot.SaddlebagRecorded ? "recorded" : "not recorded")}";
        }
        catch (Exception)
        {
            // Optional IPC may be absent, unloaded or incompatible. Never block normal tracking.
            Status = "Allagan Tools unavailable or incompatible — showing carried stock only";
        }
    }
}
