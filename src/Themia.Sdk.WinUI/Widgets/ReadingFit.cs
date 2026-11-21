using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Windows.Foundation;

namespace Themia.App.Widgets;

/// <summary>
/// [S-12808] [S-12814] [S-12804] The one panel a one-readout block is drawn in (a clock's face, a date, a reading and its
/// caption, its controls, a bar, a tile's face, a text kind's line): its parts are measured (<see cref="Ink.Of"/>) and laid
/// where <see cref="BlockLayout.Solve(double, double, Func{double, IReadOnlyList{PartBox}}, BlockFace?, ContentAlign, double, bool)"/>
/// puts them: packed top to bottom (WR-7, [S-14280]), the block scaled as one until its ink fills the room's height or its
/// parts' own width fills its width, centred on its ink in the height left where the width stops it ([S-12808]; on its foot
/// with <see cref="Foot"/>), each part's ink across where <see cref="Align"/> puts it ([S-14301]), a part that takes the
/// width it is given (<see cref="Span"/>) across the whole room. A bar, a slider or a graph never limits the scale. The
/// sides' and the foot's insets are this panel's margin (or its view's padding), so they never scale. [S-12833] A smaller
/// room scales it down as one to its smallest drawn text's legibility floor; with its kind's parts stated
/// (<see cref="Face"/>) its main reading sets that floor and its smaller pieces give way first. [S-14791] A line it cuts
/// (<see cref="CutLine"/>) never holds it under its floor, and it never grows past where that line's held text stands whole.
/// <para>
/// The layout is computed in the measure (the floor decided once a room, on the parts measured at each floor) and applied
/// in the arrange: the parts are this panel's own children, so a part measured again (its text changed) measures the block
/// again, and the panel's one transform draws them at the block's scale.
/// </para>
/// </summary>
public sealed partial class ReadingFit : Panel
{
    private readonly ScaleTransform _scale = new();
    private readonly List<(UIElement Part, Border Holder, double Size)> _givesWay = [];
    private readonly List<(UIElement Piece, double Size)> _pieces = [];
    private readonly HashSet<UIElement> _spans = [];
    private ContentAlign _align = ContentAlign.Center;
    private FaceParts? _face;
    private BlockFace? _stated;
    private double _spacing;

    // [S-12833] [S-14243] The floor the face is drawn at (its pieces under it give way) and the room it was decided for.
    private double _floor;
    private Size _decided = Size.Empty;

    // The last measure's fill and its parts, as the arrange lays them.
    private BlockFill _fill = new(1, 0, 0);
    private IReadOnlyList<PartBox> _parts = [];

    public ReadingFit()
    {
        Margin = new Thickness(FamilySpace.Inset, 0, FamilySpace.Inset, FamilySpace.Inset);
        RenderTransform = _scale;

        // [S-14301] The block is drawn scaled, so a whole pixel in its own units is the scale's pixels drawn: laid out
        // rounded, a part centred in it stood 2.6 DIP off the card's middle at 7x (the GPU card's "--" at 600x200).
        UseLayoutRounding = false;
    }

    /// <summary>
    /// [S-14319] Its last ink on the room's foot where the width stops it (a stopwatch's controls on its laps' top, WR-1),
    /// the height left over its first ink; else shared alike over and under ([S-12808]).
    /// </summary>
    public bool Foot
    {
        get => _foot;
        set
        {
            if (value != _foot)
            {
                _foot = value;
                _held = Size.Empty;
                InvalidateMeasure();
            }
        }
    }

    private bool _foot;

    /// <summary>
    /// [S-12814] WR-12 (owner, 2026-11-19: "some widgets like the now playing scrubber changes text sizes when hovering
    /// over it with the mouse ... this should not be"): while held, the block keeps the scale it has (its text may change,
    /// a linked moment's value and time on a meter's reading); a new room still fits it again.
    /// </summary>
    public bool Hold
    {
        get => _hold;
        set
        {
            if (value != _hold)
            {
                _hold = value;
                InvalidateMeasure();
            }
        }
    }

    private bool _hold;
    private Size _held = Size.Empty;

    /// <summary>The block's parts, top to bottom.</summary>
    public UIElementCollection Parts => Children;

    /// <summary>
    /// [S-14301] Where the parts with a width of their own stand across the content (owner, 2026-11-20, of a reading that
    /// fills the card's height but not its width: "Centred is fine"): centred, or where the card's alignment puts it for a
    /// kind that aligns ([S-6583]: its view hands the fit the alignment it draws). The fit sets each part's place: a view
    /// never sets a part's <see cref="FrameworkElement.HorizontalAlignment"/> of its own.
    /// </summary>
    public ContentAlign Align
    {
        get => _align;
        set
        {
            if (value != _align)
            {
                _align = value;
                InvalidateArrange();
            }
        }
    }

    /// <summary>
    /// [S-14301] A part that takes the whole width it is given (a slider's row, a bar, a row whose line is cut to its
    /// room): it runs across the content, never placed by <see cref="Align"/>. A part that asks for no width of its own when
    /// measured (a bar alone) spans without being named.
    /// </summary>
    public void Span(UIElement part)
    {
        ArgumentNullException.ThrowIfNull(part);
        if (_spans.Add(part))
        {
            InvalidateMeasure();
        }
    }

    /// <summary>The least gap between two parts (a family token).</summary>
    public double Spacing
    {
        get => _spacing;
        set
        {
            if (!value.Equals(_spacing))
            {
                _spacing = value;
                InvalidateMeasure();
            }
        }
    }

    /// <summary>
    /// [S-12833] The face's parts as its kind states them (the same its sizing's least box is made of): in a room that
    /// would draw them under the floor the pieces give way, smallest first, judged on the block as it is drawn ([S-14243]):
    /// a block's own (<see cref="IFacePieces"/>, a Readout's unit, lead and line under) and the parts added with
    /// <see cref="AddGivesWay"/>; the parts left fill the room (WR-1, WR-4: the block grows back into the room a part left).
    /// Null: every part is drawn.
    /// </summary>
    public FaceParts? Face
    {
        get => _face;
        set
        {
            if (!Equals(value, _face))
            {
                _face = value;
                _stated = value is null ? null : BlockFace.Of(value);
                _held = Size.Empty;
                _decided = Size.Empty;
                InvalidateMeasure();
            }
        }
    }

    /// <summary>
    /// [S-12833] Add a part, after those already in, that gives way as a whole where the room would draw it under the
    /// floor: a Controls row at Body, a caption. It stands in a box of the fit's own, so its view still shows and hides it.
    /// </summary>
    public void AddGivesWay(UIElement part, double size) => Children.Add(GivesWayBox(part, size));

    /// <summary>
    /// [S-12833] A box holding <paramref name="part"/> that gives way as a whole where the room would draw it under the
    /// floor, for its view to stand inside one of its parts (a line under a title, an end column): the fit shows and hides
    /// the box, the view still shows and hides the part.
    /// </summary>
    public Border GivesWayBox(UIElement part, double size)
    {
        ArgumentNullException.ThrowIfNull(part);
        var holder = new Border { Child = part };
        _givesWay.Add((part, holder, size));
        return holder;
    }

    /// <summary>
    /// [S-12833] A piece inside a part (a caption under a reading) that gives way where the room would draw it under the
    /// floor: the fit alone shows and hides it, so its view never sets its Visibility (it shows or hides the part round it).
    /// </summary>
    public void GivesWay(UIElement piece, double size)
    {
        ArgumentNullException.ThrowIfNull(piece);
        _pieces.Add((piece, size));
    }

    /// <summary>[S-12833] Let go of a piece given with <see cref="GivesWay"/> (its view dropped it).</summary>
    public void Forget(UIElement piece) => _pieces.RemoveAll(p => ReferenceEquals(p.Piece, piece));

    /// <summary>
    /// [S-14201] Measure the block again although no part's size changed: new words or figures may ink higher or lower in
    /// the same box (a descender in the line under), which XAML does not measure again while its size stays.
    /// </summary>
    public void Refit() => InvalidateMeasure();

    /// <summary>
    /// [S-12834] The block's natural height: its shown parts at their own size, packed with their spacing. A view with rows
    /// under its block (a stock's holding, a stopwatch's laps) gives the block that, grown to a grid line, and the rows the
    /// rest (WR-6: rows are whole cells and never scale with a face).
    /// </summary>
    public double Natural() => BlockLayout.Measure(Measured(), 0, _stated, _spacing).Packed;

    /// <summary>[S-12833] Show the pieces of at least <paramref name="floor"/>: the parts that give way, and every block's own.</summary>
    private void Keep(double floor)
    {
        foreach (var (piece, size) in _pieces)
        {
            var shown = _face?.Keeps(size, floor) ?? true ? Visibility.Visible : Visibility.Collapsed;
            if (piece.Visibility != shown)
            {
                piece.Visibility = shown;
            }
        }

        foreach (var (part, holder, size) in _givesWay)
        {
            var shown = part.Visibility == Visibility.Visible && (_face?.Keeps(size, floor) ?? true) ? Visibility.Visible : Visibility.Collapsed;
            if (holder.Visibility != shown)
            {
                holder.Visibility = shown;
            }
        }

        foreach (var part in Children)
        {
            Ink.Keep(part, floor);
        }
    }

    /// <summary>The parts measured at their own size (unbounded), as the block lays them: each what it draws, and whether it spans.</summary>
    private List<PartBox> Measured()
    {
        var parts = new List<PartBox>(Children.Count);
        foreach (var part in Children)
        {
            part.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var box = Ink.Of(part);
            parts.Add(box with { Spans = Spans(part) });
        }

        return parts;
    }

    /// <summary>Whether <paramref name="part"/> (or the part a give-way box holds) takes the whole width: named, or asking for none.</summary>
    private bool Spans(UIElement part)
    {
        var held = part is Border holder && _givesWay.Exists(g => ReferenceEquals(g.Holder, holder)) ? holder.Child : part;
        return held is not null && (_spans.Contains(held) || (held.Visibility == Visibility.Visible && held.DesiredSize is { Width: < 1, Height: > 0 }));
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var finite = double.IsFinite(availableSize.Width) && double.IsFinite(availableSize.Height);
        if (_stated is null || !finite)
        {
            _floor = 0;
            _decided = Size.Empty;
        }
        else if (_decided != availableSize && (!_hold || _held != availableSize))
        {
            // [S-12833] [S-14243] What the room draws of the face: decided once for a room, so hiding a piece settles.
            Decide(availableSize, _stated);
        }

        Keep(_floor);
        var parts = Measured();
        var block = BlockLayout.Measure(parts, 0, _stated, _spacing);

        // [S-14243] A text grown since (a longer reading) that would draw a kept piece under the floor: it gives way now.
        if (_stated is { } face && finite && !_hold && BlockLayout.Kept(face, _floor) * Raw(availableSize, block) < ScaleFloor.Text - FaceFloor.Hair)
        {
            Decide(availableSize, face);
            Keep(_floor);
            parts = Measured();
            block = BlockLayout.Measure(parts, 0, _stated, _spacing);
        }

        if (!_hold || _held != availableSize)
        {
            // [S-12833] Down to its smallest text's floor, up to what fills the room; [S-14791] never past where a line it
            // cuts would stand whole; [S-14319] on its foot where it stands on it.
            _fill = BlockLayout.Fill(availableSize.Width, availableSize.Height, block, _foot);
            _held = availableSize;
        }

        _parts = parts;
        return new Size(
            double.IsFinite(availableSize.Width) ? availableSize.Width : parts.Select(p => p.Width).DefaultIfEmpty(0).Max(),
            double.IsFinite(availableSize.Height) ? availableSize.Height : block.Packed);
    }

    /// <summary>The scale the room alone fills the measured block at: no floor.</summary>
    private static double Raw(Size room, BlockLayout.Measured block)
        => BlockLayout.Fill(room.Width, room.Height, block.Intrinsic, block.Packed, block.Ink, least: NoFloor).Scale;

    /// <summary>A least scale so small it never holds the block: the scale the room alone gives it.</summary>
    private const double NoFloor = 1e-6;

    /// <summary>
    /// [S-14243] The face's floor in <paramref name="room"/>, decided on the parts as they are drawn at each floor
    /// (<see cref="BlockLayout.Decide(double, double, BlockFace, Func{double, IReadOnlyList{PartBox}}, double)"/>): the
    /// stated plan is sized on the face's widest reading, so a countdown's "3d 5h" in a 300 × 164 card had dropped its name
    /// and its date (its "88 days ago" would have drawn them at 8.9) while the time itself was drawn more than twice that large.
    /// </summary>
    private void Decide(Size room, BlockFace face)
    {
        _decided = room;
        _floor = BlockLayout.Decide(room.Width, room.Height, face, floor =>
        {
            Keep(floor);
            return Measured();
        }, _spacing);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _scale.ScaleX = _fill.Scale;
        _scale.ScaleY = _fill.Scale;
        var places = BlockLayout.Place(finalSize.Width, _parts, 0, _fill, _align, _spacing);
        for (var i = 0; i < Children.Count; i++)
        {
            var part = Children[i];
            if (i >= places.Count || places[i] is not { } place)
            {
                part.Arrange(default);
                continue;
            }

            // A part that spans is laid across the room: it stretches to it, whatever it asked.
            if (_parts[i].Spans && part is FrameworkElement { HorizontalAlignment: not HorizontalAlignment.Stretch } stretch)
            {
                stretch.HorizontalAlignment = HorizontalAlignment.Stretch;
            }

            part.Arrange(new Rect(place.X, place.Y, place.Width, _parts[i].Height));
        }

        return finalSize;
    }
}
