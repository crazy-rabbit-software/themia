using Themia.App.Services;
using Themia.App.Services.SystemData;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.SystemData;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;

namespace Themia.App.Widgets.Meters;

/// <summary>
/// [S-1820]..[S-1822] [S-11000] What every disk atom's meter shares (the reading, the history): the
/// drive's reads and writes together, per second, of the drive the config names or of every drive
/// ([S-1160], [S-1822]). [S-1161] The header names the drive as Explorer does (the reading's tooltip
/// too, for a card without one), and the card's menu opens it. One path, so the atoms agree on the
/// drive, its name and its rate.
/// <para>
/// [S-1820] Space used is not shown (user requirement): it barely moves, and the question a disk
/// meter answers is "is the disk busy", not "is it full" (that is the Drive widget's).
/// </para>
/// </summary>
/// <param name="widgetHost">[S-6913] Opens the drive in Explorer.</param>
public abstract class DiskPart(IWidgetHost widgetHost) : MeterPart
{
    /// <summary>[S-6271] The bars' window: the totals of the last minute's readings, the graph's own top over them.</summary>
    private const int BarWindow = 60;

    private readonly Queue<double> _recent = new(BarWindow);
    private IDiskScope _config = new TheSystemDrive();
    private string _root = SystemDrive.Root;

    /// <summary>[S-1161] The volumes of the hub's last disk read, which the title names the drive from.</summary>
    private IReadOnlyList<DiskVolume> _volumes = [];
    private IFeedWatch<DiskSnapshot>? _snapshots;

    private sealed class TheSystemDrive : IDiskScope
    {
        public DiskScope Scope { get; set; }

        public string Drive { get; set; } = string.Empty;
    }

    /// <summary>[S-11912] A point is the drive's reads and writes per second, as the reading writes it.</summary>
    public override string? ValueAt(SeriesPoint point) => double.IsFinite(point.First) ? Formatting.Rate(point.First) : null;

    /// <summary>[S-11952] Its drive (the system drive when none is chosen); every drive together is none.</summary>
    public override Themia.Core.Links.DriveLink? DriveOf(WidgetConfig config)
        => config is IDiskScope disk ? DiskScopes.DriveLink(disk, SystemDrive.Root) : null;

    /// <summary>[S-2016] A click on the card opens Task Manager.</summary>
    public override CardOpen? Opens(WidgetConfig config) => CardOpen.TaskManager;

    /// <summary>
    /// [S-1161] The volumes come with the disk feed's snapshot, the one the reading itself is read
    /// from: no second walk, and a render on the sample machine names its drives ([S-1931]).
    /// </summary>
    public override void Watch(ISystemFeeds hub, IMeterHost host)
    {
        ArgumentNullException.ThrowIfNull(hub);
        ArgumentNullException.ThrowIfNull(host);
        _snapshots?.Dispose();
        _snapshots = hub.Watch(Sources.DiskActivity, snapshot => Volumes(snapshot, host));
        if (_snapshots.HasValue)
        {
            Volumes(_snapshots.Value, host);
        }
    }

    public override void Unwatch()
    {
        _snapshots?.Dispose();
        _snapshots = null;
    }

    private void Volumes(DiskSnapshot snapshot, IMeterHost host)
    {
        _volumes = snapshot.Volumes;
        host.SetTitle(Title());
    }

    /// <summary>"1023 KB/s", as the network meter's.</summary>
    public override ReadoutText Reading => ReadoutRules.Split(Formatting.Rate(1023 * 1024));

    /// <summary>[S-1161] The header's title on the reading too: the drive with the header switched off.</summary>
    public override string? ReadingTip => Title();

    /// <summary>[S-1821] Against the window's peak, rounded up to a doubling of 1 MB/s.</summary>
    public override ChartScale Scale => DiskRules.Scale;

    /// <summary>[S-6271] Reads and writes of the drive the reading is about, from the snapshot the reading itself comes from.</summary>
    public override MeterItemsWatch? WatchItems(ISystemFeeds hub, Action<IReadOnlyList<MeterItem>> onItems)
        => MeterItemsWatch.Of(hub, Sources.DiskActivity, snapshot => Items(snapshot.Activity?.Rates), onItems);

    public override void ApplyConfig(WidgetConfig config, IMeterHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        var previous = (_config.Scope, _root);
        _config = config as IDiskScope ?? new TheSystemDrive();
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
        ArgumentNullException.ThrowIfNull(host);
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
            new MeterItem(read, MeterPressure.Normal, Str.Details_Disk_Read.Text(), Formatting.Rate(rates.Read)),
            new MeterItem(write, MeterPressure.Normal, Str.Details_Disk_Write.Text(), Formatting.Rate(rates.Write)),
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
    private string Title() => DiskRules.HeaderTitle(Strings.Current, _config.Scope, _root, _volumes);
}
