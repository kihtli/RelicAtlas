using System.Text.Json;
using RelicAtlas.Interop;

namespace Umbra.RelicAtlas;

// Separate transport handling from the Umbra node lifecycle so reloads and protocol failures can be tested.
public sealed class RelicConnection(Func<int> version, Func<string> read, Func<ulong, string, string, bool> open)
{
    public RelicSnapshot? Snapshot { get; private set; }

    public void Refresh()
    {
        Snapshot = null;
        try
        {
            if (version() != RelicAtlasContract.Version) return;
            var value = JsonSerializer.Deserialize<RelicSnapshot>(read());
            if (value?.ApiVersion == RelicAtlasContract.Version && value.Relics != null) Snapshot = value;
        }
        catch (Exception)
        {
            // A disabled or reloaded provider must clear its previous progress and reconnect on the next poll.
        }
    }

    public bool Open(string series, string job)
    {
        Refresh();
        if (Snapshot == null) return false;
        var relic = RelicPresentation.Select(Snapshot, series, job);
        if (relic == null || Snapshot.CharacterId == 0) return false;
        try
        {
            var accepted = open(Snapshot.CharacterId, relic.SeriesId, relic.Job);
            if (!accepted) Snapshot = null;
            return accepted;
        }
        catch (Exception) { Snapshot = null; return false; }
    }
}
