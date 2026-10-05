using Themia.App.Services;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Widgets.Meters;

/// <summary>
/// [S-1820]..[S-1822] Disk activity: the drive's reads and writes together, per second, as the
/// reading and as the chart — of the drive the settings name, or of every drive. The header names
/// the drive as Explorer does, and the card's menu opens it.
/// <para>
/// [S-1820] Space used is not shown (user requirement): it barely moves, and the question a disk
/// meter on the desktop answers is "is the disk busy", not "is it full" — File Explorer says that.
/// </para>
/// </summary>
internal sealed class DiskMeter : MeterPart
{
    private DiskConfig _config = new();
    private string? _root;
    private (DiskScope, string)? _measured;
    private IDisposable? _counters;

    public override WidgetKind Kind => WidgetKind.Disk;

    /// <summary>"1.2 MB/s", as the network meter's.</summary>
    public override double ReadingCharacters => 8;

    /// <summary>[S-1821] Against the window's peak, rounded up to a doubling of 1 MB/s.</summary>
    public override ChartScale Scale => DiskRules.Scale;

    public override bool ApplyConfig(WidgetConfig config, IMeterHost host)
    {
        _config = config as DiskConfig ?? new DiskConfig();

        // [S-1162] The reading is the counters' rate, so they are held for as long as the meter lives.
        _counters ??= DiskMetrics.Acquire();

        // [S-1160] [S-1822] A different drive is a different measurement: the chart's samples belong
        // to the one before (the original's [S-22]). On the first config there is nothing to clear.
        _root = DiskRules.Root(_config.Drive) ?? DiskMetrics.SystemRoot;
        var previous = _measured;
        _measured = (_config.Scope, _config.Scope == DiskScope.AllDrives ? string.Empty : _root);

        if (previous is null || previous == _measured)
        {
            return false;
        }

        host.ClearHistory();
        return true;
    }

    /// <summary>[S-1166] Open the drive the reading is about in Explorer. Every drive at once has no one folder to open.</summary>
    public override IReadOnlyList<WidgetAction> ContextActions(IMeterHost host)
    {
        if (_config.Scope == DiskScope.AllDrives)
        {
            return [];
        }

        var root = _root ?? DiskMetrics.SystemRoot;
        return [new WidgetAction(Glyphs.Folder, Str.WidgetsB_Disk_OpenInExplorer.Format(("drive", DiskRules.Instance(root))), () => ShellLauncher.Open(root))];
    }

    public override void Sample(IMeterHost host)
        => Present(host, DiskMetrics.Volumes(refresh: true), DiskMetrics.Activity()?.Rates);

    /// <summary>[S-1820] One sample on screen: the title for what is measured, and its throughput or why there is none.</summary>
    private void Present(IMeterHost host, IReadOnlyList<DiskVolume> volumes, IReadOnlyDictionary<string, DiskRates>? rates)
    {
        var root = _root ?? DiskMetrics.SystemRoot;
        host.SetTitle(Title(root, volumes));

        var sample = DiskRules.Read(_config.Scope, root, rates);

        switch (sample.State)
        {
            case DiskReadingState.Rate:
                var total = sample.Rates.Total;

                // The fraction is only the "this is a sample" mark: a window-scaled chart plots the value.
                host.Present(new MeterReading(ChartScale.Fraction(total, DiskRules.ChartFloor), Formatting.Rate(total), true, Value: total));
                break;

            case DiskReadingState.Waiting:
                // The counters' first collection: no rate yet, so no number and no point on the chart.
                host.Present(new MeterReading(null, "--", false));
                break;

            case DiskReadingState.DriveMissing:
                host.Present(MeterReading.Unavailable(Str.WidgetsB_Disk_Unavailable.Format(("drive", DiskRules.Instance(root)))));
                break;

            default:
                host.Present(MeterReading.Unavailable(Str.WidgetsB_Meters_Unavailable.Text()));
                break;
        }
    }

    /// <summary>[S-1161] "System (C:)" for a drive Windows lists, "C:" for one it does not, "All drives" for every one.</summary>
    private string Title(string root, IReadOnlyList<DiskVolume> volumes)
    {
        if (_config.Scope == DiskScope.AllDrives)
        {
            return Str.WidgetsB_Disk_AllDrives.Text();
        }

        foreach (var volume in volumes)
        {
            if (string.Equals(volume.Root, root, StringComparison.OrdinalIgnoreCase))
            {
                return DiskRules.Title(volume);
            }
        }

        return DiskRules.Instance(root);
    }

    public override void Detach()
    {
        _counters?.Dispose();
        _counters = null;
    }
}
