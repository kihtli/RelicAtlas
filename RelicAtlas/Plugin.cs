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
    private readonly AtlasFonts fonts;
    private readonly GameTracker tracker;
    private readonly Configuration config;
    private readonly RelicAtlasApi api;
    private readonly AtmaTravel atmaTravel;
    public Plugin(IDalamudPluginInterface pluginInterface, ICommandManager commands, IClientState client,
        IPlayerState player, ICondition condition, IFramework framework, IDataManager data, IPluginLog log, IGameGui gui,
        ITextureProvider textures, IObjectTable objects, IAetheryteList aetherytes, ISigScanner scanner)
    {
        this.pluginInterface = pluginInterface; this.commands = commands;
        config = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        var catalog = Catalog.Load();
        tracker = new(config, catalog, client, player, condition, framework, data, log, gui, Save,
            new AllaganToolsInventory(pluginInterface), scanner);
        fonts = new(pluginInterface);
        atmaTravel = new(config, catalog, tracker, pluginInterface, client, condition, framework, objects, aetherytes, data, log);
        window = new(config, catalog, tracker, Save, fonts.Heading, new AtlasArtwork(textures, data, catalog), atmaTravel);
        api = new(pluginInterface, framework, data, log, config, catalog, tracker, window);
        windows.AddWindow(window);
        commands.AddHandler("/relicatlas", new CommandInfo(OnCommand) { HelpMessage = "Open Relic Atlas. /relicatlas atma opens farming; /relicatlas atma stop stops automatic travel." });
        pluginInterface.UiBuilder.Draw += windows.Draw;
        pluginInterface.UiBuilder.OpenMainUi += Open;
        pluginInterface.UiBuilder.OpenConfigUi += Open;
    }
    private void OnCommand(string command, string arguments)
    {
        var arg = arguments.Trim();
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
        atmaTravel.Dispose();
        tracker.Dispose();
        windows.RemoveAllWindows();
        fonts.Dispose();
        Save();
    }
}
