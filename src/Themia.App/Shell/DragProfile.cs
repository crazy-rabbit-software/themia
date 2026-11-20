using System.Diagnostics;

namespace Themia.App.Shell;

/// <summary>The stages of a move's pointer step that <see cref="DragProfile"/> times ([S-11890], <c>--drag-bench</c>).</summary>
internal enum DragStage
{
    /// <summary>The block's translation: the snap and the reach (<c>BlockGeometry.Move</c>, the follow).</summary>
    Geometry,

    /// <summary>[S-11959] Keeping the block apart from the cards that stay.</summary>
    Apart,

    /// <summary>Putting the moving cards where they stand (<c>PositionCard</c>, the canvas offsets, what follows them).</summary>
    Position,

    /// <summary>[S-2240] The joins' rule over the surface's cards (<c>CardJoins.Paint</c>).</summary>
    Joins,

    /// <summary>[S-2241] The cards taking their outlines (a card repainted when its outline changed).</summary>
    JoinApply,

    /// <summary>[S-2123] Liquid glass's bodies and the cards taking them.</summary>
    Glass,

    /// <summary>[S-78] The hand-off preview to another display and the leaving fade.</summary>
    HandOff,

    /// <summary>[S-11874] The local grid round the target.</summary>
    LocalGrid,

    /// <summary>[S-11881] The move's border marks.</summary>
    Marks,

    /// <summary>The bench's synchronous XAML layout pass after a frame's work.</summary>
    Layout,

    /// <summary>The drop: one store, one join pass, one undo step.</summary>
    Commit,
}

/// <summary>
/// [S-11890] Debug only: what each stage of a move's pointer step costs, summed over a run of the
/// <c>--drag-bench</c> render flag. Every member is <see cref="ConditionalAttribute"/> on DEBUG, so a
/// Release build has no call to it at all; and off (<see cref="On"/> false) it is one branch.
/// </summary>
internal static class DragProfile
{
#if DEBUG
    private static readonly long[] Ticks = new long[Enum.GetValues<DragStage>().Length];
    private static long _mark;

    /// <summary>Whether the stages are being timed: the bench switches it on round its run.</summary>
    public static bool On { get; set; }

    /// <summary>The milliseconds summed in <paramref name="stage"/> since the last <see cref="Reset"/>.</summary>
    public static double Ms(DragStage stage) => Ticks[(int)stage] * 1000.0 / Stopwatch.Frequency;

    /// <summary>Every stage back to nothing.</summary>
    public static void Reset() => Array.Clear(Ticks);
#endif

    /// <summary>A step starts: the next <see cref="Lap"/> counts from here.</summary>
    [Conditional("DEBUG")]
    public static void Start()
    {
#if DEBUG
        if (On)
        {
            _mark = Stopwatch.GetTimestamp();
        }
#endif
    }

    /// <summary>The time since the last <see cref="Start"/> or <see cref="Lap"/> was spent in <paramref name="stage"/>.</summary>
    [Conditional("DEBUG")]
    public static void Lap(DragStage stage)
    {
#if DEBUG
        if (On)
        {
            var now = Stopwatch.GetTimestamp();
            Ticks[(int)stage] += now - _mark;
            _mark = now;
        }
#endif
    }
}
