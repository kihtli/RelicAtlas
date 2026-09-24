using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using RelicAtlas.Core;

namespace RelicAtlas.UI;

internal sealed class AtlasArtwork : IAtlasArtwork
{
    private readonly ITextureProvider textures;
    private readonly Dictionary<string, uint> weapons = new();
    private readonly Dictionary<string, uint> jobs = new();
    public AtlasArtwork(ITextureProvider textures, IDataManager data, Catalog catalog)
    {
        this.textures = textures;
        var wanted = catalog.Series.SelectMany(s => s.Stages).SelectMany(s => s.Weapons.Values).SelectMany(n => n).ToHashSet();
        foreach (var item in data.GetExcelSheet<Item>(ClientLanguage.English))
        {
            var name = item.Name.ExtractText();
            if (wanted.Contains(name)) weapons.TryAdd(name, item.Icon);
        }
        foreach (var job in data.GetExcelSheet<ClassJob>(ClientLanguage.English))
            jobs.TryAdd(job.Abbreviation.ExtractText(), 62000 + job.RowId);
    }
    public bool Logo(Vector2 position, Vector2 bounds)
    {
        var source = textures.GetFromManifestResource(typeof(AtlasArtwork).Assembly, "RelicAtlas.Logo");
        if (!source.TryGetWrap(out var wrap, out _)) return false;
        // Original PNG is untouched. UV bounds omit its transparent outer margins.
        var uv0 = new Vector2(214f / 2172, 23f / 724);
        var uv1 = new Vector2(1836f / 2172, 705f / 724);
        var ratio = 1622f / 682;
        var height = System.Math.Min(bounds.Y, bounds.X / ratio);
        var size = new Vector2(height * ratio, height);
        var p = position + new Vector2(0, (bounds.Y - height) / 2);
        ImGui.GetWindowDrawList().AddImage(wrap.Handle, p, p + size, uv0, uv1);
        return true;
    }
    public bool Backdrop(Vector2 position, Vector2 size)
    {
        var source = textures.GetFromManifestResource(typeof(AtlasArtwork).Assembly, "RelicAtlas.ArchiveArtwork");
        if (!source.TryGetWrap(out var wrap, out _)) return false;
        // Center-crop through UVs; retain the original asset and avoid image preprocessing.
        var target = size.X / size.Y;
        var sourceRatio = (float)wrap.Width / wrap.Height;
        var uvSize = target > sourceRatio ? new Vector2(1, sourceRatio / target) : new Vector2(target / sourceRatio, 1);
        var uv = (Vector2.One - uvSize) / 2;
        ImGui.GetWindowDrawList().AddImageRounded(wrap.Handle, position, position + size, uv, uv + uvSize, 0xffffffff, 9 * ImGuiHelpers.GlobalScale);
        return true;
    }
    public bool Job(string job, Vector2 position, Vector2 size) => jobs.TryGetValue(job, out var icon) && Icon(icon, position, size);
    public bool Weapon(string name, Vector2 position, Vector2 size) => weapons.TryGetValue(name, out var icon) && Icon(icon, position, size);
    private bool Icon(uint id, Vector2 position, Vector2 size)
    {
        if (!textures.TryGetFromGameIcon(new GameIconLookup(id), out var source) || !source.TryGetWrap(out var wrap, out _)) return false;
        ImGui.GetWindowDrawList().AddImage(wrap.Handle, position, position + size);
        return true;
    }
}
