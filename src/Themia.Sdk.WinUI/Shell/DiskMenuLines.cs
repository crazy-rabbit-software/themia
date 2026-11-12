using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;

namespace Themia.App.Shell.Config;

/// <summary>[S-1168] [S-11000] The disk atoms' shared line: whose throughput they show.</summary>
public static class DiskMenuLines
{
    /// <summary>Every drive together: not a root, so it can never be confused with a drive.</summary>
    private const string All = "*";

    /// <summary>
    /// [S-1160] [S-1822] The drive the number is about: the system drive (said by name, so the
    /// widget's behaviour is never something to infer), every drive together, or one of the drives
    /// Windows lists. A chosen drive that has since been removed stays offered as "not connected",
    /// so the choice still reads true.
    /// </summary>
    /// <param name="drives">The drives Windows lists (the host's walk, <c>DriveList.Now()</c>).</param>
    public static ItemMenuEntry Drive(WidgetMenuLines menu, IDiskScope disk, IReadOnlyList<DriveEntry> drives)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(disk);
        ArgumentNullException.ThrowIfNull(drives);

        var system = DiskRules.Root(SystemDrive.Root) ?? "C:\\";
        var options = new List<(string Value, string Text)>
        {
            (string.Empty, Str.Shell_Meters_SystemDrive.Format(("drive", DiskRules.Instance(system)))),
            (All, Str.WidgetsB_Disk_AllDrives.Text()),
        };

        foreach (var volume in DriveRules.LocalVolumes(drives))
        {
            options.Add((volume.Root, DiskRules.Title(volume)));
        }

        var current = disk.Scope == DiskScope.AllDrives ? All : DiskRules.Root(disk.Drive) ?? string.Empty;
        var held = options.FindIndex(o => string.Equals(o.Value, current, StringComparison.OrdinalIgnoreCase));
        if (held < 0)
        {
            options.Add((current, Str.WidgetsB_Settings_Disk_NotConnectedDrive.Format(("drive", DiskRules.Instance(current)))));
            held = options.Count - 1;
        }

        return menu.Choice(Glyphs.Disk, Str.Details_Disk_Drive.Text(), options, options[held].Value,
            value => (disk.Scope, disk.Drive) = value == All ? (DiskScope.AllDrives, string.Empty) : (DiskScope.OneDrive, value));
    }
}
