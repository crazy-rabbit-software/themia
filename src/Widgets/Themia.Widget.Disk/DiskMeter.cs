using Themia.App.Services;
using Themia.App.Services.SystemData;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Themia.Widget.Disk;

namespace Themia.App.Widgets.Meters;

/// <summary>
/// [S-1820]..[S-1822] Disk activity: the drive's reads and writes together, per second, as the
/// reading and as the chart — of the drive the settings name, or of every drive. [S-1161] The
/// header names the drive as Explorer does (the reading's tooltip too, for a card without one),
/// never the card's body, and the card's menu opens it.
/// <para>
/// [S-1820] Space used is not shown (user requirement): it barely moves, and the question a disk
/// meter on the desktop answers is "is the disk busy", not "is it full" — File Explorer says that.
/// </para>
/// </summary>
/// <param name="widgetHost">[S-6913] Opens the drive in Explorer.</param>
internal sealed class DiskMeter(IWidgetHost widgetHost) : MeterPart
{
    /// <summary>[S-6271] The bars' window: the totals of the last minute's readings, the graph's own top over them.</summary>
    private const int BarWindow = 60;

    private readonly Queue<double> _recent = new(BarWindow);
    private DiskConfig _config = new();
    private string _root = SystemDrive.Root;

    public override WidgetId Kind => new WidgetId("disk");

    /// <summary>
    /// "1023 KB/s", as the network meter's. [S-3860] Each run is counted at the size it is drawn,
    /// so the rate is the step every meter's reading is at this tier, where Memory's percentage is.
    /// </summary>
    public override HeroBound Reading => HeroBound.Of(Formatting.Rate(1023 * 1024));

    /// <summary>[S-6325] The rate glides in bytes a second, each step written as the reading is (its unit with it).</summary>
    public override double? Number(MeterReading reading) => reading.Available ? reading.Value : null;

    public override string Format(double number) => Formatting.Rate(number);

    /// <summary>A rate has no step of its own: only the glide's step limit.</summary>
    public override double Resolution => 0;

    /// <summary>[S-1161] The header's title on the reading too: the drive with the header switched off.</summary>
    public override string? ReadingTip => Title();

    /// <summary>[S-1821] Against the window's peak, rounded up to a doubling of 1 MB/s.</summary>
    public override ChartScale Scale => DiskRules.Scale;

    /// <summary>[S-6271] Bars: reads and writes apart, as labelled rows under the total.</summary>
    public override MeterLook Look(WidgetConfig config)
        => (config as DiskConfig)?.Style == DiskStyle.Bars ? MeterLook.Rows : MeterLook.Graph;

    /// <summary>[S-6271] Reads and writes of the drive the reading is about, from the snapshot the reading itself comes from.</summary>
    public override MeterItemsWatch? WatchItems(ISystemFeeds hub, Action<IReadOnlyList<MeterItem>> onItems)
        => MeterItemsWatch.Of(hub, Sources.DiskActivity, snapshot => Items(snapshot.Activity?.Rates), onItems);

    /// <summary>42 MB/s read, 8 MB/s written.</summary>
    public override IReadOnlyList<MeterItem> SampleItems => Items(new DiskRates(42 * 1024 * 1024, 8 * 1024 * 1024), [64 * 1024 * 1024]);

    public override MeterReading SampleReading => new(0.4, Formatting.Rate(50 * 1024 * 1024), true, Value: 50 * 1024 * 1024);

    public override void ApplyConfig(WidgetConfig config, IMeterHost host)
    {
        var previous = (_config.Scope, _root);
        _config = config as DiskConfig ?? new DiskConfig();
        _root = DiskRules.Root(_config.Drive) ?? SystemDrive.Root;

        // Another drive is another history ([S-1160]): the bars' window starts again with it.
        if (previous != (_config.Scope, _root))
        {
            _recent.Clear();
        }

        host.SetTitle(Title());
    }

    /// <summary>[S-1161] The title follows the hub's walk of the volumes (a label renamed, a drive plugged in).</summary>
    public override void Observe(MeterReading reading, IMeterHost host)
    {
        host.SetTitle(Title());

        if (reading is { Available: true, Value: { } total })
        {
            if (_recent.Count == BarWindow)
            {
                _recent.Dequeue();
            }

            _recent.Enqueue(total);
        }
    }

    private IReadOnlyList<MeterItem> Items(IReadOnlyDictionary<string, DiskRates>? byInstance)
    {
        var sample = DiskRules.Read(_config.Scope, _root, byInstance);
        return sample.State == DiskReadingState.Rate ? Items(sample.Rates, [.. _recent]) : [];
    }

    /// <summary>[S-6271] "Read 42 MB/s" and "Write 8 MB/s", each a bar against the graph's top.</summary>
    private static IReadOnlyList<MeterItem> Items(DiskRates rates, double[] recent)
    {
        var (read, write) = DiskRules.BarShares(rates, recent);
        return
        [
            new MeterItem(read, MeterPressure.Normal, DiskStr.Styles_Io_Read.Text(), Formatting.Rate(rates.Read)),
            new MeterItem(write, MeterPressure.Normal, DiskStr.Styles_Io_Write.Text(), Formatting.Rate(rates.Write)),
        ];
    }

    /// <summary>[S-1166] Open the drive the reading is about in Explorer. Every drive at once has no one folder to open.</summary>
    public override IReadOnlyList<WidgetAction> ContextActions(IMeterHost host)
    {
        if (_config.Scope == DiskScope.AllDrives)
        {
            return [];
        }

        var root = _root;
        return [new WidgetAction(Glyphs.Folder, Str.WidgetsB_Disk_OpenInExplorer.Format(("drive", DiskRules.Instance(root))), () => widgetHost.Launcher.Open(root))];
    }

    /// <summary>
    /// [S-1161] "System (C:)" for a drive Windows lists, "C:" for one it does not, "All drives" for
    /// every one. From the last walk, never a new one: a title touches no drive.
    /// </summary>
    private string Title() => DiskRules.HeaderTitle(Strings.Current, _config.Scope, _root, DriveRules.LocalVolumes(widgetHost.Drives.Drives));
}
