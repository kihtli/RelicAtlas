using Dalamud.Interface;
using Dalamud.Plugin.Ipc;
using RelicAtlas.Interop;
using Umbra.Common;
using Umbra.Widgets;
using Una.Drawing;

namespace Umbra.RelicAtlas;

[ToolbarWidget("RelicAtlas.Progress", "Relic Atlas", "Live relic stages, objectives and next steps from Relic Atlas.", ["relic", "weapon", "progress", "atlas"])]
public sealed class RelicProgressWidget(WidgetInfo info, string? guid = null, Dictionary<string, object>? configValues = null)
    : StandardToolbarWidget(info, guid, configValues)
{
    protected override StandardWidgetFeatures Features => StandardWidgetFeatures.Text | StandardWidgetFeatures.SubText |
        StandardWidgetFeatures.Icon | StandardWidgetFeatures.ProgressBar;
    protected override int DefaultMaxTextWidth => 300;
    private ICallGateSubscriber<int>? version;
    private ICallGateSubscriber<string>? snapshotGate;
    private ICallGateSubscriber<ulong, string, string, bool>? openGate;
    private RelicConnection? connection;
    private DateTime refreshAt;

    protected override IEnumerable<IWidgetConfigVariable> GetConfigVariables() =>
    [
        ..base.GetConfigVariables(),
        new SelectWidgetConfigVariable("RelicSeries", "Relic series", "Automatic prefers unfinished pinned relics, then started relics, then the newest series for the chosen job.", "auto",
            new() { ["auto"] = "Automatic", ["arr"] = "Zodiac · A Realm Reborn", ["hw"] = "Anima · Heavensward", ["sb"] = "Eureka · Stormblood", ["shb"] = "Resistance · Shadowbringers", ["ew"] = "Manderville · Endwalker", ["dt"] = "Phantom · Dawntrail" }),
        new SelectWidgetConfigVariable("RelicJob", "Job", "Follow your current combat job, or track a fixed job. Add multiple widgets for different relics.", "current", Jobs()),
        new SelectWidgetConfigVariable("RelicLabel", "Bar label", "Choose the main label. Hover always shows the full next objective.", "stages",
            new() { ["stages"] = "Job, series and acquired stages", ["compact"] = "Series and acquired stages", ["objective"] = "Job and next objective" }),
        new SelectWidgetConfigVariable("RelicProgress", "Progress bar", "Stage objectives include partial counters; weapon stages advance only after acquiring the weapon.", "stage",
            new() { ["stage"] = "Current stage objectives", ["objective"] = "Next objective", ["stages"] = "Acquired weapon stages" }),
        new BooleanWidgetConfigVariable("HideCompletedRelic", "Hide completed relic", "Hide this widget when its selected weapon has every stage acquired.", false),
    ];

    protected override void OnLoad()
    {
        version = Framework.DalamudPlugin.GetIpcSubscriber<int>(RelicAtlasContract.VersionGate);
        snapshotGate = Framework.DalamudPlugin.GetIpcSubscriber<string>(RelicAtlasContract.SnapshotGate);
        openGate = Framework.DalamudPlugin.GetIpcSubscriber<ulong, string, string, bool>(RelicAtlasContract.OpenGate);
        connection = new(() => version.InvokeFunc(), () => snapshotGate.InvokeFunc(), (id, series, job) => openGate.InvokeFunc(id, series, job));
        Node.OnClick += OpenRelic;
        SetFontAwesomeIcon(FontAwesomeIcon.Gem);
        SetText("Relic Atlas");
        SetSubText("Connecting…");
        SetProgressBarConstraint(0, 10000);
    }

    protected override void OnDraw()
    {
        Refresh();
        var snapshot = connection?.Snapshot;
        var series = GetConfigValue<string>("RelicSeries");
        var job = GetConfigValue<string>("RelicJob");
        var relic = snapshot == null ? null : RelicPresentation.Select(snapshot, series, job);
        var display = RelicPresentation.Create(snapshot, relic, series, job, GetConfigValue<string>("RelicLabel"),
            GetConfigValue<string>("RelicProgress"), GetConfigValue<bool>("HideCompletedRelic"));
        IsVisible = display.Visible;
        SetText(display.Text); SetSubText(display.SubText); SetTooltip(display.Tooltip);
        SetProgressBarValue(display.Progress);
        if (display.IconId != 0) SetGameIconId(display.IconId);
        else SetFontAwesomeIcon(FontAwesomeIcon.Gem);
    }

    private void Refresh()
    {
        if (DateTime.UtcNow < refreshAt) return;
        refreshAt = DateTime.UtcNow.AddMilliseconds(500);
        connection?.Refresh();
    }

    private void OpenRelic(Node _)
    {
        connection?.Open(GetConfigValue<string>("RelicSeries"), GetConfigValue<string>("RelicJob"));
        refreshAt = DateTime.MinValue;
    }

    protected override void OnUnload()
    {
        Node.OnClick -= OpenRelic;
        connection = null;
        version = null; snapshotGate = null; openGate = null;
    }

    private static Dictionary<string, string> Jobs()
    {
        var options = new Dictionary<string, string> { ["current"] = "Current job" };
        foreach (var job in new[] { "PLD", "WAR", "DRK", "GNB", "WHM", "SCH", "AST", "SGE", "MNK", "DRG", "NIN", "SAM", "RPR", "VPR", "BRD", "MCH", "DNC", "BLM", "SMN", "RDM", "PCT" }) options[job] = job;
        return options;
    }
}
