using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Themia.Widget.Disk;

namespace Themia.App.Shell.Config;

/// <summary>
/// [S-1168] The disk meter's options, all on its properties menu ([S-3719]): Appearance, then whose
/// throughput it shows (one drive, or every drive); under Style the chart span every meter has
/// ([S-1122]). It has no settings page.
/// </summary>
internal static class DiskMenu
{
    /// <summary>Every drive together: not a root, so it can never be confused with a drive.</summary>
    private const string All = "*";

    /// <param name="drives">The drives Windows lists (the host's walk, <c>DriveList.Now()</c>).</param>
    public static void Lines(WidgetMenuLines menu, DiskConfig disk, IReadOnlyList<DriveEntry> drives)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(disk);
        ArgumentNullException.ThrowIfNull(drives);

        menu.PlaceCard();
        menu.Lines.Add(DriveLine(menu, disk, drives));
        MeterMenuLines.Lines(menu, disk);
    }

    /// <summary>
    /// [S-1160] [S-1822] The drive the number is about: the system drive (said by name, so the
    /// widget's behaviour is never something to infer), every drive together, or one of the drives
    /// Windows lists. A chosen drive that has since been removed stays offered as "not connected",
    /// so the choice still reads true.
    /// </summary>
    private static ItemMenuEntry DriveLine(WidgetMenuLines menu, DiskConfig disk, IReadOnlyList<DriveEntry> drives)
    {
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

        return menu.Choice(Glyphs.Disk, DiskStr.Shell_Meters_Drive_Label.Text(), options, options[held].Value,
            value => (disk.Scope, disk.Drive) = value == All ? (DiskScope.AllDrives, string.Empty) : (DiskScope.OneDrive, value));
    }
}
