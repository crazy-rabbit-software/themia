using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Settings;
using Themia.Core.Widgets;
using Themia.Sdk.Items;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Themia.Widget.Drive;

namespace Themia.App.Shell.Config;

/// <summary>[S-4674] The Drive card's options, all on its page ([S-3719]): Appearance, then which drive. It has no settings page.</summary>
internal static class DriveMenu
{
    /// <param name="drives">Every drive Windows has (the host's walk, <c>DriveList.Now()</c>).</param>
    public static void Lines(WidgetMenuLines menu, OneDriveKind kind, OneDriveConfig config, IReadOnlyList<DriveEntry> drives)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(kind);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(drives);

        // [S-4674] The look (Style, Appearance), a divider, then which drive.
        menu.Lines.Add(SettingsLine.Separator);
        // [S-16672] What the face shows, grouped in one section, after which drive.
        if (kind.Group is not { } group)
        {
            menu.Lines.Add(DriveMenuLines.Drive(menu, config.Drive, value => config.Drive = value, drives));
            menu.Lines.Add(Themia.Sdk.WinUI.Items.ItemMenuLines.Parts(menu, config, Themia.Sdk.Devices.DeviceItems.Parts, name: Themia.Sdk.WinUI.Items.ItemMenuLines.DevicePartName));
            return;
        }

        // [S-16646] A kind's drive: the first of its kind there, or one of its kind by name (a chosen one not there stays offered).
        var locale = Strings.Current;
        var options = new List<(string Value, string Text)> { (string.Empty, DriveStr.Drives_First.Text()) };
        options.AddRange(DriveRules.OfGroup(drives, group).Select(d => (d.Root, DriveRules.Title(locale, d))));
        var current = DiskRules.Root(config.Drive) ?? string.Empty;
        if (!options.Any(o => string.Equals(o.Value, current, StringComparison.OrdinalIgnoreCase)))
        {
            options.Add((current, Str.WidgetsB_Settings_Disk_NotConnectedDrive.Format(("drive", DiskRules.Instance(current)))));
        }

        var held = options.First(o => string.Equals(o.Value, current, StringComparison.OrdinalIgnoreCase)).Value;
        menu.Lines.Add(WidgetMenuLines.Shared(SharedSettings.Drive, menu.Choice(kind.Glyph, Str.Details_Disk_Drive.Text(), options, held, value => config.Drive = value)));
        menu.Lines.Add(Themia.Sdk.WinUI.Items.ItemMenuLines.Parts(menu, config, Themia.Sdk.Devices.DeviceItems.Parts, name: Themia.Sdk.WinUI.Items.ItemMenuLines.DevicePartName));
    }
}
