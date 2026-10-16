using Themia.App.Services;
using Themia.App.Services.SystemData;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Widgets.Meters;

/// <summary>
/// [S-1820]..[S-1822] Disk activity: the drive's reads and writes together, per second, as the
/// reading and as the chart — of the drive the settings name, or of every drive. [S-1161] The
/// reading names the drive by its letter, as the meter starts headerless ([S-700]); a header names
/// it as Explorer does, and the card's menu opens it.
/// <para>
/// [S-1820] Space used is not shown (user requirement): it barely moves, and the question a disk
/// meter on the desktop answers is "is the disk busy", not "is it full" — File Explorer says that.
/// </para>
/// </summary>
internal sealed class DiskMeter : MeterPart
{
    /// <summary>[S-6271] The bars' window: the totals of the last minute's readings, the graph's own top over them.</summary>
    private const int BarWindow = 60;

    private readonly Queue<double> _recent = new(BarWindow);
    private DiskConfig _config = new();
    private string _root = DiskMetrics.SystemRoot;

    public override WidgetKind Kind => WidgetKind.Disk;

    /// <summary>
    /// "1023 KB/s", as the network meter's, and the drive after it. [S-3860] Each run is counted at
    /// the size it is drawn, so the rate is the step every meter's reading is at this tier; the drive
    /// moves under the rate where it does not fit beside it.
    /// </summary>
    public override HeroBound Reading => HeroBound.Of(Formatting.Rate(1023 * 1024), TokenCharacters);

    /// <summary>[S-1161] "C:", or "All drives": which throughput this is, with no header to say so.</summary>
    public override string? Token => _config.Scope == DiskScope.AllDrives ? Str.WidgetsB_Disk_AllDrives.Text() : DiskRules.Instance(_root);

    /// <summary>[S-1821] Against the window's peak, rounded up to a doubling of 1 MB/s.</summary>
    public override ChartScale Scale => DiskRules.Scale;

    /// <summary>[S-6271] Bars: reads and writes apart, as labelled rows under the total.</summary>
    public override MeterLook Look(WidgetConfig config)
        => (config as DiskConfig)?.Style == DiskStyle.Bars ? MeterLook.Rows : MeterLook.Graph;

    /// <summary>[S-6271] Reads and writes of the drive the reading is about, from the snapshot the reading itself comes from.</summary>
    public override MeterItemsWatch? WatchItems(SystemHub hub, Action<IReadOnlyList<MeterItem>> onItems)
        => MeterItemsWatch.Of(hub, Sources.DiskActivity, snapshot => Items(snapshot.Activity?.Rates), onItems);

    /// <summary>42 MB/s read, 8 MB/s written.</summary>
    public override IReadOnlyList<MeterItem> SampleItems => Items(new DiskRates(42 * 1024 * 1024, 8 * 1024 * 1024), [64 * 1024 * 1024]);

    public override MeterReading SampleReading => new(0.4, Formatting.Rate(50 * 1024 * 1024), true, Value: 50 * 1024 * 1024);

    public override void ApplyConfig(WidgetConfig config, IMeterHost host)
    {
        var previous = (_config.Scope, _root);
        _config = config as DiskConfig ?? new DiskConfig();
        _root = DiskRules.Root(_config.Drive) ?? DiskMetrics.SystemRoot;

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
            new MeterItem(read, MeterPressure.Normal, Str.Styles_Io_Read.Text(), Formatting.Rate(rates.Read)),
            new MeterItem(write, MeterPressure.Normal, Str.Styles_Io_Write.Text(), Formatting.Rate(rates.Write)),
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
        return [new WidgetAction(Glyphs.Folder, Str.WidgetsB_Disk_OpenInExplorer.Format(("drive", DiskRules.Instance(root))), () => ShellLauncher.Open(root))];
    }

    /// <summary>
    /// [S-1161] "System (C:)" for a drive Windows lists, "C:" for one it does not, "All drives" for
    /// every one. From the last walk, never a new one: a title touches no drive.
    /// </summary>
    private string Title()
    {
        if (_config.Scope == DiskScope.AllDrives)
        {
            return Str.WidgetsB_Disk_AllDrives.Text();
        }

        foreach (var volume in DiskMetrics.Volumes())
        {
            if (string.Equals(volume.Root, _root, StringComparison.OrdinalIgnoreCase))
            {
                return DiskRules.Title(volume);
            }
        }

        return DiskRules.Instance(_root);
    }
}
