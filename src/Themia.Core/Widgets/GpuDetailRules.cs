using System.Buffers.Binary;
using Themia.Core.Localization;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-2432] What the graphics kernel reports about one adapter's sensors
/// (<c>D3DKMT_ADAPTER_PERFDATA</c>, <c>D3DKMT_NODE_PERFDATA</c>, <c>D3DKMT_ADAPTER_PERFDATACAPS</c>),
/// each field null when the driver does not report it.
/// </summary>
/// <param name="Celsius">Temperature.</param>
/// <param name="FanRpm">Fan speed; only when the driver says it has a fan sensor.</param>
/// <param name="CoreMhz">The 3D engine's clock now.</param>
/// <param name="MemoryMhz">The memory clock now.</param>
public readonly record struct GpuSensors(double? Celsius, double? FanRpm, double? CoreMhz, double? MemoryMhz);

/// <summary>[S-2430] One read of one adapter, for the details widget.</summary>
/// <param name="Name">The adapter's name, as DXGI gives it.</param>
/// <param name="Load">The busiest engine, 0..100 ([S-434]); null before the counters have a rate.</param>
/// <param name="DedicatedUsed">Video memory in use, bytes; null when the counters have no figure.</param>
/// <param name="DedicatedTotal">Video memory the adapter has (DXGI's <c>DedicatedVideoMemory</c>); 0 for an integrated one.</param>
/// <param name="SharedUsed">System memory the adapter has in use.</param>
/// <param name="SharedTotal">System memory it may use.</param>
/// <param name="Sensors">What the driver reports about temperature, fan and clocks.</param>
public sealed record GpuDetail(
    string Name,
    double? Load,
    ulong? DedicatedUsed,
    ulong DedicatedTotal,
    ulong? SharedUsed,
    ulong SharedTotal,
    GpuSensors Sensors);

/// <summary>[S-2430]..[S-2432] The GPU details widget's rules, pure.</summary>
public static class GpuDetailRules
{
    /// <summary>"1023.9 MB".</summary>
    public const double HeroCharacters = 9;

    /// <summary>Bytes of <c>D3DKMT_ADAPTER_PERFDATA</c>.</summary>
    public const int AdapterPerfSize = 64;

    /// <summary>Bytes of <c>D3DKMT_NODE_PERFDATA</c>.</summary>
    public const int NodePerfSize = 56;

    /// <summary>Bytes of <c>D3DKMT_ADAPTER_PERFDATACAPS</c>.</summary>
    public const int AdapterCapsSize = 40;

    /// <summary>
    /// [S-2430] The sum of a wildcard counter's instances that belong to <paramref name="luid"/>
    /// (<c>\GPU Adapter Memory(luid_..._phys_0)\Dedicated Usage</c>): an adapter with two physical
    /// parts files one instance each. Null when no instance is that adapter's.
    /// </summary>
    public static ulong? SumFor(IEnumerable<(string Instance, long Value)> instances, string luid)
    {
        ArgumentNullException.ThrowIfNull(instances);
        ulong? total = null;

        foreach (var (instance, value) in instances)
        {
            if (value >= 0 && string.Equals(GpuRules.LuidOf(instance), luid, StringComparison.OrdinalIgnoreCase))
            {
                total = (total ?? 0) + (ulong)value;
            }
        }

        return total;
    }

    /// <summary>
    /// [S-2432] The sensors out of the three structures. Temperature is in tenths of a degree and
    /// is kept only from 1 to 150 °C (0 is "no sensor"); the fan is kept only when the caps name a
    /// maximum RPM (a stopped fan then honestly reads 0); a clock of 0 Hz is "not reported".
    /// </summary>
    public static GpuSensors ParseSensors(ReadOnlySpan<byte> adapterPerf, ReadOnlySpan<byte> nodePerf, ReadOnlySpan<byte> caps)
    {
        double? celsius = null, fan = null, core = null, memory = null;

        if (adapterPerf.Length >= AdapterPerfSize)
        {
            var memoryHz = BinaryPrimitives.ReadUInt64LittleEndian(adapterPerf[8..]);
            var rpm = BinaryPrimitives.ReadUInt32LittleEndian(adapterPerf[48..]);
            var deci = BinaryPrimitives.ReadUInt32LittleEndian(adapterPerf[56..]);

            memory = memoryHz > 0 ? memoryHz / 1e6 : null;
            celsius = deci is >= 10 and <= 1500 ? deci / 10.0 : null;

            var maxRpm = caps.Length >= AdapterCapsSize ? BinaryPrimitives.ReadUInt32LittleEndian(caps[24..]) : 0;
            fan = maxRpm > 0 && rpm <= maxRpm * 2 ? rpm : null;
        }

        if (nodePerf.Length >= NodePerfSize)
        {
            var hz = BinaryPrimitives.ReadUInt64LittleEndian(nodePerf[8..]);
            core = hz > 0 ? hz / 1e6 : null;
        }

        return new GpuSensors(celsius, fan, core, memory);
    }

    /// <summary>"1,950 MHz".</summary>
    public static string Megahertz(Locale locale, double mhz)
        => Strings.Format(locale, Str.Details_Units_Megahertz, ("value", LocaleFormat.Integer(locale, (long)Math.Round(Math.Max(0, mhz)))));

    /// <summary>
    /// [S-2430] The sheet: video memory in use as the hero, "of 8.0 GB video memory" under it; then
    /// load, shared memory, temperature, fan, GPU clock and memory clock — each only when reported.
    /// An integrated adapter (no video memory of its own) leads with its shared memory instead.
    /// </summary>
    public static DetailSheet Sheet(Locale locale, GpuDetail? detail)
    {
        if (detail is null)
        {
            return DetailSheet.Waiting;
        }

        var facts = new List<DetailFact>(7);

        void Add(Str label, string? value)
        {
            if (value is not null)
            {
                facts.Add(new DetailFact(Strings.Get(locale, label), value));
            }
        }

        var integrated = detail.DedicatedTotal == 0;

        facts.Add(detail.Load is { } load
            ? new DetailFact(Strings.Get(locale, Str.Details_Gpu_Load), Formatting.Percent(locale, Math.Clamp(load / 100, 0, 1)))
            : DetailFact.Unavailable(Strings.Get(locale, Str.Details_Gpu_Load)));

        if (!integrated && detail.SharedTotal > 0)
        {
            Add(Str.Details_Gpu_Shared, detail.SharedUsed is { } shared ? MemoryDetailRules.UsedOfTotal(locale, shared, detail.SharedTotal) : null);
        }

        Add(Str.Details_Gpu_Temperature, detail.Sensors.Celsius is { } c ? CpuDetailRules.Celsius(locale, c) : null);
        Add(Str.Details_Gpu_Fan, detail.Sensors.FanRpm is { } rpm ? Strings.Format(locale, Str.Details_Units_Rpm, ("value", LocaleFormat.Integer(locale, (long)Math.Round(rpm)))) : null);
        Add(Str.Details_Gpu_Clock, detail.Sensors.CoreMhz is { } core ? Megahertz(locale, core) : null);
        Add(Str.Details_Gpu_MemoryClock, detail.Sensors.MemoryMhz is { } mem ? Megahertz(locale, mem) : null);

        var (used, total) = integrated ? (detail.SharedUsed, detail.SharedTotal) : (detail.DedicatedUsed, detail.DedicatedTotal);
        var caption = total > 0
            ? Strings.Format(locale, integrated ? Str.Details_Gpu_SharedCaption : Str.Details_Gpu_Caption, ("total", Formatting.Bytes(locale, total)))
            : null;

        return used is { } u
            ? new DetailSheet(Formatting.Bytes(locale, u), caption, facts, [])
            : new DetailSheet("--", caption, facts, [], HeroAvailable: false);
    }
}
