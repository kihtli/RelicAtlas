# Release validation

Relic Atlas 0.1.0.22 / compatible standalone Umbra companion 0.1.0.2 — 27 September 2026.

- Release builds: .NET 10, Dalamud.NET.Sdk/API 15; companion targets Umbra 3.1.18.0.
- 161 regression cases passed, including achievement query pacing, response targets,
  timeouts, stale replies, character changes, reward-bit mappings, HQ requirements, exchanged components,
  class/character isolation, cumulative research, manual overrides, shopping stock,
  tool overview scopes, Umbra class/series selection and backward-compatible IPC v1.
- Catalogue: 150 tracks, 32 jobs/classes, 11 series, 84 physical tool/weapon tiers,
  1,161 distinct tool/weapon names, 300 material names and 483 explicit acquisition
  achievement mappings. New names, HQ eligibility and shared quests are checked
  against extracted game sheets; Cosmic thresholds come directly from game data.
- Previous 0.1.0.21 native previews checked at 1160×760, 900×640 and 150% scale, including the tool
  overview, compact Cosmic stage selector and shopping list.
- Previous 0.1.0.21 native interaction checks: 30 scenarios passed: tool category switching, tool overview
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
source guidance, not assumed automatic component yields. Resplendent partial
progress uses the game's shared achievement query slot, with ID/target validation,
observed request state, per-character ownership, timeouts and paced requests.
Completed history remains distinct from tool claims.

The optional reward getter's full signature and both Achievement agent call sites
were checked against client version 2026.09.15.0000.0000. The game's reward-icon
state disables claimed rewards when the achievement is complete and the unclaimed
condition is false. Tool mappings are resolved from the live Achievement sheet's
reward Item, not guessed from achievement completion. All three current catalogue
rewards match the extracted sheet. Unknown, ambiguous or out-of-bounds conditions
are ignored; missing signatures disable reward detection without preventing load.
No hooks, reward claims or direct native state writes were added. Live server responses
and claimed/unclaimed examples still need in-game validation.
