using Themia.App.Services.Brightness;

namespace Themia.Sdk;

/// <summary>
/// [S-4447] The displays with a brightness as the brightness widget's menu reads them (whether its
/// Screens it dims line changes anything): the host's one worker's last answer, installed by the host
/// once at start, as the drive list is.
/// </summary>
public static class BrightnessDisplays
{
    private static Func<BrightnessSnapshot>? _now;

    /// <summary>What the displays answered last; <see cref="BrightnessSnapshot.Pending"/> before any widget has looked.</summary>
    public static BrightnessSnapshot Now()
        => (Volatile.Read(ref _now) ?? throw new InvalidOperationException("no brightness displays installed: the host installs them before any menu is built"))();

    /// <summary>The host's brightness worker. Once per process.</summary>
    internal static void Install(Func<BrightnessSnapshot> now)
    {
        ArgumentNullException.ThrowIfNull(now);
        if (Interlocked.CompareExchange(ref _now, now, null) is not null)
        {
            throw new InvalidOperationException("the brightness displays are installed once per process");
        }
    }
}
