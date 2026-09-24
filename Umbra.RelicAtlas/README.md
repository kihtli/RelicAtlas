# Relic Atlas for Umbra

An optional toolbar widget for Umbra **3.1.18.0**, Dalamud API 15 and Relic Atlas
**0.1.0.16 or newer** (IPC v1).

## Install

1. Install Relic Atlas through the repository in the [main guide](../README.md).
2. Download and extract the separate `Umbra.RelicAtlas-0.1.0.0.zip` release asset
   to a permanent folder.
3. In **Umbra → Settings → Plugins**, choose **Install from file** and select
   `Umbra.RelicAtlas.dll`.
4. Restart Umbra when prompted and add the **Relic Atlas** toolbar widget.

Load this DLL through Umbra, not Dalamud's Dev Plugin Locations.

## Behaviour

The widget follows the current job or a selected job/series. Automatic selection
prefers unfinished relics, then pinned relics, started relics and newer eligible
expansions. Multiple instances can track different weapons.

The label shows stage progress; the tooltip contains the next objective and
instructions. Click to open the matching Relic Atlas checklist. Label formats,
progress display and hiding a completed relic are configurable. Ready materials
are distinct from an acquired weapon. Missing providers, logout and unsupported
jobs clear stale progress. Both sides refresh at most twice per second.

Retainer/saddlebag inventory is planning stock, not submitted progress or weapon
evidence. The extension uses local IPC and makes no network requests.

## Build

From the repository root:

```sh
dotnet build Umbra.RelicAtlas/Umbra.RelicAtlas.csproj -c Release -p:UmbraLibPath="PATH_TO_UMBRA" -p:DalamudLibPath="PATH_TO_DALAMUD"
```

The extension is AGPL-3.0-or-later; the shared contract in
`RelicAtlas/Interop/RelicSnapshot.cs` is MIT licensed. Host assemblies are not
redistributed. See [API references](SOURCES.md) and [validation](../VALIDATION.md).
