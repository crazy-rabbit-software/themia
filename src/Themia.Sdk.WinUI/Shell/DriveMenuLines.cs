using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;

namespace Themia.App.Shell.Config;

/// <summary>[S-4674] [S-11026] The drive atoms' shared line: which drive (the reading's and the facts').</summary>
public static class DriveMenuLines
{
    /// <summary>
    /// [S-4674] Which drive: the system drive (said by name), then every drive Windows has — local,
    /// removable and mapped network drives, which are listed from the redirector without asking their
    /// shares. A chosen drive that is not there stays offered as "not connected".
    /// </summary>
    /// <param name="drives">Every drive Windows has (the host's walk, <c>DriveList.Now()</c>).</param>
    public static ItemMenuEntry Drive(WidgetMenuLines menu, string chosen, Action<string> choose, IReadOnlyList<DriveEntry> drives)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(drives);

        var locale = Strings.Current;
        var system = DiskRules.Root(SystemDrive.Root) ?? "C:\\";
        var options = new List<(string Value, string Text)>
        {
            (string.Empty, Str.Shell_Meters_SystemDrive.Format(("drive", DiskRules.Instance(system)))),
        };

        foreach (var drive in drives)
        {
            options.Add((drive.Root, DriveRules.Title(locale, drive)));
        }

        var current = DiskRules.Root(chosen) ?? string.Empty;
        var held = options.FindIndex(o => string.Equals(o.Value, current, StringComparison.OrdinalIgnoreCase));
        if (held < 0)
        {
            options.Add((current, Str.WidgetsB_Settings_Disk_NotConnectedDrive.Format(("drive", DiskRules.Instance(current)))));
            held = options.Count - 1;
        }

        return WidgetMenuLines.Shared(SharedSettings.Drive, menu.Choice(Glyphs.Disk, Str.Details_Disk_Drive.Text(), options, options[held].Value, choose));
    }
}
