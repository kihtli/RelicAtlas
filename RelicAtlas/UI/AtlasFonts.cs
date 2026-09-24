using System;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Plugin;

namespace RelicAtlas.UI;

internal sealed class AtlasFonts : IDisposable
{
    public IFontHandle Heading { get; }
    public AtlasFonts(IDalamudPluginInterface pluginInterface)
    {
        Heading = pluginInterface.UiBuilder.FontAtlas.NewDelegateFontHandle(step => step.OnPreBuild(toolkit =>
        {
            using var stream = typeof(AtlasFonts).Assembly.GetManifestResourceStream("RelicAtlas.HeadingFont")
                ?? throw new InvalidOperationException("Heading font resource missing.");
            toolkit.AddFontFromStream(stream, new SafeFontConfig { SizePx = 36 }, true, "Relic Atlas heading");
        }));
    }
    public void Dispose() => Heading.Dispose();
}
