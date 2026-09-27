# Relic Atlas IPC v1

Available from Relic Atlas 0.1.0.16. The provider has no dependency on Umbra.
The shared contract is `Interop/RelicSnapshot.cs`; consumers can compile that
file independently and exchange JSON strings, without loading RelicAtlas.dll.

| Gate | Dalamud subscriber signature | Result |
| --- | --- | --- |
| `RelicAtlas.ApiVersion` | `ICallGateSubscriber<int>` | `1` |
| `RelicAtlas.GetSnapshot` | `ICallGateSubscriber<string>` | JSON `RelicSnapshot` |
| `RelicAtlas.OpenRelic` | `ICallGateSubscriber<ulong, string, string, bool>` | Accepts a current character ID, series ID and job abbreviation; returns whether the request was accepted. |

Use `InvokeFunc` for every gate. Catch unavailable-provider exceptions during
plugin disable/reload, clear previous data, and retry later. Check both the gate
version and JSON `ApiVersion` before consuming data. A 500 ms polling interval
is sufficient. Do not retain the previous character's progress when the provider
is absent or the snapshot state is not `ready`.

Snapshots contain only the currently logged-in character's weapon and tool relic tracks;
no character name, notes or other profiles are exposed. `State` is `ready`,
`logged-out`, `waiting`, or `unavailable`. `Automatic` distinguishes paused
detection; `LiveInventory` states whether live bag counts were available. Data is
refreshed on the framework thread every 500 ms, and on character/job changes.
Subscribers read an immutable published JSON string.

Each eligible track contains series/job IDs, a job icon ID, pinned status,
acquired/total relic stages, the current target weapon, ready/total objectives,
next-objective count and detail, and separate `Complete`/`ReadyForTurnIn` flags.
`StageProgress` is 0–10,000, averaging each current-stage objective's done/required
fraction. It includes partial counters but is not an estimate of time remaining.
100% objective progress does not record a weapon or tool as acquired. The same progress
functions and manual overrides are used by the main checklist. Stored items from
retainers/saddlebags remain shopping stock, not automatic completed objectives.

Weapon series IDs: `arr`, `hw`, `sb`, `shb`, `ew`, `dt`. From 0.1.0.21, tool
series IDs are `mastercraft`, `skysteel`, `resplendent`, `splendorous`, `cosmic`.
Jobs also include `CRP`, `BSM`, `ARM`, `GSM`, `LTW`, `WVR`, `ALC`, `CUL`, `MIN`,
`BTN`, `FSH`. Unsupported combinations are rejected. The additive `Kind` field
is `weapon` (default for older v1 snapshots) or `tool`. `WeaponName` retains its
v1 name and contains the target tool name for tool tracks. Existing combat
series IDs, saved keys, endpoints and API version are unchanged.

Cosmic objectives report cumulative research totals per job/type. Tool stages
advance from actual item/achievement evidence or a manual stage correction,
not merely from enough research points. The tool catalogue has 20 Cosmic tiers,
including the prototype. Resplendent has one physical tool tier; its intermediate
component exchanges are described in the material source.
Open requests are applied on the next framework update and rechecked against the
logged-in character. A login change can cancel an accepted request. The window
follows the current character, switches to Collection, and selects the requested
relic. No IPC endpoint changes counters, acquisition stages or manual overrides.
