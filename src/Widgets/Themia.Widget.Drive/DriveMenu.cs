using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Themia.Widget.Drive;

namespace Themia.App.Shell.Config;

/// <summary>[S-4674] The Drive card's options, all on its properties menu ([S-3719]): Appearance, then which drive. It has no settings page.</summary>
internal static class DriveMenu
{
    /// <param name="drives">Every drive Windows has (the host's walk, <c>DriveList.Now()</c>).</param>
    public static void Lines(WidgetMenuLines menu, DriveConfig config, IReadOnlyList<DriveEntry> drives)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(drives);

        menu.PlaceCard();
        menu.Lines.Add(DriveLine(menu, config, drives));
    }

    /// <summary>
    /// [S-4674] Which drive: the system drive (said by name), then every drive Windows has — local,
    /// removable and mapped network drives, which are listed from the redirector without asking their
    /// shares. A chosen drive that is not there stays offered as "not connected".
    /// </summary>
    private static ItemMenuEntry DriveLine(WidgetMenuLines menu, DriveConfig config, IReadOnlyList<DriveEntry> drives)
    {
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

        var current = DiskRules.Root(config.Drive) ?? string.Empty;
        var held = options.FindIndex(o => string.Equals(o.Value, current, StringComparison.OrdinalIgnoreCase));
        if (held < 0)
        {
            options.Add((current, Str.WidgetsB_Settings_Disk_NotConnectedDrive.Format(("drive", DiskRules.Instance(current)))));
            held = options.Count - 1;
        }

        return menu.Choice(Glyphs.Disk, DriveStr.Drives_Settings_Drive_Label.Text(), options, options[held].Value, value => config.Drive = value);
    }
}
