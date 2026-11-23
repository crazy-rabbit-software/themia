using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;

namespace Themia.Sdk.Devices;

/// <summary>
/// [S-16631] What a device item draws in its icon's place: a drive (<see cref="Drive"/>: the shell's own picture of it, its
/// kind's stock icon in a render) or a device's glyph (<see cref="Glyph"/>, a Segoe Fluent code point) in its role.
/// </summary>
/// <param name="Glyph">The kind's glyph: the icon of a device, the placeholder of a drive until its picture is in.</param>
/// <param name="Role">The glyph's colour: primary for a device that is there (connected), secondary for one that is not.</param>
/// <param name="Drive">The drive the picture is the shell's of; null for a device drawn by its glyph.</param>
public sealed record DeviceIcon(string Glyph, ColourRole Role = ColourRole.TextSecondary, DriveEntry? Drive = null);

/// <summary>[S-16631] A device item's primary reading: the figure and its unit as the Readout draws them ("55" and "%").</summary>
public readonly record struct DeviceReading(string Value, string Unit)
{
    /// <summary>[S-16631] A 0..1 share as a whole percentage, split into its figure and unit ("55" "%", Turkish "%55" all figure).</summary>
    public static DeviceReading Percent(Locale locale, double fraction)
    {
        var split = ReadoutRules.Split(Formatting.Percent(locale, fraction));
        return new DeviceReading(split.Value, split.Unit);
    }
}

/// <summary>
/// [S-16670] The parts of a device item the user may leave out (owner, 2026-11-23: "settings of elements that are shown
/// should be all grouped together under a section"): a part left out takes no room and is not drawn, the others close up.
/// </summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<DevicePart>))]
public enum DevicePart
{
    /// <summary>The device's picture or glyph.</summary>
    Icon = 0,

    /// <summary>The line under the name: its figures in words, or why there are none.</summary>
    Details = 1,

    /// <summary>The percentage at the item's end.</summary>
    Reading = 2,

    /// <summary>The bar under the item.</summary>
    Bar = 3,
}

/// <summary>[S-16670] A device widget's config that lets the user leave parts of its items out.</summary>
public interface IDeviceParts
{
    /// <summary>The parts left out; none by default.</summary>
    List<DevicePart> HiddenParts { get; set; }
}

/// <summary>
/// [S-16630] [S-16631] One device as the device family draws it (owner, 2026-11-23: "i want you to create a base design that
/// all of those widgets can use. it should look very similar to how todays drive widget looks like"; and, the same day:
/// "from left to right: icon, then title and subtitle, then right aligned percentage, under all of them the progress
/// bar"): its icon, its name with its secondary line under it ("419 GB free of 931 GB", "Headphones · Connected"), its
/// primary reading at the end (a drive's used share, a device's battery) and the bar of that reading under them all. A
/// part the device has none of is no part ("if elements are not shown like the battery percentage for example, it should
/// also not show them in the design, not just grey them out"): no reading is no percentage and no bar, the line saying
/// why. Pure: a widget maps its own data onto it (a drive by <see cref="DeviceItems.Drive"/>, a Bluetooth device and a
/// battery in their modules).
/// </summary>
/// <param name="Id">Stable for the device while it is there: a drive's root, a device's endpoint or reading id.</param>
/// <param name="Name">What the device is called ("System (C:)", "Surface Headphones 2"), as Windows names it.</param>
/// <param name="Reading">The primary reading; null where the device gives none (or the widget reads none): not drawn.</param>
/// <param name="Line">The secondary line under the name: its figures in words, or why there is no reading.</param>
/// <param name="Fill">The bar's share, 0..1; null for a device without a reading: no bar.</param>
/// <param name="FillRole">The bar's colour: the accent, Caution or Critical by the reading's state ([S-6717]).</param>
/// <param name="Tooltip">Everything the item says, in words.</param>
public sealed record DeviceItem(
    string Id,
    string Name,
    DeviceIcon Icon,
    DeviceReading? Reading,
    string Line,
    double? Fill,
    ColourRole FillRole,
    string Tooltip)
{
    /// <summary>[S-16631] Whether the device has no reading now (its percentage and bar not drawn, its line saying why).</summary>
    public bool Absent => Reading is null;
}

/// <summary>[S-16632] The device family's mappings of shared data onto the item, and the glyphs of the kinds of device.</summary>
public static class DeviceItems
{
    /// <summary>
    /// [S-16632] [S-4653] A drive as a device item: its root the id, its name as This PC names it ("System (C:)"), its own
    /// picture, the used share the reading with its bar in the load's role (Caution from 70 %, Critical from 90 %), the
    /// space line under its name ("419 GB free of 931 GB"). A drive without figures has no percentage and no bar, its line
    /// saying why (a share Windows cannot reach "Disconnected network drive", "Connecting…", "No media", "Not connected").
    /// </summary>
    public static DeviceItem Drive(Locale locale, DriveEntry drive)
    {
        ArgumentNullException.ThrowIfNull(drive);
        var name = DriveRules.Title(locale, drive);
        var space = DriveRules.SpaceLine(locale, drive);
        var used = drive.UsedFraction;
        var line = used is null && drive.State == DriveState.Disconnected ? DriveRules.Description(locale, drive) : space;
        return new DeviceItem(
            drive.Root,
            name,
            new DeviceIcon(DriveRules.Glyph(drive), ColourRole.TextSecondary, drive),
            used is { } share ? DeviceReading.Percent(locale, share) : null,
            line,
            used ?? (drive.Kind == VolumeKind.Network ? 0 : null),
            DriveRules.Role(drive),
            $"{name} · {DriveRules.Description(locale, drive)} · {space}");
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
