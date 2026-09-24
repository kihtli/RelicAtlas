using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using Dalamud.Game;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using RelicAtlas.Core;
using RelicAtlas.Tracking;
using RelicAtlas.UI;

namespace RelicAtlas.Interop;

internal sealed class RelicAtlasApi : IDisposable
{
    private readonly ICallGateProvider<int> version;
    private readonly ICallGateProvider<string> snapshot;
    private readonly ICallGateProvider<ulong, string, string, bool> open;
    private readonly IFramework framework;
    private readonly IPluginLog log;
    private readonly Configuration config;
    private readonly Catalog catalog;
    private readonly GameTracker tracker;
    private readonly MainWindow window;
    private readonly Dictionary<string, uint> icons;
    private sealed record Publication(ulong CharacterId, string Json);
    private sealed record OpenRequest(ulong CharacterId, string Series, string Job);
    private Publication published = new(0, JsonSerializer.Serialize(new RelicSnapshot()));
    private OpenRequest? pendingOpen;
    private DateTime refreshAt;
    private ulong lastCharacter;
    private string lastJob = "";
    private bool lastAutomatic;

    public RelicAtlasApi(IDalamudPluginInterface pi, IFramework framework, IDataManager data, IPluginLog log,
        Configuration config, Catalog catalog, GameTracker tracker, MainWindow window)
    {
        this.framework = framework; this.log = log; this.config = config; this.catalog = catalog;
        this.tracker = tracker; this.window = window;
        icons = new();
        foreach (var row in data.GetExcelSheet<ClassJob>(ClientLanguage.English))
            if (row.RowId > 0) icons.TryAdd(row.Abbreviation.ExtractText(), 62000 + row.RowId);
        version = pi.GetIpcProvider<int>(RelicAtlasContract.VersionGate);
        snapshot = pi.GetIpcProvider<string>(RelicAtlasContract.SnapshotGate);
        open = pi.GetIpcProvider<ulong, string, string, bool>(RelicAtlasContract.OpenGate);
        version.RegisterFunc(() => RelicAtlasContract.Version);
        snapshot.RegisterFunc(() => Volatile.Read(ref published).Json);
        open.RegisterFunc(QueueOpen);
        framework.Update += Update;
    }

    private bool QueueOpen(ulong characterId, string seriesId, string job)
    {
        if (characterId == 0 || Volatile.Read(ref published).CharacterId != characterId ||
            !catalog.Series.Any(s => s.Id == seriesId && s.Jobs.Contains(job))) return false;
        Interlocked.Exchange(ref pendingOpen, new(characterId, seriesId, job));
        return true;
    }

    private void Update(IFramework _)
    {
        var id = tracker.CurrentId;
        var job = tracker.CurrentJob;
        var request = Interlocked.Exchange(ref pendingOpen, null);
        if (request != null && request.CharacterId == id)
            window.OpenRelic(request.Series, request.Job);
        if (id == lastCharacter && job == lastJob && config.Automatic == lastAutomatic && DateTime.UtcNow < refreshAt) return;
        lastCharacter = id; lastJob = job; lastAutomatic = config.Automatic;
        refreshAt = DateTime.UtcNow.AddMilliseconds(500);
        try
        {
            var value = RelicBarProgress.Build(catalog, config.Characters.GetValueOrDefault(id), id, job,
                config.Automatic, tracker.InventoryFor(id));
            foreach (var relic in value.Relics) relic.IconId = icons.GetValueOrDefault(relic.Job);
            Volatile.Write(ref published, new(id, JsonSerializer.Serialize(value)));
        }
        catch (Exception ex)
        {
            // Drop stale data, including the previous character, if a refresh fails.
            Volatile.Write(ref published, new(0, JsonSerializer.Serialize(new RelicSnapshot { State = "unavailable" })));
            refreshAt = DateTime.UtcNow.AddSeconds(5);
            log.Warning(ex, "Unable to publish Relic Atlas toolbar progress.");
        }
    }

    public void Dispose()
    {
        framework.Update -= Update;
        open.UnregisterFunc(); snapshot.UnregisterFunc(); version.UnregisterFunc();
        Interlocked.Exchange(ref pendingOpen, null);
    }
}
