using Themia.Core.Widgets;

namespace Themia.Core.SystemData;

/// <summary>
/// [S-2504] One point of a charted series, in the chart's own terms: a fraction on a share chart, the
/// value on a window-scaled one (<see cref="ChartScale"/>).
/// </summary>
/// <param name="First">What the chart fills.</param>
/// <param name="Second">The second line (upload, writes); NaN where there is none.</param>
/// <param name="Missed">A probe that went unanswered: plotted where <see cref="First"/> says (the baseline), but no answer for statistics.</param>
public readonly record struct SeriesPoint(double First, double Second = double.NaN, bool Missed = false)
{
    /// <summary>[S-2428] The latency a ping point stands for: null when the probe was not answered.</summary>
    public double? Answer => Missed ? null : First;
}

/// <summary>
/// [S-2504] A fixed-size ring of the newest points of one series, oldest first. Kept by the hub, not
/// the view, so a view built again reads the same history.
/// </summary>
/// <typeparam name="T">The point type.</typeparam>
public sealed class SampleRing<T>
    where T : struct
{
    private readonly T[] _items;
    private int _start;

    public SampleRing(int capacity)
    {
        _items = new T[Math.Max(1, capacity)];
    }

    /// <summary>How many points the ring holds at most.</summary>
    public int Capacity => _items.Length;

    /// <summary>How many it holds now.</summary>
    public int Count { get; private set; }

    /// <summary>
    /// How many points were ever added (not reset by forgetting the oldest): a point's absolute
    /// position, so a chart that folds points into buckets keeps each bucket's members fixed ([S-1121]).
    /// </summary>
    public long Added { get; private set; }

    /// <summary>The newest, or null when empty.</summary>
    public T? Latest => Count == 0 ? null : _items[(_start + Count - 1) % _items.Length];

    /// <summary>Add the newest point; a full ring forgets its oldest.</summary>
    public void Add(T item)
    {
        Added++;

        if (Count < _items.Length)
        {
            _items[(_start + Count) % _items.Length] = item;
            Count++;
            return;
        }

        _items[_start] = item;
        _start = (_start + 1) % _items.Length;
    }

    public void Clear()
    {
        _start = 0;
        Count = 0;
        Added = 0;
    }

    /// <summary>The newest <paramref name="max"/> points (all of them when fewer), oldest first, as a copy.</summary>
    public T[] Newest(int max)
    {
        var count = Math.Clamp(max, 0, Count);
        var result = new T[count];
        var from = Count - count;

        for (var i = 0; i < count; i++)
        {
            result[i] = _items[(_start + from + i) % _items.Length];
        }

        return result;
    }
}

/// <summary>[S-2504] How long a series' history is.</summary>
public static class SeriesHistory
{
    /// <summary>[S-2427] The widest statistics window a ping series serves: the ping details' largest window.</summary>
    public const int MaxPingWindow = 500;

    /// <summary>
    /// Every series of a source holds the longest history any chart can ask for
    /// (<see cref="MeterTiming.MaxSpanSeconds"/> at the source's spacing), and a ping series at least
    /// the widest statistics window, so a span made longer shows the longer history at once.
    /// </summary>
    public static int Capacity(SystemSource source)
    {
        var points = MeterTiming.MaxSpanSeconds / SystemCadence.PeriodSeconds(source);
        return source == SystemSource.Ping ? Math.Max(points, MaxPingWindow) : points;
    }
}
