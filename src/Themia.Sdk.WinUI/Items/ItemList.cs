using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Themia.App.Widgets;
using Themia.Core.Geometry;
using Themia.Core.Widgets;
using Themia.Sdk.Items;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI.Kit;
using Windows.Foundation;

namespace Themia.Sdk.WinUI.Items;

/// <summary>
/// [S-16940] [S-16954] [S-16955] [S-16635] The item base's one list (owner, 2026-11-23: "same component as the bluetooth one as
/// well. also with spacing capability"): its items (<see cref="ItemView"/>) in its arrangement, each its one fixed size laid
/// by <see cref="ItemLayout.Lay"/> from the card's edges (the last the card reaches cut by its edge, WR-4), touching or with
/// the block gap between them; a table's column titles over its rows. It stands on the card's top edge
/// (<see cref="ListEdge"/>) and runs to its foot and both sides (WR-1, WR-3). The items past the card are scrolled to by whole
/// items (<see cref="ItemScroll"/>: the wheel and the touchpad a line a notch, the keys), the rows fading at the foot while
/// more are below. An item lights under the pointer and pressed (the item layer's row fill, WR-13; only its fill changes,
/// WR-12, and only its own box, never the gap), opens on a click, shows its menu on a right click, and takes the keys
/// (the arrows walk, Page Up/Down a page, Home/End, Enter opens, the Menu key its menu, Delete its own step, Escape lets
/// go). With the gap on it names its items' boxes as the card's grounds (<see cref="Grounds"/>).
/// </summary>
public sealed partial class ItemList : Panel, IStatusRoom
{
    private readonly IWidgetHost _host;
    private readonly List<ItemView> _views = [];
    private readonly Grid _titles = new() { Visibility = Visibility.Collapsed };
    private IReadOnlyList<Item> _items = [];
    private ItemDraw _draw = new([], ItemShape.Device);
    private IReadOnlyList<ItemPart> _parts = [];
    private ItemLay _lay = new(ItemStyle.List, false, 1, 0, 0, 0, 0, 0, []);
    private Size _arranged;
    private WidgetArea _area;
    private ItemStyle _style;
    private ItemCells _cells = ItemCells.Base;
    private int _gap;
    private int _first;
    private int _carry;
    private int _cursor = -1;
    private ItemView? _pressed;
    private double _statusRoom;
    private ItemView? _roomed;

    public ItemList(IWidgetHost host)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        ListEdge.Apply(this);
        Background = Family.Brush(ColourRole.Clear);
        ItemMark.Mark(_titles);
        Children.Add(_titles);
        AddHandler(PointerWheelChangedEvent, new PointerEventHandler(OnWheel), true);
    }

    /// <summary>What a click, a double-click or Enter on an item does (null: nothing); it is not asked in edit mode.</summary>
    public Action<Item>? Opened { get; set; }

    /// <summary>[S-16948] The end's button of an item was pressed (Join); not in edit mode.</summary>
    public Action<Item>? Acted { get; set; }

    /// <summary>Whether an item lights under the pointer and pressed (off where its card lights as the one item: a single item's face).</summary>
    public bool LightsItems { get; set; } = true;

    /// <summary>Whether an item opens now (false in edit mode, in a preview).</summary>
    public Func<bool>? CanOpen { get; set; }

    /// <summary>An item's menu, read on every right click.</summary>
    public Func<Item, IReadOnlyList<ItemMenuEntry>>? Menu { get; set; }

    /// <summary>Delete on the item under the keyboard cursor (a drive's Eject, a dismissal); null: the key is not this list's.</summary>
    public Action<Item>? Deleted { get; set; }

    /// <summary>A picture only the widget draws for a lead with a key (an app's icon, a contact's photo).</summary>
    public Func<ItemLead, double, FrameworkElement?>? Lead { get; set; }

    /// <summary>[S-16952] A part's words over its table column; null: no titles.</summary>
    public Func<ItemPart, string>? PartTitle { get; set; }

    /// <summary>[S-11950] A drawn item's place on the view's links (its key lit and published); called again on every draw.</summary>
    public Action<Item, FrameworkElement>? Decorate { get; set; }

    /// <summary>[S-11950] The items are drawn again: what was decorated is forgotten first.</summary>
    public Action? Redrawing { get; set; }

    /// <summary>[S-3307] The keyboard cursor moved onto an item (null: it let go).</summary>
    public Action<Item?>? CursorMoved { get; set; }

    /// <summary>[S-16640] The items' grounds changed (they came, went or moved): the card paints them again.</summary>
    public event Action? GroundsChanged;

    /// <summary>The items it shows, in their order.</summary>
    public IReadOnlyList<Item> Items => _items;

    /// <summary>The layout as last arranged.</summary>
    public ItemLay Lay => _lay;

    /// <summary>[S-16955] Whether there is anything to walk or scroll to: a click takes the keyboard then.</summary>
    public bool WantsKeys => _items.Count > 0;

    /// <summary>The arrangement: its items are made again when it changes.</summary>
    public ItemStyle Arrangement
    {
        get => _style;
        set
        {
            if (value == _style)
            {
                return;
            }

            _style = value;
            _first = 0;
            Unmake();
            InvalidateMeasure();
            GroundsChanged?.Invoke();
        }
    }

    /// <summary>Take every item view down: they are made again in the list's arrangement and bar place.</summary>
    private void Unmake()
    {
        foreach (var view in _views)
        {
            Unhook(view);
            Children.Remove(view);
        }

        _views.Clear();
    }

    /// <summary>[S-16953] The kind's own measures (a row list's column, a grid's card); the base's by default.</summary>
    public ItemCells Cells
    {
        get => _cells;
        set
        {
            _cells = value ?? ItemCells.Base;
            InvalidateMeasure();
        }
    }

    /// <summary>[S-16634] The grid blocks between two items (0..4: touching to four cells), each then a card of its own.</summary>
    public int Gap
    {
        get => _gap;
        set
        {
            if (value != _gap)
            {
                _gap = value;
                InvalidateMeasure();
                GroundsChanged?.Invoke();
            }
        }
    }

    /// <summary>[S-16952] A table's column titles over its rows.</summary>
    public bool Titles { get; set; }

    /// <summary>
    /// [S-16640] The boxes the card paints its ground under while the gap is on, in this list's DIPs: each item the card
    /// shows, cut by the card's edge; null with the gap off or in a table (the card is one ground).
    /// </summary>
    public IReadOnlyList<DipRect>? Grounds
    {
        get
        {
            if (_gap <= 0 || !ItemLayout.TakesGap(_style))
            {
                return null;
            }

            // The box as last arranged (read while the arrange tells of it, before ActualHeight has caught up).
            var (width, height) = (_arranged.Width, _arranged.Height);
            var grounds = new List<DipRect>(_lay.Boxes.Count);
            foreach (var box in _lay.Boxes.Where(b => b.Index < _items.Count))
            {
                var right = Math.Min(box.Right, width);
                var bottom = Math.Min(box.Bottom, height);
                if (right > box.X && bottom > box.Y)
                {
                    grounds.Add(new DipRect(box.X, box.Y, right - box.X, bottom - box.Y));
                }
            }

            return grounds;
        }
    }

    /// <summary>
    /// [S-16635] Show <paramref name="items"/> in their order: the item views standing are shown again in place (a drive's
    /// picture is not asked again), the rest made or taken down. <paramref name="parts"/>: the parts shown in their order, a
    /// table's columns.
    /// </summary>
    public void Show(IReadOnlyList<Item> items, ItemDraw draw, IReadOnlyList<ItemPart>? parts = null)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(draw);
        var was = _items.Count;
        _items = items;
        _draw = draw;
        _parts = parts ?? [ItemPart.Lead, ItemPart.Title, ItemPart.Subtitle, ItemPart.Note, ItemPart.Bar, ItemPart.End];

        // [S-17180] An item's bar place is how it is built: made again where it moves.
        if (_views.Count > 0 && _views[0].BarAt != draw.Shape.BarAt)
        {
            Unmake();
        }

        while (_views.Count > items.Count)
        {
            var gone = _views[^1];
            _views.RemoveAt(_views.Count - 1);
            Unhook(gone);
            Children.Remove(gone);
        }

        while (_views.Count < items.Count)
        {
            var view = new ItemView(_host, _style, draw.Shape.BarAt);
            view.Lead.Own = Lead;
            Hook(view);
            _views.Add(view);
            Children.Add(view);
        }

        Redrawing?.Invoke();
        Draw();

        if (_cursor >= items.Count)
        {
            MoveCursor(items.Count - 1);
        }

        InvalidateMeasure();
        if (_gap > 0 && was != items.Count)
        {
            GroundsChanged?.Invoke();
        }
    }

    /// <summary>
    /// [S-16951] Show <paramref name="items"/> as <paramref name="config"/> arranges them: its style, gap, column titles and
    /// parts, for a kind whose items have <paramref name="offered"/>'s parts (an action at their end).
    /// </summary>
    public void Show(IReadOnlyList<Item> items, IItemList config, IReadOnlyList<ItemPart> offered, bool stock = false, bool action = false)
    {
        ArgumentNullException.ThrowIfNull(config);
        Show(items, config, offered, ItemShape.Of(offered, config.HiddenParts, action: action), stock);
    }

    /// <summary>
    /// [S-16951] [S-17180] Show <paramref name="items"/> as <paramref name="config"/> arranges them, drawn as
    /// <paramref name="shape"/> says (the devices': a reading at the end, the bar standing beside it, even columns).
    /// </summary>
    public void Show(IReadOnlyList<Item> items, IItemList config, IReadOnlyList<ItemPart> offered, ItemShape shape, bool stock = false)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(offered);
        Arrangement = config.Arrangement;
        Gap = config.Gap;
        Titles = config.ColumnTitles;
        PartTitle ??= ItemMenuLines.PartName;
        Show(items, new ItemDraw(config.HiddenParts, shape, stock), ItemOrder.Shown(offered, config));
    }

    /// <summary>[S-16640] <see cref="Grounds"/> in the view's DIPs: the list stands its margin over the view's top.</summary>
    public IReadOnlyList<DipRect>? ViewGrounds => Grounds?.Select(r => new DipRect(r.X + Margin.Left, r.Y + Margin.Top, r.Width, r.Height)).ToList();

    /// <summary>
    /// Every item shown again: a table's columns from its texts as they are now; [S-17181] where the columns are even, every
    /// List or Column list item's end held to the widest of them.
    /// </summary>
    private void Draw()
    {
        var draw = _style == ItemStyle.Table ? _draw with { Columns = TableColumns(out var inner), Inner = inner } : _draw with { EndWidth = 0 };
        _draw = draw;
        for (var i = 0; i < _items.Count; i++)
        {
            _views[i].Show(_items[i], draw);
            Paint(_views[i]);
            Decorate?.Invoke(_items[i], _views[i]);
        }

        if (draw.Shape.Even && _style is ItemStyle.List or ItemStyle.ColumnList && _items.Count > 1)
        {
            var end = ItemLayout.EvenEnd(_views.Take(_items.Count).Select(v => v.EndWidth()));
            _draw = draw with { EndWidth = end };
            foreach (var view in _views)
            {
                view.HoldEnd(end);
            }
        }

        ShowTitles(draw);
    }

    /// <summary>The card's box: the items are laid in it from its top edge (the list stands the inset over the view).</summary>
    public void Size(WidgetArea area)
    {
        var table = _style == ItemStyle.Table && !area.Width.Equals(_area.Width);
        _area = area;
        if (table)
        {
            Draw();
        }

        InvalidateMeasure();
    }

    // ---- a table's columns -----------------------------------------------------------------------

    /// <summary>[S-16956] The table's columns over the row's width: each part shown at its widest text on any row.</summary>
    private IReadOnlyList<ItemColumn> TableColumns(out double inner)
    {
        inner = Math.Max(0, (_area.IsMeasured ? _area.Width : ItemLayout.NaturalWidth) - (2 * FamilySpace.Inset));
        var shown = _parts.Where(p => !_draw.Hidden.Contains(p) && _items.Any(i => i.Has(p))).ToList();
        var measure = Family.Text(FamilyType.Body, ColourRole.TextPrimary);
        Family.Figures(measure);
        double Width(string text, TypeStep step)
        {
            if (text.Length == 0)
            {
                return 0;
            }

            Family.Step(measure, step);
            measure.Text = text;
            measure.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            return measure.DesiredSize.Width;
        }

        double Widest(ItemPart part) => part switch
        {
            ItemPart.Title => _items.Select(i => Width(i.Title, i.Strong ? FamilyType.BodyStrong : FamilyType.Body)).DefaultIfEmpty(0).Max(),
            ItemPart.Subtitle => _items.Select(i => Width(i.Subtitle, FamilyType.Body)).DefaultIfEmpty(0).Max(),
            ItemPart.Note => _items.Select(i => Width(i.Note, FamilyType.Caption)).DefaultIfEmpty(0).Max(),
            ItemPart.End => _items.Select(i => i.End is { } end
                ? end.Kind == ItemEndKind.Action
                    ? Width(end.Value, FamilyType.Body) + (2 * FamilyShape.ButtonPadding)
                    : Width(end.Kind == ItemEndKind.Reading ? end.Value + end.Unit : end.Value, FamilyType.Body) + (end.Second.Length > 0 ? FamilySpace.S2 + Width(end.Second, FamilyType.Body) : 0)
                : 0).DefaultIfEmpty(0).Max(),
            _ => 0,
        };

        // The column titles are part of their column's widest.
        double Titled(ItemPart part) => Math.Max(Widest(part), Titles && PartTitle is { } title && part is not (ItemPart.Lead or ItemPart.Bar) ? Width(title(part), FamilyType.Caption) + FamilySpace.S1 : 0);
        return ItemTable.Columns([.. shown.Select(p => (p, Titled(p)))], inner, even: _draw.Shape.Even);
    }

    /// <summary>[S-16952] A table's column titles: each its part's words over its column, Caption secondary, the band one row tall.</summary>
    private void ShowTitles(ItemDraw draw)
    {
        _titles.Children.Clear();
        var on = _style == ItemStyle.Table && Titles && PartTitle is not null && draw.Columns is { Count: > 0 };
        _titles.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
        if (!on)
        {
            return;
        }

        _titles.Padding = new Thickness(FamilySpace.Inset, 0, FamilySpace.Inset, 0);
        foreach (var column in draw.Columns!)
        {
            if (column.Part is ItemPart.Lead)
            {
                continue;
            }

            var title = Family.Text(FamilyType.Caption, ColourRole.TextSecondary, PartTitle!(column.Part));
            title.TextTrimming = TextTrimming.CharacterEllipsis;
            title.MaxLines = 1;
            title.VerticalAlignment = VerticalAlignment.Center;
            title.Margin = new Thickness(column.X, 0, Math.Max(0, draw.Inner - column.Right), 0);
            title.HorizontalAlignment = column.Part == ItemPart.End ? HorizontalAlignment.Right : HorizontalAlignment.Left;
            title.TextAlignment = column.Part == ItemPart.End ? TextAlignment.Right : TextAlignment.Left;
            _titles.Children.Add(title);
        }
    }

    // ---- laying -------------------------------------------------------------------------------

    private ItemLay LayIn(double width, double height)
        => ItemLayout.Lay(_style, _items.Count, width, height, _gap, _draw.Shape, _cells, Grid(), _first, Titles && _style == ItemStyle.Table && PartTitle is not null);

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : ItemLayout.NaturalWidth;
        var height = double.IsFinite(availableSize.Height) ? availableSize.Height : _area.IsMeasured ? _area.Height + FamilySpace.Inset : 0;

        // The measure's own lay: the arranged one (what the grounds are) is the arrange's alone.
        var lay = LayIn(width, height);
        var sizes = new Size[_views.Count];
        foreach (var box in lay.Boxes.Where(b => b.Index < sizes.Length))
        {
            sizes[box.Index] = new Size(box.Width, box.Height);
        }

        for (var i = 0; i < _views.Count; i++)
        {
            _views[i].Measure(sizes[i]);
        }

        _titles.Measure(new Size(width, lay.Header));
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var before = _lay;
        var resized = finalSize != _arranged;
        _arranged = finalSize;
        _lay = LayIn(finalSize.Width, finalSize.Height);
        _first = _lay.First;
        var shown = new bool[_views.Count];
        var down = !_lay.Across;
        var viewBottom = _lay.Header + _lay.Viewport;
        var more = _lay.First < _lay.LastFirst;
        foreach (var box in _lay.Boxes.Where(b => b.Index < _views.Count))
        {
            var view = _views[box.Index];
            shown[box.Index] = true;
            view.Visibility = Visibility.Visible;
            view.Arrange(new Rect(box.X, box.Y, box.Width, box.Height));

            // [S-1281] The rows near the foot fade while more are below, as the kit's lists do.
            view.Opacity = down ? ListFade.Opacity(box.Y, box.Bottom, viewBottom, more) : 1;
        }

        for (var i = 0; i < _views.Count; i++)
        {
            if (!shown[i])
            {
                // Scrolled past, or past the card's edge: not drawn (the card shows what its box holds, [S-14410]).
                _views[i].Visibility = Visibility.Collapsed;
                _views[i].Arrange(new Rect(0, 0, 0, 0));
            }
        }

        _titles.Arrange(new Rect(0, 0, finalSize.Width, _lay.Header));
        ApplyRoom();
        Clip = new Microsoft.UI.Xaml.Media.RectangleGeometry { Rect = new Rect(0, 0, finalSize.Width, finalSize.Height) };
        if (_gap > 0 && (resized || !SameBoxes(before, _lay)))
        {
            GroundsChanged?.Invoke();
        }

        return finalSize;
    }

    /// <summary>
    /// [S-12813] [S-12834] The status glyph over the card's top end corner (a stale list's warning, the busy ring) stands beside
    /// the top end item's text, never over it: that item (or a table's column titles) ends that much sooner.
    /// </summary>
    public void StatusRoom(double width)
    {
        _statusRoom = double.IsFinite(width) ? Math.Max(0, width) : 0;
        InvalidateArrange();
    }

    /// <summary>The room laid on the item (or the titles) at the card's top end corner, the one before let go.</summary>
    private void ApplyRoom()
    {
        var titled = _titles.Visibility == Visibility.Visible;
        _titles.Margin = new Thickness(0, 0, titled ? _statusRoom : 0, 0);
        var top = titled ? null : _lay.Boxes.Where(b => b.Index < _views.Count && b.Y <= _lay.Header + LayoutMath.Tolerance).OrderByDescending(b => b.Right).Select(b => _views[b.Index]).FirstOrDefault();
        if (_roomed is not null && !ReferenceEquals(_roomed, top))
        {
            _roomed.Room(0);
        }

        _roomed = top;
        top?.Room(_statusRoom);
    }

    private ItemGrid Grid() => _area.Grid(XamlRoot?.RasterizationScale ?? 1);

    private static bool SameBoxes(ItemLay a, ItemLay b) => a.Across == b.Across && a.Boxes.SequenceEqual(b.Boxes);

    // ---- scrolling ------------------------------------------------------------------------------

    /// <summary>[S-16955] A wheel or touchpad turn: a line a notch, the rest carried; a list that shows every item lets it go on.</summary>
    private void OnWheel(object sender, PointerRoutedEventArgs args)
    {
        if (_lay.LastFirst == 0)
        {
            return;
        }

        var properties = args.GetCurrentPoint(this).Properties;

        // A tilt to the right reads as a turn down: the next items, as a row list runs.
        var delta = properties.IsHorizontalMouseWheel ? -properties.MouseWheelDelta : properties.MouseWheelDelta;
        (_first, _carry) = ItemScroll.Wheel(_first, _carry, delta, _lay.Lines, _lay.Step, _lay.Viewport + _lay.Gap);
        InvalidateArrange();
        args.Handled = true;
    }

    /// <summary>[S-16955] Bring the line of <paramref name="index"/> into the card: the least scroll that shows it whole.</summary>
    private void Reveal(int index)
    {
        if (index < 0 || _lay.Step <= 0)
        {
            return;
        }

        var line = index / Math.Max(1, _lay.Columns);
        var page = ItemScroll.Page(_lay.Step, _lay.Viewport + _lay.Gap);
        var first = line < _first ? line : line >= _first + page ? line - page + 1 : _first;
        if (first != _first)
        {
            _first = Math.Clamp(first, 0, _lay.LastFirst);
            _carry = 0;
            InvalidateArrange();
        }
    }

    // ---- keys ------------------------------------------------------------------------------------

    /// <summary>[S-16635] [S-16955] The keys over the items: walk (a grid's lines by Up/Down), a page, the first and the last, open, menu, the list's own Delete, let go.</summary>
    public bool HandleKey(WidgetKey key)
    {
        var count = _items.Count;
        if (count == 0)
        {
            return false;
        }

        var columns = Math.Max(1, _lay.Columns);
        var page = Math.Max(1, ItemScroll.Page(_lay.Step, _lay.Viewport + _lay.Gap)) * columns;
        int? next = key switch
        {
            WidgetKey.Down when columns > 1 => _cursor < 0 ? 0 : _cursor + columns,
            WidgetKey.Up when columns > 1 => _cursor < 0 ? 0 : _cursor - columns,
            WidgetKey.Down or WidgetKey.Right => _cursor < 0 ? 0 : _cursor + 1,
            WidgetKey.Up or WidgetKey.Left => _cursor < 0 ? 0 : _cursor - 1,
            WidgetKey.PageDown => _cursor < 0 ? 0 : _cursor + page,
            WidgetKey.PageUp => _cursor < 0 ? 0 : _cursor - page,
            WidgetKey.Home => 0,
            WidgetKey.End => count - 1,
            _ => null,
        };

        if (next is { } to)
        {
            MoveCursor(Math.Clamp(to, 0, count - 1));
            Reveal(_cursor);
            return true;
        }

        switch (key)
        {
            case WidgetKey.Enter when _cursor >= 0:
                Open(_items[_cursor]);
                return true;
            case WidgetKey.Menu when _cursor >= 0 && Menu is { } menu:
                return ItemMenu.Open(_views[_cursor], menu(_items[_cursor]));
            case WidgetKey.Delete when _cursor >= 0 && Deleted is { } deleted:
                deleted(_items[_cursor]);
                return true;
            case WidgetKey.Escape:
                MoveCursor(-1);
                return false;
            default:
                return false;
        }
    }

    /// <summary>Put the keyboard cursor on the item at <paramref name="index"/> and bring it into the card (a search's first, type-ahead).</summary>
    public void Point(int index)
    {
        if (index >= 0 && index < _items.Count)
        {
            MoveCursor(index);
            Reveal(index);
        }
    }

    /// <summary>Let the keyboard cursor go (the widget reads something else now).</summary>
    public void Release() => MoveCursor(-1);

    // ---- pointer -----------------------------------------------------------------------------------

    private void Hook(ItemView view)
    {
        view.PointerEntered += OnEntered;
        view.PointerExited += OnLeft;
        view.PointerCaptureLost += OnLeft;
        view.PointerCanceled += OnLeft;
        view.AddHandler(PointerPressedEvent, new PointerEventHandler(OnPressed), handledEventsToo: true);
        view.AddHandler(PointerReleasedEvent, new PointerEventHandler(OnReleased), handledEventsToo: true);
        view.Tapped += OnTapped;
        view.RightTapped += OnRightTapped;
        view.Acted += OnActed;
        if (LightsItems)
        {
            PointerPreview.Register(view, pressed => ItemFill.Paint(view, ItemRole.Row, pressed ? ItemState.Pressed : ItemState.Hover));
        }
    }

    private void Unhook(ItemView view)
    {
        view.PointerEntered -= OnEntered;
        view.PointerExited -= OnLeft;
        view.PointerCaptureLost -= OnLeft;
        view.PointerCanceled -= OnLeft;
        view.Tapped -= OnTapped;
        view.RightTapped -= OnRightTapped;
        view.Acted -= OnActed;
        PointerPreview.Forget(view);
    }

    private void OnEntered(object sender, PointerRoutedEventArgs args)
    {
        if (sender is ItemView view && LightsItems)
        {
            ItemFill.Paint(view, ItemRole.Row, ItemState.Hover);
        }
    }

    private void OnLeft(object sender, PointerRoutedEventArgs args)
    {
        if (sender is ItemView view)
        {
            _pressed = null;
            Paint(view);
        }
    }

    private void OnPressed(object sender, PointerRoutedEventArgs args)
    {
        if (sender is ItemView view && Openable)
        {
            _pressed = view;
            ItemFill.Paint(view, ItemRole.Row, ItemState.Pressed);
        }
    }

    private void OnReleased(object sender, PointerRoutedEventArgs args)
    {
        if (sender is ItemView view)
        {
            _pressed = null;
            ItemFill.Paint(view, ItemRole.Row, LightsItems ? ItemState.Hover : ItemState.Rest);
        }
    }

    private void OnTapped(object sender, TappedRoutedEventArgs args)
    {
        if (sender is ItemView { Item: { } item })
        {
            args.Handled = Open(item);
        }
    }

    private void OnRightTapped(object sender, RightTappedRoutedEventArgs args)
    {
        if (sender is ItemView { Item: { } item } view && Menu is { } menu)
        {
            ItemMenu.Show(view, args, menu(item));
        }
    }

    /// <summary>[S-16948] The end's button: the item's action, never in edit mode or a render.</summary>
    private void OnActed(ItemView view)
    {
        if (view.Item is { } item && Acted is { } acted && CanOpen?.Invoke() != false)
        {
            acted(item);
        }
    }

    private bool Openable => Opened is not null && CanOpen?.Invoke() != false;

    private bool Open(Item item)
    {
        if (!Openable)
        {
            return false;
        }

        Opened!(item);
        return true;
    }

    /// <summary>An item at rest: its fill, or the selected fill where the keyboard cursor stands on it.</summary>
    private void Paint(ItemView view)
    {
        if (ReferenceEquals(view, _pressed))
        {
            return;
        }

        var at = _views.IndexOf(view);
        ItemFill.Paint(view, ItemRole.Row, at >= 0 && at == _cursor ? ItemState.Selected : ItemState.Rest);
    }

    private void MoveCursor(int index)
    {
        var was = _cursor;
        _cursor = index;
        if (was >= 0 && was < _views.Count)
        {
            Paint(_views[was]);
        }

        if (index >= 0 && index < _views.Count)
        {
            Paint(_views[index]);
        }

        CursorMoved?.Invoke(index >= 0 && index < _items.Count ? _items[index] : null);
    }
}
