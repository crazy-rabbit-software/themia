using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI.Kit;
using Themia.App.Widgets;
using Themia.App.Services;
using Themia.Core.Widgets;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace Themia.App.Shell;

/// <summary>
/// [S-15526] [S-13912] [S-13917] A container's layout editing on this display (owner, 2026-11-21: "when i select the widget it
/// should just select it and you should be able to change its size but not the contents. then you should be able to click
/// somewhere when you have the widget selected and get into a widget layout editing mode, this is on the desktop, the
/// settings menu is not shown then, and you can add parts of the widget right there and change the content size"). Selected,
/// a container is a widget like any other: a press on any of its parts or days selects it, a resize sets how many days show.
/// A click on it while it is selected (or Edit layout on its page) starts its layout editing: its page closes, its parts take
/// presses of their own (selected, moved, resized on the desktop's grid), its day box is framed with grips (right and bottom:
/// whole cells, never under its parts' reach, the tied side with it), and the parts' bento stands beside it: a tile dragged
/// onto a day adds that part there; a part dragged out of the day is removed. Escape, Done, a click on the empty desktop or
/// selecting anything else ends it, as does leaving edit mode.
/// </summary>
internal sealed partial class DesktopWindow
{
    /// <summary>[S-13912] The day box's resize under way: which container, by which grip, from where, its size then and now.</summary>
    private sealed class DayBoxSession(string id, IRepeater repeater, ResizeHandle handle, Point pressed, (int W, int H) start, (int W, int H) least, (int W, int H) stored)
    {
        public string Id { get; } = id;

        public IRepeater Repeater { get; } = repeater;

        public ResizeHandle Handle { get; } = handle;

        public Point Pressed { get; } = pressed;

        public (int W, int H) Start { get; } = start;

        public (int W, int H) Least { get; } = least;

        /// <summary>What the config held before the drag: put back before the drop is stored, so its undo step holds it.</summary>
        public (int W, int H) Stored { get; } = stored;

        public (int W, int H) Now { get; set; } = start;

        public bool Stepped { get; set; }
    }

    private DayBoxSession? _dayBox;
    private bool _dayHooked;

    /// <summary>[S-13917] The container whose layout is being edited on this display, by id; null for none.</summary>
    private string? _composing;

    /// <summary>[S-13917] The parts' bento, made the first time a layout is edited.</summary>
    private PartPalette? _palette;

    /// <summary>[S-13917] The part dragged from the bento: its kind and its ghost under the pointer.</summary>
    private (WidgetDescriptor Kind, FrameworkElement Ghost, (int W, int H) Cells)? _paletteDrag;

    /// <summary>[S-13917] Whether the press that is ending found its card's container already selected (a click then edits its layout).</summary>
    private bool _pressedSelected;

    /// <summary>[S-13917] Whether <paramref name="containerId"/>'s layout is being edited.</summary>
    private bool IsOpen(ViewState view, string containerId)
        => EditMode && string.Equals(_composing, containerId, StringComparison.Ordinal) && view.Find(containerId)?.Config is IRepeater;

    /// <summary>[S-13917] The container whose layout is being edited, if any.</summary>
    private RepeaterOn? OpenContainer(ViewState view)
        => EditMode && _composing is { } id && view.Find(id) is { Config: IRepeater repeater } widget ? new RepeaterOn(widget, repeater) : null;

    /// <summary>
    /// [S-13917] Start editing <paramref name="containerId"/>'s layout: its page closes, it stays selected, its day box is framed
    /// and the parts' bento stands beside it.
    /// </summary>
    internal void Compose(string containerId)
    {
        var view = ViewForThisMonitor(_host.State);
        if (!EditMode || _closed || view.Find(containerId)?.Config is not IRepeater)
        {
            return;
        }

        _composing = containerId;
        if (BarMenuOpen)
        {
            CloseFlyout(focusEntry: false);
        }

        // [S-13917] Nothing selected: the container is not a widget to move or resize now, its parts are what is picked.
        _host.StepOutOfSelection();
        ShowPalette();
        RefreshRepeaters();
    }

    /// <summary>[S-13917] The layout editing ends: the bento and the day box's frame go; the container is a widget again.</summary>
    internal void EndCompose()
    {
        if (_composing is null)
        {
            return;
        }

        var was = _composing;
        _composing = null;
        EndPaletteDrag(null);
        if (_palette is not null)
        {
            _palette.Visibility = Visibility.Collapsed;
            _palette.Close();
        }

        if (_dim is not null)
        {
            _widgetLayer.Children.Remove(_dim);
            _dim = null;
        }

        ApplyPaintOrder(ViewForThisMonitor(_host.State));
        LayCopies(live: []);
        RefreshRepeaters();

        // The container is a widget again: selected, as it was before its layout was edited.
        if (EditMode && !_closed && ViewForThisMonitor(_host.State).Find(was) is not null)
        {
            _host.Select(was);
        }
    }

    /// <summary>
    /// [S-13917] The selection changed: the layout editing goes on while it names the container or its parts alone; anything
    /// else selected, nothing selected or edit mode left ends it.
    /// </summary>
    private void FollowCompose()
    {
        if (_composing is not { } id)
        {
            return;
        }

        var view = ViewForThisMonitor(_host.State);
        // Nothing selected keeps it (a click on the forecast's empty room or on another day lets go of a part); anything but its
        // own parts selected ends it. The rule is Core's.
        if (!LayoutEditing.GoesOn(view, id, EditMode, _host.Selection.ViewId, _host.Selection.Ids))
        {
            EndCompose();
            return;
        }

        PlacePalette();
    }

    /// <summary>[S-13917] The desktop darkened round the container while its layout is edited; a press on it ends the editing.</summary>
    private Microsoft.UI.Xaml.Shapes.Path? _dim;

    /// <summary>Over every card but the container's own: theirs are lifted over it.</summary>
    private const int DimOrder = 100_000;

    /// <summary>
    /// [S-13917] The rest of the desktop darkened (owner, 2026-11-21: "when in edit mode of the row the rest of the desktop should
    /// be darkened"): Windows' smoke over every card but the container's, its box left clear; the container's cards (it, its
    /// parts, their days) over the smoke, in their order among themselves.
    /// </summary>
    private void Dim(ViewState view, RepeaterOn on)
    {
        var metrics = Metrics;
        if (_dim is null)
        {
            _dim = new Microsoft.UI.Xaml.Shapes.Path { Fill = ShellBrush.Of(ShellRole.Smoke) };
            _dim.PointerPressed += (_, e) =>
            {
                EndCompose();
                e.Handled = true;
            };
            _widgetLayer.Children.Add(_dim);
            Canvas.SetZIndex(_dim, DimOrder);
        }

        var hole = GridGeometry.PaintedCell(BlockGeometry.Bounds([on.Widget.Rect, .. Repeaters.SlotsOf(view, on, metrics, _repeaterPlan.Values)]), metrics, RasterScale);
        _dim.Data = new GeometryGroup
        {
            FillRule = FillRule.EvenOdd,
            Children =
            {
                new RectangleGeometry { Rect = new Rect(0, 0, metrics.Width, metrics.Height) },
                new RectangleGeometry { Rect = new Rect(hole.X, hole.Y, hole.Width, hole.Height) },
            },
        };

        var own = Repeaters.WithDescendants(view, [on.Widget.Id]);
        var lift = DimOrder + 1;
        foreach (var card in own.Select(CardFor).OfType<WidgetCard>().OrderBy(Canvas.GetZIndex).ToList())
        {
            Canvas.SetZIndex(card, lift++);
        }

        foreach (var copy in _copies.Values)
        {
            if (_planCopies.TryGetValue(copy.Model.Id, out var planned) && own.Contains(planned.Template.Id) && CardFor(planned.Template.Id) is { } template)
            {
                Canvas.SetZIndex(copy, Canvas.GetZIndex(template));
            }
        }
    }

    /// <summary>
    /// [S-13922] Every container's days outlined in edit mode, where the cards are (the surface's DIPs on device pixels): a
    /// dashed hairline in the tertiary text role round each day it shows; none outside edit mode.
    /// </summary>
    private void SyncDayLines()
    {
        _dayLines.Children.Clear();
        var metrics = Metrics;
        if (!EditMode || _closed || !metrics.IsUsable)
        {
            return;
        }

        var view = _planView ?? ViewForThisMonitor(_host.State);
        var stroke = Family.Brush(ColourRole.TextTertiary);
        foreach (var on in Repeaters.On(view))
        {
            foreach (var slot in Repeaters.SlotsOf(view, on, metrics, _repeaterPlan.Values))
            {
                var box = GridGeometry.PaintedCell(slot, metrics, RasterScale);
                var line = FamilyShape.Hairline / RasterScale;
                var outline = new Microsoft.UI.Xaml.Shapes.Rectangle
                {
                    Width = box.Width,
                    Height = box.Height,
                    Stroke = stroke,
                    StrokeThickness = line,
                    StrokeDashArray = [FamilyShape.Track, FamilyShape.Track],
                    IsHitTestVisible = false,
                };
                Canvas.SetLeft(outline, box.X);
                Canvas.SetTop(outline, box.Y);
                _dayLines.Children.Add(outline);
            }
        }
    }

    /// <summary>[S-13922] The days' outlines: over the cards, under the edit marks.</summary>
    private Canvas _dayLines => _dayLinesCanvas ??= MakeDayLines();

    private Canvas? _dayLinesCanvas;

    private Canvas MakeDayLines()
    {
        var canvas = new Canvas { IsHitTestVisible = false };
        _markerLayer.Halos.Children.Insert(0, canvas);
        return canvas;
    }

    /// <summary>[S-13917] The bento beside the container, offering what its day takes.</summary>
    private void ShowPalette()
    {
        if (_palette is null)
        {
            _palette = new PartPalette();
            _palette.Done += EndCompose;
            _palette.DragStarted += OnPaletteDragStarted;
            _palette.Dragged += OnPaletteDragged;
            _palette.Dropped += EndPaletteDrag;
            _markerLayer.Lifts.Children.Add(_palette);
            Canvas.SetZIndex(_palette, PaletteOrder);
        }

        var view = ViewForThisMonitor(_host.State);
        if (OpenContainer(view) is not { } on)
        {
            _palette.Visibility = Visibility.Collapsed;
            return;
        }

        _palette.Visibility = Visibility.Visible;
        PlacePalette();
    }

    /// <summary>Over the frames and grips on their plate.</summary>
    private const int PaletteOrder = 3;

    /// <summary>
    /// [S-13917] The shelf's place and what it shows: on the desktop's own cells, two cells from the container's box (to its
    /// right where the display has the room, else its left), from the container's top where it has the room and never under
    /// the edit bar or past the work area; as wide as its lines need to fit that room (<see cref="PartShelf.Fit"/>). Each part
    /// at the size it lands with in this container's day.
    /// </summary>
    private void PlacePalette()
    {
        var metrics = Metrics;
        if (_palette is not { Visibility: Visibility.Visible } palette || !metrics.IsUsable)
        {
            return;
        }

        var view = _planView ?? ViewForThisMonitor(_host.State);
        if (OpenContainer(view) is not { } on)
        {
            return;
        }

        var (cols, rows) = GridGeometry.GridDims(metrics);
        var (pitchX, pitchY) = (metrics.Width / cols, metrics.Height / rows);
        var kinds = AppHost.PartKinds(on.Repeater);
        var day = Repeaters.DaySize(view, on, metrics);
        var sizes = kinds.Select(k => AppHost.PartCells(k.Id, metrics, day)).ToList();

        var area = WorkArea;
        var bar = EditBarRect(area);
        var top = (int)Math.Ceiling((bar.Y + bar.Height) / pitchY) + PartShelf.Gap;
        var foot = (int)Math.Floor((area.Y + area.Height) / pitchY) - PartShelf.Gap;
        var box = BlockGeometry.Bounds([on.Widget.Rect, .. Repeaters.SlotsOf(view, on, metrics, _repeaterPlan.Values)]);
        var after = (int)Math.Ceiling((box.Right * cols) - 1e-6) + (2 * PartShelf.Gap);
        var before = (int)Math.Floor((box.X * cols) + 1e-6) - (2 * PartShelf.Gap);
        var roomAfter = (int)Math.Floor((area.X + area.Width) / pitchX) - PartShelf.Gap - after;
        var roomBefore = before - ((int)Math.Ceiling(area.X / pitchX) + PartShelf.Gap);
        var right = roomAfter >= PartShelf.LeastWidth || roomAfter >= roomBefore;
        var shelf = PartShelf.Fit(sizes, Math.Max(1, foot - top - PartPalette.HeadCells), Math.Max(PartShelf.LeastWidth, right ? roomAfter : roomBefore));
        var height = PartPalette.HeadCells + shelf.Height;
        var row = Math.Max(top, Math.Min((int)Math.Round(box.Y * rows), foot - height));
        var column = right ? after : Math.Max(0, before - shelf.Width);

        palette.Show(kinds, sizes, shelf, column, row, metrics, RasterScale, _host.State.Design, w => ContentScale(w));
        var at = GridGeometry.PaintedCell(new FracRect(column / (double)cols, row / (double)rows, shelf.Width / (double)cols, height / (double)rows), metrics, RasterScale);
        Canvas.SetLeft(palette, at.X);
        Canvas.SetTop(palette, at.Y);
    }

    /// <summary>
    /// [S-13917] A part left the shelf: where it would land follows the pointer, its box at the size it lands with on the
    /// desktop's cells (the card's fill and the accent's rim, its glyph and name in it).
    /// </summary>
    private void OnPaletteDragStarted(WidgetDescriptor kind, PointerRoutedEventArgs e)
    {
        EndPaletteDrag(null);
        var metrics = Metrics;
        var view = ViewForThisMonitor(_host.State);
        if (!metrics.IsUsable || OpenContainer(view) is not { } on)
        {
            return;
        }

        var glyph = Fluent.Icon(kind.Glyph);
        var name = Fluent.Text(FamilyType.Caption);
        name.Text = kind.Label.Text();
        name.TextTrimming = TextTrimming.CharacterEllipsis;
        var ghost = new Border
        {
            Background = ShellBrush.Of(ShellRole.CardFill),
            BorderBrush = WidgetCard.SelectedInk(),
            BorderThickness = new Thickness(FamilyShape.Hairline),
            CornerRadius = new CornerRadius(ShellMetrics.Page.CardRadius),
            Opacity = SelectionLook.DragOpacity,
            IsHitTestVisible = false,
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = ShellMetrics.Bar.IconGap,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Children = { glyph, name },
            },
        };
        _ghostLayer.Children.Add(ghost);
        _paletteDrag = (kind, ghost, AppHost.PartCells(kind.Id, metrics, Repeaters.DaySize(view, on, metrics)));
        OnPaletteDragged(e);
    }

    /// <summary>[S-13917] The landing box under the pointer: its middle there, on whole cells of the desktop's grid.</summary>
    private void OnPaletteDragged(PointerRoutedEventArgs e)
    {
        var metrics = Metrics;
        if (_paletteDrag is not { } drag || !metrics.IsUsable)
        {
            return;
        }

        var (cols, rows) = GridGeometry.GridDims(metrics);
        var at = e.GetCurrentPoint(_root).Position;
        var column = Math.Round((at.X / metrics.Width * cols) - (drag.Cells.W / 2.0));
        var row = Math.Round((at.Y / metrics.Height * rows) - (drag.Cells.H / 2.0));
        var box = GridGeometry.PaintedCell(new FracRect(column / cols, row / rows, drag.Cells.W / (double)cols, drag.Cells.H / (double)rows), metrics, RasterScale);
        drag.Ghost.Width = box.Width;
        drag.Ghost.Height = box.Height;
        Canvas.SetLeft(drag.Ghost, box.X);
        Canvas.SetTop(drag.Ghost, box.Y);
    }

    /// <summary>
    /// [S-13917] The part let go: over one of the container's days it is added there (at the same place in the day box, its
    /// top left where the pointer holds its middle, on whole cells); anywhere else nothing is added. Null: the drag was lost.
    /// </summary>
    private void EndPaletteDrag(PointerRoutedEventArgs? e)
    {
        if (_paletteDrag is not { } drag)
        {
            return;
        }

        _paletteDrag = null;
        _ghostLayer.Children.Remove(drag.Ghost);
        var metrics = Metrics;
        var view = ViewForThisMonitor(_host.State);
        if (e is null || !metrics.IsUsable || OpenContainer(view) is not { } on)
        {
            return;
        }

        var at = e.GetCurrentPoint(_root).Position;
        _host.DropPart(this, on.Widget.Id, drag.Kind.Id, at.X / metrics.Width, at.Y / metrics.Height);
    }

    /// <summary>
    /// [S-13912] The day box's frame where the open container's first day stands now (as a move under way would leave it, or
    /// as a resize of it draws it); hidden with no container open.
    /// </summary>
    private void SyncDayBox()
    {
        var frame = _markerLayer.DayFrame;
        if (!_dayHooked)
        {
            _dayHooked = true;
            frame.Pressed += OnDayBoxPressed;
            frame.Dragged += OnDayBoxDragged;
            frame.Released += EndDayBox;
        }

        SyncDayLines();
        var view = _planView ?? ViewForThisMonitor(_host.State);
        if (_closed || !Metrics.IsUsable || OpenContainer(view) is not { } on)
        {
            frame.Hide();
            return;
        }

        var slot = Repeaters.Grid(view, on, Metrics).Slot(0);
        frame.Show(GridGeometry.PaintedCell(slot, Metrics, RasterScale), RasterScale, freeWidth: true, freeHeight: true, cornersOnly: false);
        PlacePalette();
        Dim(view, on);
    }

    /// <summary>
    /// [S-13912] A grip of the day box pressed: its right, bottom or bottom-right edge starts a resize (its corner is the
    /// container's own, so the left and top grips hold it where it is).
    /// </summary>
    private void OnDayBoxPressed(ResizeHandle handle, PointerRoutedEventArgs e)
    {
        var view = ViewForThisMonitor(_host.State);
        var metrics = Metrics;
        if (handle is not (ResizeHandle.Right or ResizeHandle.Bottom or ResizeHandle.BottomRight)
            || _move is not null || _blockResize is not null || !metrics.IsUsable || OpenContainer(view) is not { } on)
        {
            _markerLayer.DayFrame.ReleaseHeld();
            return;
        }

        var (cols, rows) = GridGeometry.GridDims(metrics);
        var grid = Repeaters.Grid(view, on, metrics);
        var parts = Repeaters.Template(view, on);
        var box = on.Widget.Rect;
        _ = box;
        _ = grid;
        var least = (Math.Max(1, parts.Count), 1);
        var start = Repeaters.DaySize(view, on, metrics);
        _dayBox = new DayBoxSession(on.Widget.Id, on.Repeater, handle, e.GetCurrentPoint(_root).Position, start, least, (on.Repeater.DayWidth, on.Repeater.DayHeight));

        // [S-2343] Nothing hangs over the forecast while its day box is resized; the keys stay the surface's.
        ClearForDrag();
        _root.Focus(FocusState.Pointer);

    }

    /// <summary>
    /// [S-13912] The pointer moved: the day box takes the whole grid cells the pointer has gone past, never under its parts'
    /// reach, and every other day is laid again at once from it (drawn as if stored; stored only on the drop).
    /// </summary>
    private void OnDayBoxDragged(PointerRoutedEventArgs e)
    {
        var metrics = Metrics;
        if (_dayBox is not { } session || !metrics.IsUsable)
        {
            return;
        }

        var (cols, rows) = GridGeometry.GridDims(metrics);
        var now = e.GetCurrentPoint(_root).Position;
        var wide = session.Handle is ResizeHandle.Right or ResizeHandle.BottomRight;
        var tall = session.Handle is ResizeHandle.Bottom or ResizeHandle.BottomRight;
        var w = wide ? Math.Max(session.Least.W, session.Start.W + (int)Math.Round((now.X - session.Pressed.X) / (metrics.Width / cols))) : session.Start.W;
        var h = tall ? Math.Max(session.Least.H, session.Start.H + (int)Math.Round((now.Y - session.Pressed.Y) / (metrics.Height / rows))) : session.Start.H;
        if ((w, h) == session.Now)
        {
            return;
        }

        session.Now = (w, h);
        session.Stepped = true;

        // [S-13916] Drawn on a scratch copy of the view (Repeaters.Preview: new instances, the container's config a clone): the
        // parts scaled with the day box and the tied side with it; the stored view waits for the drop.
        var view = ViewForThisMonitor(_host.State);
        var before = _repeaterPlan.Templates;
        var (scratch, _) = Repeaters.Preview(view, new Dictionary<string, FracRect>(StringComparer.Ordinal), metrics, _repeaterPlan.Values);
        if (scratch.Find(session.Id) is { Config: IRepeater scratchRepeater } scratchWidget)
        {
            Repeaters.Resize(scratch, new RepeaterOn(scratchWidget, scratchRepeater), w, h, metrics);
        }

        _planView = scratch;
        _repeaterPlan = Repeaters.Plan(scratch, metrics, _repeaterPlan.Values);
        _planCopies = _repeaterPlan.Copies.ToDictionary(c => c.Key, StringComparer.Ordinal);
        foreach (var id in Repeaters.WithDescendants(scratch, [session.Id]))
        {
            if (CardFor(id) is { } card && scratch.Find(id) is { } drawn)
            {
                PositionCard(card, drawn.Rect);
            }
        }

        LayCopies(live: []);
        Reshown(before);
        JoinCards();
    }

    /// <summary>
    /// [S-13916] A step of <paramref name="card"/>'s resize, a container's: drawn as if let go there (its tied side scales its day
    /// box and its parts, owner: "when you resize the width the widgets inside get resized accordingly"), every day laid again
    /// from it. Nothing is stored until the resize lands; any other card's resize draws nothing more.
    /// </summary>
    private void PreviewContainerResize(WidgetCard card)
    {
        var metrics = Metrics;
        if (card.Model.Config is not IRepeater || !metrics.IsUsable || _closed)
        {
            return;
        }

        var view = ViewForThisMonitor(_host.State);
        var before = _repeaterPlan.Templates;
        var (scratch, plan) = Repeaters.Preview(view, new Dictionary<string, FracRect>(StringComparer.Ordinal) { [card.Model.Id] = card.LiveRect }, metrics, _repeaterPlan.Values);
        _planView = scratch;
        _repeaterPlan = plan;
        _planCopies = plan.Copies.ToDictionary(c => c.Key, StringComparer.Ordinal);
        foreach (var id in Repeaters.Descendants(scratch, card.Model.Id))
        {
            if (CardFor(id) is { } part && scratch.Find(id) is { } drawn)
            {
                PositionCard(part, drawn.Rect);
            }
        }

        LayCopies(live: [card]);
        Reshown(before);
    }

    /// <summary>[S-13912] The day box let go: its size is stored in one undo step and every day laid from it; a press with no step stores nothing.</summary>
    private void EndDayBox()
    {
        if (_dayBox is not { } session)
        {
            return;
        }

        _dayBox = null;
        EndRepeaterPreview();
        if (session.Stepped)
        {
            _host.SetDayBox(this, session.Id, session.Now.W, session.Now.H);
        }
    }
}
