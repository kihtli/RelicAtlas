using System;
using System.Collections.Generic;

namespace RelicAtlas.Interop;

// JSON is the IPC boundary: consumers do not load or share the RelicAtlas assembly.
public static class RelicAtlasContract
{
    public const int Version = 1;
    public const string VersionGate = "RelicAtlas.ApiVersion";
    public const string SnapshotGate = "RelicAtlas.GetSnapshot";
    public const string OpenGate = "RelicAtlas.OpenRelic";
}

public sealed class RelicSnapshot
{
    public int ApiVersion { get; set; } = RelicAtlasContract.Version;
    public string State { get; set; } = "waiting";
    public ulong CharacterId { get; set; }
    public string CurrentJob { get; set; } = "";
    public bool Automatic { get; set; }
    public bool LiveInventory { get; set; }
    public DateTime GeneratedAtUtc { get; set; }
    public List<RelicTrackSnapshot> Relics { get; set; } = [];
}

public sealed class RelicTrackSnapshot
{
    public string SeriesId { get; set; } = "";
    public string SeriesName { get; set; } = "";
    // Additive v1 field; old publishers/consumers default to weapons.
    public string Kind { get; set; } = "weapon";
    public string Expansion { get; set; } = "";
    public string Job { get; set; } = "";
    public uint IconId { get; set; }
    public bool Pinned { get; set; }
    public int AcquiredStages { get; set; }
    public int TotalStages { get; set; }
    public bool Complete { get; set; }
    public bool ReadyForTurnIn { get; set; }
    public string StageName { get; set; } = "";
    public string WeaponName { get; set; } = "";
    public string Npc { get; set; } = "";
    public int ReadyObjectives { get; set; }
    public int TotalObjectives { get; set; }
    public int StageProgress { get; set; } // 0..10,000; each objective contributes equally.
    public string NextLabel { get; set; } = "";
    public string NextDetail { get; set; } = "";
    public int NextDone { get; set; }
    public int NextRequired { get; set; }
}
