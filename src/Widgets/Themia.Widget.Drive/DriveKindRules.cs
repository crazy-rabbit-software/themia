using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk.Devices;
using Themia.Sdk.Kit;

namespace Themia.Widget.Drive;

/// <summary>[S-4670] [S-16646] Which drive a one-drive widget shows, by its kind. Pure.</summary>
public static class DriveKindRules
{
    /// <summary>
    /// [S-4670] [S-16646] The drive a widget of <paramref name="group"/> shows: any drive (null: the one named, else the
    /// system drive, [S-4670]); a kind's the one named while it is there, else that one not connected (it keeps its place);
    /// with none named, the first of its kind there by letter; null where there is none.
    /// </summary>
    public static DriveEntry? Pick(DriveGroup? group, IReadOnlyList<DriveEntry> drives, string? chosen, string systemRoot)
    {
        ArgumentNullException.ThrowIfNull(drives);
        if (group is not { } kind)
        {
            return DriveRules.Pick(drives, chosen, systemRoot);
        }

        if (DiskRules.Root(chosen) is { } root)
        {
            return DriveRules.Pick(drives, root, systemRoot);
        }

        return DriveRules.OfGroup(drives, kind).FirstOrDefault();
    }

    /// <summary>[S-16646] WR-10: none of the kind there keeps the face: its glyph, "--", the empty bar and why ("Nothing plugged in").</summary>
    public static DeviceItem Nothing(OneDriveKind kind, Locale locale)
    {
        ArgumentNullException.ThrowIfNull(kind);
        var why = kind.Nothing(locale);
        return new DeviceItem(string.Empty, string.Empty, new DeviceIcon(kind.Glyph), null, why, null, ColourRole.Accent, why);
    }
}
