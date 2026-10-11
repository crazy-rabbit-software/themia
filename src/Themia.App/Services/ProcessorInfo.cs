using System.Runtime.InteropServices;
using Microsoft.Win32;
using Themia.Core.Diagnostics;
using Themia.Core.Widgets;

namespace Themia.App.Services;

/// <summary>
/// [S-2405]..[S-2407] The processor's figures for the CPU details widget, from Win32 and PDH only:
/// the load from <see cref="SystemMetrics.Cpu"/> (the CPU meter's own shared read), the clock from
/// <c>CallNtPowerInformation</c> and <c>\Processor Information(_Total)\% Processor Performance</c>,
/// cores from <c>GetLogicalProcessorInformationEx</c>, the counts from <see cref="PerformanceInfo"/>,
/// the thermal zone from <c>\Thermal Zone Information(*)\Temperature</c> and the name from the
/// registry. No WMI, no driver, no admin.
/// <para>
/// The counters are one <see cref="PdhQuery"/> held while a CPU details widget lives
/// (<see cref="Acquire"/>) and closed when the last one goes. What cannot change while Windows runs
/// (the name, the cores, the base clock) is read once. Every read happens on the UI thread's tick;
/// widgets reading in the same tick share one (<see cref="SampleCache{T}"/>).
/// </para>
/// </summary>
internal static unsafe partial class ProcessorInfo
{
    private const string Scope = "cpu";

    /// <summary>POWER_INFORMATION_LEVEL ProcessorInformation.</summary>
    private const int ProcessorInformationLevel = 11;

    /// <summary>LOGICAL_PROCESSOR_RELATIONSHIP RelationProcessorCore.</summary>
    private const int RelationProcessorCore = 0;

    /// <summary>ALL_PROCESSOR_GROUPS.</summary>
    private const ushort AllGroups = 0xFFFF;

    private static readonly SampleCache<Reading> Cache = new();

    private static int _leases;
    private static PdhQuery? _query;
    private static PdhCounter? _performance;
    private static PdhCounter? _zones;
    private static bool _queryFailed;

    private static (double Base, double Reported)? _power;
    private static int? _cores;
    private static string? _name;
    private static bool _staticRead;

    /// <summary>Hold the counters open; dispose to let them go.</summary>
    public static IDisposable Acquire()
    {
        _leases++;
        return new SharedLease(Release);
    }

    /// <summary>One read of every figure; a figure Windows would not give is null.</summary>
    public static CpuFigures Read()
    {
        var now = Environment.TickCount64;

        if (Cache.TryReuse(now, out var recent))
        {
            return recent.Figures;
        }

        ReadStatic();

        double? performance = null;
        double? celsius = null;

        if (Open() is { } query)
        {
            query.Collect();
            performance = _performance is { } p ? query.Double(p) : null;
            celsius = _zones is { } z ? CpuDetailRules.HottestZone(query.Doubles(z).Select(i => i.Value)) : null;
        }

        var load = SystemMetrics.Cpu();
        var counts = PerformanceInfo.Read();
        var logical = (int)GetActiveProcessorCount(AllGroups);

        var figures = new CpuFigures(
            Load: load.Available ? load.Fraction : null,
            Mhz: _power is { } power ? CpuDetailRules.EffectiveMhz(performance, power.Base, power.Reported) : null,
            BaseMhz: _power?.Base is > 0 and var b ? b : null,
            Cores: _cores,
            Logical: logical > 0 ? logical : null,
            Processes: counts?.Processes,
            Threads: counts?.Threads,
            Handles: counts?.Handles,
            Uptime: TimeSpan.FromMilliseconds(Environment.TickCount64),
            Celsius: celsius,
            Name: _name);

        Cache.Store(now, new Reading(figures));
        return figures;
    }

    /// <summary>The name, the cores and the base clock: once per run.</summary>
    private static void ReadStatic()
    {
        if (_staticRead)
        {
            return;
        }

        _staticRead = true;
        _power = ReadPower();
        _cores = ReadCores();
        _name = ReadName();
    }

    private static (double Base, double Reported)? ReadPower()
    {
        var count = Math.Max(1, (int)GetActiveProcessorCount(AllGroups));
        var bytes = (uint)(count * sizeof(PowerInformation));
        var buffer = (PowerInformation*)NativeMemory.Alloc(bytes);

        try
        {
            var status = CallNtPowerInformation(ProcessorInformationLevel, null, 0, buffer, bytes);

            if (status != 0)
            {
                Log.Warn(Scope, $"CallNtPowerInformation(ProcessorInformation) returned 0x{status:X8}; no clock speed");
                return null;
            }

            uint max = 0;
            uint current = 0;

            for (var i = 0; i < count; i++)
            {
                max = Math.Max(max, buffer[i].MaxMhz);
                current = Math.Max(current, buffer[i].CurrentMhz);
            }

            return (max, current);
        }
        finally
        {
            NativeMemory.Free(buffer);
        }
    }

    /// <summary>Physical cores: one RelationProcessorCore record each.</summary>
    private static int? ReadCores()
    {
        uint length = 0;
        _ = GetLogicalProcessorInformationEx(RelationProcessorCore, null, &length); // ignore: sizing call, fails by design

        if (length is 0 or > 1 << 20)
        {
            Log.Warn(Scope, $"could not size the processor topology ({Marshal.GetLastPInvokeError()})");
            return null;
        }

        var buffer = (byte*)NativeMemory.Alloc(length);

        try
        {
            if (!GetLogicalProcessorInformationEx(RelationProcessorCore, buffer, &length))
            {
                Log.Warn(Scope, $"could not read the processor topology ({Marshal.GetLastPInvokeError()})");
                return null;
            }

            var cores = 0;
            uint offset = 0;

            // Each record starts with its relationship and its own size: walk by the size.
            while (offset + 8 <= length)
            {
                var size = *(uint*)(buffer + offset + 4);

                if (size < 8 || offset + size > length)
                {
                    break;
                }

                if (*(int*)(buffer + offset) == RelationProcessorCore)
                {
                    cores++;
                }

                offset += size;
            }

            return cores > 0 ? cores : null;
        }
        finally
        {
            NativeMemory.Free(buffer);
        }
    }

    private static string? ReadName()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
            return CpuDetailRules.CleanName(key?.GetValue("ProcessorNameString") as string);
        }
        catch (System.Security.SecurityException ex)
        {
            Log.Warn(Scope, "could not read the processor's name", ex);
            return null;
        }
        catch (IOException ex)
        {
            Log.Warn(Scope, "could not read the processor's name", ex);
            return null;
        }
        catch (UnauthorizedAccessException ex)
        {
            Log.Warn(Scope, "could not read the processor's name", ex);
            return null;
        }
    }

    private static PdhQuery? Open()
    {
        if (_query is not null || _queryFailed || _leases == 0)
        {
            return _query;
        }

        _query = PdhQuery.Open(Scope);

        if (_query is null)
        {
            // Logged by Open. Not retried every tick: the clock falls back to the reported one.
            _queryFailed = true;
            return null;
        }

        _performance = _query.Add(@"\Processor Information(_Total)\% Processor Performance");
        _zones = _query.Add(@"\Thermal Zone Information(*)\Temperature");

        if (_zones is null)
        {
            Log.Info(Scope, "this machine reports no ACPI thermal zone; the CPU details show no temperature");
        }

        // A rate counter needs a first collection to have something to rate against.
        _query.Collect();
        return _query;
    }

    private static void Release()
    {
        _leases = Math.Max(0, _leases - 1);

        if (_leases > 0)
        {
            return;
        }

        _query?.Dispose();
        _query = null;
        _performance = _zones = null;
        _queryFailed = false;
        Cache.Clear();
    }

    private readonly record struct Reading(CpuFigures Figures);

    [LibraryImport("powrprof")]
    private static partial int CallNtPowerInformation(int level, void* input, uint inputLength, void* output, uint outputLength);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetLogicalProcessorInformationEx(int relationship, byte* buffer, uint* length);

    [LibraryImport("kernel32.dll")]
    private static partial uint GetActiveProcessorCount(ushort group);

    /// <summary>PROCESSOR_POWER_INFORMATION: Number, MaxMhz, CurrentMhz, MhzLimit, MaxIdleState, CurrentIdleState.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 24)]
    private readonly struct PowerInformation
    {
        [FieldOffset(4)]
        public readonly uint MaxMhz;

        [FieldOffset(8)]
        public readonly uint CurrentMhz;
    }
}

/// <summary>One holder's lease on a shared reader. Disposing twice releases once.</summary>
internal sealed class SharedLease(Action release) : IDisposable
{
    private bool _released;

    public void Dispose()
    {
        if (_released)
        {
            return;
        }

        _released = true;
        release();
    }
}
