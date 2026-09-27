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
    private readonly Action save;
    private readonly AllaganToolsInventory allagan;
    private readonly Dictionary<uint, string> materialIds = [];
    private readonly Dictionary<uint, string> itemNames = [];
    private readonly Dictionary<uint, List<string>> sharedQuests = [];
    private readonly Dictionary<uint, string> eventItemNames = [];
    private readonly Dictionary<uint, string> relicJobs = [];
    private readonly Dictionary<uint, List<BookObjective>> books = [];
    private readonly List<AchievementEvidence> achievements = [];
    private readonly List<(Series Series, Stage Stage, Requirement Requirement)> objectiveAchievements = [];
    public bool AchievementsLoaded { get; private set; }
    private sealed record BookObjective(Requirement Requirement, string Kind, int Index);
    private readonly HashSet<string> materialNames = [];
    private DateTime nextScan;
    private ulong inventoryOwner;
    private ulong lastCharacter;
    public ulong CurrentId => client.IsLoggedIn && player.IsLoaded ? player.ContentId : 0;
    public string CurrentJob => player.IsLoaded && player.ClassJob.TryGetValue(out var job) ? job.Abbreviation.ExtractText() : "";
    public IReadOnlyDictionary<string, int>? InventoryFor(ulong id) =>
        config.Automatic && id != 0 && id == inventoryOwner && id == CurrentId ? inventory : null;
    private Dictionary<string, int>? inventory;
    public List<string> Unresolved { get; } = [];
    public string Status { get; private set; } = "Waiting for character data";
    public DateTime? LastScan { get; private set; }
    public string StorageStatus => allagan.Status;
    public ExternalInventory? StorageFor(ulong id) => config.Automatic && id != 0 && id == CurrentId &&
        allagan.Snapshot?.Owner == id ? allagan.Snapshot : null;

    public GameTracker(Configuration config, Catalog catalog, IClientState client, IPlayerState player,
        ICondition condition, IFramework framework, IDataManager data, IPluginLog log, IGameGui gui, Action save,
        AllaganToolsInventory allagan)
    {
        this.config = config; this.catalog = catalog; this.client = client; this.player = player;
        this.condition = condition; this.framework = framework; this.log = log; this.save = save;
        this.gui = gui;
        this.allagan = allagan;
        var names = catalog.Series.SelectMany(s => s.Stages).SelectMany(s => s.Weapons.Values).SelectMany(x => x).ToHashSet();
        foreach (var r in catalog.Series.SelectMany(s => s.Stages).SelectMany(s => s.Requirements))
            if (r.Item.Length > 0) { names.Add(r.Item); materialNames.Add(r.Item); }
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
        var quests = catalog.Series.SelectMany(s => s.Stages).SelectMany(s => s.Requirements)
            .Where(r => r.Quest.Length > 0 && r.Shared.Length > 0).GroupBy(r => Normalize(r.Quest))
            .ToDictionary(g => g.Key, g => g.Select(r => r.Shared).Distinct().ToList());
        var foundQuests = new HashSet<string>();
        foreach (var quest in data.GetExcelSheet<Quest>(ClientLanguage.English))
            if (quests.TryGetValue(Normalize(quest.Name.ExtractText()), out var keys))
            { sharedQuests[quest.RowId] = keys; foundQuests.Add(Normalize(quest.Name.ExtractText())); }
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
            var bindings = new List<BookObjective>();
            void Match(string name, string kind, int index)
            {
                if (name.Length == 0) return;
                var matches = objectives.Where(r => kind == "dungeon"
                    ? Normalize(r.Detail).StartsWith("defeat " + Normalize(name) + " with ", StringComparison.Ordinal)
                    : Normalize(r.Label) == Normalize(name)).ToArray();
                if (matches.Length == 1) bindings.Add(new(matches[0], kind, index));
            }
            for (int i = 0; i < note.MonsterNoteTargetCommon.Count; i++)
                Match(BookRowNames.Monster(note.MonsterNoteTargetCommon[i]), "monster", i);
            for (int i = 0; i < note.MonsterNoteTargetNM.Count; i++)
                Match(BookRowNames.Monster(note.MonsterNoteTargetNM[i]), "dungeon", i);
            for (int i = 0; i < note.Fate.Count; i++) Match(BookRowNames.Fate(note.Fate[i]), "fate", i);
            for (int i = 0; i < note.Leve.Count; i++) Match(BookRowNames.Leve(note.Leve[i]), "leve", i);
            var purchase = objectives.SingleOrDefault(r => r.Label.StartsWith("Purchase ", StringComparison.Ordinal));
            if (purchase != null) bindings.Add(new(purchase, "purchase", 0));
            books[note.RowId] = bindings;
        }
    }

    private unsafe bool ReadOpenBook(CharacterProgress character)
    {
        var addon = gui.GetAddonByName("RelicNoteBook");
        if (addon.Address == 0 || !addon.IsVisible) return false;
        var note = FFXIVClientStructs.FFXIV.Client.Game.UI.RelicNote.Instance();
        if (note == null || !relicJobs.TryGetValue(note->RelicId, out var job) || !books.TryGetValue(note->RelicNoteId, out var bindings)) return false;
        var series = catalog.Series.Single(s => s.Id == "arr");
        var stage = series.Stages.Single(s => s.Id == "animus");
        var changed = false;
        var activeBook = bindings.FirstOrDefault()?.Requirement.Group;
        if (activeBook != null && character.ActiveBooks.GetValueOrDefault(job) != activeBook)
        { character.ActiveBooks[job] = activeBook; changed = true; }
        foreach (var binding in bindings)
        {
            var value = binding.Kind switch
            {
                "monster" => note->GetMonsterProgress(binding.Index),
                "dungeon" => note->IsDungeonComplete(binding.Index) ? 1 : 0,
                "fate" => note->IsFateComplete(binding.Index) ? 1 : 0,
                "leve" => note->IsLeveComplete(binding.Index) ? 1 : 0,
                "purchase" => 1,
                _ => 0,
            };
            var key = Progress.Key(series, job, stage, binding.Requirement);
            value = Math.Clamp(value, 0, binding.Requirement.Count);
            if (character.DetectedCounters.GetValueOrDefault(key) == value) continue;
            character.DetectedCounters[key] = value; changed = true;
        }
        return changed;
    }

    public static string Normalize(string value) => value.Replace('’', '\'').Trim().ToLowerInvariant();
    public void Rescan() { nextScan = DateTime.MinValue; allagan.RefreshSoon(); }

    private void OnUpdate(IFramework _)
    {
        var id = CurrentId;
        if (id == 0 || condition[ConditionFlag.BetweenAreas] || condition[ConditionFlag.BetweenAreas51])
        {
            inventory = null; inventoryOwner = 0;
            allagan.Reset();
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
        if (!config.Automatic) { inventory = null; allagan.Reset(); Status = "Automatic detection paused"; return; }
        if (lastCharacter != id) { inventory = null; nextScan = DateTime.MinValue; lastCharacter = id; }
        if (DateTime.UtcNow < nextScan) return;
        nextScan = DateTime.UtcNow.AddSeconds(2);
        try
        {
            Scan(id, character);
        }
        catch (Exception ex)
        {
            inventory = null; inventoryOwner = 0;
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
        if (manager == null) return;
        var counts = itemNames.Values.Distinct().Where(materialNames.Contains)
            .SelectMany(n => new[] { n, n + "|HQ" }).ToDictionary(n => n, _ => 0);
        var owned = new HashSet<string>();
        foreach (var type in Containers)
        {
            var container = manager->GetInventoryContainer(type);
            if (container == null || !container->IsLoaded) { inventory = null; Status = "Waiting for inventory"; return; }
            for (int i = 0; i < container->Size; i++)
            {
                var item = container->GetInventorySlot(i);
                if (item == null || item->Quantity <= 0 || !itemNames.TryGetValue(item->ItemId, out var name)) continue;
                owned.Add(name);
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
        foreach (var (quest, keys) in sharedQuests)
            if (QuestManager.IsQuestComplete(quest))
                foreach (var key in keys) changed |= character.CompletedShared.Add(key);
        changed |= ReadOpenBook(character);
        var cosmic = FFXIVClientStructs.FFXIV.Client.Game.WKS.WKSManager.Instance();
        if (cosmic != null && cosmic->IsLoaded && cosmic->ResearchModule != null && cosmic->ResearchModule->IsLoaded)
            changed |= CosmicResearch.Apply(catalog, character, cosmic->ResearchModule->Analysis);
        inventory = counts; inventoryOwner = id; LastScan = DateTime.UtcNow;
        allagan.Update(id, materialIds);
        Status = "Live: inventory, relics, shared quests, open ARR books and loaded Cosmic research";
        if (changed) save();
    }
    public void Dispose() => framework.Update -= OnUpdate;
}
