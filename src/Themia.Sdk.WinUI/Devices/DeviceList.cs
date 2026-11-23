using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Themia.App.Widgets;
using Themia.Core.Widgets;
using Themia.Sdk.Devices;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI.Kit;
using Themia.Core.Geometry;
using Windows.Foundation;

namespace Themia.Sdk.WinUI.Devices;

/// <summary>
/// [S-16633] [S-16635] A list of device items (<see cref="DeviceItemView"/>), the device family's one list: every item its
/// one fixed size laid by <see cref="DeviceLayout.Lay"/> (down from the card's top edge, or across where the card is one
/// item tall; the last the card reaches cut by its edge, WR-4), touching or with the block gap between them. It stands on
/// the card's top edge (<see cref="ListEdge"/>) and runs to its foot and both sides (WR-1, WR-3). An item lights under the
/// pointer and pressed (the item layer's row fill, WR-13; only its fill changes, WR-12), opens on a click, shows its menu
/// on a right click, and takes the keys (Up/Down or Left/Right walk, Enter opens, the Menu key its menu, Delete its own
/// step, Escape lets go). With the gap on it names its items' boxes as the card's grounds (<see cref="Grounds"/>).
/// </summary>
public sealed partial class DeviceList : Panel
{
    private readonly IWidgetHost _host;
    private readonly List<DeviceItemView> _views = [];
    private IReadOnlyList<DeviceItem> _items = [];
    private DeviceLay _lay = new(false, 0, 0, []);
    private Size _arranged;
    private WidgetArea _area;
    private int _gap;
    private bool _stock;
    private int _cursor = -1;
    private DeviceItemView? _pressed;

    public DeviceList(IWidgetHost host)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        ListEdge.Apply(this);
        Background = Family.Brush(ColourRole.Clear);
    }

    /// <summary>What a click, a double-click or Enter on an item does (null: nothing); it is not asked in edit mode.</summary>
    public Action<DeviceItem>? Opened { get; set; }

    /// <summary>Whether an item opens now (false in edit mode, in a preview).</summary>
    public Func<bool>? CanOpen { get; set; }

    /// <summary>An item's menu, read on every right click (Open, Eject, Refresh).</summary>
    public Func<DeviceItem, IReadOnlyList<ItemMenuEntry>>? Menu { get; set; }

    /// <summary>Delete on the item under the keyboard cursor (a drive's Eject); null: the key is not this list's.</summary>
    public Action<DeviceItem>? Deleted { get; set; }

    /// <summary>[S-11950] A drawn item's place on the view's links (its key lit and published); called again on every draw.</summary>
    public Action<DeviceItem, FrameworkElement>? Decorate { get; set; }

    /// <summary>[S-11950] The items are drawn again: what was decorated is forgotten first.</summary>
    public Action? Redrawing { get; set; }

    /// <summary>[S-3307] The keyboard cursor moved onto an item (null: it let go).</summary>
    public Action<DeviceItem?>? CursorMoved { get; set; }

    /// <summary>[S-16640] The items' grounds changed (they came, went or moved): the card paints them again.</summary>
    public event Action? GroundsChanged;

    /// <summary>The items it shows, in their order.</summary>
    public IReadOnlyList<DeviceItem> Items => _items;

    /// <summary>The layout as last arranged.</summary>
    public DeviceLay Lay => _lay;

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

    /// <summary>
    /// [S-16640] The boxes the card paints its ground under while the gap is on, in this list's DIPs: each item the card
    /// shows, cut by the card's edge; null with the gap off (the card is one ground).
    /// </summary>
    public IReadOnlyList<DipRect>? Grounds
    {
        get
        {
            if (_gap <= 0)
            {
                return null;
            }

            // The box as last arranged (read while the arrange tells of it, before ActualHeight has caught up).
            var (width, height) = (_arranged.Width, _arranged.Height);
            var grounds = new List<DipRect>(_lay.Slots.Count);
            foreach (var slot in _lay.Slots.Take(_items.Count))
            {
                var right = Math.Min(slot.Right, width);
                var bottom = Math.Min(slot.Bottom, height);
                if (right > slot.X && bottom > slot.Y)
                {
                    grounds.Add(new DipRect(slot.X, slot.Y, right - slot.X, bottom - slot.Y));
                }
            }

            return grounds;
        }
    }

    /// <summary>
    /// [S-16635] Show <paramref name="items"/> in their order: the item views standing are shown again in place (a drive's
    /// picture is not asked again), the rest made or taken down. <paramref name="stock"/>: drives wear their kinds' stock icons.
    /// </summary>
    public void Show(IReadOnlyList<DeviceItem> items, IReadOnlyCollection<DevicePart> hidden, bool stock)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(hidden);
        var was = _items;
        _items = items;
        _stock = stock;

        while (_views.Count > items.Count)
        {
            var gone = _views[^1];
            _views.RemoveAt(_views.Count - 1);
            Unhook(gone);
            Children.Remove(gone);
        }

        while (_views.Count < items.Count)
        {
            var view = new DeviceItemView(_host);
            Hook(view);
            _views.Add(view);
            Children.Add(view);
        }

        Redrawing?.Invoke();
        for (var i = 0; i < items.Count; i++)
        {
            _views[i].Show(items[i], hidden, stock);
            Paint(_views[i]);
            Decorate?.Invoke(items[i], _views[i]);
        }

        if (_cursor >= items.Count)
        {
            MoveCursor(items.Count - 1);
        }

        InvalidateMeasure();
        if (_gap > 0 && was.Count != items.Count)
        {
            GroundsChanged?.Invoke();
        }
    }

    /// <summary>The card's box: the items are laid in it from its top edge (the list stands the inset over the view).</summary>
    public void Size(WidgetArea area)
    {
        _area = area;
        InvalidateMeasure();
    }

    /// <summary>[S-16635] The kit's keys over the items: walk, open, menu, the list's own Delete, let go.</summary>
    public bool HandleKey(WidgetKey key)
    {
        var count = _items.Count;
        if (count == 0)
        {
            return false;
        }

        switch (key)
        {
            case WidgetKey.Down or WidgetKey.Right:
                MoveCursor(_cursor < 0 ? 0 : Math.Min(count - 1, _cursor + 1));
                return true;
            case WidgetKey.Up or WidgetKey.Left:
                MoveCursor(_cursor < 0 ? 0 : Math.Max(0, _cursor - 1));
                return true;
            case WidgetKey.Home:
                MoveCursor(0);
                return true;
            case WidgetKey.End:
                MoveCursor(count - 1);
                return true;
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

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsFinite(availableSize.Width) ? availableSize.Width : DeviceLayout.NaturalWidth;
        var height = double.IsFinite(availableSize.Height) ? availableSize.Height : _area.IsMeasured ? _area.Height + FamilySpace.Inset : 0;
        // The measure's own lay: the arranged one (what the grounds are) is the arrange's alone.
        var lay = DeviceLayout.Lay(_items.Count, width, height, _gap, Grid());
        for (var i = 0; i < _views.Count; i++)
        {
            var slot = i < lay.Slots.Count ? lay.Slots[i] : default;
            _views[i].Measure(new Size(slot.Width, slot.Height));
        }

        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var before = _lay;
        var resized = finalSize != _arranged;
        _arranged = finalSize;
        _lay = DeviceLayout.Lay(_items.Count, finalSize.Width, finalSize.Height, _gap, Grid());
        for (var i = 0; i < _views.Count; i++)
        {
            if (i < _lay.Slots.Count)
            {
                var slot = _lay.Slots[i];
                _views[i].Visibility = Visibility.Visible;
                _views[i].Arrange(new Rect(slot.X, slot.Y, slot.Width, slot.Height));
            }
            else
            {
                // Past the card's edge: not drawn (the card shows what its box holds, [S-14410]).
                _views[i].Visibility = Visibility.Collapsed;
                _views[i].Arrange(new Rect(0, 0, 0, 0));
            }
        }

        Clip = new Microsoft.UI.Xaml.Media.RectangleGeometry { Rect = new Rect(0, 0, finalSize.Width, finalSize.Height) };
        if (_gap > 0 && (resized || !SameSlots(before, _lay)))
        {
            GroundsChanged?.Invoke();
        }

        return finalSize;
    }

    private ItemGrid Grid() => _area.Grid(XamlRoot?.RasterizationScale ?? 1);

    private static bool SameSlots(DeviceLay a, DeviceLay b) => a.Across == b.Across && a.Slots.SequenceEqual(b.Slots);

    private void Hook(DeviceItemView view)
    {
        view.PointerEntered += OnEntered;
        view.PointerExited += OnLeft;
        view.PointerCaptureLost += OnLeft;
        view.PointerCanceled += OnLeft;
        view.AddHandler(PointerPressedEvent, new PointerEventHandler(OnPressed), handledEventsToo: true);
        view.AddHandler(PointerReleasedEvent, new PointerEventHandler(OnReleased), handledEventsToo: true);
        view.Tapped += OnTapped;
        view.RightTapped += OnRightTapped;
        PointerPreview.Register(view, pressed => ItemFill.Paint(view, ItemRole.Row, pressed ? ItemState.Pressed : ItemState.Hover));
    }

    private void Unhook(DeviceItemView view)
    {
        view.PointerEntered -= OnEntered;
        view.PointerExited -= OnLeft;
        view.PointerCaptureLost -= OnLeft;
        view.PointerCanceled -= OnLeft;
        view.Tapped -= OnTapped;
        view.RightTapped -= OnRightTapped;
        PointerPreview.Forget(view);
    }

    private void OnEntered(object sender, PointerRoutedEventArgs args)
    {
        if (sender is DeviceItemView view)
        {
            ItemFill.Paint(view, ItemRole.Row, ItemState.Hover);
        }
    }

    private void OnLeft(object sender, PointerRoutedEventArgs args)
    {
        if (sender is DeviceItemView view)
        {
            _pressed = null;
            Paint(view);
        }
    }

    private void OnPressed(object sender, PointerRoutedEventArgs args)
    {
        if (sender is DeviceItemView view && Openable)
        {
            _pressed = view;
            ItemFill.Paint(view, ItemRole.Row, ItemState.Pressed);
        }
    }

    private void OnReleased(object sender, PointerRoutedEventArgs args)
    {
        if (sender is DeviceItemView view)
        {
            _pressed = null;
            ItemFill.Paint(view, ItemRole.Row, ItemState.Hover);
        }
    }

    private void OnTapped(object sender, TappedRoutedEventArgs args)
    {
        if (sender is DeviceItemView { Item: { } item })
        {
            args.Handled = Open(item);
        }
    }

    private void OnRightTapped(object sender, RightTappedRoutedEventArgs args)
    {
        if (sender is DeviceItemView { Item: { } item } view && Menu is { } menu)
        {
            ItemMenu.Show(view, args, menu(item));
        }
    }

    private bool Openable => Opened is not null && CanOpen?.Invoke() != false;

    private bool Open(DeviceItem item)
    {
        if (!Openable)
        {
            return false;
        }

        Opened!(item);
        return true;
    }

    /// <summary>An item at rest: its fill, or the selected fill where the keyboard cursor stands on it.</summary>
    private void Paint(DeviceItemView view)
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
