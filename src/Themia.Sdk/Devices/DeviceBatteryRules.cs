namespace Themia.Sdk.Devices;

/// <summary>
/// What a peripheral is, as far as its icon is concerned. A closed set: the glyph tables cover every
/// case at compile time. The Bluetooth widget's own kind has the same members in the same order.
/// </summary>
public enum DeviceKind
{
    Unknown = 0,
    Audio = 1,
    Mouse = 2,
    Keyboard = 3,
    Phone = 4,
    Gamepad = 5,
    Pen = 6,
    Watch = 7,
}

/// <summary>[S-240] What a device is, guessed from what Windows says of it.</summary>
public static class DeviceKinds
{
    /// <summary>
    /// [S-240] Guess what a device is from its name: headphones and buds are audio, then mouse,
    /// keyboard, watch, phone, controller, pen; anything else (and no name) the generic device.
    /// <para>
    /// A guess, and only ever used to pick an icon. Windows exposes a class of device for Classic
    /// devices and an appearance for LE ones, but neither is filled in for every paired device, and a
    /// list where half the rows have an icon looks broken in a way a slightly wrong icon does not.
    /// </para>
    /// </summary>
    public static DeviceKind FromName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return DeviceKind.Unknown;
        }

        var lower = name.ToLowerInvariant();

        if (Contains(lower, "headphone", "headset", "speaker", "earbud", "airpod", "buds", "audio", "soundbar", "soundcore"))
        {
            return DeviceKind.Audio;
        }

        if (Contains(lower, "mouse", "trackpad", "magic trackpad"))
        {
            return DeviceKind.Mouse;
        }

        if (Contains(lower, "keyboard", "keychron"))
        {
            return DeviceKind.Keyboard;
        }

        // Before the phone brands, not after: a "Galaxy Watch" and a "Pixel Watch" both carry a
        // phone maker's name and are not phones. The more specific word wins.
        if (Contains(lower, "watch", "band", "fitbit", "garmin"))
        {
            return DeviceKind.Watch;
        }

        if (Contains(lower, "phone", "iphone", "galaxy", "pixel"))
        {
            return DeviceKind.Phone;
        }

        if (Contains(lower, "controller", "gamepad", "xbox", "dualsense", "dualshock", "joy-con"))
        {
            return DeviceKind.Gamepad;
        }

        if (Contains(lower, "pen", "stylus"))
        {
            return DeviceKind.Pen;
        }

        return DeviceKind.Unknown;
    }

    private static bool Contains(string haystack, params string[] needles)
    {
        foreach (var needle in needles)
        {
            if (haystack.Contains(needle, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>[S-3252] How Windows' battery readings of a peripheral are read.</summary>
public static class DeviceBatteryRules
{
    /// <summary>
    /// [S-3252] <c>DEVPKEY_Bluetooth_Battery</c>, <c>{104EA319-6EE2-4701-BD47-8DDBF425BBE5} 2</c>: the
    /// level Windows itself shows in Settings, on a device node of the container. Requested by GUID and
    /// property id: it has no canonical name.
    /// </summary>
    public const string BluetoothBatteryProperty = "{104EA319-6EE2-4701-BD47-8DDBF425BBE5} 2";

    /// <summary>
    /// [S-3252] A battery property as a percentage. A <c>DEVPROP_TYPE_BYTE</c> on the wire; any
    /// whole-number box is accepted, and anything that is not a whole number in 0..100 (255 is
    /// "unknown") is no reading rather than a guess.
    /// </summary>
    public static int? ParsePercent(object? raw)
    {
        long? value = raw switch
        {
            byte b => b,
            sbyte sb => sb,
            short s => s,
            ushort us => us,
            int i => i,
            uint ui => ui,
            long l => l,
            ulong ul => ul <= long.MaxValue ? (long)ul : null,
            _ => null,
        };

        return value is >= 0 and <= 100 ? (int)value.Value : null;
    }

    /// <summary>
    /// [S-3252] One battery per device container from the device nodes that carry the property. A
    /// headset's container holds several nodes (the device, its hands-free and audio services), and
    /// more than one may report; the highest valid reading is the device's. Nodes without a
    /// container or a valid reading say nothing.
    /// </summary>
    public static Dictionary<Guid, int> BatteryByContainer(IEnumerable<(Guid? Container, object? Raw)> nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);

        var map = new Dictionary<Guid, int>();

        foreach (var (container, raw) in nodes)
        {
            if (container is not { } id || id == Guid.Empty || ParsePercent(raw) is not { } percent)
            {
                continue;
            }

            map[id] = map.TryGetValue(id, out var known) ? Math.Max(known, percent) : percent;
        }

        return map;
    }
}
