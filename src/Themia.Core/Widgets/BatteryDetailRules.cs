using System.Buffers.Binary;
using System.Text;
using Themia.Core.Localization;

namespace Themia.Core.Widgets;

/// <summary>[S-2424] <c>BATTERY_INFORMATION</c>, parsed. Capacities in mWh, or relative units when <paramref name="Relative"/>.</summary>
public readonly record struct BatteryInformation(bool Relative, string? Chemistry, uint? DesignCapacity, uint? FullCapacity, uint? Cycles);

/// <summary>[S-2424] <c>BATTERY_STATUS</c>, parsed. Rate in mW: positive charging, negative discharging.</summary>
public readonly record struct BatteryState(uint PowerState, uint? Capacity, uint? VoltageMillivolts, int? Rate);

/// <summary>[S-2423] Every battery the machine has, summed: what the details widget reads.</summary>
public readonly record struct BatteryPack(
    bool Relative,
    uint? DesignCapacity,
    uint? FullCapacity,
    uint? RemainingCapacity,
    int? Rate,
    uint? VoltageMillivolts,
    uint? Cycles,
    double? TemperatureCelsius,
    string? Chemistry,
    string? Manufacturer,
    int Count);

/// <summary>
/// [S-2423]..[S-2425] The battery details widget's rules: the battery class driver's structures and
/// what follows from them. Pure; the IOCTLs are the App's <c>BatteryDevice</c>.
/// </summary>
public static class BatteryDetailRules
{
    /// <summary>Bytes of <c>BATTERY_INFORMATION</c>.</summary>
    public const int InformationSize = 36;

    /// <summary>Bytes of <c>BATTERY_STATUS</c>.</summary>
    public const int StatusSize = 16;

    /// <summary>"100%".</summary>
    public const double HeroCharacters = 5;

    private const uint CapacityRelative = 0x40000000;
    private const uint UnknownValue = 0xFFFFFFFF;
    private const int UnknownRate = int.MinValue;

    /// <summary>[S-2424] <c>BATTERY_INFORMATION</c> from the bytes the driver returned; null when short.</summary>
    public static BatteryInformation? ParseInformation(ReadOnlySpan<byte> buffer)
    {
        if (buffer.Length < InformationSize)
        {
            return null;
        }

        var capabilities = BinaryPrimitives.ReadUInt32LittleEndian(buffer);
        var designed = BinaryPrimitives.ReadUInt32LittleEndian(buffer[12..]);
        var full = BinaryPrimitives.ReadUInt32LittleEndian(buffer[16..]);
        var cycles = BinaryPrimitives.ReadUInt32LittleEndian(buffer[32..]);

        return new BatteryInformation(
            (capabilities & CapacityRelative) != 0,
            Chemistry(buffer.Slice(8, 4)),
            Capacity(designed),
            Capacity(full),
            // Zero is what firmware without a counter reports; a battery that has genuinely done no
            // cycle yet loses nothing by not saying so.
            cycles is 0 or UnknownValue ? null : cycles);
    }

    /// <summary>[S-2424] <c>BATTERY_STATUS</c>; unknown fields (all ones, a zero voltage) are null.</summary>
    public static BatteryState? ParseStatus(ReadOnlySpan<byte> buffer)
    {
        if (buffer.Length < StatusSize)
        {
            return null;
        }

        var state = BinaryPrimitives.ReadUInt32LittleEndian(buffer);
        var capacity = BinaryPrimitives.ReadUInt32LittleEndian(buffer[4..]);
        var voltage = BinaryPrimitives.ReadUInt32LittleEndian(buffer[8..]);
        var rate = BinaryPrimitives.ReadInt32LittleEndian(buffer[12..]);

        return new BatteryState(
            state,
            capacity == UnknownValue ? null : capacity,
            voltage is 0 or UnknownValue ? null : voltage,
            rate == UnknownRate ? null : rate);
    }

    /// <summary>[S-2424] The temperature level's tenths of a Kelvin as whole °C; null outside -40..120 (no sensor).</summary>
    public static double? Celsius(uint tenthsKelvin)
    {
        var celsius = (tenthsKelvin / 10.0) - 273.15;
        return celsius is >= -40 and <= 120 ? Math.Round(celsius, MidpointRounding.AwayFromZero) : null;
    }

    /// <summary>[S-2424] The four-byte chemistry code as a word ("LION" → "Li-ion"); null when empty or not text.</summary>
    public static string? Chemistry(ReadOnlySpan<byte> code)
    {
        var builder = new StringBuilder(4);

        foreach (var b in code)
        {
            if (b == 0)
            {
                break;
            }

            if (b is < 0x20 or > 0x7E)
            {
                return null;
            }

            builder.Append((char)b);
        }

        var raw = builder.ToString().Trim();

        return raw.ToUpperInvariant() switch
        {
            "" => null,
            "LION" or "LI-I" or "LI I" or "LIO" => "Li-ion",
            "LIP" or "LIPO" or "LI-P" => "Li-polymer",
            "NIMH" => "NiMH",
            "NICD" => "NiCd",
            "NIZN" => "NiZn",
            "PBAC" => Str.Details_Battery_LeadAcid.Text(),
            "RAM" => Str.Details_Battery_Alkaline.Text(),
            _ => raw,
        };
    }

    /// <summary>[S-2424] A maker's name as the driver wrote it, cut at the first NUL, no control characters, at most 32.</summary>
    public static string? Manufacturer(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var clean = new string(raw.TakeWhile(c => c != '\0').Where(c => !char.IsControl(c)).ToArray()).Trim();
        return clean.Length == 0 ? null : clean.Length <= 32 ? clean : clean[..32];
    }

    /// <summary>
    /// [S-2423] Several batteries (a ThinkPad's two) as one: capacities and rates added, the first
    /// battery's voltage, cycles, chemistry, temperature and maker.
    /// </summary>
    public static BatteryPack Combine(IReadOnlyList<(BatteryInformation? Information, BatteryState? State, double? Temperature, string? Manufacturer)> batteries)
    {
        ArgumentNullException.ThrowIfNull(batteries);
        uint? design = null, full = null, remaining = null, voltage = null, cycles = null;
        int? rate = null;
        double? temperature = null;
        string? chemistry = null, manufacturer = null;
        var relative = false;

        foreach (var (information, state, temp, maker) in batteries)
        {
            if (information is { } info)
            {
                relative |= info.Relative;
                design = Add(design, info.DesignCapacity);
                full = Add(full, info.FullCapacity);
                cycles ??= info.Cycles;
                chemistry ??= info.Chemistry;
            }

            if (state is { } s)
            {
                remaining = Add(remaining, s.Capacity);
                voltage ??= s.VoltageMillivolts;
                rate = s.Rate is { } r ? (rate ?? 0) + r : rate;
            }

            temperature ??= temp;
            manufacturer ??= Manufacturer(maker);
        }

        return new BatteryPack(relative, design, full, remaining, rate, voltage, cycles, temperature, chemistry, manufacturer, batteries.Count);
    }

    /// <summary>[S-2425] Wear: full-charge capacity over design capacity; null when either is unknown.</summary>
    public static double? Health(BatteryPack pack)
        => pack is { DesignCapacity: > 0 and var design, FullCapacity: > 0 and var full } ? (double)full / design : null;

    /// <summary>[S-2425] Time to full at the present charge rate; null unless charging with real capacities.</summary>
    public static TimeSpan? TimeToFull(BatteryPack pack)
    {
        if (pack is not { Relative: false, Rate: > 0 and var rate, FullCapacity: { } full, RemainingCapacity: { } remaining } || remaining >= full)
        {
            return null;
        }

        return Bounded((full - remaining) / (double)rate);
    }

    /// <summary>[S-2425] Time to empty at the present drain; null unless discharging with a real capacity.</summary>
    public static TimeSpan? TimeToEmpty(BatteryPack pack)
    {
        if (pack is not { Relative: false, Rate: < 0 and var rate, RemainingCapacity: { } remaining })
        {
            return null;
        }

        return Bounded(remaining / (double)-rate);
    }

    /// <summary>An estimate past two days, or not positive, is a driver's noise, not a time.</summary>
    private static TimeSpan? Bounded(double hours)
        => double.IsFinite(hours) && hours > 0 && hours <= 48 ? TimeSpan.FromHours(hours) : null;

    /// <summary>"12.5 W" (the size, never the sign).</summary>
    public static string Watts(Locale locale, int milliwatts)
        => Strings.Format(locale, Str.Details_Units_Watts, ("value", LocaleFormat.Number(locale, Math.Abs((long)milliwatts) / 1000.0, Math.Abs((long)milliwatts) >= 100_000 ? 0 : 1)));

    /// <summary>"52.4 Wh".</summary>
    public static string WattHours(Locale locale, uint milliwattHours)
        => Strings.Format(locale, Str.Details_Units_WattHours, ("value", LocaleFormat.Number(locale, milliwattHours / 1000.0, milliwattHours >= 100_000 ? 0 : 1)));

    /// <summary>"12.38 V".</summary>
    public static string Volts(Locale locale, uint millivolts)
        => Strings.Format(locale, Str.Details_Units_Volts, ("value", LocaleFormat.Number(locale, millivolts / 1000.0, 2)));

    /// <summary>[S-2425] The state in words: charging, fully charged, on battery, or plugged in and holding.</summary>
    public static string Status(Locale locale, BatteryPower power)
    {
        if (power.Charging)
        {
            return Strings.Get(locale, Str.Details_Battery_Charging);
        }

        if (power.OnMains)
        {
            return Strings.Get(locale, power.Percent >= 99 ? Str.Details_Battery_Full : Str.Details_Battery_PluggedIn);
        }

        return Strings.Get(locale, Str.Details_Battery_OnBattery);
    }

    /// <summary>
    /// [S-2425] The sheet: the charge as the hero and the status under it; then the time left (or to
    /// full), health, full-charge and design capacity, cycles, the charge or discharge rate, voltage,
    /// temperature, chemistry and maker — each only when the battery reports it. A machine without a
    /// battery is the empty state ([S-82a]); one whose details the firmware hides still shows the
    /// charge, the status and Windows' own time estimate.
    /// </summary>
    public static DetailSheet Sheet(Locale locale, BatteryPower power, BatteryPack? pack)
    {
        if (!power.HasBattery)
        {
            return DetailSheet.Missing(Strings.Get(locale, Str.WidgetsB_Battery_None));
        }

        var facts = new List<DetailFact>(11);

        void Add(Str label, string? value)
        {
            if (value is not null)
            {
                facts.Add(new DetailFact(Strings.Get(locale, label), value));
            }
        }

        if (power.Charging && pack is { } charging && TimeToFull(charging) is { } toFull)
        {
            Add(Str.Details_Battery_FullIn, Hours(locale, toFull));
        }
        else if (!power.OnMains && (power.Remaining ?? (pack is { } draining ? TimeToEmpty(draining) : null)) is { } left)
        {
            Add(Str.Details_Battery_TimeLeft, Hours(locale, left));
        }

        if (pack is { } p)
        {
            Add(Str.Details_Battery_Health, Health(p) is { } health ? LocaleFormat.Percent(locale, Math.Round(Math.Min(health, 1.5) * 100)) : null);

            if (!p.Relative)
            {
                Add(Str.Details_Battery_FullCapacity, p.FullCapacity is { } full ? WattHours(locale, full) : null);
                Add(Str.Details_Battery_DesignCapacity, p.DesignCapacity is { } design ? WattHours(locale, design) : null);
            }

            Add(Str.Details_Battery_Cycles, p.Cycles is { } cycles ? LocaleFormat.Integer(locale, cycles) : null);

            if (!p.Relative && p.Rate is { } rate and not 0)
            {
                Add(rate > 0 ? Str.Details_Battery_ChargeRate : Str.Details_Battery_DischargeRate, Watts(locale, rate));
            }

            Add(Str.Details_Battery_Voltage, p.VoltageMillivolts is { } mv ? Volts(locale, mv) : null);
            Add(Str.Details_Battery_Temperature, p.TemperatureCelsius is { } t ? CpuDetailRules.Celsius(locale, t) : null);
            Add(Str.Details_Battery_Chemistry, p.Chemistry);
            Add(Str.Details_Battery_Manufacturer, p.Manufacturer);
        }

        var status = Status(locale, power);

        return power.Percent is { } percent
            ? new DetailSheet(LocaleFormat.Percent(locale, percent), status, facts, [])
            : new DetailSheet("--", status, facts, [], HeroAvailable: false);
    }

    /// <summary>"2h 10m".</summary>
    private static string Hours(Locale locale, TimeSpan span)
    {
        var total = (long)Math.Max(0, span.TotalSeconds);
        return Strings.Format(locale, Str.WidgetsB_Units_HoursMinutes, ("hours", total / 3600), ("minutes", total % 3600 / 60));
    }

    private static uint? Capacity(uint value) => value is 0 or UnknownValue ? null : value;

    private static uint? Add(uint? total, uint? value)
        => value is { } v ? (uint)Math.Min(uint.MaxValue - 1, (long)(total ?? 0) + v) : total;
}
