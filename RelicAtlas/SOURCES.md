# Catalogue provenance

Reviewed 21 September 2026. The catalogue contains game names, numerical
requirements and authored instructions. It does not bundle copied guide articles
or images. Individual stages retain their reference URL in the in-game UI.

- [Zodiac](https://ffxiv.consolegameswiki.com/wiki/Zodiac_Weapons), [starter quests](https://ffxiv.consolegameswiki.com/wiki/Zodiac_Weapons/Quest), [Animus books](https://ffxiv.consolegameswiki.com/wiki/Animus_Zodiac_Weapons/Quest), [Novus](https://ffxiv.consolegameswiki.com/wiki/Novus_Zodiac_Weapons/Quest), [Zodiac Braves](https://ffxiv.consolegameswiki.com/wiki/Zodiac_Braves_Weapons/Quest), [Zeta](https://ffxiv.consolegameswiki.com/wiki/Zodiac_Zeta_Weapons/Quest).
- Individual book target tables: Book of Skyfire I/II, Netherfire I, Skyfall I/II, Netherfall I, Skywind I/II and Skyearth I on the same wiki.
- [Anima](https://ffxiv.consolegameswiki.com/wiki/Anima_Weapons), [enhancement points](https://ffxiv.consolegameswiki.com/wiki/Reconditioned_Anima_Weapons/Quest).
- [Eureka](https://ffxiv.consolegameswiki.com/wiki/Eurekan_Weapons).
- [Resistance](https://ffxiv.consolegameswiki.com/wiki/Resistance_Weapons).
- [Manderville](https://ffxiv.consolegameswiki.com/wiki/Manderville_Weapons).
- [Phantom](https://ffxiv.consolegameswiki.com/wiki/Phantom_Weapons), [Knowledge Crystal](https://ffxiv.consolegameswiki.com/wiki/Phantom_Weapons_Occultum/Quest).
- [Extracted game sheets](https://github.com/xivapi/ffxiv-datamining/tree/master/csv/en): Item.csv, EventItem.csv and Achievement.csv, used to verify names and map explicit achievements. Game data and names belong to Square Enix.
- [Dalamud sample project](https://github.com/goatcorp/SamplePlugin), [Dalamud SDK](https://github.com/goatcorp/Dalamud.NET.Sdk), and [FFXIVClientStructs](https://github.com/aers/FFXIVClientStructs), used for API compatibility and read-only game structure definitions.

`tools/build_catalog.py` reads cached reference HTML from `/tmp/relic-<page>.html`
(slashes in page names become hyphens). Phantom_Weapons uses the local alias
`/tmp/relic-phantom.html`. It imports factual weapon and objective tables and
combines them with authored instructions. No source HTML is distributed.

`tools/build_achievements.py` reads `/tmp/relic-Achievement.csv` and the catalogue.
It only considers descriptions starting with “Obtain”, rejects disjunctions and
multiple-job matches, and requires every PLD component. A reviewed generated
mapping is embedded. Runtime also verifies the expected achievement name for
each ID; mismatches are reported instead of silently used.

Future game patches can change requirements and data schemas. Update and review
the catalogue, rebuild against the appropriate SDK, and re-run the tests before
distributing an update. Manual tracking remains available for unmatched names.

## Allagan Tools integration (0.1.0.3)

Contract reviewed from the maintainer's source:
- [InventoryTools IPCService.cs](https://github.com/Critical-Impact/InventoryTools/blob/70a9f410d9a8ab97bef6bcf795415ea2d7c75097/InventoryTools/IPC/IPCService.cs): `IsInitialized`, `CurrentCharacter`, `GetCharactersOwnedByActive`, `GetCharacterItems`.
- [CriticalCommonLib InventoryItem.cs](https://github.com/Critical-Impact/CriticalCommonLib/blob/34d364ea938e585b4f7eeab5b36e4261fdd817d0/Models/InventoryItem.cs): numeric IPC record layout (`ToNumeric`).
- [InventoryType.cs](https://github.com/Critical-Impact/CriticalCommonLib/blob/34d364ea938e585b4f7eeab5b36e4261fdd817d0/Enums/InventoryType.cs): storage identifiers.

The plugin uses Dalamud IPC subscribers; it does not bundle Allagan Tools or
CriticalCommonLib binaries. Only saddlebag (4000/4001/4100/4101) and retainer bag
(10000–10006) records are accepted, scoped to the provider's active owner set.
The character ID is checked before and after the query. All carried counts come
from the existing native scan. Unknown/unvisited inventories cannot be inferred
from IPC and are identified in the UI.

## Shopping purchase costs (0.1.0.4)

Explicit per-item prices in `Core/ShoppingList.cs` are derived from the existing
catalogue's acquisition instructions. The first listed route is used for estimates;
alternatives are not additive. Spot checks during this update:
- [Pneumite, official Eorzea Database](https://na.finalfantasyxiv.com/lodestone/playguide/db/item/3416aa8b1c6/).
- [Zodiac Braves acquisition table](https://ffxiv.consolegameswiki.com/wiki/Zodiac_Braves_Weapons/Quest): distinguish per-unit prices from four-item totals.
- [Phantom Penumbrae](https://ffxiv.consolegameswiki.com/wiki/Phantom_Weapons_Penumbrae/Quest): Arcanite purchase route.
- [Rroneek Glue](https://ffxiv.consolegameswiki.com/wiki/Rroneek_Glue): shared vendor material.

These estimates exclude market pricing, crafting recipe expansion, variable
upgrade quantities, currency already owned and non-material objective purchases.

## Visual inspiration (0.1.0.6)

[Lightless UI colors](https://github.com/defnotken/LightlessClient/blob/master/LightlessSync/UI/UIColors.cs)
and [compact UI](https://github.com/defnotken/LightlessClient/blob/master/LightlessSync/UI/CompactUI.cs)
informed the lavender/cyan accents and compact status/navigation treatment.
Relic Atlas's theme and layout implementation are authored for this plugin;
Lightless code, logos and assets are not included.

## Solution Nine-inspired styling (0.1.0.9)

Visual reference: [Solution Nine concept art and Dawntrail zone imagery](https://www.icy-veins.com/ffxiv/dawntrail-new-zones).
Used the dark architecture, angular silhouettes and cyan/magenta lighting as
inspiration. All header artwork, panel frames and gauges are locally drawn
original vectors; no screenshots or game assets ship with the plugin.

## Interface typography (0.1.0.12)

Noto Sans Medium is copied unmodified from the installed `noto-fonts` distribution
and embedded as `RelicAtlas.HeadingFont`. The font metadata specifies SIL Open Font License 1.1. Its embedded copyright
notice and the standard OFL 1.1 text are preserved in `Assets/FONT-LICENSE.txt` and included as `FONT-LICENSE.txt` in the
release package. UI graphics are original procedural geometry, without game assets.
Managed font atlas calls were checked against the installed Dalamud API 15 XML
documentation, including fallback behavior for unavailable font handles.

## Visual assets (0.1.0.13)

The original generated background and complete prompt are documented in
`Assets/ARTWORK.md`. The transparent logo is generated separately; its prompt is in
`Assets/LOGO-PROMPT.md`. Job icons
use the game's 62000 + ClassJob row ID set, and weapon icons use the English Item
sheet's exact-name matches against the catalogue. Missing textures retain labels.
The plugin obtains images through Dalamud's shared texture provider and does not
own or dispose those borrowed wraps.

A small set of game icons was retrieved from XIVAPI solely for the ignored local
preview harness, following its [asset documentation](https://v2.xivapi.com/docs/guides/assets/).
Those downloaded icons are not in the plugin package. No XIVAPI calls are made
by the plugin at runtime.

## ARR Atma schedule and Teleporter (0.1.0.18, checked 24 September 2026)

- [Kaze Ridingstory's original Lodestone post](https://jp.finalfantasyxiv.com/lodestone/character/5360731/blog/1049228/): player hypothesis, twelve repeating hourly zones, optional 15-minute lead. The author confirms real-world time in a comment but does not explicitly establish JST. Relic Atlas assumes JST and labels the route unverified.
- [Square Enix's ST clarification](https://forum.square-enix.com/ffxiv/threads/143918): ST displays UTC regardless of data centre or World.
- [NICT's Japan Standard Time explanation](https://www.nict.go.jp/en/sts/jst.html): JST is UTC+9. The supplied ST conversion was checked against the post's full schedule.
- [Teleporter IPC provider](https://github.com/pohky/TeleporterPlugin/blob/20a9235830e1415e79ddef0c3e7ccf5a0cd52b79/TeleporterPlugin/Managers/IpcManager.cs), revision `20a9235830e1415e79ddef0c3e7ccf5a0cd52b79`: `Teleport`, `(uint aetheryteId, byte subIndex) -> bool`. The provider checks unlocked destinations and teleport action availability and returns the native request result. Relic Atlas consumes the interface; no Teleporter implementation is bundled or modified.

ST comes from FFXIVClientStructs `Framework.GetServerTime`; computer UTC is only
a labelled display fallback and cannot drive automatic travel. The installed
Dalamud API 15 source/XML was checked for IPC `HasFunction`, `IAetheryteList`,
condition sets and player state. Aetheryte IDs/names are resolved from guarded
English game-sheet references and cross-checked against the twelve territory
names. Only entries in the current character's unlocked teleport list are used.
Zenith main-hand IDs come from exact catalogue names in the English Item sheet.
FFXIVClientStructs `FateManager.GetCurrentFateId` guards active FATEs. All calls
that request travel execute on the framework thread; there are no runtime HTTP calls.

## Relic tools (0.1.0.21, checked 27 September 2026)

- [Mastercraft / Supra / Lucis](https://ffxiv.consolegameswiki.com/wiki/Mastercraft_Tools)
- [Skysteel](https://ffxiv.consolegameswiki.com/wiki/Skysteel_Tools), [Flintstrike](https://ffxiv.consolegameswiki.com/wiki/Flintstrike), [Pickled Pom](https://ffxiv.consolegameswiki.com/wiki/Pickled_Pom)
- [Resplendent](https://ffxiv.consolegameswiki.com/wiki/Resplendent_Tools)
- [Splendorous](https://ffxiv.consolegameswiki.com/wiki/Splendorous_Tools)
- [Cosmic](https://ffxiv.consolegameswiki.com/wiki/Cosmic_Tool)
- [Game sheets, revision d71de329](https://github.com/xivapi/ffxiv-datamining/tree/d71de329cc6ed30c6fb16b9108cdf7d29c653302/csv/en): Item, Quest, Achievement, WKSCosmoToolClass and WKSCosmoToolDataAmount. Cosmic names and cumulative thresholds come directly from these sheets; the reference wiki's obsolete Needle/Round Knife prototype names are not used. The Crystalline Weaver high collectability tier is 1100, correcting the wiki summary's inconsistent 660 entry.
- [WKSResearchModule](https://github.com/aers/FFXIVClientStructs/blob/6adf262b97e61506b3c7d35edb0e081d4b4e1bd2/FFXIVClientStructs/FFXIV/Client/Game/WKS/WKSResearchModule.cs) and [WKSManager](https://github.com/aers/FFXIVClientStructs/blob/6adf262b97e61506b3c7d35edb0e081d4b4e1bd2/FFXIVClientStructs/FFXIV/Client/Game/WKS/WKSManager.cs): loaded-state guards and the 11-class × 7-data-type analysis layout. No signature scans or custom memory layouts are added.

`tools/build_tools_catalog.py` lists its cached input names and validates every
tool/material name, HQ eligibility and shared quest name against the game sheets.
It can rebuild only tool collections without altering combat save keys. Raw
reference HTML/CSV files are not distributed. Instructions are authored from
factual exchanges, not copied guide prose. Shopping lists direct turn-in items;
recipe ingredients and variable yields remain source guidance, not additive
mandatory requirements. Generic ARR Lucis achievements never complete all jobs.

## Achievement progress and claimed tool rewards — 0.1.0.22

- [Achievement progress API](https://github.com/aers/FFXIVClientStructs/blob/6adf262b97e61506b3c7d35edb0e081d4b4e1bd2/FFXIVClientStructs/FFXIV/Client/Game/UI/Achievement.cs): request state, response ID/current/maximum and completion-history readiness.
- [Upstream reward-state discussion](https://github.com/aers/FFXIVClientStructs/pull/1890#discussion_r3725453350) and [named functions](https://github.com/aers/FFXIVClientStructs/blob/6adf262b97e61506b3c7d35edb0e081d4b4e1bd2/ida/data.yml): the unmerged reward-map description was independently checked against client 2026.09.15.0000.0000 before use.
- [Official achievement reward guide](https://na.finalfantasyxiv.com/uiguide/faq/faq-other/achieve_exchange.html): completion permits a separate reward claim in the Achievements window.

The native `MatchesAgentState` getter checks one bit of the 26-byte reward map.
Both Achievement agent call sites use Achievement.Unknown1 as an unclaimed-when-set
index or Unknown2 as an unclaimed-when-clear index, with 255 meaning absent.
The resulting UI flag is separate from achievement completion; the addon disables
the reward icon when completed and no longer unclaimed. The optional reader matches
the full getter signature and calls it read-only, after checking loaded history
and completion. Ambiguous or out-of-range indices are rejected. No game binaries,
disassembly, player data or third-party source copies are distributed.

Reward items are matched to catalogue tools from the live sheet: achievements
2830/2831/2832 reward items 33356/33357/33358 respectively, with Unknown2 bits
172/173/174. This is independent of the 483 achievements which prove acquisition
by requiring an explicitly named tool or weapon. Partial discovery queries apply
to the three objective achievements and never establish reward collection.

## Background ARR book tracking (0.1.0.24)

Reviewed 30 September 2026 against the installed game sheets and API 15 SDK.
`Relic.ItemAtma` maps the native relic ID to the catalogue job (including separate
Paladin sword/shield IDs). `RelicNote.EventItem` corroborates the held key item;
its monster, dungeon, FATE and leve references bind the native slots to the
existing requirement IDs. All nine books have 20 unambiguous bindings, including
purchase, and all monster targets require three kills. Thirty catalogue labels
include an exact `x3` suffix; matching accepts that suffix without changing saved
keys or relying on catalogue order.

[FFXIVClientStructs RelicNote](https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Client/Game/UI/RelicNote.cs)
defines the book/relic IDs, ten monster counters and dungeon/FATE/leve completion
bits. [UIState](https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Client/Game/UI/UIState.cs)
holds this state independently of the book addon. There is no documented native
owner or loaded flag for this structure. The reader therefore uses the loaded
player identity, loaded inventory, matching key item and two consecutive identity
observations, resetting its session checks on logout, transitions and paused
detection. Runtime lifecycle timing still requires live-client verification.

## Zodiac book destinations and travel (0.1.0.25)

The embedded `Data/book-locations.json` preserves the catalogue's existing
monster and FATE coordinates. Dungeon destinations are overworld entrance
markers from the installed game's Map/MapMarker sheets, not dungeon interior
coordinates. Leve destinations use Leve.LevelLevemete → Level → ENpcResident
to identify the actual issuer. The book vendor uses the game's Level placement.
The 180 entries cover 90 monster, 27 dungeon, 27 FATE, 27 leve and nine purchase
requirements. Only the 171 activity objectives appear in the held-book guide.

All destinations were checked against the installed TerritoryType/Map sheets
and the runtime aetheryte resolver: the English zone name, territory and map
match, and every destination map has at least one resolvable aetheryte (20
aetherytes across 15 maps). Actual availability comes from the character's
unlocked aetherytes, not that static coverage check. Level coordinates use
Dalamud's MapUtil.WorldToMap; MapMarker positions use the documented
[map texture coordinate conversion](https://github.com/xivapi/ffxiv-datamining/blob/master/docs/MapCoordinates.md).
Closest destinations use straight-line map distance and do not calculate terrain
or walking paths. MapLinkPayload plus IGameGui.OpenMapWithMapLink places flags;
Teleporter's existing `Teleport(uint, byte) → bool` IPC requests teleportation.

The guide reads detected objective counters independently of manual checklist
corrections. Travel uses only a currently confirmed held-book snapshot and
revalidates the character, relic ID, book ID and objective at execution.
Mounted, riding-pillion (the current name for Mounted2) and InFlight condition
flags are allowed by the shared Atma/book teleport gate; other busy flags remain
blocking. Native cast acceptance, arrival flag behaviour and in-game rendering
still require client validation.


## ARR objective readers (2 October 2026)

- Installed English `Relic3` rows map all 11 sphere-scroll items to their Novus
  weapons and capacities (75, or 53/22 for Paladin). The native
  `RelicSphereScroll` arguments provide inventory container and slot at 4/5,
  the item name at 3, and infusion count/capacity at 10/11. Container/slot,
  held item, localized title, numeric types and capacity are checked. A live
  Bravura observation confirmed 72/75; the counter is retained after closing.
- Installed client relic checks read `InventoryItem.GetFlags()`'s Relic bit
  and `GetSpiritbondOrCollectability()`. Novus uses 0–2000; the flag is its
  soulglaze state. Zodiac/Mahatma work uses an index stride of 500 and an
  80-unit current stone, finishing at 5580. The UI normalizes the native
  current-stone value to the catalogue's existing 40-point scale. Only exact
  catalogue weapon names in the corresponding tiers are eligible. Ordinary
  spiritbond and other relic families are not treated as ARR light.
- Structure access follows the installed
  [FFXIVClientStructs InventoryItem](https://github.com/aers/FFXIVClientStructs/blob/main/FFXIVClientStructs/FFXIV/Client/Game/InventoryItem.cs)
  and QuestManager definitions. No fixed executable addresses are used at runtime.
- All ten non-repeatable A Relic Reborn quests were matched by job, title and
  verified ToDoCompleteSeq layout. Only passed journal milestones are credited;
  group kills are not estimated. The four repeatable Zodiac material quests
  use their verified delivery sequences and held rewards: Book of Skylight,
  Zodium, Zodiac Scroll and Flawless Alexandrite. Their historical completion
  flags are deliberately not used as fresh progress.
- Soulglazing duty notes were checked against the
  [Nexus quest reference](https://ffxiv.consolegameswiki.com/wiki/Nexus_Zodiac_Weapons/Quest)
  and [Icy Veins Nexus guide](https://www.icy-veins.com/ffxiv/nexus-relic-weapons).
  Recommendations distinguish normal yields and observed two-hour bonuses;
  they do not predict which duty is currently on bonus.

## Anima progress audit — 3 October 2026

The installed Quest sheet and quest scripts were used to validate accepted-job
objectives and sequence boundaries for Toughening Up (67749), A Dream Fulfilled
(67864), Born Again Anima (67932) and Best Friends Forever (67940). Runtime
validates names, repeatability and expected objective sequences before binding.
Item rows validate the seven exchange products. Private-use quest icons are
removed for name matching. Generational Bonding is quest 70255.

Exchange quantities were cross-checked against the [Anima quest guide](https://ffxiv.consolegameswiki.com/wiki/Anima_Weapons/Quest)
and [Complete Anima guide](https://ffxiv.consolegameswiki.com/wiki/Complete_Anima_Weapons/Quest).
The native Relic2Glass display was inspected locally to identify density and
inventory-slot arguments; the reader validates the visible window, value types,
held weapon, localized title and active quest owner. No executable addresses,
game binaries or extracted quest text are distributed.

Anima shopping estimates use the 60-item maximum in the official [patch 6.0 notes](https://na.finalfantasyxiv.com/lodestone/topics/detail/bdd208b52ddababad086dc9679e96a8412962edf) and the conversion/Umbrite cost in the [Reconditioned quest guide](https://ffxiv.consolegameswiki.com/wiki/Reconditioned_Anima_Weapons/Quest), checked 3 October 2026.

The live AWMakingSpiritGrow window uses the Relic2Growth layout and a 111-value
allocation payload. Proposed/committed totals (29/30), limit (31) and per-stat
pairs (42/43 with stride seven, a second group for PLD) were checked against the
native layout reader and the live 36/180 display. The production reader uses SDK
AtkValue fields and active-quest ownership, with no raw executable addresses.

The 4 October density reader traces the installed client’s GetAnimaWeapon7EnhancePoint
Lua binding to the same ushort state getter used by the Anima Glass. Native
character-data initialization and server updates write that state even when the
glass is closed. A unique glass call-site signature and exact getter-body check
validate the field layout; only the shared value is read. Runtime quest/job and
weapon checks prevent attributing it to a different relic.
