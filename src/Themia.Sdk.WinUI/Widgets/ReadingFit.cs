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
    private readonly SpreadStack _plan = new() { Justify = true };
    private readonly ScaleTransform _scale = new();
    private BlockFill _fill = new(1, 0, 0);
    private FaceFit _faceFit = FaceFit.All;
    private FaceParts? _face;
    private readonly List<(UIElement Part, Border Holder, double Size)> _givesWay = [];
    private readonly List<(UIElement Piece, double Size)> _pieces = [];

    public ReadingFit()
    {
        Margin = new Thickness(FamilySpace.Inset, 0, FamilySpace.Inset, FamilySpace.Inset);
        _plan.RenderTransform = _scale;
        Children.Add(_plan);
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
    /// [S-12833] The face's parts as its kind states them (the same its sizing's least box is made of): in a room smaller
    /// than its natural box the pieces under the floor the room draws give way, smallest first (<see cref="FaceParts.Fit"/>):
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
        // [S-12833] What the room draws of the face: the same in every pass for one room, so hiding a piece settles.
        if (!_hold || _held != availableSize)
        {
            _faceFit = _face is { } face && double.IsFinite(availableSize.Width) && double.IsFinite(availableSize.Height)
                ? face.Fit(availableSize.Width, availableSize.Height)
                : FaceFit.All;
        }

        Keep(_faceFit.Floor);

        // The parts at their own size: what has a width of its own, and the parts packed.
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
        if (!_hold || _held != availableSize)
        {
            // [S-12833] Down to its smallest text's floor, up to what fills the room.
            _fill = BlockFit.Fill(availableSize.Width, availableSize.Height, intrinsic, packed, held, ink, _face is { } stated ? ScaleFloors.Of(shown, stated.Main) : ScaleFloors.Of(shown));
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

    protected override Size ArrangeOverride(Size finalSize)
    {
        _plan.Arrange(new Rect(0, _fill.Top, finalSize.Width / _fill.Scale, _fill.Height));
        return finalSize;
    }
}
