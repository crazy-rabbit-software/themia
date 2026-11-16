namespace Themia.Core.Widgets;

/// <summary>[S-6412] Where a left press in the folder list landed, which decides what it does.</summary>
public enum FolderPress
{
    /// <summary>On an item's icon or name: a click selects it, a drag carries it out ([S-1332]).</summary>
    Item,

    /// <summary>
    /// On an item's empty part (a row beside its name, a tile around its icon and label): a click
    /// selects it, as Explorer's full-row Details view does; a drag from there carries nothing.
    /// </summary>
    ItemBlank,

    /// <summary>Between or below the items: a click clears the selection.</summary>
    Background,
}

/// <summary>What one pointer move means ([S-1328]).</summary>
public enum GestureStep
{
    /// <summary>Nothing to do yet: inside the dead zone, or no drag candidate in flight.</summary>
    Idle,

    /// <summary>The press on an item moved past the dead zone: drag <see cref="FolderGesture.DragPaths"/> out.</summary>
    StartDrag,
}

/// <summary>
/// [S-1328] The press → click | file drag decision, ported from the original's <c>gesture.ts</c>
/// ([S-65], issue #58). Pure, so the "every exit path resets" rule is tested. The folder draws no
/// rubber band of its own ([S-6412]): the one band is the desktop's ([S-5544]).
/// <para>
/// The bug it exists for: an aborted gesture left a drag origin behind, the next plain press measured
/// its threshold against that stale origin, started a drag at once, and the drag's modal loop ate the
/// release, so single-click open died until restart. Here every exit (release, cancel, capture lost,
/// the drag starting) resets the candidate, <see cref="BeginPress"/> wipes everything before arming,
/// and a generation number lets a deferred drag start tell that its press is long gone.
/// </para>
/// </summary>
public sealed class FolderGesture
{
    /// <summary>A 5 DIP dead zone: movement below it is still a click ([S-6624]: the one every item-selecting widget uses).</summary>
    public const double Threshold = SelectionBand.Threshold;

    private (double X, double Y)? _fileStart;

    /// <summary>Bumped on every reset and press; a deferred action compares it to the one it captured.</summary>
    public int Generation { get; private set; }

    /// <summary>The paths a drag starting now would carry.</summary>
    public IReadOnlyList<string> DragPaths { get; private set; } = [];

    /// <summary>A drag was handed off this gesture; the trailing click must not also select or open.</summary>
    public bool DragWasActive { get; private set; }

    /// <summary>Whether a press that may become a drag is in flight.</summary>
    public bool Pressed => _fileStart is not null;

    public static bool PastThreshold(double dx, double dy) => SelectionBand.PastThreshold(dx, dy);

    /// <summary>[S-6412] The press kind from what the pointer is over: only the icon and the name carry the item.</summary>
    public static FolderPress Classify(bool onItem, bool onIconOrName)
        => !onItem ? FolderPress.Background : onIconOrName ? FolderPress.Item : FolderPress.ItemBlank;

    /// <summary>
    /// A left press. Wipes every earlier candidate and the suppress flag first, then arms a drag
    /// candidate only on an item's icon or name ([S-6412]); any other press stays a click. Returns the
    /// generation the gesture owns.
    /// </summary>
    public int BeginPress(double x, double y, FolderPress press, IReadOnlyList<string>? paths = null)
    {
        Reset();
        DragWasActive = false;

        if (press == FolderPress.Item)
        {
            _fileStart = (x, y);
            DragPaths = paths ?? [];
        }

        return Generation;
    }

    /// <summary>One pointer move with the left button still down.</summary>
    public GestureStep Move(double x, double y)
    {
        if (_fileStart is not { } start || !PastThreshold(x - start.X, y - start.Y))
        {
            return GestureStep.Idle;
        }

        var paths = DragPaths;
        DragWasActive = true;
        Reset();
        DragPaths = paths;
        return GestureStep.StartDrag;
    }

    /// <summary>
    /// The release (or a cancel / capture loss): resets the candidate and says whether a drag ran, so
    /// the caller can swallow the click that follows. The flag stays readable until the next press.
    /// </summary>
    public bool Release()
    {
        var wasDrag = DragWasActive;
        Reset();
        return wasDrag;
    }

    private void Reset()
    {
        _fileStart = null;
        DragPaths = [];
        Generation++;
    }
}
