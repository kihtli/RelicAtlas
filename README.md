# Relic Atlas

A Dalamud plugin for tracking relic weapons and crafting/gathering tools, every
eligible job and the next step in each relic’s progression, from ARR through Dawntrail.

## Install

Requires Dalamud API 15.

1. Open `/xlsettings` → **Experimental** → **Custom Plugin Repositories**.
2. Add this URL, enable it and save:

   ```text
   https://raw.githubusercontent.com/kihtli/plugins/main/repo.json
   ```

3. Open `/xlplugins`, search for **Relic Atlas**, and install it.
4. Enter `/relicatlas` to open the tracker.

If upgrading from a development-plugin copy, disable that copy and remove its
Dev Plugin Location before enabling the installer version. Keep the existing
Relic Atlas plugin configuration to retain saved progress.

Release downloads are also available on the [releases page](https://github.com/kihtli/RelicAtlas/releases).
The main plugin ZIP contains the DLL and manifest at its root for Dalamud's
installer. The Umbra extension is installed from its
[own GitHub repository](https://github.com/kihtli/Umbra.RelicAtlas) through Umbra.

## Features

- **Overview:** separate weapon and tool grids covering 150 eligible relic tracks
  across 32 jobs/classes. Select a cell to open its checklist.
- **Collection:** weapon/tool stages, next objectives, material quantities, quests,
  NPCs, all nine ARR books, manual corrections, pins and notes.
- **Shopping list:** remaining materials across jobs, with currency/source
  filters, purchase estimates and optional Allagan Tools storage counts.
- **Atma farming:** per-type Required, Owned and Remaining counts alongside the
  zone and both suggested Server Time windows. Hover quantities for bags,
  saddlebag, retainer and recorded-progress details.
- **Optional travel:** Teleporter buttons and a manually started Atma route.
- **Umbra companion:** relic progress on an Umbra toolbar, following the current
  job or a fixed job/expansion.

Progress is saved separately for each character. The interface uses a custom
purple/cyan theme with original embedded artwork. Relic armour is outside this release.

## Crafting and gathering tools

Use **Crafting & gathering** below the main navigation to switch Overview,
Collection and Shopping list to the 55 tool tracks across all 11 classes.
Collections: Mastercraft (base/Supra/Lucis), Skysteel (six stages), Resplendent,
Splendorous (seven stages), and Cosmic (20 stages through Tools of Stars).

Tools use inventory and explicit job-specific achievement evidence, with manual
corrections. Cosmic research is read while Cosmic Exploration and its research
module are loaded; otherwise the last recorded totals remain available. Research
counters are cumulative per class/type. Partial Resplendent gathering-log counts
can be entered manually; loaded completed achievements mark the objective ready,
but the reward tool still needs to be claimed.

Shopping compares **turn-in products and exchanged components** with carried stock
and optional Allagan Tools saddlebag/retainer stock. It does not expand crafting
recipes into a second ingredient shopping list. Source details explain exchanges,
collectability tiers and scrip ingredients. Purple Crafters’ Scrips and Skybuilders’
Scrips can be filtered; variable recipe costs are excluded from fixed purchase
budgets. Unexchanged collectables do not count as components.

Umbra companion **0.1.0.2** adds every crafting/gathering class and tool series to
its selectors. Automatic mode follows your current class; click opens the tool
checklist. Update the main plugin and companion for the complete feature.

## Tracking and corrections

Weapon/tool detection uses carried/equipped/armoury items and explicit job-specific
achievements. Open the game's Achievements window to load its history. Paladin
sword and shield evidence is tracked separately. Generic achievements do not
complete every job's relic. Historical and partial progress can be corrected
from the relic's checklist.

Materials come from live carried inventory. Shared inventory is counted once in
planning totals; it is not reserved for every job. Manual entries take precedence
in individual checklists. Material planning uses the larger of recorded credit
or known owned stock, because those amounts may overlap. Acquiring a weapon or tool
completes its stage; collecting its materials alone does not.

Allagan Tools is optional. Open saddlebags and retainers to refresh its cache.
Only known storage belonging to the selected active character is counted.
Unavailable stock is shown as unknown. Storage does not prove weapon stages.
Saved/offline profiles use their own recorded progress, without another
character's inventory. Light, infusions and other unsupported partial objectives
remain manual. The glamour dresser is not scanned.

## Atma schedule and Teleporter

Open **Overview → Atma farming** or enter `/relicatlas atma`.

The schedule is an **unverified player hypothesis**, assuming the original author
meant JST. Its displayed times are ST/UTC, not Eorzea or local time. Each suggested
window lasts one hour and repeats twelve hours later. These are not confirmed
optimal drop times. The in-game **About this theory** popup explains the assumptions
and links the sources.

Per-type totals include all ARR jobs still awaiting their Atma stage, including
unstarted jobs. A surplus of one Atma type cannot cover a different type.

Enable **Teleporter** to use the travel buttons. Automatic travel must be started
explicitly for each session, on the selected current job with its Zenith weapon
equipped. It waits for the game clock, current inventory and an idle character;
combat, FATEs, duties, casting and other busy states defer travel. Normal teleport
fees and ticket behaviour apply.

**Skip collected Atma** follows the selected job's carried items and checklist,
while the table shows totals across all jobs and known storage. Disable skipping
when farming extra sets. **Travel 15 minutes early** changes the travel target at
`:45` without changing the displayed windows. Stored Atma must be withdrawn to
turn it in.

Sessions stop on logout, job changes, reload, paused tracking or a travel failure.
Stop from the farming page, Settings, or `/relicatlas atma stop`. A session can
continue with the window closed. No FATE combat or reward collection is automated.

## Umbra extension

Install the optional toolbar widget directly from
[kihtli/Umbra.RelicAtlas](https://github.com/kihtli/Umbra.RelicAtlas). It targets
Umbra **3.1.18.0**, Dalamud API 15 and Relic Atlas **0.1.0.16 or newer** (IPC v1).

1. Keep **Relic Atlas** and **Umbra** installed and enabled in Dalamud.
2. Open **Umbra → Settings → Plugins** and enable custom plugins if prompted.
3. Under **Install from repository**, enter:

   | Field | Value |
   | --- | --- |
   | Author / owner | `kihtli` |
   | Repository | `Umbra.RelicAtlas` |

4. Add the repository and confirm the **Relic Atlas for Umbra** release.
5. Restart Umbra when prompted, then choose **Add Widget → Relic Atlas** in the
   toolbar configuration.

Use those field values directly; this Umbra dialog does not need the Dalamud
`repo.json` URL. If you previously installed the companion DLL manually, remove
its old entry from Umbra's Plugins list before adding the repository to avoid
loading two copies. Keep the main Relic Atlas plugin enabled.

The dedicated repository supplies one companion package through a normal GitHub
release, which Umbra can discover and update. See the
[companion installation guide](https://github.com/kihtli/Umbra.RelicAtlas#install-directly-from-github)
for manual installation and build instructions.

## Privacy

Relic Atlas makes no runtime HTTP requests or telemetry calls. Character progress
is stored in Dalamud's local plugin configuration. Optional integrations use local
Dalamud IPC. The public repository and release packages contain no user saves,
inventory snapshots, account credentials, logs or build-machine paths.

## Build

Install .NET 10 and Dalamud API 15 development references. The Dalamud SDK uses
the normal XIVLauncher location; set `DALAMUD_HOME` for a custom location.

```sh
dotnet build RelicAtlas/RelicAtlas.csproj -c Release
python3 tools/package_release.py
```

Current Umbra companion builds and releases are maintained in
[its standalone repository](https://github.com/kihtli/Umbra.RelicAtlas#build-and-package).
The `Umbra.RelicAtlas/` directory here preserves the original 0.1.0.0 source snapshot.

Packages and checksums are written to ignored `artifacts/`. Game, Dalamud and Umbra
host assemblies are reference-only and are not redistributed. Local test suites
and development preview files are excluded from this repository.

This development release has passed the build, regression and preview checks in
[VALIDATION.md](VALIDATION.md). Live FFXIV/Teleporter/Umbra validation remains
outstanding. Game patches can change the catalogue and detection interfaces.

## Source and licences

- [Catalogue and integration provenance](RelicAtlas/SOURCES.md)
- [Relic Atlas IPC contract](RelicAtlas/IPC.md)
- [Umbra integration references](https://github.com/kihtli/Umbra.RelicAtlas/blob/main/SOURCES.md)

Relic Atlas is MIT licensed. The optional Umbra extension is AGPL-3.0-or-later;
its shared Relic Atlas contract remains MIT licensed. The embedded font uses
SIL OFL 1.1, preserved in `RelicAtlas/Assets/FONT-LICENSE.txt`. Game names and data
belong to Square Enix. Host assemblies and game icons are not bundled.
