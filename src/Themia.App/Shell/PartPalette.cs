using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Themia.App.Widgets;
using Themia.Core.Geometry;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Windows.Foundation;

namespace Themia.App.Shell;

/// <summary>
/// [S-13917] The parts' shelf beside a container in its layout editing (owner, 2026-11-21: "the atoms that can be added to the
/// forecast widget should be shown in a bento to the side in the row edit mode and should be addable by just dragging them
/// into it"; 2026-11-22: "the menu that comes up with the items that you can place onto the row should be on the background
/// grid already. the items should appear aligned to the background grid, somewhat like the not on home page does it"). It
/// stands on the desktop's own grid lines: its head (what to do, and Done) a few whole cells tall, and under it the desktop's
/// grid drawn in the edit mode's look with each part the container's day takes as a real card on the sample day's data
/// (<see cref="SampleWorld"/>, <see cref="SampleCards"/>), at the size it lands with, on the cells <see cref="PartShelf"/>
/// gives it, its name in the cells under it. A part pressed and dragged is on its way: the surface draws where it lands and,
/// let go over a day, adds it there. The shelf says what happens; the surface decides.
/// </summary>
internal sealed partial class PartPalette : Grid
{
    private const string Scope = "part-shelf";

    /// <summary>The head's height in grid cells: its heading and Done.</summary>
    public const int HeadCells = 5;

    private readonly Border _ground = new();
    private readonly SnapGridOverlay _grid = new() { IsHitTestVisible = false };
    private readonly Canvas _shelf = new();
    private readonly Grid _head = new();
    private readonly List<WidgetCard> _cards = [];
    private Services.Ticker? _ticker;
    private SampleWorld? _samples;
    private Host? _host;
    private ScreenMetrics _metrics = new(0, 0);
    private string _built = string.Empty;
    private (WidgetDescriptor Kind, Point Pressed, UIElement Plate)? _held;
    private bool _dragging;
    private bool _closed = true;

    public PartPalette()
    {
        _ground.Background = ShellBrush.Of(ShellRole.Flyout);
        _ground.BorderBrush = ShellBrush.Of(ShellRole.FlyoutStroke);
        _ground.BorderThickness = new Thickness(FamilyShape.Hairline);
        _ground.CornerRadius = new CornerRadius(ShellMetrics.OverlayRadius);
        Children.Add(_ground);

        var heading = Fluent.Text(FamilyType.BodyStrong);
        heading.Text = Str.Series_Parts.Text();
        heading.VerticalAlignment = VerticalAlignment.Center;
        heading.TextTrimming = TextTrimming.CharacterEllipsis;
        var done = new Button { Content = Str.Common_Done.Text(), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
        done.Click += (_, _) => Done?.Invoke();
        _head.Padding = new Thickness(ShellMetrics.Flyout.Gutter, 0, ShellMetrics.Flyout.Gutter, 0);
        _head.ColumnSpacing = ShellMetrics.Bar.IconGap;
        _head.VerticalAlignment = VerticalAlignment.Top;
        _head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _head.Children.Add(heading);
        SetColumn(done, 1);
        _head.Children.Add(done);
        Children.Add(_head);

        _shelf.VerticalAlignment = VerticalAlignment.Top;
        _shelf.HorizontalAlignment = HorizontalAlignment.Left;
        Children.Add(_shelf);

        // A press on the shelf is the shelf's: never the surface's (a rubber band, a deselect).
        PointerPressed += (_, e) => e.Handled = true;
    }

    /// <summary>Done pressed: the layout editing ends.</summary>
    public event Action? Done;

    /// <summary>A part left the shelf under the pointer: its kind, and the pointer where it is now.</summary>
    public event Action<WidgetDescriptor, PointerRoutedEventArgs>? DragStarted;

    /// <summary>The dragged part's pointer moved.</summary>
    public event Action<PointerRoutedEventArgs>? Dragged;

    /// <summary>The dragged part let go (null: the drag was lost, nothing lands).</summary>
    public event Action<PointerRoutedEventArgs?>? Dropped;

    /// <summary>
    /// The shelf drawn for <paramref name="kinds"/> at <paramref name="sizes"/> (cells), laid as <paramref name="shelf"/> says,
    /// its corner on the desktop's cell (<paramref name="column"/>, <paramref name="row"/>) of a display of
    /// <paramref name="metrics"/>: every edge where the desktop's own grid has it. The cards are built once for what is offered
    /// and only put in their places after.
    /// </summary>
    public void Show(IReadOnlyList<WidgetDescriptor> kinds, IReadOnlyList<(int W, int H)> sizes, ShelfLayout shelf, int column, int row,
        ScreenMetrics metrics, double raster, DesignState design, Func<WidgetInstance, double> scale)
    {
        ArgumentNullException.ThrowIfNull(kinds);
        ArgumentNullException.ThrowIfNull(sizes);
        ArgumentNullException.ThrowIfNull(shelf);
        ArgumentNullException.ThrowIfNull(scale);
        var (cols, rows) = GridGeometry.GridDims(metrics);
        DipRect Cells(int x, int y, int w, int h) => GridGeometry.PaintedCell(new FracRect(x / (double)cols, y / (double)rows, w / (double)cols, h / (double)rows), metrics, raster);

        var whole = Cells(column, row, shelf.Width, HeadCells + shelf.Height);
        var head = Cells(column, row, shelf.Width, HeadCells);
        var area = Cells(column, row + HeadCells, shelf.Width, shelf.Height);
        Width = whole.Width;
        Height = whole.Height;
        _head.Height = head.Height;
        _shelf.Margin = new Thickness(0, area.Y - whole.Y, 0, 0);
        _shelf.Width = area.Width;
        _shelf.Height = area.Height;

        var key = string.Join("|", kinds.Select((k, i) => $"{k.Id.Value}:{sizes[i].W}x{sizes[i].H}")) + $"@{metrics.Width}x{metrics.Height}";
        if (_closed || key != _built)
        {
            Build(kinds, sizes, metrics, design, scale);
            _built = key;
        }

        _metrics = metrics;
        _grid.Draw(area.Width, area.Height, (metrics.Width / cols, metrics.Height / rows), raster);
        for (var i = 0; i < _cards.Count && i < shelf.Boxes.Count; i++)
        {
            var box = shelf.Boxes[i];
            var at = Cells(column + box.X, row + HeadCells + box.Y, box.W, box.H);
            var card = _cards[i];
            card.Width = at.Width;
            card.Height = at.Height;
            Canvas.SetLeft(card, at.X - area.X);
            Canvas.SetTop(card, at.Y - area.Y);

            // Its name in the cells under it, and the plate that takes its press over both.
            var caption = Cells(column + box.X, row + HeadCells + box.Y + box.H, box.W, PartShelf.Caption);
            if (card.Tag is (TextBlock name, Grid plate))
            {
                name.Width = Math.Max(at.Width, caption.Width);
                name.Height = caption.Height;
                Canvas.SetLeft(name, caption.X - area.X);
                Canvas.SetTop(name, caption.Y - area.Y);
                plate.Width = at.Width;
                plate.Height = at.Height + caption.Height;
                Canvas.SetLeft(plate, at.X - area.X);
                Canvas.SetTop(plate, at.Y - area.Y);
            }
        }

        WidgetCard.Join(_cards, metrics, raster, editMode: false, _ => 0);
    }

    /// <summary>The cards of what is offered, each a new widget of its kind at its size, shown on its sample.</summary>
    private void Build(IReadOnlyList<WidgetDescriptor> kinds, IReadOnlyList<(int W, int H)> sizes, ScreenMetrics metrics, DesignState design, Func<WidgetInstance, double> scale)
    {
        Close();
        _closed = false;
        var dispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread() ?? throw new InvalidOperationException("the shelf is built on the UI thread");
        _ticker = new Services.Ticker(dispatcher);
        _samples = new SampleWorld(_ticker, dispatcher);
        _metrics = metrics;
        _host = new Host(_ticker, this) { Design = design, Samples = _samples };
        var (cols, rows) = GridGeometry.GridDims(metrics);
        _shelf.Children.Add(_grid);
        for (var i = 0; i < kinds.Count; i++)
        {
            var kind = kinds[i];
            try
            {
                var model = WidgetInstance.Create(kind.Id, new FracRect(0, 0, sizes[i].W / (double)cols, sizes[i].H / (double)rows));
                var card = new WidgetCard(_host, model) { IsHitTestVisible = false };
                card.SetLiveRect(model.Rect);
                card.ApplyEditMode(false);
                card.ApplyContentScale(scale(model));

                var name = Fluent.Text(FamilyType.Caption, "TextFillColorSecondaryBrush");
                name.Text = kind.Label.Text();
                name.TextTrimming = TextTrimming.CharacterEllipsis;
                name.TextWrapping = TextWrapping.NoWrap;
                name.IsHitTestVisible = false;
                var plate = Plate(kind);
                card.Tag = (name, plate);
                _shelf.Children.Add(card);
                _shelf.Children.Add(name);
                _shelf.Children.Add(plate);
                _cards.Add(card);
            }
            catch (Exception ex)
            {
                Themia.Core.Diagnostics.Log.Warn(Scope, $"the shelf's {kind.Id.Value} card could not be built; the shelf shows the rest", ex);
            }
        }

        var cards = _cards.ToList();
        _ = SampleCards.ShowAsync(cards, () => _closed || !_cards.SequenceEqual(cards), Scope);
    }

    /// <summary>The shelf goes away: its cards are let go and their sample world with them.</summary>
    public void Close()
    {
        _closed = true;
        _held = null;
        _dragging = false;
        foreach (var card in _cards)
        {
            card.Detach();
        }

        _cards.Clear();
        _shelf.Children.Clear();
        _samples?.Dispose();
        _ticker?.Dispose();
        (_samples, _ticker, _host) = (null, null, null);
        _built = string.Empty;
    }

    /// <summary>The plate over a part and its name: its press and drag are the part's on its way.</summary>
    private Grid Plate(WidgetDescriptor kind)
    {
        var plate = new ShelfPlate { Background = Fluent.Clear() };
        ToolTipService.SetToolTip(plate, kind.Description.Text());
        plate.PointerPressed += (_, e) =>
        {
            if (plate.CapturePointer(e.Pointer))
            {
                _held = (kind, e.GetCurrentPoint(null).Position, plate);
                _dragging = false;
                e.Handled = true;
            }
        };
        plate.PointerMoved += (_, e) =>
        {
            if (_held is not { } held || !ReferenceEquals(held.Plate, plate))
            {
                return;
            }

            var at = e.GetCurrentPoint(null).Position;
            if (!_dragging && (Math.Abs(at.X - held.Pressed.X) > DragStart || Math.Abs(at.Y - held.Pressed.Y) > DragStart))
            {
                _dragging = true;
                DragStarted?.Invoke(held.Kind, e);
            }

            if (_dragging)
            {
                Dragged?.Invoke(e);
            }

            e.Handled = true;
        };
        plate.PointerReleased += (_, e) => Let(plate, e);
        plate.PointerCaptureLost += (_, _) =>
        {
            if (_held is { } held && ReferenceEquals(held.Plate, plate))
            {
                var dragging = _dragging;
                (_held, _dragging) = (null, false);
                if (dragging)
                {
                    Dropped?.Invoke(null);
                }
            }
        };
        return plate;
    }

    /// <summary>The pointer let go of <paramref name="plate"/>: a drag lands where it is; a press with no drag adds nothing.</summary>
    private void Let(UIElement plate, PointerRoutedEventArgs e)
    {
        if (_held is not { } held || !ReferenceEquals(held.Plate, plate))
        {
            return;
        }

        var dragging = _dragging;
        (_held, _dragging) = (null, false);
        plate.ReleasePointerCaptures();
        if (dragging)
        {
            Dropped?.Invoke(e);
        }

        e.Handled = true;
    }

    /// <summary>How far the pointer goes before a press on a part is a drag: Windows' drag threshold.</summary>
    private const double DragStart = 4;

    /// <summary>A part's plate: the move cursor over it, saying it is dragged rather than clicked.</summary>
    private sealed partial class ShelfPlate : Grid
    {
        public ShelfPlate() => ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeAll);
    }

    /// <summary>The cards' host: a preview's, on the display the shelf stands on (its grid is the cards' grid).</summary>
    private sealed class Host(Services.Ticker ticker, PartPalette shelf) : PreviewCardHost(ticker)
    {
        protected override ScreenMetrics? Screen => shelf._metrics.IsUsable ? shelf._metrics : null;
    }
}
