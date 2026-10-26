namespace Themia.Core.Settings;

/// <summary>How a <see cref="ListDrag"/> ended, when it did.</summary>
public enum ListDragEnd
{
    /// <summary>Nothing ends here: no drag was under way, or it had ended already.</summary>
    None,

    /// <summary>The drag is dropped: nothing moves.</summary>
    Cancel,

    /// <summary>The item moves to the gap the drag was aimed at (<see cref="ListDrag.Gap"/>).</summary>
    Drop,
}

/// <summary>
/// [S-3732] A press on an ordered list's line and the drag it becomes: the rules, so they are tested
/// without a window. A press that travels <see cref="Threshold"/> DIPs up or down while the button is
/// down starts it; the drop line is aimed at a gap (<see cref="Aim"/>, <see cref="ListDrop.Gap"/>);
/// the release drops it there. [S-3747] The capture lost as the release is handled (the line's button
/// gives up the pointer it held before the release reaches the menu) is that release, not a cancel:
/// only a capture taken away while the button is still down cancels (user bug 2026-10-26: a view
/// dragged in View ▾ never moved). A drag ends once.
/// </summary>
public sealed class ListDrag(double startY)
{
    /// <summary>[S-913] The pointer's travel, in DIPs, that turns a press on a line into a drag.</summary>
    public const double Threshold = 4;

    private bool _ended;

    /// <summary>Where the press was, in the list's coordinates.</summary>
    public double StartY { get; } = startY;

    /// <summary>The press has travelled far enough: the line is being dragged.</summary>
    public bool Active { get; private set; }

    /// <summary>The gap the drop line stands in; -1 until it is aimed.</summary>
    public int Gap { get; private set; } = -1;

    /// <summary>
    /// The pointer moved to <paramref name="y"/>: true when this move started the drag. A move with
    /// the button up ends nothing here (the release does); before the drag starts it is ignored.
    /// </summary>
    public bool Move(double y, bool buttonDown)
    {
        if (_ended || Active || !buttonDown || Math.Abs(y - StartY) < Threshold)
        {
            return false;
        }

        Active = true;
        return true;
    }

    /// <summary>The drop line stands in <paramref name="gap"/>; false when it stood there already.</summary>
    public bool Aim(int gap)
    {
        if (gap == Gap)
        {
            return false;
        }

        Gap = gap;
        return true;
    }

    /// <summary>The button was let go: a drag under way drops where it is aimed.</summary>
    public ListDragEnd Release() => End(drop: true);

    /// <summary>
    /// [S-3747] The pointer's capture was lost. With the button up it is the release being handled
    /// (the drop); with it still down something took the pointer away (the drag is dropped, nothing moves).
    /// </summary>
    public ListDragEnd CaptureLost(bool buttonDown) => End(drop: !buttonDown);

    /// <summary>The drag is abandoned (its list drawn again, the menu closed): nothing moves.</summary>
    public ListDragEnd Cancel() => End(drop: false);

    private ListDragEnd End(bool drop)
    {
        if (_ended || !Active)
        {
            _ended = true;
            return ListDragEnd.None;
        }

        _ended = true;
        return drop && Gap >= 0 ? ListDragEnd.Drop : ListDragEnd.Cancel;
    }
}
