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

        // [S-4674] The look (Style, Appearance), a divider, then which drive.
        menu.Lines.Add(ItemMenuEntry.Separator);
        menu.Lines.Add(DriveMenuLines.Drive(menu, config.Drive, value => config.Drive = value, drives));
    }
}
