using Themia.App.Interop;
using Themia.Core.Localization;

namespace Themia.App.Services;

/// <summary>
/// System readings, straight from Win32.
/// <para>
/// Not <c>PerformanceCounter</c>: it pulls in the whole perf-counter registry stack, is slow to
/// first value, and is not trim-safe — none of which a widget that samples one number per second
/// can justify. These calls are a few microseconds each and work under AOT unchanged.
/// </para>
/// </summary>
internal static class SystemMetrics
{
    // CPU load is a delta between two samples of the system's cumulative timers, so the previous
    // sample has to be remembered. [S-2501] The system hub is the only reader, once per due tick, so
    // two CPU widgets never split one interval between them. Single-threaded: the UI thread's tick.
    private static ulong _lastIdle;
    private static ulong _lastKernel;
    private static ulong _lastUser;
    private static bool _haveCpuBaseline;

    /// <summary>
    /// Processor load across all cores.
    /// <para>
    /// The very first call has no previous sample to subtract, so it reports 0 rather than
    /// inventing a number — one tick later it is real.
    /// </para>
    /// </summary>
    public static MeterReading Cpu()
    {
        if (!Win32.GetSystemTimes(out var idle, out var kernel, out var user))
        {
            return MeterReading.Unavailable(Str.Shell_Meter_Unavailable.Text());
        }

        var idleTicks = idle.ToUInt64();
        var kernelTicks = kernel.ToUInt64();
        var userTicks = user.ToUInt64();

        if (!_haveCpuBaseline)
        {
            (_lastIdle, _lastKernel, _lastUser, _haveCpuBaseline) = (idleTicks, kernelTicks, userTicks, true);
            return new MeterReading(0, Themia.Core.Widgets.Formatting.Percent(0), true);
        }

        // GetSystemTimes' kernel total already includes idle, so busy = (kernel+user) - idle.
        var idleDelta = idleTicks - _lastIdle;
        var totalDelta = (kernelTicks - _lastKernel) + (userTicks - _lastUser);

        (_lastIdle, _lastKernel, _lastUser) = (idleTicks, kernelTicks, userTicks);

        if (totalDelta == 0)
        {
            return new MeterReading(0, Themia.Core.Widgets.Formatting.Percent(0), true);
        }

        var busy = Math.Clamp(1.0 - ((double)idleDelta / totalDelta), 0, 1);
        return new MeterReading(busy, Themia.Core.Widgets.Formatting.Percent(busy), true);
    }

    /// <summary>
    /// [S-1180] Charge, source and whether it is charging, from <c>GetSystemPowerStatus</c>; null
    /// when Windows will not answer. What the bytes mean is <see cref="Themia.Core.Widgets.BatteryRules.Read"/>'s.
    /// </summary>
    public static Themia.Core.Widgets.BatteryPower? Battery()
    {
        if (!Win32.GetSystemPowerStatus(out var status))
        {
            return null;
        }

        return Themia.Core.Widgets.BatteryRules.Read(status.ACLineStatus, status.BatteryFlag, status.BatteryLifePercent, status.BatteryLifeTime);
    }
}
