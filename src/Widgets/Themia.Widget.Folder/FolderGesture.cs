namespace Themia.Core.Widgets;

/// <summary>What one pointer move or release means ([S-1328]).</summary>
public enum GestureStep
{
    /// <summary>Nothing to do yet: inside the dead zone, or no press in flight.</summary>
    Idle,

    /// <summary>The press on an item moved past the dead zone: hand <see cref="FolderGesture.DragPaths"/> to an OLE drag.</summary>
    StartDrag,

    /// <summary>The press on the background (or an item's empty part, [S-6412]) is a rubber band: paint <see cref="FolderGesture.BandRect"/>.</summary>
    Band,
}

/// <summary>
/// [S-1328] The press → click | rubber band | file drag decision, ported from the original's
/// <c>gesture.ts</c> ([S-65], issue #58). Pure, so the "every exit path resets" rule is tested.
/// <para>
/// The bug it exists for: an aborted gesture left a drag origin behind, the next plain press measured
/// its threshold against that stale origin, started a drag at once, and the drag's modal loop ate the
/// release, so single-click open died until restart. Here every exit (release, cancel, capture lost,
/// the drag starting) resets the candidates, <see cref="BeginPress"/> wipes everything before arming,
/// and a generation number lets a deferred drag start tell that its press is long gone.
/// </para>
/// </summary>
public sealed class FolderGesture
{
    /// <summary>A 5 DIP dead zone: movement below it is still a click ([S-6624]: the one every item-selecting widget uses).</summary>
    public const double Threshold = SelectionBand.Threshold;

    private (double X, double Y)? _fileStart;
    private (double X, double Y)? _bandOrigin;

    /// <summary>Bumped on every reset and press; a deferred action compares it to the one it captured.</summary>
    public int Generation { get; private set; }

    /// <summary>The paths a drag starting now would carry.</summary>
    public IReadOnlyList<string> DragPaths { get; private set; } = [];

    /// <summary>The band to paint after a <see cref="GestureStep.Band"/>.</summary>
    public BandRect BandRect { get; private set; }

    /// <summary>A drag was handed off this gesture; the trailing click must not also select or open.</summary>
    public bool DragWasActive { get; private set; }

    /// <summary>A band ran this gesture; the trailing click must not also clear the selection.</summary>
    public bool BandWasActive { get; private set; }

    /// <summary>Whether a press is in flight.</summary>
    public bool Pressed => _fileStart is not null || _bandOrigin is not null;

    public static bool PastThreshold(double dx, double dy) => SelectionBand.PastThreshold(dx, dy);

    /// <summary>
    /// A left press. Wipes every earlier candidate and suppress flag first, then arms exactly one:
    /// a drag candidate on an item's icon or name, a band candidate on the background or an item's
    /// empty part ([S-6412]). Returns the generation the gesture owns.
    /// </summary>
    public int BeginPress(double x, double y, FolderPress press, IReadOnlyList<string>? paths = null)
    {
        Reset();
        DragWasActive = false;
        BandWasActive = false;

        if (press == FolderPress.Item)
        {
            _fileStart = (x, y);
            DragPaths = paths ?? [];
        }
        else
        {
            _bandOrigin = (x, y);
        }

        return Generation;
    }

    /// <summary>One pointer move with the left button still down.</summary>
    public GestureStep Move(double x, double y)
    {
        if (_fileStart is { } start)
        {
            if (!PastThreshold(x - start.X, y - start.Y))
            {
                return GestureStep.Idle;
            }

            var paths = DragPaths;
            DragWasActive = true;
            Reset();
            DragPaths = paths;
            return GestureStep.StartDrag;
        }

        if (_bandOrigin is { } origin && (BandWasActive || PastThreshold(x - origin.X, y - origin.Y)))
        {
            BandWasActive = true;
            BandRect = new BandRect(origin.X, origin.Y, x, y);
            return GestureStep.Band;
        }

        return GestureStep.Idle;
    }

    /// <summary>
    /// The release (or a cancel / capture loss): resets the candidates and says whether a drag or a
    /// band ran, so the caller can swallow the click that follows. The flags stay readable until
    /// the next press.
    /// </summary>
    public (bool WasDrag, bool WasBand) Release()
    {
        var result = (DragWasActive, BandWasActive);
        Reset();
        return result;
    }

    private void Reset()
    {
        _fileStart = null;
        _bandOrigin = null;
        DragPaths = [];
        Generation++;
    }
}
