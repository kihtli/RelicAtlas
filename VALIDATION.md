# Release validation

Relic Atlas 0.1.0.20 / Umbra companion 0.1.0.0 — 24 September 2026.

- Built against .NET 10, Dalamud.NET.Sdk 15.0.0 and Dalamud API 15 references.
- 119 local regression cases passed, covering catalogue/progress rules, character
  isolation, shared stock, storage, overview, Atma scheduling/travel and Umbra IPC.
- Catalogue audit: six expansions, 95 eligible relic tracks, 754 weapon names,
  120 material names and 376 explicit single-job achievement mappings.
- Native UI previews checked at 1160×760, 900×640 and 150% scale. Atma rows show
  per-type required, owned and remaining amounts beside both ST window starts.
- Synthetic navigation and travel checks covered the per-row teleport at all
  three sizes, current/next travel, start/stop, early mode, disabled travel,
  farming entry, checklist return and main tabs.

The public source is selected from explicit file lists. User configuration,
local tests, preview harness files, screenshots, caches, old releases, logs,
debug symbols and host/game assemblies are excluded. Source, commit identity,
image metadata and release contents are checked for private paths, credentials
and personal data before publication. Original artwork has no embedded metadata.
Public third-party attribution and source links are retained.

Release ZIPs contain only explicitly selected runtime files and licences.
The main DLL and manifest are at the archive root. Manifest version/API, safe
archive paths, checksums and the published installer feed are verified separately.

No live FFXIV client was available for these checks. Real client rendering,
inventory refresh, Teleporter dispatch/gil behaviour and Umbra toolbar loading
still require in-game validation. The Atma schedule is an unverified player
hypothesis and does not establish a drop-rate bonus.
