using Themia.Core.Widgets;

namespace Themia.Core.SystemData;

/// <summary>
/// [S-6318] Each logical processor's recent load, for the CPU meter's Core graphs style: one ring
/// for every core together (they are read together, once a tick), oldest first. Kept by the hub's
/// <c>CpuCores</c> source, not by a view, so a meter built again (a view switch, edit mode) draws
/// every core's history at once, as the total's does ([S-2504]).
/// <para>
/// Unlike a charted series it is not an hour by default: it holds what the longest Core graphs span
/// in any view needs (<see cref="CapacityFor"/>) and nothing while no widget shows the style. A load is
/// a <see cref="float"/> (4 bytes): a minute on 16 cores is about 4 KB, an hour on 32 cores about
/// 470 KB. An unknown load is a gap (NaN).
/// </para>
/// </summary>
public sealed class CoreHistory
{
    /// <summary>The narrowest core tile a history is sized for, in DIPs: its folding sets the lead the slide needs ([S-1826]).</summary>
    public const double NarrowestTile = 24;

    private float[] _loads = [];
    private int _capacity;
    private int _start;

    /// <summary>How many processors each sample has.</summary>
    public int Cores { get; private set; }

    /// <summary>How many samples it holds at most; 0 keeps nothing.</summary>
    public int Capacity => _capacity;

    /// <summary>How many samples it holds now.</summary>
    public int Count { get; private set; }

    /// <summary>How many samples were ever added since the last reset: the newest one's absolute position ([S-1121]).</summary>
    public long Added { get; private set; }

    /// <summary>
    /// [S-6318] The samples a <paramref name="spanSeconds"/> span needs at the hub's one-second
    /// cadence: the span's points and the lead its band slides with on the narrowest tile (the slot
    /// sliding out at the left, the newest beyond the right, [S-1826]). 0 for no span: nothing shows
    /// the style.
    /// </summary>
    public static int CapacityFor(int spanSeconds)
    {
        if (spanSeconds <= 0)
        {
            return 0;
        }

        var points = MeterTiming.Points(spanSeconds, SystemCadence.TickSeconds);
        return points + SparkScroll.Lead(MeterTiming.BucketSize(points, NarrowestTile));
    }

    /// <summary>
    /// Hold <paramref name="capacity"/> samples: the newest ones kept when it shrinks, all of them
    /// when it grows; 0 drops everything.
    /// </summary>
    public void Resize(int capacity)
    {
        capacity = Math.Max(0, capacity);

        if (capacity == _capacity)
        {
            return;
        }

        var keep = Math.Min(Count, capacity);
        var loads = new float[capacity * Cores];

        for (var i = 0; i < keep; i++)
        {
            var from = Slot(Count - keep + i);
            Array.Copy(_loads, from * Cores, loads, i * Cores, Cores);
        }

        _loads = loads;
        _capacity = capacity;
        _start = 0;
        Count = keep;

        if (capacity == 0)
        {
            Added = 0;
        }
    }

    /// <summary>
    /// Add one read of every processor (0..1, null unknown). A read with another number of
    /// processors (the first, or a machine whose processors changed) starts the history again: a
    /// core's line is never another core's.
    /// </summary>
    public void Add(IReadOnlyList<double?> loads)
    {
        ArgumentNullException.ThrowIfNull(loads);

        if (_capacity == 0 || loads.Count == 0)
        {
            return;
        }

        if (loads.Count != Cores)
        {
            Cores = loads.Count;
            _loads = new float[_capacity * Cores];
            _start = 0;
            Count = 0;
            Added = 0;
        }

        int slot;

        if (Count < _capacity)
        {
            slot = Slot(Count);
            Count++;
        }
        else
        {
            slot = _start;
            _start = (_start + 1) % _capacity;
        }

        for (var core = 0; core < Cores; core++)
        {
            _loads[(slot * Cores) + core] = loads[core] is { } load && double.IsFinite(load) ? (float)Math.Clamp(load, 0, 1) : float.NaN;
        }

        Added++;
    }

    /// <summary>One processor's newest <paramref name="max"/> loads, oldest first, as chart points (a copy); empty for a core it does not have.</summary>
    public SeriesPoint[] Newest(int core, int max)
    {
        if ((uint)core >= (uint)Cores)
        {
            return [];
        }

        var count = Math.Clamp(max, 0, Count);
        var points = new SeriesPoint[count];

        for (var i = 0; i < count; i++)
        {
            points[i] = new SeriesPoint(_loads[(Slot(Count - count + i) * Cores) + core]);
        }

        return points;
    }

    private int Slot(int index) => _capacity == 0 ? 0 : (_start + index) % _capacity;
}
