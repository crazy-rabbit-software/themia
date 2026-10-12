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
    private DiskConfig _config = new();
    private string _root = DiskMetrics.SystemRoot;

    public override WidgetKind Kind => WidgetKind.Disk;

    /// <summary>"1.2 MB/s", as the network meter's, and the drive after it.</summary>
    public override double ReadingCharacters => 8 + 4 + (Token?.Length ?? 0);

    /// <summary>[S-1161] "C:", or "All drives": which throughput this is, with no header to say so.</summary>
    public override string? Token => _config.Scope == DiskScope.AllDrives ? Str.WidgetsB_Disk_AllDrives.Text() : DiskRules.Instance(_root);

    /// <summary>[S-1821] Against the window's peak, rounded up to a doubling of 1 MB/s.</summary>
    public override ChartScale Scale => DiskRules.Scale;

    public override void ApplyConfig(WidgetConfig config, IMeterHost host)
    {
        _config = config as DiskConfig ?? new DiskConfig();
        _root = DiskRules.Root(_config.Drive) ?? DiskMetrics.SystemRoot;
        host.SetTitle(Title());
    }

    /// <summary>[S-1161] The title follows the hub's walk of the volumes (a label renamed, a drive plugged in).</summary>
    public override void Observe(MeterReading reading, IMeterHost host) => host.SetTitle(Title());

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
