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

    /// <summary>
    /// [S-4471] What a device is from Windows' own categories for its container
    /// (<c>System.Devices.Category</c>: "Input.Mouse", "Audio.Headphone", "Input.Gaming"), read part by
    /// part, never by substring; null where none says (the name's guess is then the fallback).
    /// </summary>
    public static DeviceKind? FromCategories(IEnumerable<string?>? categories)
    {
        if (categories is null)
        {
            return null;
        }

        DeviceKind? audio = null;

        foreach (var category in categories)
        {
            if (string.IsNullOrWhiteSpace(category))
            {
                continue;
            }

            foreach (var part in category.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                switch (part.ToLowerInvariant())
                {
                    case "mouse" or "touchpad" or "trackpad":
                        return DeviceKind.Mouse;
                    case "keyboard":
                        return DeviceKind.Keyboard;
                    case "gaming" or "gamepad" or "gamecontroller" or "joystick":
                        return DeviceKind.Gamepad;
                    case "pen" or "stylus":
                        return DeviceKind.Pen;
                    case "phone" or "cellphone" or "smartphone":
                        return DeviceKind.Phone;
                    case "watch" or "wearable":
                        return DeviceKind.Watch;
                    case "headphone" or "headphones" or "headset" or "speaker" or "speakers" or "audio":
                        // A headset is often also "Communication": the audio answer holds unless a
                        // more specific category follows.
                        audio = DeviceKind.Audio;
                        break;
                }
            }
        }

        return audio;
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
    /// [S-4471] A charging flag (<c>System.Devices.ChargingState</c>, a byte that is 0 or 1; a boolean
    /// box too): anything else is no answer, never "not charging".
    /// </summary>
    public static bool? ParseCharging(object? raw) => raw switch
    {
        bool flag => flag,
        byte b and (0 or 1) => b == 1,
        int i and (0 or 1) => i == 1,
        uint u and (0 or 1) => u == 1,
        _ => null,
    };

    /// <summary>
    /// [S-4471] A battery report's level (a controller's remaining and full capacity, in mWh) as a
    /// percentage, rounded; no reading where either is missing, the full capacity is not positive or
    /// the remaining one is negative. A remaining capacity over the full one is full.
    /// </summary>
    public static int? Percent(int? remaining, int? full)
    {
        if (remaining is not { } left || full is not { } whole || whole <= 0 || left < 0)
        {
            return null;
        }

        return (int)Math.Round(Math.Min(left, whole) * 100.0 / whole, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// [S-4471] A categories property as its strings: a multi-string comes boxed as an array or a
    /// list, a single category as a string; anything else is none.
    /// </summary>
    public static string?[] Categories(object? raw) => raw switch
    {
        string one => [one],
        IEnumerable<string?> many => [.. many],
        IEnumerable<object?> boxed => [.. boxed.Select(item => item as string)],
        _ => [],
    };

    /// <summary>[S-4471] A device container's id ("{guid}", as Windows writes it) as a Guid; null for anything else or the empty one.</summary>
    public static Guid? ParseContainer(object? raw) => raw switch
    {
        Guid id when id != Guid.Empty => id,
        string text when Guid.TryParse(text.Trim(), out var id) && id != Guid.Empty => id,
        _ => null,
    };

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
