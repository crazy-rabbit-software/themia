using Themia.Core.Model;

namespace Themia.Core.Widgets;

/// <summary>What <c>GetSystemPowerStatus</c> says about the battery, in the battery meter's terms.</summary>
/// <param name="HasBattery">False on a desktop (see <see cref="BatteryRules.Read"/>).</param>
/// <param name="Percent">Charge 0..100, or null when Windows will not say.</param>
/// <param name="OnMains">Plugged in.</param>
/// <param name="Charging">
/// Actually taking charge (<c>BatteryFlag</c> bit 3) — not the same as plugged in: a full battery
/// on mains, or one a vendor tool holds at 80%, is plugged in and not charging.
/// </param>
/// <param name="Remaining">Windows' own estimate of the time left, only ever while discharging.</param>
public readonly record struct BatteryPower(bool HasBattery, int? Percent, bool OnMains, bool Charging, TimeSpan? Remaining);

/// <summary>
/// [S-1180] [S-1187] The battery meter's rules, as pure functions of what Windows hands back: whether
/// there is a battery, its charge, whether it is charging, and which battery glyph a charge draws as.
/// </summary>
public static class BatteryRules
{
    /// <summary><c>BatteryFlag</c> bit 3: charging.</summary>
    private const byte ChargingFlag = 8;

    /// <summary><c>BatteryFlag</c> bit 7: this machine has no system battery.</summary>
    private const byte NoSystemBattery = 128;

    /// <summary>The value the status bytes use for "Windows does not know".</summary>
    private const byte Unknown = 255;

    /// <summary>
    /// [S-1180] The power status in the meter's terms.
    /// <para>
    /// The encoding is a minefield: <c>255</c> means "unknown" in three fields, <c>BatteryFlag</c>
    /// carries "no battery" in a bit rather than in the percentage, and <c>BatteryLifeTime</c> uses
    /// <c>-1</c> as a sentinel in an unsigned field. Each reads as a plausible number at face value:
    /// 255% charge, a battery on a desktop, 136 years remaining.
    /// </para>
    /// </summary>
    public static BatteryPower Read(byte acLineStatus, byte batteryFlag, byte batteryLifePercent, uint batteryLifeTime)
    {
        // Three states packed into two bytes, and only one of them is "there is no battery":
        //  - bit 7 of a flag that is not itself the unknown sentinel, with no usable percentage, is a
        //    desktop: Windows is stating the absence.
        //  - a flag of 255 is Windows saying it does not know. That machine keeps its battery and
        //    shows "--" rather than being told it is a desktop.
        //  - a real percentage outranks the flag: something reporting 80% charge has a battery.
        var hasBattery = !(batteryFlag != Unknown && (batteryFlag & NoSystemBattery) != 0 && batteryLifePercent > 100);
        int? percent = hasBattery && batteryLifePercent <= 100 ? batteryLifePercent : null;
        var onBattery = acLineStatus == 0;

        // Only ever offered while discharging: plugged in, Windows leaves whatever it last computed
        // in the field, and "2h 10m left" under a charging battery is wrong.
        TimeSpan? remaining = onBattery && hasBattery && batteryLifeTime is > 0 and not uint.MaxValue
            ? TimeSpan.FromSeconds(batteryLifeTime)
            : null;

        var charging = batteryFlag != Unknown && (batteryFlag & ChargingFlag) != 0;
        return new BatteryPower(hasBattery, percent, acLineStatus == 1, charging, remaining);
    }

    /// <summary>[S-1187] Which of the eleven battery glyphs (empty … full) a charge draws as.</summary>
    public static int Level(int? percent)
        => percent is { } p ? (int)Math.Round(Math.Clamp(p, 0, 100) / 10.0, MidpointRounding.AwayFromZero) : 0;
}
