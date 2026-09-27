# Release validation

Relic Atlas 0.1.0.21 / standalone Umbra companion 0.1.0.2 — 27 September 2026.

- Release builds: .NET 10, Dalamud.NET.Sdk/API 15; companion targets Umbra 3.1.18.0.
- 137 regression cases passed, including HQ requirements, exchanged components,
  class/character isolation, cumulative research, manual overrides, shopping stock,
  tool overview scopes, Umbra class/series selection and backward-compatible IPC v1.
- Catalogue: 150 tracks, 32 jobs/classes, 11 series, 84 physical tool/weapon tiers,
  1,161 distinct tool/weapon names, 300 material names and 483 explicit acquisition
  achievement mappings. New names, HQ eligibility and shared quests are checked
  against extracted game sheets; Cosmic thresholds come directly from game data.
- Native previews checked at 1160×760, 900×640 and 150% scale, including the tool
  overview, compact Cosmic stage selector and shopping list.
- 30 native interaction scenarios passed: tool category switching, tool overview
  navigation/scrolling, existing tabs, combat cell navigation, saved profile,
  IPC navigation, Atma entry, travel and checklist return, at all three sizes.
- Standalone companion contract matches the provider's contract exactly. Tool
  metadata is additive in IPC v1; combat IDs and saved keys are preserved.

Source and packages use explicit allowlists. No user configuration, inventory
snapshots, logs, local tests, preview assets, screenshots, caches, symbols or
host/game assemblies are distributed. Source, commit identity and package bytes
are checked for private paths, credentials and personal data. Public source
attribution and original artwork/font licences are retained.

No live FFXIV client was available. Live inventory/achievement updates, Cosmic
research memory reads, in-game rendering, Teleporter behaviour and Umbra loading
still require client validation. Research is read only with both Cosmic manager
and research module loaded. Missing or unloaded data keeps recorded progress;
manual corrections remain available. Collectability and recipe ingredients are
source guidance, not assumed automatic component yields. Partial Resplendent
log progress is manual; a completed achievement does not prove a reward was claimed.
