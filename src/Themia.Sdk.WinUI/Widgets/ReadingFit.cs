using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Windows.Foundation;

namespace Themia.App.Widgets;

/// <summary>
/// [S-12808] [S-12814] A one-readout view's block in its room (<see cref="BlockFit.Fill"/>): its parts (a reading and
/// its caption, its controls, a bar) stand together and the block scales up as one to the largest size at which the
/// parts with a width of their own fit the room's width and the parts packed together fit its height; then it is laid
/// out in the whole height left (a <see cref="SpreadStack"/> that justifies: the room between its parts, one part
/// taking it all), its first ink on the room's top and its last on its foot, never a band over or under them. A bar,
/// a slider or a graph spans the width it is given and never limits the scale. The sides' and the foot's insets are
/// this panel's margin (or its view's padding), so they never scale. [S-12833] A smaller room scales it down as one,
/// to its smallest drawn text's legibility floor (<see cref="ScaleFloor"/>); with its kind's parts stated
/// (<see cref="Face"/>) its main reading sets that floor and its smaller pieces give way first (<see cref="FaceFloor"/>).
/// </summary>
public sealed partial class ReadingFit : Panel
{
    private readonly Plan _plan;
    private readonly ScaleTransform _scale = new();
    private BlockFill _fill = new(1, 0, 0);
    private FaceParts? _face;
    private readonly List<(UIElement Part, Border Holder, double Size)> _givesWay = [];
    private readonly List<(UIElement Piece, double Size)> _pieces = [];

    // [S-12833] [S-14243] The floor the face is drawn at (its pieces under it give way) and the room it was decided for.
    private double _floor;
    private Size _decided = Size.Empty;

    // [S-14244] Inside this fit's own measure: the plan measured from here is not a part's change.
    private bool _measuring;

    public ReadingFit()
    {
        _plan = new Plan(this) { Justify = true };
        Margin = new Thickness(FamilySpace.Inset, 0, FamilySpace.Inset, FamilySpace.Inset);
        _plan.RenderTransform = _scale;
        Children.Add(_plan);
    }

    /// <summary>
    /// [S-14244] The block's parts, laid in its height. A part measured again on its own (its text changed: a reading
    /// becoming "--", a placeholder its reading) measures the plan again, and the plan has its fit fit the block again:
    /// the scale and where the first ink stands follow the part's new width and ink even where the plan's own size stays
    /// (a Controls row wider than the reading kept it, and a brightness card's "--" stood 25 under the top, its fit
    /// still placing the "0%" it had replaced).
    /// </summary>
    private sealed partial class Plan : SpreadStack
    {
        private readonly ReadingFit _fit;

        public Plan(ReadingFit fit) => _fit = fit;

        protected override Size MeasureOverride(Size availableSize)
        {
            var size = base.MeasureOverride(availableSize);
            if (!_fit._measuring)
            {
                _fit.InvalidateMeasure();
            }

            return size;
        }
    }

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
    public UIElementCollection Parts => _plan.Children;

    /// <summary>
    /// [S-12833] The face's parts as its kind states them (the same its sizing's least box is made of): in a room that
    /// would draw them under the floor the pieces give way, smallest first, judged on the block as it is drawn ([S-14243]):
    /// a block's own (<see cref="IFacePieces"/>, a Readout's unit, lead and line under) and the parts added with
    /// <see cref="AddGivesWay"/>; the parts left fill the room (WR-1, WR-4: <see cref="FaceFit.Fill"/>, the block grows back
    /// into the room a part left). Null: every part is drawn.
    /// </summary>
    public FaceParts? Face
    {
        get => _face;
        set
        {
            if (!Equals(value, _face))
            {
                _face = value;
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
    public void AddGivesWay(UIElement part, double size) => _plan.Children.Add(GivesWayBox(part, size));

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

        foreach (var part in _plan.Children)
        {
            KeepIn(part, floor);
        }
    }

    private static void KeepIn(UIElement node, double floor)
    {
        switch (node)
        {
            case IFacePieces pieces:
                pieces.Keep(floor);
                break;
            case Panel panel:
                foreach (var child in panel.Children)
                {
                    KeepIn(child, floor);
                }

                break;
            case Border { Child: { } child }:
                KeepIn(child, floor);
                break;
            case ContentControl { Content: UIElement content }:
                KeepIn(content, floor);
                break;
        }
    }

    /// <summary>The least gap between two parts (a family token).</summary>
    public double Spacing
    {
        get => _plan.Spacing;
        set => _plan.Spacing = value;
    }

    /// <summary>
    /// [S-12834] The block's natural height: its shown parts at their own size, packed with their spacing. A view with rows
    /// under its block (a stock's holding, a stopwatch's laps) gives the block that, grown to a grid line, and the rows the
    /// rest (WR-6: rows are whole cells and never scale with a face).
    /// </summary>
    public double Natural()
    {
        var shown = _plan.Children.OfType<FrameworkElement>().Where(c => c.Visibility == Visibility.Visible).ToList();
        var packed = 0.0;
        foreach (var part in shown)
        {
            part.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            packed += part.DesiredSize.Height;
        }

        return packed + (Math.Max(0, shown.Count - 1) * _plan.Spacing);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _measuring = true;
        try
        {
            var finite = double.IsFinite(availableSize.Width) && double.IsFinite(availableSize.Height);
            if (_face is null || !finite)
            {
                _floor = 0;
                _decided = Size.Empty;
            }
            else if (_decided != availableSize && (!_hold || _held != availableSize))
            {
                // [S-12833] [S-14243] What the room draws of the face: decided once for a room, so hiding a piece settles.
                Decide(availableSize, _face);
            }

            Keep(_floor);
            var block = Measured(availableSize);

            // [S-14243] A text grown since (a longer reading) that would draw a kept piece under the floor: it gives way now.
            if (_face is { } face && finite && !_hold && Kept(face) * block.Raw < ScaleFloor.Text - 1e-9)
            {
                Decide(availableSize, face);
                Keep(_floor);
                block = Measured(availableSize);
            }

            if (!_hold || _held != availableSize)
            {
                // [S-12833] Down to its smallest text's floor, up to what fills the room.
                _fill = BlockFit.Fill(availableSize.Width, availableSize.Height, block.Intrinsic, block.Packed, block.Held, block.Ink, block.Least);
                _held = availableSize;
            }

            _scale.ScaleX = _fill.Scale;
            _scale.ScaleY = _fill.Scale;

            // [S-12814] The plan is measured unbounded across, as its parts were: a part whose text changes is measured again
            // at that same width, so its new width reaches this fit and the scale follows it (a measure at the scaled width
            // would clamp a grown text and the change would stop there, the old scale cutting the new text).
            _plan.Measure(new Size(double.PositiveInfinity, _fill.Height));
            return new Size(
                double.IsFinite(availableSize.Width) ? availableSize.Width : _plan.DesiredSize.Width,
                double.IsFinite(availableSize.Height) ? availableSize.Height : _plan.DesiredSize.Height);
        }
        finally
        {
            _measuring = false;
        }
    }

    /// <summary>The block's shown parts at their own size, and the scale that fills the room with them (<see cref="Raw"/>: no floor).</summary>
    private readonly record struct Block(double Intrinsic, double Packed, LineInk Held, LineInk Ink, double Least, double Raw);

    /// <summary>Measure the shown parts unbounded: what has a width of its own, the parts packed, their ink.</summary>
    private Block Measured(Size room)
    {
        var shown = _plan.Children.OfType<FrameworkElement>().Where(c => c.Visibility == Visibility.Visible).ToList();
        var intrinsic = 0.0;
        var packed = 0.0;
        foreach (var part in shown)
        {
            part.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            packed += part.DesiredSize.Height;

            // Measured unbounded, a bar, a slider or a graph asks for next to no width: only text and icons count.
            intrinsic = Math.Max(intrinsic, part.DesiredSize.Width);
        }

        packed += Math.Max(0, shown.Count - 1) * _plan.Spacing;
        // [S-12813] Sized by the reading's ink (a placeholder keeps its reading's size), placed by the ink drawn.
        var held = shown.Count == 0 ? LineInk.None : new LineInk(PartInk.Held(shown[0]).Over, PartInk.Held(shown[^1]).Under);
        var ink = shown.Count == 0 ? LineInk.None : new LineInk(PartInk.Of(shown[0]).Over, PartInk.Of(shown[^1]).Under);
        var least = _face is { } stated ? ScaleFloors.Of(shown, stated.Main) : ScaleFloors.Of(shown);
        var raw = BlockFit.Fill(room.Width, room.Height, intrinsic, packed, held, ink, least: NoFloor).Scale;
        return new Block(intrinsic, packed, held, ink, least, raw);
    }

    /// <summary>A least scale so small it never holds the block: the scale the room alone gives it.</summary>
    private const double NoFloor = 1e-6;

    /// <summary>
    /// [S-14243] The face's floor in <paramref name="room"/>, decided on the parts as they are drawn (owner, 2026-11-19, WR-7:
    /// "Keep them together"; CLAUDE.md: size never decides content): its pieces give way smallest first only where the
    /// scale the drawn block fills the room at would draw them under the legibility floor (<see cref="FaceFloor"/>'s rule
    /// on the measured block). The stated plan is sized on the face's widest reading, so a countdown's "3d 5h" in a 300 ×
    /// 164 card had dropped its name and its date (its "88 days ago" would have drawn them at 8.9) while the time itself
    /// was drawn more than twice that large.
    /// </summary>
    private void Decide(Size room, FaceParts face)
    {
        _decided = room;
        _floor = FaceFloor.Fit(room.Width, room.Height, face.Sizes, floor =>
        {
            Keep(floor);
            var block = Measured(room);
            return new FacePlan(block.Intrinsic + BlockFit.WidthSlack, Math.Max(0, block.Packed - Room(block.Held.Over) - Room(block.Held.Under)));
        }).Floor;

        static double Room(double v) => double.IsFinite(v) ? Math.Max(0, v) : 0;
    }

    /// <summary>The smallest text size the face draws at the decided floor (its main reading's where only that is left).</summary>
    private double Kept(FaceParts face)
    {
        var kept = face.Sizes.Where(s => s > 0 && double.IsFinite(s) && s >= _floor - 1e-9).DefaultIfEmpty(face.Main).Min();
        return kept >= face.Main ? double.PositiveInfinity : kept;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _plan.Arrange(new Rect(0, _fill.Top, finalSize.Width / _fill.Scale, _fill.Height));
        return finalSize;
    }
}
