using Microsoft.UI.Xaml;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Widget.Drive;

namespace Themia.App.Shell.Config;

internal static class DriveSettings
{
    /// <summary>
    /// [S-4674] Which drive: the system drive (said by name), then every drive Windows has — local,
    /// removable and mapped network drives, which are listed from the redirector without asking their
    /// shares. A chosen drive that is not there stays offered as "not connected".
    /// </summary>
    public static void Rows(List<FrameworkElement> rows, DriveConfig config, Action save)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(save);

        var locale = Strings.Current;
        var system = DiskRules.Root(SystemDrive.Root) ?? "C:\\";
        var options = new List<(string Value, string Label)>
        {
            (string.Empty, Str.Shell_Meters_SystemDrive.Format(("drive", DiskRules.Instance(system)))),
        };

        // The last walk if a drive widget made one, else one now: local drives at once, a share pending.
        foreach (var drive in DriveList.Now())
        {
            options.Add((drive.Root, DriveRules.Title(locale, drive)));
        }

        var current = DiskRules.Root(config.Drive) ?? string.Empty;
        if (!options.Any(o => string.Equals(o.Value, current, StringComparison.OrdinalIgnoreCase)))
        {
            options.Add((current, Str.WidgetsB_Settings_Disk_NotConnectedDrive.Format(("drive", DiskRules.Instance(current)))));
        }

        rows.Add(FluentLayout.Row(Glyphs.Disk, DriveStr.Drives_Settings_Drive_Label.Text(), DriveStr.Drives_Settings_Drive_Desc.Text(),
            FluentLayout.Choice(
                [.. options.Select((o, i) => (i, o.Label))],
                Math.Max(0, options.FindIndex(o => string.Equals(o.Value, current, StringComparison.OrdinalIgnoreCase))),
                index =>
                {
                    config.Drive = options[index].Value;
                    save();
                }),
            "drive", "disk", "volume", "network drive", "share"));
    }
}
