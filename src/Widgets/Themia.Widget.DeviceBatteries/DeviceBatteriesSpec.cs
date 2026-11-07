using Themia.Core.Localization;
using Themia.Sdk.Devices;
using Themia.Sdk.Kit;

namespace Themia.Widget.DeviceBatteries;

/// <summary>
/// [S-10001] The Device batteries widget on the kit: rows of the rich look held to two lines (the
/// device, its battery as the value; what it is and whether it charges under it), the kind's icon as
/// the lead, the battery kept at every width ([S-7073]), up to two columns.
/// </summary>
public static class DeviceBatteriesSpec
{
    /// <summary>The Open action's id: the view opens Windows' Bluetooth &amp; devices page itself.</summary>
    public const string OpenSettings = "settings";

    public static KitSpec Spec { get; } = new(
        KitRowLook.Rich,
        KitShow.Lead | KitShow.Subtitle | KitShow.When,
        () => DeviceBatteriesStr.DeviceBatteries_Unknown.Text(),
        () => DeviceBatteriesStr.DeviceBatteries_Empty.Text(),
        KitTime.None,
        MaxColumns: 2)
    {
        Readings = true,
    };

    /// <summary>[S-10001] The kind's icon.</summary>
    public static KitIcon Icon(DeviceKind kind) => kind switch
    {
        DeviceKind.Audio => KitIcon.Headphones,
        DeviceKind.Mouse => KitIcon.Mouse,
        DeviceKind.Keyboard => KitIcon.Keyboard,
        DeviceKind.Phone => KitIcon.Phone,
        DeviceKind.Gamepad => KitIcon.Gamepad,
        DeviceKind.Pen => KitIcon.Pen,
        DeviceKind.Watch => KitIcon.Watch,
        _ => KitIcon.Device,
    };

    /// <summary>[S-10001] What the device is, in words: the second line's start.</summary>
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
    /// [S-10001] [S-10003] A device as a kit item: its name, its battery as the value (Critical at or
    /// below the threshold), the kind and "Charging" (where it says so) on the second line, the kind's
    /// icon as the lead, and all of it in the tooltip, for the widths that drop a part.
    /// </summary>
    public static KitItem Item(DeviceBatteryRow row, int threshold, Locale locale)
    {
        ArgumentNullException.ThrowIfNull(row);

        var percent = LocaleFormat.Percent(locale, row.Percent);
        var kind = KindLabel(row.Kind, locale);
        var second = row.Charging == true ? $"{kind} · {DeviceBatteriesStr.DeviceBatteries_Charging.Text(locale)}" : kind;
        var name = row.Name.Length > 0 ? row.Name : DeviceBatteriesStr.DeviceBatteries_Unknown.Text(locale);
        var low = DeviceBatteriesRules.IsLow(row.Percent, threshold);

        return new KitItem(
            row.Id,
            row.Name,
            Subtitle: second,
            Lead: new KitLead(KitLeadKind.Icon, Icon(row.Kind).ToString()),
            Tone: low ? KitTone.Critical : KitTone.Neutral,
            Tooltip: $"{name} · {percent} · {second}",
            Value: percent);
    }

    /// <summary>[S-10005] A row's menu: Windows' Bluetooth &amp; devices page, and Refresh.</summary>
    public static IReadOnlyList<KitAction> Actions(KitItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return
        [
            new KitAction(KitActionKind.Open, DeviceBatteriesStr.DeviceBatteries_OpenSettings.Text(), KitIcon.Open, Id: OpenSettings),
            new KitAction(KitActionKind.Refresh, Themia.Core.Localization.Str.Common_Refresh.Text()),
        ];
    }
}
