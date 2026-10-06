using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using RelicAtlas.Core;

namespace RelicAtlas.Tracking;

/// <summary>Reads the server-updated Anima value used by GetAnimaWeapon7EnhancePoint and the glass.</summary>
internal sealed class AnimaDensityReader
{
    // The glass loads the shared Anima state, then calls its ushort getter. Verify
    // the entire getter before using its field offset; a changed client fails closed.
    private const string Signature = "48 8D 0D ?? ?? ?? ?? 48 89 AC 24 78 01 00 00 E8 ?? ?? ?? ?? 8B C8 B8 D0 07 00 00 3B C8 0F 47 C8 B8 1F 85 EB 51 F7 E1 C1 EA 06";
    private readonly nint density;
    public bool Available => density != 0;

    public AnimaDensityReader(ISigScanner scanner, IPluginLog log)
    {
        try
        {
            var binding = scanner.ScanText(Signature);
            var getter = binding + 20 + Marshal.ReadInt32(binding + 16);
            byte[] code = new byte[5]; Marshal.Copy(getter, code, 0, code.Length);
            if (!code.SequenceEqual(new byte[] { 0x0f, 0xb7, 0x41, 0x0c, 0xc3 }))
                throw new InvalidOperationException("Anima density getter layout changed.");
            density = scanner.GetStaticAddressFromSig(Signature) + 0x0c;
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Relic Atlas automatic Anima density reader unavailable; the glass reader remains available.");
        }
    }

    public unsafe bool Read(ulong characterId, CharacterProgress character, Catalog catalog,
        IReadOnlyDictionary<byte, string> jobs, HashSet<string> owned)
    {
        var player = PlayerState.Instance();
        var quests = QuestManager.Instance();
        if (!Available || player == null || !player->IsLoaded || player->ContentId != characterId || quests == null) return false;
        var work = quests->GetQuestById((ushort)(67932 & 0xffff));
        // Only the light-gathering phase may read this shared native value.
        if (work == null || work->Sequence != 5 || !jobs.TryGetValue(work->AcceptClassJob, out var owner)) return false;
        var sharpened = catalog.Series.Single(s => s.Id == "hw").Stages.Single(s => s.Id == "sharpened");
        var hasWeapon = sharpened.Weapons.TryGetValue(owner, out var weapons) && weapons.All(owned.Contains);
        if (!hasWeapon) return false;
        return AnimaProgress.RecordLiveDensity(character, owner, work->Sequence, hasWeapon,
            (ushort)Marshal.ReadInt16(density));
    }
}
