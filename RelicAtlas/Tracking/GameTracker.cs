using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.Sheets;
using RelicAtlas.Core;
using Action = System.Action;

namespace RelicAtlas.Tracking;

public sealed class GameTracker : IDisposable
{
    private readonly Configuration config;
    private readonly Catalog catalog;
    private readonly IClientState client;
    private readonly IPlayerState player;
    private readonly ICondition condition;
    private readonly IFramework framework;
    private readonly IPluginLog log;
    private readonly IGameGui gui;
    private readonly IObjectTable objects;
    private readonly List<RelicQuestBinding> relicQuestBindings = [];
    private readonly Dictionary<byte,string> jobAbbreviations = [];
    private readonly Dictionary<uint,AnimaGlassWeapon> animaGlassWeapons = [];
    private readonly AnimaDensityReader animaDensity;
    private readonly Action save;
    private readonly AllaganToolsInventory allagan;
    private readonly Dictionary<uint, string> materialIds = [];
    private readonly Dictionary<uint, string> itemNames = [];
    private readonly Dictionary<uint, List<string>> sharedQuests = [];
    private readonly Dictionary<uint, string> arrRelicQuests = [];
    private readonly Dictionary<uint, string> zodiacMaterialQuests = [];
    private readonly Dictionary<uint, string> eventItemNames = [];
    private readonly Dictionary<uint, string> relicJobs = [];
    private readonly Dictionary<uint, ArrBookDefinition> books = [];
    private readonly HashSet<uint> bookItemIds = [];
    private readonly ArrBookTracking bookTracking;
    private readonly Dictionary<uint, ArrScrollDefinition> scrolls = [];
    private readonly Dictionary<uint, string> localizedScrollNames = [];
    private readonly Dictionary<string, (string Job, string Stage, int Component)> lightWeapons = [];
    private readonly ArrScrollTracking scrollTracking;
    public string ScrollStatus { get; private set; } = "Waiting for sphere-scroll data.";
    private readonly List<AchievementEvidence> achievements = [];
    private readonly List<(Series Series, Stage Stage, Requirement Requirement)> objectiveAchievements = [];
    private readonly AchievementProgress achievementProgress;
    private readonly AchievementRewards achievementRewards;
    private DateTime nextAchievementAttempt;
    public bool AchievementsLoaded { get; private set; }
    private readonly HashSet<string> materialNames = [];
    private DateTime nextScan;
    private ulong inventoryOwner;
    private ulong lastCharacter;
    public ulong CurrentId => client.IsLoggedIn && player.IsLoaded ? player.ContentId : 0;
    public uint CurrentTerritoryId => CurrentId != 0 ? client.TerritoryType : 0;
    public string CurrentJob => player.IsLoaded && player.ClassJob.TryGetValue(out var job) ? job.Abbreviation.ExtractText() : "";
    public IReadOnlyDictionary<string, int>? InventoryFor(ulong id) =>
        config.Automatic && id != 0 && id == inventoryOwner && id == CurrentId ? inventory : null;
    private Dictionary<string, int>? inventory;
    public List<string> Unresolved { get; } = [];
    public string Status { get; private set; } = "Waiting for character data";
    public DateTime? LastScan { get; private set; }
    public string StorageStatus => allagan.Status;
    public string BookStatus => !config.Automatic ? "ARR book detection paused" : bookTracking.Status;
    public bool BookIsCurrent => config.Automatic && CurrentId != 0 && lastCharacter == CurrentId &&
        bookTracking.IsCurrent && LastScan is { } scan && DateTime.UtcNow - scan < TimeSpan.FromSeconds(6);
    public ExternalInventory? StorageFor(ulong id) => config.Automatic && id != 0 && id == CurrentId &&
        allagan.Snapshot?.Owner == id ? allagan.Snapshot : null;

    public GameTracker(Configuration config, Catalog catalog, IClientState client, IPlayerState player,
        ICondition condition, IFramework framework, IDataManager data, IPluginLog log, IGameGui gui, Action save,
        AllaganToolsInventory allagan, ISigScanner scanner, IObjectTable objects)
    {
        this.config = config; this.catalog = catalog; this.client = client; this.player = player;
        this.condition = condition; this.framework = framework; this.log = log; this.save = save;
        this.gui = gui;
        animaDensity = new AnimaDensityReader(scanner, log);
        this.objects = objects;
        foreach (var job in data.GetExcelSheet<ClassJob>(ClientLanguage.English))
            if (job.RowId <= byte.MaxValue) jobAbbreviations[(byte)job.RowId] = job.Abbreviation.ExtractText();
        foreach (var binding in QuestObjectiveProgress.Bindings(catalog))
        {
            var sheet = data.GetExcelSheet<Quest>(ClientLanguage.English);
            if (sheet.TryGetRow(binding.QuestId,out var row) && row.IsRepeatable &&
                QuestObjectiveProgress.NormalizeName(row.Name.ExtractText()) == QuestObjectiveProgress.NormalizeName(binding.Name) &&
                binding.Objectives.All(o => o.Todo < row.TodoParams.Count && row.TodoParams[o.Todo].ToDoCompleteSeq == o.Sequence))
                relicQuestBindings.Add(binding);
            else Unresolved.Add("Quest objectives: " + binding.Name);
        }
        this.allagan = allagan;
        var names = catalog.Series.SelectMany(s => s.Stages).SelectMany(s => s.Weapons.Values).SelectMany(x => x).ToHashSet();
        foreach (var r in catalog.Series.SelectMany(s => s.Stages).SelectMany(s => s.Requirements))
            if (r.Item.Length > 0) { names.Add(r.Item); materialNames.Add(r.Item); }
        foreach (var quest in ArrZodiacMaterials.Quests) { names.Add(quest.Reward); materialNames.Add(quest.Reward); }
        foreach (var exchange in AnimaExchangeProgress.Exchanges) { names.Add(exchange.Product); materialNames.Add(exchange.Product); }
        foreach (var item in AnimaEnhancementMaterials.Items) { names.Add(item); materialNames.Add(item); }
        var lookup = names.GroupBy(Normalize).ToDictionary(g => g.Key, g => g.ToList());
        var resolved = new HashSet<string>();
        foreach (var item in data.GetExcelSheet<Item>(ClientLanguage.English))
        {
            var name = Normalize(item.Name.ExtractText());
            if (!lookup.TryGetValue(name, out var canonical) || canonical.Count != 1) continue;
            itemNames[item.RowId] = canonical[0]; resolved.Add(canonical[0]);
        }
        foreach (var item in data.GetExcelSheet<EventItem>(ClientLanguage.English))
            if (lookup.TryGetValue(Normalize(item.Name.ExtractText()), out var canonical) && canonical.Count == 1)
            { eventItemNames[item.RowId] = canonical[0]; resolved.Add(canonical[0]); }
        Unresolved.AddRange(names.Except(resolved).Order());
        foreach (var (itemId, name) in itemNames)
            if (materialNames.Contains(name)) materialIds[itemId] = name;
        var sharpened = catalog.Series.Single(s => s.Id == "hw").Stages.Single(s => s.Id == "sharpened");
        foreach (var (itemId,name) in itemNames)
        foreach (var (job,weapons) in sharpened.Weapons)
            if (weapons[0] == name) animaGlassWeapons[itemId] = new(job,data.GetExcelSheet<Item>().GetRow(itemId).Name.ExtractText());
        var quests = catalog.Series.SelectMany(s => s.Stages).SelectMany(s => s.Requirements)
            .Where(r => r.Quest.Length > 0 && r.Shared.Length > 0).GroupBy(r => Normalize(r.Quest))
            .ToDictionary(g => g.Key, g => g.Select(r => r.Shared).Distinct().ToList());
        var foundQuests = new HashSet<string>();
        foreach (var quest in data.GetExcelSheet<Quest>(ClientLanguage.English))
            if (!quest.IsRepeatable && quests.TryGetValue(QuestObjectiveProgress.NormalizeName(quest.Name.ExtractText()), out var keys))
            { sharedQuests[quest.RowId] = keys; foundQuests.Add(QuestObjectiveProgress.NormalizeName(quest.Name.ExtractText())); }
        Unresolved.AddRange(quests.Keys.Except(foundQuests).Select(q => "Quest: " + q));
        var achievementSheet = data.GetExcelSheet<Achievement>(ClientLanguage.English);
        foreach (var evidence in AchievementEvidence.Load())
        {
            if (achievementSheet.TryGetRow(evidence.Id, out var row) && Normalize(row.Name.ExtractText()) == Normalize(evidence.Name))
                achievements.Add(evidence);
            else Unresolved.Add("Achievement: " + evidence.Name);
        }
        foreach (var series in catalog.Series)
        foreach (var stage in series.Stages)
        foreach (var requirement in stage.Requirements.Where(r => r.Achievement != 0))
        {
            if (achievementSheet.TryGetRow(requirement.Achievement, out var row) && Normalize(row.Name.ExtractText()) == Normalize(requirement.AchievementName))
                objectiveAchievements.Add((series, stage, requirement));
            else Unresolved.Add("Achievement: " + requirement.AchievementName);
        }
        achievementProgress = new(objectiveAchievements);
        achievementRewards = new(catalog, data, scanner, log);
        try
        {
            ResolveBooks(data);
        }
        catch (Exception ex)
        {
            // A changed optional sheet must not prevent the entire tracker from loading.
            relicJobs.Clear();
            books.Clear();
            Unresolved.Add("ARR book auto-detection unavailable; use the manual book checklists.");
            log.Warning(ex, "Relic Atlas could not resolve optional ARR book data.");
        }
        bookTracking = new(catalog.Series.Single(s => s.Id == "arr"), relicJobs, books, bookItemIds);
        var arr = catalog.Series.Single(s => s.Id == "arr");
        foreach (var stage in arr.Stages.Where(s => s.Id is "novus" or "zodiac-braves"))
        foreach (var job in arr.Jobs)
        for (var component = 0; component < stage.Weapons[job].Count; component++)
            lightWeapons.Add(stage.Weapons[job][component],(job,stage.Id,component));
        try
        {
            int[] expectedSequences = [1,2,3,4,5,6,7,8,9,10,10,10,11,12,13,14,15,16,17,18,255];
            var first = arr.Stages.Single(s => s.Id == "relic");
            foreach (var quest in data.GetExcelSheet<Quest>(ClientLanguage.English))
            {
                var questName = quest.Name.ExtractText();
                if (quest.IsRepeatable && ArrZodiacMaterials.Quests.SingleOrDefault(q => QuestObjectiveProgress.NormalizeName(q.Name) == QuestObjectiveProgress.NormalizeName(questName)) is { } materialQuest)
                    zodiacMaterialQuests.Add(quest.RowId,materialQuest.Name);
                if (quest.IsRepeatable) continue;
                var job = arr.Jobs.SingleOrDefault(j => quest.Name.ExtractText() == $"A Relic Reborn ({first.Weapons[j][0]})");
                if (job == null || quest.ClassJobRequired.Value.Abbreviation.ExtractText() != job) continue;
                if (quest.TodoParams.Count < expectedSequences.Length || expectedSequences.Where((value, i) => quest.TodoParams[i].ToDoCompleteSeq != value).Any()) continue;
                arrRelicQuests.Add(quest.RowId, job);
            }
            if (arrRelicQuests.Count != arr.Jobs.Count) Unresolved.Add("Some initial ARR relic quests could not be matched.");
        }
        catch (Exception ex)
        {
            arrRelicQuests.Clear();
            log.Warning(ex, "Relic Atlas could not resolve initial ARR relic quest objectives.");
        }
        try
        {
            var novus = arr.Stages.Single(s => s.Id == "novus");
            foreach (var row in data.GetExcelSheet<Relic3>(ClientLanguage.English))
            {
                var name = Normalize(BookRowNames.Item(row.ItemNovus));
                foreach (var job in arr.Jobs)
                {
                    var part = novus.Weapons[job].FindIndex(n => Normalize(n) == name);
                    if (part >= 0 && row.ItemScroll.RowId != 0 && row.MateriaLimit > 0)
                    {
                        scrolls.Add(row.ItemScroll.RowId, new(row.ItemScroll.RowId, job, part, row.MateriaLimit));
                        localizedScrollNames.Add(row.ItemScroll.RowId,data.GetExcelSheet<Item>().GetRow(row.ItemScroll.RowId).Name.ExtractText());
                    }
                }
            }
            if (scrolls.Count != 11 || arr.Jobs.Any(j => scrolls.Values.Where(d => d.Job == j).Sum(d => d.Limit) != 75))
                throw new InvalidOperationException("Incomplete ARR scroll mapping.");
        }
        catch (Exception ex)
        {
            scrolls.Clear();
            Unresolved.Add("ARR sphere-scroll detection unavailable.");
            log.Warning(ex, "Relic Atlas could not resolve ARR sphere scrolls.");
        }
        scrollTracking = new(arr, scrolls);
        framework.Update += OnUpdate;
    }

    private void ResolveBooks(IDataManager data)
    {
        var arr = catalog.Series.Single(s => s.Id == "arr");
        var atma = arr.Stages.Single(s => s.Id == "atma");
        var animus = arr.Stages.Single(s => s.Id == "animus");
        foreach (var relic in data.GetExcelSheet<Relic>(ClientLanguage.English))
        {
            var name = Normalize(BookRowNames.Item(relic.ItemAtma));
            if (name.Length == 0) continue;
            var job = arr.Jobs.SingleOrDefault(j => atma.Weapons[j].Any(n => Normalize(n) == name));
            if (job != null) relicJobs[relic.RowId] = job;
        }
        foreach (var note in data.GetExcelSheet<RelicNote>(ClientLanguage.English))
        {
            var group = Normalize(BookRowNames.EventItem(note.EventItem));
            if (group.Length == 0) continue;
            var objectives = animus.Requirements.Where(r => Normalize(r.Group) == group).ToArray();
            if (objectives.Length == 0) continue;
            bookItemIds.Add(note.EventItem.RowId);
            var bindings = new List<ArrBookObjective>();
            void Match(string name, ArrBookObjectiveKind kind, int index)
            {
                if (name.Length == 0) return;
                var matches = objectives.Where(r => kind == ArrBookObjectiveKind.Dungeon
                    ? Normalize(r.Detail).StartsWith("defeat " + Normalize(name) + " with ", StringComparison.Ordinal)
                    : kind == ArrBookObjectiveKind.Monster ? ArrBookMatching.MonsterMatches(r, name)
                    : Normalize(r.Label) == Normalize(name)).ToArray();
                if (matches.Length == 1) bindings.Add(new(matches[0], kind, index));
            }
            for (int i = 0; i < note.MonsterNoteTargetCommon.Count; i++)
                Match(BookRowNames.Monster(note.MonsterNoteTargetCommon[i]), ArrBookObjectiveKind.Monster, i);
            for (int i = 0; i < note.MonsterNoteTargetNM.Count; i++)
                Match(BookRowNames.Monster(note.MonsterNoteTargetNM[i]), ArrBookObjectiveKind.Dungeon, i);
            for (int i = 0; i < note.Fate.Count; i++) Match(BookRowNames.Fate(note.Fate[i]), ArrBookObjectiveKind.Fate, i);
            for (int i = 0; i < note.Leve.Count; i++) Match(BookRowNames.Leve(note.Leve[i]), ArrBookObjectiveKind.Leve, i);
            var purchase = objectives.SingleOrDefault(r => r.Label.StartsWith("Purchase ", StringComparison.Ordinal));
            if (purchase != null) bindings.Add(new(purchase, ArrBookObjectiveKind.Purchase, 0));
            // A partial/name-ambiguous mapping must never write the wrong objective.
            if (note.MonsterNoteTargetCommon.Count != 10 || note.MonsterNoteTargetNM.Count != 3 ||
                note.Fate.Count != 3 || note.Leve.Count != 3 || objectives.Length != 20 ||
                bindings.Count != 20 || bindings.Select(b => b.Requirement.Id).Distinct().Count() != 20)
            {
                Unresolved.Add("ARR book objectives: " + BookRowNames.EventItem(note.EventItem));
                continue;
            }
            books[note.RowId] = new(note.RowId, note.EventItem.RowId, purchase!.Group, bindings);
        }
    }

    private unsafe bool ReadBook(ulong id, CharacterProgress character, InventoryManager* manager)
    {
        var keyItems = manager->GetInventoryContainer(InventoryType.KeyItems);
        HashSet<uint>? held = null;
        if (keyItems != null && keyItems->IsLoaded)
        {
            held = [];
            for (var i = 0; i < keyItems->Size; i++)
            {
                var item = keyItems->GetInventorySlot(i);
                if (item != null && item->Quantity > 0 && bookItemIds.Contains(item->ItemId)) held.Add(item->ItemId);
            }
        }
        var note = FFXIVClientStructs.FFXIV.Client.Game.UI.RelicNote.Instance();
        ArrBookReading? reading = null;
        if (note != null)
        {
            var monsters = new int[10];
            for (var i = 0; i < monsters.Length; i++) monsters[i] = note->GetMonsterProgress(i);
            reading = new(note->RelicId, note->RelicNoteId, monsters, note->ObjectiveProgress);
        }
        return bookTracking.Update(id, character, reading, held, DateTime.UtcNow);
    }

    public static string Normalize(string value) => ArrBookMatching.Normalize(value);
    public void Rescan() { nextScan = DateTime.MinValue; allagan.RefreshSoon(); }

    private void OnUpdate(IFramework _)
    {
        var id = CurrentId;
        if (id == 0 || condition[ConditionFlag.BetweenAreas] || condition[ConditionFlag.BetweenAreas51])
        {
            inventory = null; inventoryOwner = 0;
            allagan.Reset();
            achievementProgress.Reset(DateTime.UtcNow);
            bookTracking.Reset();
            scrollTracking.Reset();
            lastCharacter = 0;
            AchievementsLoaded = false;
            Status = "Waiting for character data";
            return;
        }
        if (!config.Characters.TryGetValue(id, out var character))
        {
            config.Characters[id] = character = new CharacterProgress();
            character.Name = player.CharacterName + " @ " + player.HomeWorld.Value.Name.ExtractText();
            save();
        }
        if (!config.Automatic)
        {
            inventory = null; allagan.Reset(); achievementProgress.Reset(DateTime.UtcNow);
            bookTracking.Reset();
            scrollTracking.Reset();
            Status = "Automatic detection paused"; return;
        }
        if (lastCharacter != id) { inventory = null; scrollTracking.Reset(); nextScan = DateTime.MinValue; lastCharacter = id; }
        if (DateTime.UtcNow < nextScan) return;
        nextScan = DateTime.UtcNow.AddSeconds(2);
        try
        {
            Scan(id, character);
        }
        catch (Exception ex)
        {
            inventory = null; inventoryOwner = 0;
            bookTracking.Reset();
            scrollTracking.Reset();
            Status = "Detection unavailable — manual tracking still works";
            nextScan = DateTime.UtcNow.AddSeconds(30);
            log.Warning(ex, "Relic Atlas could not read progress.");
        }
    }

    private static readonly InventoryType[] Containers =
    [InventoryType.Inventory1, InventoryType.Inventory2, InventoryType.Inventory3, InventoryType.Inventory4,
     InventoryType.EquippedItems, InventoryType.ArmoryMainHand, InventoryType.ArmoryOffHand];

    private unsafe void Scan(ulong id, CharacterProgress character)
    {
        var manager = InventoryManager.Instance();
        if (manager == null) { bookTracking.Reset(); scrollTracking.Reset(); return; }
        var counts = itemNames.Values.Distinct().Where(materialNames.Contains)
            .SelectMany(n => new[] { n, n + "|HQ" }).ToDictionary(n => n, _ => 0);
        var owned = new HashSet<string>();
        var heldScrolls = new HashSet<uint>();
        var lightReadings = new List<ArrLightReading>();
        foreach (var type in Containers)
        {
            var container = manager->GetInventoryContainer(type);
            if (container == null || !container->IsLoaded) { inventory = null; bookTracking.Reset(); scrollTracking.Reset(); Status = "Waiting for inventory"; return; }
            for (int i = 0; i < container->Size; i++)
            {
                var item = container->GetInventorySlot(i);
                if (item != null && item->Quantity > 0 && scrolls.ContainsKey(item->ItemId)) heldScrolls.Add(item->ItemId);
                if (item == null || item->Quantity <= 0 || !itemNames.TryGetValue(item->ItemId, out var name)) continue;
                owned.Add(name);
                if (!item->IsSymbolic && lightWeapons.TryGetValue(name,out var light))
                    lightReadings.Add(new(light.Job,light.Stage,light.Component,item->SpiritbondOrCollectability,
                        (item->Flags & InventoryItem.ItemFlags.Relic) != 0));
                if (!materialNames.Contains(name)) continue;
                counts[name] += item->Quantity;
                if ((item->Flags & InventoryItem.ItemFlags.HighQuality) != 0) counts[name + "|HQ"] += item->Quantity;
            }
        }
        var keyItems = manager->GetInventoryContainer(InventoryType.KeyItems);
        if (keyItems != null && keyItems->IsLoaded)
        {
            foreach (var name in eventItemNames.Values.Where(materialNames.Contains).Distinct()) counts.TryAdd(name, 0);
            for (int i = 0; i < keyItems->Size; i++)
            {
                var item = keyItems->GetInventorySlot(i);
                if (item != null && item->Quantity > 0 && eventItemNames.TryGetValue(item->ItemId, out var name) && counts.ContainsKey(name))
                    counts[name] += item->Quantity;
            }
        }
        var changed = false;
        var achievementState = FFXIVClientStructs.FFXIV.Client.Game.UI.Achievement.Instance();
        AchievementsLoaded = achievementState != null && achievementState->IsLoaded();
        if (AchievementsLoaded)
            foreach (var evidence in achievements)
            {
                if (!achievementState->IsComplete((int)evidence.Id)) continue;
                var series = catalog.Series.Single(s => s.Id == evidence.Series);
                var weapon = character.Weapon(series, evidence.Job);
                if (weapon.ObservedStage >= evidence.Stage) continue;
                // Verified achievement explicitly requires this job's full weapon (both PLD components).
                weapon.ObservedStage = evidence.Stage;
                foreach (var name in series.Stages[evidence.Stage].Weapons[evidence.Job]) weapon.ObservedWeapons.Add(name);
                changed = true;
            }
        achievementRewards.AddClaimedTools(owned);
        foreach (var series in catalog.Series)
            foreach (var job in series.Jobs)
            {
                changed |= Progress.Observe(character, series, job, owned);
                // Possession proves prerequisites for the completed tier, never later tiers.
                var completed = character.Weapon(series, job).ObservedStage;
                foreach (var r in series.Stages.Take(completed + 1).SelectMany(s => s.Requirements).Where(r => r.Applies(job)))
                    if (r.Quest.Length > 0 && r.Shared.Length > 0) changed |= character.CompletedShared.Add(r.Shared);
            }
        if (AchievementsLoaded)
            foreach (var (series, stage, requirement) in objectiveAchievements)
            {
                if (!achievementState->IsComplete((int)requirement.Achievement)) continue;
                // An achievement reward may not have been claimed: complete only its objective.
                foreach (var job in series.Jobs.Where(requirement.Applies))
                {
                    var key = Progress.Key(series, job, stage, requirement);
                    if (character.DetectedCounters.GetValueOrDefault(key) == requirement.Count) continue;
                    character.DetectedCounters[key] = requirement.Count; changed = true;
                }
            }
        changed |= ReadAchievementProgress(id, character);
        try
        {
            if (animaDensity.Available)
                changed |= animaDensity.Read(id, character, catalog, jobAbbreviations, owned);
            else changed |= AnimaGlassReader.Read(gui,character,animaGlassWeapons,jobAbbreviations);
            // Quest completion is stronger evidence than a pending density refresh.
            changed |= QuestObjectiveReader.Read(catalog,character,relicQuestBindings,jobAbbreviations,objects);
            changed |= AnimaGrowthReader.Read(gui,character,catalog,jobAbbreviations,CurrentJob,owned);
        }
        catch (Exception ex) { log.Warning(ex,"Relic Atlas could not read optional Anima progress; saved values are retained."); }
        changed |= ArrLightProgress.Apply(catalog.Series.Single(s => s.Id == "arr"),character,lightReadings);
        foreach (var (quest, keys) in sharedQuests)
            if (QuestManager.IsQuestComplete(quest))
                foreach (var key in keys) changed |= character.CompletedShared.Add(key);
        var questManager = QuestManager.Instance();
        if (questManager != null)
        {
            var arr = catalog.Series.Single(s => s.Id == "arr");
            foreach (var (quest, job) in arrRelicQuests)
            {
                var complete = QuestManager.IsQuestComplete(quest);
                var work = questManager->GetQuestById((ushort)(quest & 0xffff));
                if (complete || work != null)
                    changed |= ArrRelicQuestProgress.Apply(arr, character, job, work == null ? (byte)0 : work->Sequence, complete);
            }
            if (keyItems != null && keyItems->IsLoaded && zodiacMaterialQuests.Count == 4 &&
                ArrZodiacMaterials.Quests.All(q => counts.ContainsKey(q.Reward)))
            {
                var active = new Dictionary<string,int>();
                foreach (var (quest,name) in zodiacMaterialQuests)
                {
                    var work = questManager->GetQuestById((ushort)(quest & 0xffff));
                    if (work != null) active[name] = work->Sequence;
                }
                changed |= ArrZodiacMaterials.Update(character,active,counts);
            }
        }
        if (keyItems != null && keyItems->IsLoaded) changed |= AnimaExchangeProgress.Update(character,counts);
        changed |= ReadBook(id, character, manager);
        try
        {
            var reading = ArrScrollReader.Read(gui, manager, localizedScrollNames, out var scrollStatus);
            ScrollStatus = scrollStatus;
            if (reading is { } read && (!scrolls.TryGetValue(read.ItemId, out var definition) ||
                !heldScrolls.Contains(read.ItemId) || definition.Limit != read.Limit || read.Infused < 0 || read.Infused > read.Limit))
                ScrollStatus = "Scroll window data could not be matched to a held scroll; saved progress is retained.";
            changed |= scrollTracking.Update(id, character, heldScrolls, reading);
        }
        catch (Exception ex)
        {
            scrollTracking.Reset();
            log.Warning(ex, "Relic Atlas could not read sphere-scroll progress; saved counts are retained.");
        }
        var cosmic = FFXIVClientStructs.FFXIV.Client.Game.WKS.WKSManager.Instance();
        if (cosmic != null && cosmic->IsLoaded && cosmic->ResearchModule != null && cosmic->ResearchModule->IsLoaded)
            changed |= CosmicResearch.Apply(catalog, character, cosmic->ResearchModule->Analysis);
        inventory = counts; inventoryOwner = id; LastScan = DateTime.UtcNow;
        allagan.Update(id, materialIds);
        Status = "Live: inventory, relics, achievements, quests, ARR books, scrolls, light, Mahatmas and Zodiac materials";
        if (changed) save();
    }

    private unsafe bool ReadAchievementProgress(ulong id, CharacterProgress character)
    {
        var now = DateTime.UtcNow;
        if (now < nextAchievementAttempt) return false;
        try
        {
            var state = FFXIVClientStructs.FFXIV.Client.Game.UI.Achievement.Instance();
            if (state == null) { achievementProgress.Reset(now); return false; }
            var reading = new AchievementProgressReading(state->ProgressAchievementId,
                state->ProgressCurrent, state->ProgressMax,
                state->ProgressRequestState == FFXIVClientStructs.FFXIV.Client.Game.UI.Achievement.AchievementState.Requested,
                state->ProgressRequestState == FFXIVClientStructs.FFXIV.Client.Game.UI.Achievement.AchievementState.Loaded);
            // The game's UI and other plugins share this slot. Never replace a pending query
            // or request while the player is browsing the Achievements window.
            var addon = gui.GetAddonByName("Achievement");
            var changed = achievementProgress.Update(id, character, now, reading,
                addon.Address == 0 || !addon.IsVisible, out var request);
            if (request != 0)
            {
                state->RequestAchievementProgress(request);
                achievementProgress.ObserveRequest(state->ProgressRequestState ==
                    FFXIVClientStructs.FFXIV.Client.Game.UI.Achievement.AchievementState.Requested);
            }
            return changed;
        }
        catch (Exception ex)
        {
            achievementProgress.Reset(now);
            nextAchievementAttempt = now.AddMinutes(1);
            log.Warning(ex, "Relic Atlas could not refresh achievement objectives; recorded and manual progress is retained.");
            return false;
        }
    }
    public void Dispose() => framework.Update -= OnUpdate;
}
