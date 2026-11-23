using Themia.App.Shell;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Sdk.Items;
using Themia.Sdk.Devices;
using Themia.Sdk.Kit;

namespace Themia.Widget.DeviceBatteries;

/// <summary>
/// [S-10001] [S-16665] The peripherals' batteries on the device family's item ([S-16630]): the kind's glyph, the device's
/// name, its battery as the reading with its bar (Critical at or below the threshold), what it is and whether it charges
/// as its line ("Controller · Charging").
/// </summary>
public static class DeviceBatteriesItems
{
    /// <summary>[S-10005] Windows' Bluetooth &amp; devices page, where a device's battery is Windows' own.</summary>
    public static readonly Uri SettingsPage = new("ms-settings:bluetooth");

    /// <summary>[S-10001] What the device is, in words: the line's start.</summary>
    public static string KindLabel(DeviceKind kind, Locale locale) => (kind switch
    {
        DeviceKind.Audio => DeviceBatteriesStr.DeviceBatteries_Kind_Audio,
        DeviceKind.Mouse => DeviceBatteriesStr.DeviceBatteries_Kind_Mouse,
        DeviceKind.Keyboard => DeviceBatteriesStr.DeviceBatteries_Kind_Keyboard,
        DeviceKind.Phone => DeviceBatteriesStr.DeviceBatteries_Kind_Phone,
        DeviceKind.Gamepad => DeviceBatteriesStr.DeviceBatteries_Kind_Gamepad,
        DeviceKind.Pen => DeviceBatteriesStr.DeviceBatteries_Kind_Pen,
        DeviceKind.Watch => DeviceBatteriesStr.DeviceBatteries_Kind_Watch,
        _ => DeviceBatteriesStr.DeviceBatteries_Kind_Device,
    }).Text(locale);

    /// <summary>
    /// [S-10001] [S-10003] [S-16665] A device as a device item: its name, its battery the reading and the bar (Critical at
    /// or below <paramref name="threshold"/>), the kind and "Charging" (where its source says so) its line, the kind's
    /// glyph its icon, and all of it in the tooltip.
    /// </summary>
    public static Item Item(DeviceBatteryRow row, int threshold, Locale locale)
    {
        ArgumentNullException.ThrowIfNull(row);

        var percent = LocaleFormat.Percent(locale, row.Percent);
        var kind = KindLabel(row.Kind, locale);
        var line = row.Charging == true ? $"{kind} · {DeviceBatteriesStr.DeviceBatteries_Charging.Text(locale)}" : kind;
        var name = row.Name.Length > 0 ? row.Name : DeviceBatteriesStr.DeviceBatteries_Unknown.Text(locale);
        return new Item(
            row.Id,
            name,
            new ItemLead(DeviceItems.Glyph(row.Kind), ColourRole.TextPrimary),
            line,
            ItemEnd.Percent(locale, row.Percent / 100.0),
            new ItemBar(row.Percent / 100.0, 0, DeviceItems.BatteryRole(row.Percent, threshold)),
            Tooltip: $"{name} · {percent} · {line}");
    }

    /// <summary>
    /// [S-10000] [S-16665] WR-10: no battery to show keeps the face, one item: the devices glyph, "--", the empty bar and why
    /// ("No connected device reports its battery", "Couldn't read the batteries", reading).
    /// </summary>
    public static Item Empty(DeviceBatteryStatus status, Locale locale)
    {
        var why = status switch
        {
            DeviceBatteryStatus.Failed => DeviceBatteriesStr.DeviceBatteries_Failed.Text(locale),
            DeviceBatteryStatus.Loading => Strings.Get(locale, Str.Common_Loading),
            _ => DeviceBatteriesStr.DeviceBatteries_Empty.Text(locale),
        };
        var role = status == DeviceBatteryStatus.Failed ? ColourRole.Caution : ColourRole.TextSecondary;
        return new Item(string.Empty, string.Empty, new ItemLead(Glyphs.Devices, role), why, Tooltip: why);
    }

    /// <summary>[S-10005] A device's menu: Windows' Bluetooth &amp; devices page, and Refresh.</summary>
    public static IReadOnlyList<ItemMenuEntry> Menu(Action open, Action refresh) =>
    [
        new ItemMenuEntry(Glyphs.Open, DeviceBatteriesStr.DeviceBatteries_OpenSettings.Text(), open),
        ItemMenuEntry.Separator,
        new ItemMenuEntry(Glyphs.Refresh, Str.Common_Refresh.Text(), refresh),
    ];
}
