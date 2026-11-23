using Themia.App.Shell;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Widgets;

namespace Themia.Widget.Drives;

/// <summary>[S-16657] A drive item's menu: Open in Explorer, Eject for a removable drive, then Refresh.</summary>
internal static class DriveItemMenu
{
    /// <param name="drive">The item's drive; null for the face with nothing to show (Refresh alone).</param>
    public static IReadOnlyList<ItemMenuEntry> Of(DriveEntry? drive, Action open, Action eject, Action refresh)
    {
        var entries = new List<ItemMenuEntry>();
        if (drive is not null)
        {
            entries.Add(new ItemMenuEntry(Glyphs.Folder, Str.WidgetsB_Disk_OpenInExplorer.Format(("drive", drive.Letter)), open));
            if (DriveRules.IsRemovable(drive))
            {
                entries.Add(new ItemMenuEntry(Glyphs.Usb, Str.Drives_Eject.Text(), eject));
            }

            entries.Add(ItemMenuEntry.Separator);
        }

        entries.Add(new ItemMenuEntry(Glyphs.Refresh, Str.Common_Refresh.Text(), refresh));
        return entries;
    }
}
