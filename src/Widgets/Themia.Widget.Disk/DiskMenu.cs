using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;

namespace Themia.App.Shell.Config;

/// <summary>[S-1168] The disk reading's options, all on its properties menu ([S-3719]): Appearance, then whose throughput it shows (one drive, or every drive). It has no settings page.</summary>
internal static class DiskMenu
{
    /// <param name="drives">The drives Windows lists (the host's walk, <c>DriveList.Now()</c>).</param>
    public static void Lines(WidgetMenuLines menu, DiskConfig disk, IReadOnlyList<DriveEntry> drives)
    {
        ArgumentNullException.ThrowIfNull(menu);
        menu.Lines.Add(DiskMenuLines.Drive(menu, disk, drives));
    }
}
