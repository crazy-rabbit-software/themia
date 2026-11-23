using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk.Items;
using Themia.Sdk.Kit;

namespace Themia.Sdk.Devices;

/// <summary>
/// [S-16630] [S-16632] The device family's mappings of shared data onto the item base's item (<see cref="Item"/>; owner,
/// 2026-11-23: "i want you to create a base design that all of those widgets can use. it should look very similar to how
/// todays drive widget looks like"): its lead the device's picture or glyph, its title the name, its subtitle the details
/// ("419 GB free of 931 GB", "Headphones · Connected"), its end the primary reading (a drive's used share, a device's
/// battery) and its bar that reading. A part the device has none of is no part: no reading is no percentage and no bar,
/// the line saying why. And the glyphs of the kinds of device.
/// </summary>
public static class DeviceItems
{
    /// <summary>The parts a device item has: every part but the note.</summary>
    public static IReadOnlyList<ItemPart> Parts { get; } = [ItemPart.Lead, ItemPart.Title, ItemPart.Subtitle, ItemPart.End, ItemPart.Bar];

    /// <summary>
    /// [S-17180] [S-17181] What a device list draws with <paramref name="hidden"/> left out: its reading at the end, the bar
    /// standing beside it at the item's far end (owner, 2026-11-23: "it should be all the way on the right side as a vertical
    /// bar and not horizontal anymore"), its columns even ("the columns in the device view should also all get the same
    /// width").
    /// </summary>
    public static ItemShape Shape(IReadOnlyCollection<ItemPart> hidden) => ItemShape.Of(Parts, hidden, reading: true, barAt: ItemBarAt.Side, even: true);

    /// <summary>[S-16634] [S-17180] A device list's sizing in its arrangement, its items drawn as <see cref="Shape"/> says.</summary>
    public static WidgetSizing Sizing(IItemList config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return ItemLayout.Sizing(config.Arrangement, config.Rows, config.Gap, Shape(config.HiddenParts), titles: config.ColumnTitles);
    }

    /// <summary>
    /// [S-16632] [S-4653] A drive as an item: its root the id, its name as This PC names it ("System (C:)"), its own picture,
    /// the used share the reading with its bar in the load's role (Caution from 70 %, Critical from 90 %), the space line
    /// under its name ("419 GB free of 931 GB"). A drive without figures has no percentage and no bar, its line saying why
    /// (a share Windows cannot reach "Disconnected network drive", "Connecting…", "No media", "Not connected").
    /// </summary>
    public static Item Drive(Locale locale, DriveEntry drive)
    {
        ArgumentNullException.ThrowIfNull(drive);
        var name = DriveRules.Title(locale, drive);
        var space = DriveRules.SpaceLine(locale, drive);
        var used = drive.UsedFraction;
        var line = used is null && drive.State == DriveState.Disconnected ? DriveRules.Description(locale, drive) : space;
        return new Item(
            drive.Root,
            name,
            new ItemLead(DriveRules.Glyph(drive), ColourRole.TextSecondary, drive),
            line,
            used is { } share ? ItemEnd.Percent(locale, share) : null,
            // [S-16631] A network drive not connected keeps its bar, empty (owner, 2026-11-23: "the network drives should show
            // the progress bar even if they are not connected. otherwise they have empty looking space.").
            used is { } fill ? new ItemBar(fill, 0, DriveRules.Role(drive)) : drive.Kind == VolumeKind.Network ? new ItemBar(0, 0, DriveRules.Role(drive)) : null,
            Tooltip: $"{name} · {DriveRules.Description(locale, drive)} · {space}");
    }

    /// <summary>[S-16632] [S-240] A peripheral's glyph by its kind: headphones, mouse, keyboard, phone, controller, pen, watch, any other device.</summary>
    public static string Glyph(DeviceKind kind) => kind switch
    {
        DeviceKind.Audio => Glyphs.Headphone,
        DeviceKind.Mouse => Glyphs.Mouse,
        DeviceKind.Keyboard => Glyphs.Keyboard,
        DeviceKind.Phone => Glyphs.Phone,
        DeviceKind.Gamepad => Glyphs.Game,
        DeviceKind.Pen => Glyphs.Pen,
        DeviceKind.Watch => Glyphs.Watch,
        _ => Glyphs.Devices,
    };

    /// <summary>
    /// [S-16632] A battery's bar: the accent, Critical at or below <paramref name="low"/> % (the battery's own low, as the
    /// tray turns a low battery red); a battery is good full, so it never takes Caution for being high.
    /// </summary>
    public static ColourRole BatteryRole(int percent, int low) => percent <= low ? ColourRole.Critical : ColourRole.Accent;
}
