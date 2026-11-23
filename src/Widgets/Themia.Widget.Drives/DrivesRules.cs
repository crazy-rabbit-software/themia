using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk.Items;
using Themia.Sdk;
using Themia.Sdk.Devices;

namespace Themia.Widget.Drives;

/// <summary>
/// [S-16650] A drive list as data (owner, 2026-11-23: "for all of those there should also be the list version of it that
/// basically shows the list of them"): its id, the kinds of drive it lists, its words and glyph, its place on the add page,
/// why it shows none, and the removed kind a placed one of stands in for.
/// </summary>
public sealed record DriveListKind(
    string Id,
    IReadOnlyList<DriveGroup> Groups,
    LocalText Label,
    LocalText Description,
    string Glyph,
    IReadOnlyList<string> Keywords,
    int Rank,
    Func<Locale, string> Nothing,
    IReadOnlyList<MergeFrom> Merges);

/// <summary>[S-16650] The family's kinds: the PC's drives, the USB sticks, the network drives.</summary>
public static class DriveListKinds
{
    public static DriveListKind Drives { get; } = new(
        "drives", [DriveGroup.Local], DrivesStr.Drives_List_Label.Local(), DrivesStr.Drives_List_Desc.Local(), Glyphs.Disk,
        ["drives", "disks", "storage", "free space", "ssd", "hdd", "disc"], 1,
        locale => DrivesStr.Drives_List_None.Text(locale), []);

    /// <summary>[S-16656] A placed Removable drives widget becomes this list in place (owner, 2026-11-23).</summary>
    public static DriveListKind UsbSticks { get; } = new(
        "usbSticks", [DriveGroup.External], DrivesStr.Drives_Sticks_Label.Local(), DrivesStr.Drives_Sticks_Desc.Local(), Glyphs.Usb,
        ["usb", "usb sticks", "flash drive", "sd card", "memory card", "external disk", "eject", "safely remove"], 3,
        locale => DrivesStr.Drives_Nothing.Text(locale), [new MergeFrom("removableDrives")]);

    public static DriveListKind NetworkDrives { get; } = new(
        "networkDrives", [DriveGroup.Network], DrivesStr.Drives_Shares_Label.Local(), DrivesStr.Drives_Shares_Desc.Local(), Glyphs.NetworkDrive,
        ["network drives", "mapped drives", "nas", "shares", "server"], 5,
        locale => DrivesStr.Drives_Shares_None.Text(locale), []);
}

/// <summary>[S-16650]..[S-16652] Which drives a drive list shows, in what order. Pure.</summary>
public static class DrivesRules
{
    /// <summary>[S-16650] The drives of the list's kinds in a walk, by letter (an empty reader left out), hidden ones too.</summary>
    public static IReadOnlyList<DriveEntry> OfKind(IEnumerable<DriveEntry> drives, DriveListKind kind)
    {
        ArgumentNullException.ThrowIfNull(kind);
        var walk = (drives ?? []).ToList();
        return [.. kind.Groups.SelectMany(g => DriveRules.OfGroup(walk, g)).OrderBy(d => d.Root, StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>[S-16650] [S-16651] [S-16652] The drives the list shows: of its kind, not hidden, the user's order first.</summary>
    public static IReadOnlyList<DriveEntry> Arrange(IEnumerable<DriveEntry> drives, DriveListKind kind, IItemChoice config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return ItemOrder.Arrange(OfKind(drives, kind), d => d.Root, config.Hidden, config.Order);
    }
}
