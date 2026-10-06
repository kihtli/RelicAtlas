# Release validation

## 0.1.0.34 — 6 October 2026

- Release built from the publication checkout; 346 regression cases pass.
  Companion compatibility checks use the current standalone Umbra companion,
  rather than the archived bundled copy.
- Native UI previews checked compact/default sizes and enlarged fonts. Book
  popout checks cover travel, flags, hiding completed objectives, locking,
  scrolling, zone changes and resizing down to 320×360.
- Anima density native signature is unique in the inspected client; exact getter
  validation disables the optional reader if its layout changes. Quest phase,
  accepted job, loaded character and held weapon checks gate reads.
- Live post-duty density increases have not yet been verified. The glass reader
  remains a fallback when native layout validation fails. Manual corrections
  remain available.
- Runtime archive contains only DLL, manifest, dependency manifest, README and
  licences. Private tests, previews and audit artifacts are excluded.

## Previous release validation

Relic Atlas 0.1.0.23 / compatible standalone Umbra companion 0.1.0.2 — 27 September 2026.

- Release builds: .NET 10, Dalamud.NET.Sdk/API 15; companion targets Umbra 3.1.18.0.
- 173 regression cases passed, including Atma shortage filtering, shared stock,
  current/next suggested windows, timezones, midnight/year rollover, unknown
  inventory, manual credit, character isolation, achievement query pacing, response targets,
  timeouts, stale replies, character changes, reward-bit mappings, HQ requirements, exchanged components,
  class/character isolation, cumulative research, manual overrides, shopping stock,
  tool overview scopes, Umbra class/series selection and backward-compatible IPC v1.
- Catalogue: 150 tracks, 32 jobs/classes, 11 series, 84 physical tool/weapon tiers,
  1,161 distinct tool/weapon names, 300 material names and 483 explicit acquisition
  achievement mappings. New names, HQ eligibility and shared quests are checked
  against extracted game sheets; Cosmic thresholds come directly from game data.
- Atma popout: 42 native ImGui scenarios passed across normal and 150% scale,
  covering row travel/disabled travel, locking, dragging, closing, page navigation,
  stopping auto travel, character changes/logout, inventory changes, clock
  rollover, complete/unknown/paused states and the main page entry button.
  Rendered previews reviewed with all 12 types, partial stock, all types covered,
  the next-needed header at 150%, and the main page button at 900×640.
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
