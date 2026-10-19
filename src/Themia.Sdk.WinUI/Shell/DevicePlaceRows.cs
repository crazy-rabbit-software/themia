using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Sdk.WinUI;

namespace Themia.App.Shell.Config;

/// <summary>
/// [S-2850] [S-2851] The device-location lines every widget about a place shares on its properties
/// menu (the weather family's and the Sun's and Moon's): the Use my location switch, as Windows
/// words it, and while it is on and Windows gives no position, why, in the menu's own words.
/// </summary>
public static class DevicePlaceRows
{
    /// <summary>
    /// The switch; while it is on and no position is held, a note saying what Windows says (finding
    /// it, turned off, not available), and while location is turned off, the line that opens
    /// Windows' location settings, where it is turned on.
    /// </summary>
    public static IReadOnlyList<ItemMenuEntry> Lines(WidgetMenuLines menu, IPlaced config)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);

        List<ItemMenuEntry> lines = [menu.Toggle(Glyphs.Location, Str.Shell_Place_UseDevice.Text(), config.UseDeviceLocation, on => config.UseDeviceLocation = on)];
        if (!config.UseDeviceLocation || DevicePlaces.Waits((WidgetConfig)config, menu.Device) is not { } waits)
        {
            return lines;
        }

        lines.Add(WidgetMenuLines.Note(Sentence(waits)));
        if (waits == DeviceLocationState.Denied)
        {
            lines.Add(new(Glyphs.Settings, Str.Shell_Place_DeviceSettings.Text(), () => menu.OpenLocationSettings?.Invoke(), Disabled: menu.OpenLocationSettings is null));
        }

        return lines;
    }

    /// <summary>[S-2854] What a widget waiting for the device's location says of it (its card's words too).</summary>
    public static string Sentence(DeviceLocationState waits) => waits switch
    {
        DeviceLocationState.Denied => Str.Shell_Place_DeviceDenied.Text(),
        DeviceLocationState.Unavailable => Str.Shell_Place_DeviceUnavailable.Text(),
        _ => Str.Shell_Place_DeviceLocating.Text(),
    };
}
