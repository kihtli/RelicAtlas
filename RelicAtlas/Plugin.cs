using System;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using RelicAtlas.Core;
using RelicAtlas.Tracking;
using RelicAtlas.UI;
using RelicAtlas.Interop;

namespace RelicAtlas;

public sealed class Plugin : IDalamudPlugin
{
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly ICommandManager commands;
    private readonly WindowSystem windows = new("RelicAtlas");
    private readonly MainWindow window;
    private readonly AtmaPopoutWindow atmaPopout;
    private readonly BookPopoutWindow bookPopout;
    private readonly AtlasFonts fonts;
    private readonly GameTracker tracker;
    private readonly Configuration config;
    private readonly RelicAtlasApi api;
    private readonly AtmaTravel atmaTravel;
    private readonly BookTravel bookTravel;
    public Plugin(IDalamudPluginInterface pluginInterface, ICommandManager commands, IClientState client,
        IPlayerState player, ICondition condition, IFramework framework, IDataManager data, IPluginLog log, IGameGui gui,
        ITextureProvider textures, IObjectTable objects, IAetheryteList aetherytes, ISigScanner scanner)
    {
        this.pluginInterface = pluginInterface; this.commands = commands;
        config = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        var catalog = Catalog.Load();
        tracker = new(config, catalog, client, player, condition, framework, data, log, gui, Save,
            new AllaganToolsInventory(pluginInterface), scanner, objects);
        fonts = new(pluginInterface);
        atmaTravel = new(config, catalog, tracker, pluginInterface, client, condition, framework, objects, aetherytes, data, log);
        atmaPopout = new(config, catalog, tracker, atmaTravel, Save);
        bookTravel = new(config, catalog, tracker, atmaTravel, pluginInterface, client, condition, framework, objects, aetherytes, data, gui, log);
        bookPopout = new(config, catalog, tracker, bookTravel, Save);
        window = new(config, catalog, tracker, Save, fonts.Heading, new AtlasArtwork(textures, data, catalog), atmaTravel, atmaPopout.Show, bookPopout.Show);
        atmaPopout.OpenFarmingPage = () => window.OpenAtma();
        bookPopout.OpenChecklist = job => window.OpenRelic("arr", job);
        api = new(pluginInterface, framework, data, log, config, catalog, tracker, window);
        windows.AddWindow(window);
        windows.AddWindow(atmaPopout);
        windows.AddWindow(bookPopout);
        commands.AddHandler("/relicatlas", new CommandInfo(OnCommand) { HelpMessage = "Open Relic Atlas. /relicatlas book opens the Zodiac book tracker. /relicatlas atma opens farming; /relicatlas atma popout opens the compact Atma tracker; /relicatlas atma stop stops automatic travel." });
        pluginInterface.UiBuilder.Draw += windows.Draw;
        pluginInterface.UiBuilder.OpenMainUi += Open;
        pluginInterface.UiBuilder.OpenConfigUi += Open;
    }
    private void OnCommand(string command, string arguments)
    {
        var arg = arguments.Trim();
        if (arg.Equals("book", StringComparison.OrdinalIgnoreCase) || arg.Equals("books", StringComparison.OrdinalIgnoreCase) ||
            arg.Equals("book popout", StringComparison.OrdinalIgnoreCase)) { bookPopout.Show(); return; }
        if (arg.Equals("atma popout", StringComparison.OrdinalIgnoreCase)) { atmaPopout.Show(); return; }
        if (arg.Equals("atma stop", StringComparison.OrdinalIgnoreCase)) atmaTravel.Stop();
        if (arg.StartsWith("atma", StringComparison.OrdinalIgnoreCase)) window.OpenAtma();
        else Open();
    }
    private void Open() => window.IsOpen = true;
    private void Save() => pluginInterface.SavePluginConfig(config);
    public void Dispose()
    {
        pluginInterface.UiBuilder.Draw -= windows.Draw;
        pluginInterface.UiBuilder.OpenMainUi -= Open;
        pluginInterface.UiBuilder.OpenConfigUi -= Open;
        commands.RemoveHandler("/relicatlas");
        api.Dispose();
        bookTravel.Dispose();
        atmaTravel.Dispose();
        tracker.Dispose();
        windows.RemoveAllWindows();
        fonts.Dispose();
        Save();
    }
}
