using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.App.Shell.Config;

internal static partial class WidgetSettings
{
    /// <summary>
    /// [S-1160] [S-1168] [S-1822] Whose throughput the meter shows — one drive, or every drive —
    /// then the rows every meter has ([S-1762]).
    /// </summary>
    public static void Disk(List<FrameworkElement> rows, DiskConfig disk, Action save)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(disk);
        ArgumentNullException.ThrowIfNull(save);

        rows.Add(FluentLayout.Header(Str.WidgetsB_Settings_Disk_Disk.Text()));
        AddDriveRow(rows, disk, save);

        MeterSettingsRows.Rows(rows, disk, save);
    }

    /// <summary>
    /// [S-1160] [S-1822] The drive the number is about: the system drive (said by name, so the
    /// widget's behaviour is never something to infer), every drive together, or one of the drives
    /// Windows lists. A chosen drive that has since been removed stays offered, so the choice still
    /// reads true.
    /// </summary>
    private static void AddDriveRow(List<FrameworkElement> rows, DiskConfig disk, Action save)
    {
        // Not a root, so it can never be confused with a drive.
        const string All = "*";

        var system = DiskRules.Root(SystemDrive.Root) ?? "C:\\";
        var options = new List<(string Value, string Label)>
        {
            (string.Empty, Str.Shell_Meters_SystemDrive.Format(("drive", DiskRules.Instance(system)))),
            (All, Str.WidgetsB_Disk_AllDrives.Text()),
        };

        foreach (var volume in DriveRules.LocalVolumes(DriveList.Now()))
        {
            options.Add((volume.Root, DiskRules.Title(volume)));
        }

        var current = disk.Scope == DiskScope.AllDrives ? All : DiskRules.Root(disk.Drive) ?? string.Empty;

        if (!options.Any(o => string.Equals(o.Value, current, StringComparison.OrdinalIgnoreCase)))
        {
            options.Add((current, Str.WidgetsB_Settings_Disk_NotConnectedDrive.Format(("drive", DiskRules.Instance(current)))));
        }

        rows.Add(FluentLayout.Row(Glyphs.Disk, Str.Shell_Meters_Drive_Label.Text(), Str.Shell_Meters_Drive_Desc.Text(),
            FluentLayout.Choice(
                [.. options.Select((o, i) => (i, o.Label))],
                Math.Max(0, options.FindIndex(o => string.Equals(o.Value, current, StringComparison.OrdinalIgnoreCase))),
                index =>
                {
                    var value = options[index].Value;
                    (disk.Scope, disk.Drive) = value == All ? (DiskScope.AllDrives, string.Empty) : (DiskScope.OneDrive, value);
                    save();
                }),
            "drive", "volume", "disk", "read", "write", "throughput", "activity", "i/o"));
    }
}
