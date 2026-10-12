using Themia.Core.SystemData;
using Themia.Core.Widgets;

namespace Themia.App.Services.SystemData;

/// <summary>[S-2500] A typed name for a source, so a watch says what it gets back: <c>SystemHub.Watch(Sources.Cpu, ...)</c>.</summary>
/// <typeparam name="T">The snapshot the source reads.</typeparam>
internal sealed record SourceId<T>(SystemSource Source);

/// <summary>
/// [S-2501] One meter reading and the point it adds to its chart: null when the reading is not a
/// sample (unavailable, the counters' first collection), so a gap is never drawn as a zero.
/// </summary>
internal readonly record struct MeterSample(MeterReading Reading, SeriesPoint? Point)
{
    /// <summary>
    /// [S-1167] [S-1172] A reading with a fraction is a sample even when it is not available (an
    /// unanswered probe plots on the baseline). A window-scaled chart plots the value, a share chart
    /// the fraction.
    /// </summary>
    public static MeterSample Of(MeterReading reading, ChartScale scale)
        => reading.Fraction is { } fraction
            ? new(reading, new SeriesPoint(
                scale.FollowsWindow ? reading.Value ?? fraction : fraction,
                reading.Second is { } second && double.IsFinite(second) ? second : double.NaN,
                Missed: !reading.Available))
            : new(reading, null);
}

/// <summary>
/// [S-2501] One place system figures come from, read by the hub alone: at most once per due tick,
/// however many widgets show it. What it holds between reads (a PDH query, a GPU session, a pinned
/// buffer) is taken in <see cref="Open"/> and given back in <see cref="Close"/>, which the hub calls
/// when the first feed starts reading it and when the last one is dropped ([S-2505]).
/// </summary>
internal abstract class HubSource
{
    /// <summary>Take what reads need. Called once, before the first read.</summary>
    public virtual void Open()
    {
    }

    /// <summary>Give it back; nothing reads after this. Called once.</summary>
    public virtual void Close()
    {
    }
}

/// <summary>A source of <typeparamref name="T"/> snapshots.</summary>
internal abstract class HubSource<T> : HubSource
{
    /// <summary>
    /// Whether a read is a round trip ([S-2507]): run with <see cref="ReadAsync"/>, one at a time,
    /// rather than on the tick with <see cref="Read"/>.
    /// </summary>
    public virtual bool IsAsync => false;

    /// <summary>One read on the UI thread's tick. A throw is logged by the hub and delivers nothing.</summary>
    public virtual T Read() => throw new NotSupportedException($"{GetType().Name} reads asynchronously");

    /// <summary>One read that takes a while (a probe, a pool sample); the continuation comes back to the UI thread.</summary>
    public virtual Task<T> ReadAsync(CancellationToken token) => Task.FromResult(Read());

    /// <summary>
    /// [S-2504] The meter reading for one series of this source (<paramref name="instance"/>: a drive,
    /// an adapter; empty for the source's one series). Only sources a meter charts implement it.
    /// </summary>
    public virtual MeterSample Project(T snapshot, string instance)
        => throw new NotSupportedException($"{GetType().Name} has no charted series");
}
