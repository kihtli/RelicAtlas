# Relic Atlas

A Dalamud plugin for tracking every combat relic weapon, every eligible job and
the next step in each relic's progression, from ARR through Dawntrail.

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
installer. The Umbra extension is a separate download.

## Features

- **Overview:** a job-by-expansion grid covering 95 eligible relic tracks across
  21 jobs and six expansions. Select a cell to open its checklist.
- **Collection:** weapon stages, next objectives, material quantities, quests,
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
purple/cyan theme with original embedded artwork. Crafting/gathering tools and
relic armour are outside this release.

## Tracking and corrections

Weapon detection uses carried/equipped/armoury items and explicit job-specific
achievements. Open the game's Achievements window to load its history. Paladin
sword and shield evidence is tracked separately. Generic achievements do not
complete every job's relic. Historical and partial progress can be corrected
from the relic's checklist.

Materials come from live carried inventory. Shared inventory is counted once in
planning totals; it is not reserved for every job. Manual entries take precedence
in individual checklists. Material planning uses the larger of recorded credit
or known owned stock, because those amounts may overlap. Acquiring a weapon
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

The optional extension targets Umbra **3.1.18.0** and Relic Atlas IPC v1. Install
its DLL through **Umbra → Settings → Plugins → Install from file**, restart Umbra,
then add the **Relic Atlas** widget. It is not a separate Dalamud plugin.
See [the companion guide](Umbra.RelicAtlas/README.md).

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

The optional Umbra extension also requires the installed Umbra assemblies:

```sh
dotnet build Umbra.RelicAtlas/Umbra.RelicAtlas.csproj -c Release -p:UmbraLibPath="PATH_TO_UMBRA" -p:DalamudLibPath="PATH_TO_DALAMUD"
python3 tools/package_release.py --include-umbra
```

Packages and checksums are written to ignored `artifacts/`. Game, Dalamud and Umbra
host assemblies are reference-only and are not redistributed. Local test suites
and development preview files are excluded from this repository.

This development release has passed the build, regression and preview checks in
[VALIDATION.md](VALIDATION.md). Live FFXIV/Teleporter/Umbra validation remains
outstanding. Game patches can change the catalogue and detection interfaces.

## Source and licences

- [Catalogue and integration provenance](RelicAtlas/SOURCES.md)
- [Relic Atlas IPC contract](RelicAtlas/IPC.md)
- [Umbra integration references](Umbra.RelicAtlas/SOURCES.md)

Relic Atlas is MIT licensed. The optional Umbra extension is AGPL-3.0-or-later;
its shared Relic Atlas contract remains MIT licensed. The embedded font uses
SIL OFL 1.1, preserved in `RelicAtlas/Assets/FONT-LICENSE.txt`. Game names and data
belong to Square Enix. Host assemblies and game icons are not bundled.
