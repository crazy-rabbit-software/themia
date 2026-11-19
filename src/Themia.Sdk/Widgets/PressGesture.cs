namespace Themia.Core.Widgets;

/// <summary>
/// [S-13060] What a press on a widget outside edit mode lands on, as far as the card's move is concerned: the part met
/// first on the way from the element pressed up to the card.
/// </summary>
public enum PressPart
{
    /// <summary>The card's own ground, or a part that only shows: a text, a picture, a chart.</summary>
    Ground,

    /// <summary>An item: a list row, a tile, a day, a link card. Its click is on the release.</summary>
    Item,

    /// <summary>A control that clicks on the release: a button, a toggle, a check, a widget that is one button.</summary>
    Control,

    /// <summary>A slider's or a seek bar's track: a click jumps there on the release.</summary>
    Track,

    /// <summary>A text box at rest: the press itself gave it the focus.</summary>
    Text,

    /// <summary>A slider's or a seek bar's thumb: its drag is the slider's.</summary>
    Thumb,

    /// <summary>A text box that held the focus before the press: a drag selects its text.</summary>
    Typing,
}

/// <summary>
/// [S-13060] The one press rule of a widget outside edit mode (owner, 2026-11-19: "sometimes when i hover over a widget an
/// try to drag it it does not work on the first try. it feels like that this could be because it selects the underlying
/// item in the list. this should not happen"): a press anywhere on a widget that moves past <see cref="DragThreshold"/> is
/// the card's move, on the first press, whatever part is under the pointer, but a thumb's drag and a text being typed in;
/// a press let go without moving is the part's click, as ever. The parts that wait for the release to tell a click from a
/// drag (a slider's track, a seek bar) measure the drag by the same threshold.
/// </summary>
public static class PressGesture
{
    /// <summary>DIPs the pointer must travel, on either axis, before a press is a drag rather than a click.</summary>
    public const double DragThreshold = 3;

    /// <summary>Whether the pointer has travelled far enough from the press to be a drag.</summary>
    public static bool Travelled(double dx, double dy)
        => !double.IsFinite(dx) || !double.IsFinite(dy) || Math.Abs(dx) >= DragThreshold || Math.Abs(dy) >= DragThreshold;

    /// <summary>
    /// Whether a press on <paramref name="part"/> may become the card's move. <paramref name="handled"/>: a part of the
    /// view took the press before the card heard it. A control, a track or a text at rest takes it for its click and gives
    /// it up to a drag; ground or an item that took a press did so for a drag of its own (a fence's band, a folder's file),
    /// which it keeps; a thumb's drag and a text being typed in are always their own.
    /// </summary>
    public static bool CardMayMove(PressPart part, bool handled) => part switch
    {
        PressPart.Thumb or PressPart.Typing => false,
        PressPart.Ground or PressPart.Item => !handled,
        _ => true,
    };
}

/// <summary>What a track does with a pointer step (<see cref="TrackPress"/>).</summary>
public enum TrackStep
{
    /// <summary>Nothing yet: the press may still be a click, or the card's move.</summary>
    Wait,

    /// <summary>A click: the value jumps to where the track was pressed.</summary>
    Click,

    /// <summary>The track follows the pointer: nothing else took the drag.</summary>
    Follow,
}

/// <summary>
/// [S-13060] A press on a slider's or a seek bar's track, outside its thumb: it never acts on the press (a press that
/// becomes the card's move must not have set the volume on its way). Let go without moving, it is a click that jumps to
/// where it was pressed; moved past <see cref="PressGesture.DragThreshold"/>, the card takes it if it may, on that very
/// move (it hears the move after the track); a move after that still held by the track means nothing took it (Lock
/// widgets, a preview), and the track follows the pointer as a slider's track always did. Pure; one per track.
/// </summary>
public sealed class TrackPress
{
    private double _x;
    private double _y;
    private bool _travelled;

    /// <summary>Whether a press on the track waits for its release or its drag.</summary>
    public bool Pending { get; private set; }

    /// <summary>Whether the track follows the pointer (nothing else took the drag).</summary>
    public bool Following { get; private set; }

    /// <summary>Where the waiting press was, in the track's own coordinates.</summary>
    public (double X, double Y) At => (_x, _y);

    /// <summary>A left press on the track at (<paramref name="x"/>, <paramref name="y"/>): it waits.</summary>
    public void Press(double x, double y)
    {
        (_x, _y) = (x, y);
        Pending = true;
        _travelled = false;
        Following = false;
    }

    /// <summary>A move the track still holds.</summary>
    public TrackStep Move(double x, double y)
    {
        if (!Pending)
        {
            return TrackStep.Wait;
        }

        if (Following)
        {
            return TrackStep.Follow;
        }

        if (!PressGesture.Travelled(x - _x, y - _y))
        {
            return TrackStep.Wait;
        }

        // The first move past the threshold is the card's to take: it hears it after the track.
        if (!_travelled)
        {
            _travelled = true;
            return TrackStep.Wait;
        }

        Following = true;
        return TrackStep.Follow;
    }

    /// <summary>The release reached the track: a click where it was pressed, or the end of a drag the track followed (its last point).</summary>
    public TrackStep Release()
    {
        var step = !Pending ? TrackStep.Wait : _travelled ? TrackStep.Follow : TrackStep.Click;
        Pending = false;
        Following = false;
        _travelled = false;
        return step;
    }

    /// <summary>The track lost the pointer (the card took it for its move, or the window went): nothing happens.</summary>
    public void Lost()
    {
        Pending = false;
        Following = false;
        _travelled = false;
    }
}

/// <summary>[S-13060] Where on a slider's track a point falls, as WinUI's slider places its thumb at a click (its <c>MoveThumbToPoint</c>).</summary>
public static class SliderTrack
{
    /// <summary>
    /// The value at <paramref name="offset"/> along a track <paramref name="length"/> long whose thumb is
    /// <paramref name="thumb"/> long (the thumb's middle never comes nearer an end than half of it), from
    /// <paramref name="minimum"/> to <paramref name="maximum"/>, on the nearest whole <paramref name="step"/>;
    /// <paramref name="reversed"/> runs it the other way.
    /// </summary>
    public static double ValueAt(double offset, double length, double thumb, double minimum, double maximum, double step, bool reversed = false)
    {
        if (!double.IsFinite(offset) || !double.IsFinite(length) || maximum <= minimum)
        {
            return minimum;
        }

        var clickable = Math.Max(length - thumb, 1);
        var fraction = Math.Clamp((offset - (thumb / 2)) / clickable, 0, 1);
        if (reversed)
        {
            fraction = 1 - fraction;
        }

        var value = minimum + (fraction * (maximum - minimum));
        if (!(step > 0))
        {
            return value;
        }

        var steps = value / step;
        var next = Math.Min(maximum, Math.Ceiling(steps) * step);
        var previous = Math.Max(minimum, Math.Floor(steps) * step);
        return next - value < value - previous ? next : previous;
    }
}
