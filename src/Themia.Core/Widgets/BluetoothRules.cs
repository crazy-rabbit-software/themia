using Themia.Core.Localization;
namespace Themia.Core.Widgets;

/// <summary>
/// What a paired device is, as far as its icon is concerned. A closed set rather than the original's
/// free-form string: the glyph table has to cover every case at compile time, and a typo in a
/// category name is otherwise a device that silently draws the generic icon.
/// </summary>
public enum BluetoothDeviceKind
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

/// <summary>Why a Bluetooth list has no rows. Each one wants a different sentence, and none of them is blank.</summary>
public enum BluetoothStatus
{
    /// <summary>Before the first enumeration comes back.</summary>
    Loading = 0,

    Ok = 1,

    /// <summary>This machine has no Bluetooth radio at all. Not an error — a fact about the hardware.</summary>
    NoRadio = 2,

    /// <summary>There is a radio and it is switched off.</summary>
    RadioOff = 3,

    /// <summary>Windows refused the app access to the radios.</summary>
    PermissionDenied = 4,

    /// <summary>The enumeration itself failed. The only one of these that is a fault.</summary>
    Failed = 5,
}

/// <summary>One paired device, as the widget draws it.</summary>
/// <param name="Name">The device's own name. Attacker-influenced in principle, so it is displayed and nothing more.</param>
/// <param name="Id">The Windows device-interface id. Used to re-read battery, never shown.</param>
/// <param name="Connected">Whether it is connected right now.</param>
/// <param name="Kind">Which glyph it gets.</param>
/// <param name="BatteryPercent">
/// 0..100 where Windows reports it for a connected device ([S-3252]: the device property, else the
/// GATT Battery Service), else null.
/// </param>
/// <param name="SignalDbm">
/// [S-1256] Received signal strength in dBm where the radio reported one (an LE device seen
/// advertising, or the association endpoint's own reading), else null. Most paired Classic devices
/// never have one.
/// </param>
public readonly record struct BluetoothDeviceEntry(
    string Name,
    string Id,
    bool Connected,
    BluetoothDeviceKind Kind,
    int? BatteryPercent,
    int? SignalDbm = null);

/// <summary>
/// The rules the Bluetooth widget draws by: what a device is called, what it is, and in what order.
/// <para>
/// Pure, and kept out of the enumeration code, because the enumeration needs a radio and a machine
/// to run on and these are the parts that are easy to get quietly wrong — a device whose name is
/// empty, two radios reporting the same headset, a battery percentage of 255 from a device that
/// means "I do not know".
/// </para>
/// </summary>
public static class BluetoothRules
{
    /// <summary>
    /// Guess what a device is from its name.
    /// <para>
    /// A guess, and deliberately only used to pick an icon. Windows exposes a class-of-device for
    /// Classic devices and an appearance value for LE ones, but neither is populated for every
    /// paired device, and a list where half the rows have an icon and half do not looks broken in a
    /// way that a slightly wrong icon does not.
    /// </para>
    /// </summary>
    public static BluetoothDeviceKind Classify(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return BluetoothDeviceKind.Unknown;
        }

        var lower = name.ToLowerInvariant();

        if (Contains(lower, "headphone", "headset", "speaker", "earbud", "airpod", "buds", "audio", "soundbar", "soundcore"))
        {
            return BluetoothDeviceKind.Audio;
        }

        if (Contains(lower, "mouse", "trackpad", "magic trackpad"))
        {
            return BluetoothDeviceKind.Mouse;
        }

        if (Contains(lower, "keyboard", "keychron"))
        {
            return BluetoothDeviceKind.Keyboard;
        }

        // Before the phone brands, not after: a "Galaxy Watch" and a "Pixel Watch" both carry a
        // phone maker's name and are not phones. The more specific word wins.
        if (Contains(lower, "watch", "band", "fitbit", "garmin"))
        {
            return BluetoothDeviceKind.Watch;
        }

        if (Contains(lower, "phone", "iphone", "galaxy", "pixel"))
        {
            return BluetoothDeviceKind.Phone;
        }

        if (Contains(lower, "controller", "gamepad", "xbox", "dualsense", "dualshock", "joy-con"))
        {
            return BluetoothDeviceKind.Gamepad;
        }

        if (Contains(lower, "pen", "stylus"))
        {
            return BluetoothDeviceKind.Pen;
        }

        return BluetoothDeviceKind.Unknown;
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

    /// <summary>
    /// What to call a device. A paired device with no name is common enough — some LE peripherals
    /// simply do not advertise one — and an empty row is indistinguishable from a rendering bug.
    /// <para>
    /// Collapsed to a single line: the name comes off the radio, so it is not this widget's to
    /// trust, and a row is one line tall whatever arrives in it.
    /// </para>
    /// </summary>
    public static string DisplayName(string? raw, Locale locale)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Strings.Get(locale, Str.WidgetsA_Bluetooth_UnknownDevice);
        }

        var words = raw.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (words.Length == 0)
        {
            return Strings.Get(locale, Str.WidgetsA_Bluetooth_UnknownDevice);
        }

        var collapsed = string.Join(' ', words);
        return collapsed.Length <= 64 ? collapsed : collapsed[..64];
    }

    /// <summary>
    /// The battery column, or null when the device does not report one — which is most of them.
    /// <para>
    /// 255 is the Battery Service's own "unknown", and anything above 100 is a device answering
    /// out of spec. Both become no column rather than a percentage nobody should act on.
    /// </para>
    /// </summary>
    public static string? BatteryLabel(int? percent, Locale locale)
        => percent is >= 0 and <= 100 ? LocaleFormat.Percent(locale, percent.Value) : null;

    /// <summary>[S-3254] At or below this the battery is drawn in the critical colour, as the tray does.</summary>
    public const int LowBattery = 20;

    /// <summary>[S-3254] Whether a reported battery is low. No reading is never low.</summary>
    public static bool IsLowBattery(int? percent) => percent is >= 0 and <= LowBattery;

    /// <summary>
    /// [S-3252] The Windows battery property (<c>DEVPKEY_Bluetooth_Battery</c>,
    /// <c>{104EA319-6EE2-4701-BD47-8DDBF425BBE5} 2</c>) as a percentage. A <c>DEVPROP_TYPE_BYTE</c>
    /// on the wire; any whole-number box is accepted, and anything that is not a whole number in
    /// 0..100 (255 is "unknown") is no reading rather than a guess.
    /// </summary>
    public static int? ParseBattery(object? raw)
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
            if (container is not { } id || id == Guid.Empty || ParseBattery(raw) is not { } percent)
            {
                continue;
            }

            map[id] = map.TryGetValue(id, out var known) ? Math.Max(known, percent) : percent;
        }

        return map;
    }

    /// <summary>
    /// [S-3252] Give each connected device its container's battery. A paired but disconnected device
    /// keeps none: the property holds the last value Windows saw, and a headset in its case at "80%"
    /// from yesterday is a number nobody should act on. Where the property has nothing, a reading the
    /// entry already holds stays.
    /// </summary>
    /// <param name="containers">The device container of each entry, by index; null where unknown.</param>
    public static List<BluetoothDeviceEntry> WithBattery(
        IReadOnlyList<BluetoothDeviceEntry> devices,
        IReadOnlyList<Guid?> containers,
        IReadOnlyDictionary<Guid, int> batteries)
    {
        ArgumentNullException.ThrowIfNull(devices);
        ArgumentNullException.ThrowIfNull(containers);
        ArgumentNullException.ThrowIfNull(batteries);

        var result = new List<BluetoothDeviceEntry>(devices.Count);

        for (var i = 0; i < devices.Count; i++)
        {
            var device = devices[i];

            if (!device.Connected)
            {
                result.Add(device with { BatteryPercent = null });
                continue;
            }

            var container = i < containers.Count ? containers[i] : null;
            result.Add(container is { } id && batteries.TryGetValue(id, out var percent)
                ? device with { BatteryPercent = percent }
                : device);
        }

        return result;
    }

    /// <summary>
    /// [S-1256] The signal column, or null. Real RSSI sits between about -100 and -20 dBm; zero is
    /// what a stack reports for "no reading", and anything outside the plausible range is a device or
    /// driver answering out of spec. Both become no column rather than a number nobody should read.
    /// </summary>
    public static string? SignalLabel(int? dbm)
        => dbm is >= -127 and <= -1 ? $"{dbm} dBm" : null;

    /// <summary>
    /// [S-1256] Signal strength as 0..4 bars, the way Windows draws Wi-Fi: -60 dBm and better is
    /// full, every 10 dB below drops a bar. Null when there is no plausible reading.
    /// </summary>
    public static int? SignalBars(int? dbm)
    {
        if (SignalLabel(dbm) is null || dbm is not { } value)
        {
            return null;
        }

        return value switch
        {
            >= -60 => 4,
            >= -70 => 3,
            >= -80 => 2,
            >= -90 => 1,
            _ => 0,
        };
    }

    /// <summary>
    /// [S-1256] A Bluetooth address as the 48-bit number an advertisement carries, from the
    /// association endpoint's "aa:bb:cc:dd:ee:ff" (or bare hex), or null for anything else.
    /// </summary>
    public static ulong? ParseAddress(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var hex = text.Trim().Replace(":", string.Empty, StringComparison.Ordinal).Replace("-", string.Empty, StringComparison.Ordinal);

        if (hex.Length != 12)
        {
            return null;
        }

        return ulong.TryParse(hex, System.Globalization.NumberStyles.AllowHexSpecifier, System.Globalization.CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    /// <summary>
    /// [S-1256] The device's own address from an association-endpoint id
    /// (<c>BluetoothLE#BluetoothLE&lt;radio&gt;-&lt;device&gt;</c>): the part after the last dash.
    /// The fallback for when the address property was not returned.
    /// </summary>
    public static ulong? AddressFromId(string? id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        var dash = id.LastIndexOf('-');
        return dash < 0 || dash == id.Length - 1 ? null : ParseAddress(id[(dash + 1)..]);
    }

    /// <summary>[S-1255] What a device is, in words, for a two-line row's second line.</summary>
    public static string KindLabel(BluetoothDeviceKind kind, Locale locale) => Strings.Get(locale, kind switch
    {
        BluetoothDeviceKind.Audio => Str.WidgetsA_Bluetooth_Kind_Audio,
        BluetoothDeviceKind.Mouse => Str.WidgetsA_Bluetooth_Kind_Mouse,
        BluetoothDeviceKind.Keyboard => Str.WidgetsA_Bluetooth_Kind_Keyboard,
        BluetoothDeviceKind.Phone => Str.WidgetsA_Bluetooth_Kind_Phone,
        BluetoothDeviceKind.Gamepad => Str.WidgetsA_Bluetooth_Kind_Controller,
        BluetoothDeviceKind.Pen => Str.WidgetsA_Bluetooth_Kind_Pen,
        BluetoothDeviceKind.Watch => Str.WidgetsA_Bluetooth_Kind_Watch,
        _ => Str.WidgetsA_Bluetooth_Kind_Device,
    });

    /// <summary>
    /// [S-1255] A two-line row's second line: what the device is, then each enabled fact the device
    /// actually reported ("Audio · Connected · 80%"). Nothing reported is left out, never "--".
    /// </summary>
    public static string DetailLine(BluetoothDeviceEntry device, bool showConnection, bool showBattery, bool showSignal, Locale locale)
    {
        var parts = new List<string>(4) { KindLabel(device.Kind, locale) };

        if (showConnection)
        {
            parts.Add(ConnectionLabel(device.Connected, locale));
        }

        if (showBattery && BatteryLabel(device.BatteryPercent, locale) is { } battery)
        {
            parts.Add(battery);
        }

        if (showSignal && SignalLabel(device.SignalDbm) is { } signal)
        {
            parts.Add(signal);
        }

        return string.Join(" · ", parts);
    }

    /// <summary>[S-1255] The header's summary at large sizes: "2 connected", or null when none is.</summary>
    public static string? ConnectedSummary(int connected, Locale locale)
        => connected > 0 ? Strings.Plural(locale, Str.WidgetsA_Bluetooth_ConnectedCount, connected) : null;

    /// <summary>The connection state, in words. Carried by text as well as by the dot beside it.</summary>
    public static string ConnectionLabel(bool connected, Locale locale)
        => Strings.Get(locale, connected ? Str.WidgetsA_Bluetooth_Connected : Str.WidgetsA_Bluetooth_Paired);

    /// <summary>The sentence a tile with no rows shows. Every state names itself.</summary>
    public static string StatusMessage(BluetoothStatus status, Locale locale) => Strings.Get(locale, status switch
    {
        BluetoothStatus.Loading => Str.WidgetsA_Bluetooth_Status_Loading,
        BluetoothStatus.NoRadio => Str.WidgetsA_Bluetooth_Status_NoRadio,
        BluetoothStatus.RadioOff => Str.WidgetsA_Bluetooth_Status_RadioOff,
        BluetoothStatus.PermissionDenied => Str.WidgetsA_Bluetooth_Status_PermissionDenied,
        BluetoothStatus.Failed => Str.WidgetsA_Bluetooth_Status_Failed,
        _ => Str.WidgetsA_Bluetooth_Status_None,
    });

    /// <summary>
    /// Merge, filter and order what the two enumerations returned.
    /// <para>
    /// The merge is the part that matters: a headset that supports both Classic and LE is enumerated
    /// twice, under one name, and showing it twice makes the list look like it is double-counting.
    /// The LE entry is the one that carries a GATT battery reading, so where the two disagree the
    /// richer answer wins rather than whichever happened to be enumerated first.
    /// </para>
    /// <para>
    /// Connected devices first, then by name, so the order is stable between polls — a list that
    /// reshuffles every thirty seconds cannot be read.
    /// </para>
    /// </summary>
    public static List<BluetoothDeviceEntry> Arrange(
        IEnumerable<BluetoothDeviceEntry> devices,
        bool connectedOnly,
        int maxCount,
        Locale locale)
    {
        ArgumentNullException.ThrowIfNull(devices);

        var merged = new List<BluetoothDeviceEntry>();

        foreach (var device in devices)
        {
            var name = DisplayName(device.Name, locale);
            var normalised = device with { Name = name, Kind = Classify(name) };

            // Nameless devices are not merged with each other: they all normalise to the same
            // string, and folding four unnamed peripherals into one row would hide three devices.
            var index = string.IsNullOrWhiteSpace(device.Name)
                ? -1
                : merged.FindIndex(existing => existing.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

            if (index < 0)
            {
                merged.Add(normalised);
                continue;
            }

            var current = merged[index];

            merged[index] = current with
            {
                Connected = current.Connected || normalised.Connected,
                BatteryPercent = normalised.BatteryPercent ?? current.BatteryPercent,
                SignalDbm = normalised.SignalDbm ?? current.SignalDbm,
            };
        }

        if (connectedOnly)
        {
            merged.RemoveAll(device => !device.Connected);
        }

        merged.Sort(static (left, right) =>
        {
            if (left.Connected != right.Connected)
            {
                return left.Connected ? -1 : 1;
            }

            return string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
        });

        var limit = Math.Clamp(maxCount, 1, 50);

        if (merged.Count > limit)
        {
            merged.RemoveRange(limit, merged.Count - limit);
        }

        return merged;
    }
}
