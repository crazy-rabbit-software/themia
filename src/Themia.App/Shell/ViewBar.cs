using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Windows.Foundation;

namespace Themia.App.Shell;

/// <summary>One chip on the view bar.</summary>
/// <param name="IsDefault">The view new displays start on ([S-847]); its name is drawn bold, as in the original.</param>
internal sealed record ViewBarItem(string Id, string Name, string? Icon, bool Shown, bool IsDefault);

/// <summary>How the view bar is drawn: its size, labels, direction and edit-mode state.</summary>
internal sealed record ViewBarLook(ViewBarSize Size, bool IconOnly, bool Vertical, bool Editing, bool Preview);

/// <summary>What the view bar asks of the surface it sits on. Points are in the window's content coordinates.</summary>
internal interface IViewBarOwner
{
    /// <summary>A chip was clicked: show its view on this display.</summary>
    void SwitchTo(string viewId);

    /// <summary>[S-914] A chip was dragged into another chip's slot.</summary>
    void CommitReorder(string draggedId, string targetId);

    /// <summary>[S-915] A chip was right-clicked in edit mode.</summary>
    void ShowChipMenu(FrameworkElement chip, string viewId, RightTappedRoutedEventArgs args);

    /// <summary>[S-913] The grip is being dragged; the pointer is at <paramref name="at"/>.</summary>
    void BarDragMoved(Point at);

    /// <summary>[S-913] The grip was let go, after a real drag or not.</summary>
    void BarDragEnded(Point at, bool moved);
}

/// <summary>
/// [S-906] The desktop view bar: a row (or column) of chips, one per view, that puts a view on the
/// display it sits on. The original's on-desktop switcher, ported as one element on the surface
/// rather than a window of its own, so it costs no HWND and sits wherever the surface does —
/// on the desktop, never over another app.
/// <para>
/// The chips are plain borders with their own pointer handling, not buttons: a button captures the
/// pointer and turns every press into a click, and here a press in edit mode may become a drag.
/// The bar captures the pointer itself, so a chip can be moved within the row mid-drag without the
/// drag being lost.
/// </para>
/// </summary>
internal sealed partial class ViewBar : Grid
{
    private readonly IViewBarOwner _owner;
    private readonly Border _fill = new();
    private readonly Rectangle _stroke = new() { StrokeThickness = Fluent.Hairline, IsHitTestVisible = false };
    private readonly StackPanel _panel = new();

    private IReadOnlyList<ViewBarItem> _items = [];
    private ViewBarLook? _look;
    private string _built = string.Empty;

    // A press in progress: on a chip (the view's id) or on the grip (null id).
    private bool _pressing;
    private string? _pressedId;
    private Point _pressedAt;
    private bool _dragging;
    private List<string> _orderAtPress = [];

    /// <summary>[S-1979] The view whose chip takes the focus back after the rebuild an Enter caused.</summary>
    private string? _refocusId;

    public ViewBar(IViewBarOwner owner)
    {
        _owner = owner;

        // Transparent, not null: the gaps between chips are still the bar, not the desktop.
        Background = new SolidColorBrush(Colors.Transparent);
        HorizontalAlignment = HorizontalAlignment.Left;
        VerticalAlignment = VerticalAlignment.Top;
        Children.Add(_fill);
        Children.Add(_panel);
        Children.Add(_stroke);

        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        PointerCaptureLost += OnPointerCaptureLost;
        AutomationProperties.SetName(this, Str.Shell_ViewBar_Name.Text());
    }

    /// <summary>
    /// Draw the chips. Skipped when nothing that shows has changed: the surface calls this on every
    /// state change, and rebuilding would drop the pointer-over state under the cursor. A drag in
    /// progress is never rebuilt under the pointer.
    /// </summary>
    public void Update(IReadOnlyList<ViewBarItem> items, ViewBarLook look, DesignState design)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(look);
        ArgumentNullException.ThrowIfNull(design);

        PaintSurface(look, design);

        // The language is part of what was built: a switch redraws the tooltips and names ([S-1110]).
        var signature = $"{look}|{string.Join("|", items)}|{FluentType.Body.FontSize}|{Strings.Current}";
        if ((_dragging && _pressedId is not null) || signature == _built)
        {
            return;
        }

        _built = signature;
        _items = items;
        _look = look;
        Rebuild();
    }

    // ---- look ------------------------------------------------------------------------------

    /// <summary>[S-911] The sizes of one step: the original's M, L and XL, on the Fluent ramp.</summary>
    private readonly record struct Metrics(TypeStep Type, double Icon, double PadX, double PadY, double BarPad, double Square);

    private static Metrics MetricsFor(ViewBarSize size) => size switch
    {
        ViewBarSize.Large => new(FluentType.Body, 16, Fluent.Space3, 6, 6, 34),
        ViewBarSize.ExtraLarge => new(FluentType.BodyLarge, 20, Fluent.Space4, Fluent.Space2, Fluent.Space2, 40),
        _ => new(FluentType.Caption, 14, Fluent.Space2, Fluent.Space1, Fluent.Space1, 28),
    };

    /// <summary>
    /// The bar reads as a widget: the card's own fill, colour and opacity, and its stroke. In edit
    /// mode on a display where the user hid it, it is drawn faded with a dashed edge ([S-907]).
    /// </summary>
    private void PaintSurface(ViewBarLook look, DesignState design)
    {
        var metrics = MetricsFor(look.Size);
        var radius = Fluent.ControlRadius + metrics.BarPad;

        _fill.CornerRadius = new CornerRadius(radius);
        _fill.Background = DesignApplier.UserBrush(design.WidgetBackgroundColor)
            ?? FluentLayout.Themed("SolidBackgroundFillColorBaseBrush", Colors.Black);
        _fill.Opacity = Math.Clamp(design.SurfaceOpacity, 0.2, 1);

        _stroke.RadiusX = _stroke.RadiusY = Math.Max(0, radius - (Fluent.Hairline / 2));
        _stroke.Stroke = DesignApplier.UserBrush(design.BorderColor)
            ?? FluentLayout.Themed(look.Preview ? "TextFillColorTertiaryBrush" : "CardStrokeColorDefaultBrush", Colors.Gray);
        var dashes = new DoubleCollection();
        if (look.Preview)
        {
            dashes.Add(4);
            dashes.Add(3);
        }

        _stroke.StrokeDashArray = dashes;

        Opacity = look.Preview ? 0.45 : 1;
    }

    private void Rebuild()
    {
        if (_look is not { } look)
        {
            return;
        }

        var metrics = MetricsFor(look.Size);
        _panel.Children.Clear();
        _panel.Orientation = look.Vertical ? Orientation.Vertical : Orientation.Horizontal;
        _panel.Spacing = Fluent.Space1;
        _panel.Margin = new Thickness(metrics.BarPad);

        if (look.Editing)
        {
            _panel.Children.Add(BuildGrip(look, metrics));
        }

        foreach (var item in _items)
        {
            _panel.Children.Add(BuildChip(item, look, metrics));
        }

        // [S-1979] Enter on a chip rebuilds the bar under the focus: give it back to the same view's chip.
        if (_refocusId is { } refocus && Chip(refocus) is { } again)
        {
            _refocusId = null;
            again.Loaded += (_, _) => again.Focus(FocusState.Keyboard);
        }

        ToolTipService.SetToolTip(this, null);
        if (look.Preview)
        {
            ToolTipService.SetToolTip(this, Str.Shell_ViewBar_HiddenTip.Text());
        }
    }

    /// <summary>[S-913] The handle the bar is dragged by, in edit mode only, as in the original.</summary>
    private Border BuildGrip(ViewBarLook look, Metrics metrics)
    {
        var icon = Fluent.Icon(look.Vertical ? Glyphs.GripperHorizontal : Glyphs.GripperVertical, "TextFillColorTertiaryBrush", metrics.Icon);
        var grip = new Border
        {
            Child = icon,
            Background = new SolidColorBrush(Colors.Transparent),
            Padding = new Thickness(look.Vertical ? 0 : Fluent.Space1, look.Vertical ? Fluent.Space1 : 0, look.Vertical ? 0 : Fluent.Space1, look.Vertical ? Fluent.Space1 : 0),
            CornerRadius = new CornerRadius(Fluent.ControlRadius),
            HorizontalAlignment = look.Vertical ? HorizontalAlignment.Stretch : HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Stretch,
        };

        ToolTipService.SetToolTip(grip, Str.App_DragBar.Text());
        AutomationProperties.SetName(grip, Str.Shell_ViewBar_GripName.Text());
        grip.PointerPressed += (_, e) => OnPressed(e, null);
        HoverFill(grip, shown: false);
        return grip;
    }

    private Border BuildChip(ViewBarItem item, ViewBarLook look, Metrics metrics)
    {
        var brush = item.Shown ? "TextFillColorPrimaryBrush" : "TextFillColorSecondaryBrush";

        // [S-903] The view's own icon, or its initials, at the step's icon size.
        FrameworkElement mark;
        if (item.Icon is { } glyph)
        {
            mark = Fluent.Icon(glyph, item.Shown ? "AccentTextFillColorPrimaryBrush" : brush, metrics.Icon);
        }
        else
        {
            var initials = Fluent.Text(FluentType.Strong(metrics.Type), item.Shown ? "AccentTextFillColorPrimaryBrush" : brush);
            initials.Text = ViewListRules.Initials(item.Name);
            initials.TextTrimming = TextTrimming.None;
            initials.VerticalAlignment = VerticalAlignment.Center;
            mark = initials;
        }

        FrameworkElement content = mark;
        if (!look.IconOnly)
        {
            var name = Fluent.Text(item.IsDefault ? FluentType.Strong(metrics.Type) : metrics.Type, brush);
            name.Text = item.Name;
            name.VerticalAlignment = VerticalAlignment.Center;
            name.MaxWidth = 220;

            content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = Fluent.Space2 * 0.75,
                Children = { mark, name },
            };
        }

        content.HorizontalAlignment = look.Vertical && !look.IconOnly ? HorizontalAlignment.Left : HorizontalAlignment.Center;
        content.VerticalAlignment = VerticalAlignment.Center;

        var chip = new Border
        {
            Child = content,
            Tag = item.Id,
            Padding = look.IconOnly ? new Thickness(0) : new Thickness(metrics.PadX, metrics.PadY, metrics.PadX, metrics.PadY),
            CornerRadius = new CornerRadius(Fluent.ControlRadius),
            BorderThickness = new Thickness(Fluent.Hairline),
            MinHeight = metrics.Square,
            HorizontalAlignment = look.Vertical ? HorizontalAlignment.Stretch : HorizontalAlignment.Left,
        };

        if (look.IconOnly)
        {
            chip.Width = metrics.Square;
            chip.Height = metrics.Square;
        }

        var tip = item.IsDefault ? Str.Shell_ViewBar_DefaultTip.Format(("name", item.Name)) : item.Name;
        ToolTipService.SetToolTip(chip, look.Editing ? Str.Shell_ViewBar_EditTip.Format(("name", tip)) : tip);
        AutomationProperties.SetName(chip, item.Name);
        AutomationProperties.SetItemStatus(chip, item.Shown ? Str.Shell_ViewBar_Showing.Text() : string.Empty);

        HoverFill(chip, item.Shown);

        // [S-1979] In edit mode the chips take the keyboard (F6 reaches the bar): the arrows rove,
        // Enter or Space shows the view. Outside edit mode the surface never has the keyboard.
        if (look.Editing)
        {
            chip.IsTabStop = true;
            chip.UseSystemFocusVisuals = true;
            chip.KeyDown += (_, e) => e.Handled = OnChipKey(item.Id, e.Key);
        }

        chip.PointerPressed += (_, e) => OnPressed(e, item.Id);
        chip.RightTapped += (_, e) =>
        {
            if (_look?.Editing == true)
            {
                e.Handled = true;
                _owner.ShowChipMenu(chip, item.Id, e);
            }
        };

        return chip;
    }

    /// <summary>
    /// The shown view is a raised control on the bar, the rest are subtle until the pointer is over
    /// them — the way a Windows 11 selector reads, and the same as the switcher widget.
    /// </summary>
    private static void HoverFill(Border element, bool shown)
    {
        Brush Rest() => FluentLayout.Themed(shown ? "ControlFillColorDefaultBrush" : "SubtleFillColorTransparentBrush", Colors.Transparent);

        element.Background = Rest();
        element.BorderBrush = shown
            ? FluentLayout.Themed("ControlStrokeColorDefaultBrush", Colors.Transparent)
            : new SolidColorBrush(Colors.Transparent);
        element.PointerEntered += (_, _) => element.Background = FluentLayout.Themed(shown ? "ControlFillColorSecondaryBrush" : "SubtleFillColorSecondaryBrush", Colors.Transparent);
        element.PointerExited += (_, _) => element.Background = Rest();
    }

    // ---- pointer -----------------------------------------------------------------------------

    private void OnPressed(PointerRoutedEventArgs e, string? viewId)
    {
        var point = e.GetCurrentPoint(null);
        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        // A press on the grip outside edit mode cannot happen (there is no grip); a chip press
        // outside edit mode is only ever a click.
        _pressing = true;
        _pressedId = viewId;
        _pressedAt = point.Position;
        _dragging = false;
        _orderAtPress = [.. _items.Select(i => i.Id)];

        CapturePointer(e.Pointer);

        // Handled, so the surface underneath neither starts a rubber band nor drops a ghost here.
        e.Handled = true;
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_pressing || _look is not { Editing: true } look)
        {
            return;
        }

        var at = e.GetCurrentPoint(null).Position;
        if (!_dragging)
        {
            var moved = Math.Max(Math.Abs(at.X - _pressedAt.X), Math.Abs(at.Y - _pressedAt.Y));
            if (moved < ViewBarRules.DragThreshold)
            {
                return;
            }

            _dragging = true;
            if (_pressedId is not null && Chip(_pressedId) is { } lifted)
            {
                lifted.Opacity = 0.6;
            }
        }

        if (_pressedId is null)
        {
            _owner.BarDragMoved(at);
        }
        else
        {
            MoveChipTo(_pressedId, e.GetCurrentPoint(_panel).Position, look.Vertical);
        }

        e.Handled = true;
    }

    /// <summary>[S-914] Slide the dragged chip into the slot nearest the pointer, in the row only; the state changes on release.</summary>
    private void MoveChipTo(string viewId, Point inPanel, bool vertical)
    {
        var chips = Chips().ToList();
        var from = chips.FindIndex(c => (string)c.Tag == viewId);
        if (from < 0)
        {
            return;
        }

        var centres = chips.Select(c =>
        {
            var origin = c.TransformToVisual(_panel).TransformPoint(new Point(0, 0));
            return vertical ? origin.Y + (c.ActualHeight / 2) : origin.X + (c.ActualWidth / 2);
        }).ToList();

        var to = ViewBarRules.NearestSlot(centres, vertical ? inPanel.Y : inPanel.X);
        if (to < 0 || to == from)
        {
            return;
        }

        var offset = _panel.Children.Count - chips.Count; // the grip, when there is one
        _panel.Children.Move((uint)(from + offset), (uint)(to + offset));
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_pressing)
        {
            return;
        }

        var at = e.GetCurrentPoint(null).Position;
        var (id, dragged) = (_pressedId, _dragging);
        EndPress();
        ReleasePointerCapture(e.Pointer);
        e.Handled = true;

        if (id is null)
        {
            _owner.BarDragEnded(at, dragged);
            return;
        }

        if (!dragged)
        {
            _owner.SwitchTo(id);
            return;
        }

        // The slot the chip ended in, named by the view that held it when the drag began.
        var slot = Chips().ToList().FindIndex(c => (string)c.Tag == id);
        if (slot >= 0 && slot < _orderAtPress.Count && _orderAtPress[slot] != id)
        {
            _owner.CommitReorder(id, _orderAtPress[slot]);
        }
        else
        {
            ForceRebuild();
        }
    }

    /// <summary>The drag was taken away (a window came up, the pointer was cancelled): undo what it showed.</summary>
    private void OnPointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        if (!_pressing)
        {
            return;
        }

        var (id, dragged, at) = (_pressedId, _dragging, _pressedAt);
        EndPress();

        if (id is null)
        {
            _owner.BarDragEnded(at, moved: false);
        }
        else if (dragged)
        {
            ForceRebuild();
        }
    }

    private void EndPress()
    {
        _pressing = false;
        _dragging = false;
    }

    private void ForceRebuild()
    {
        _built = string.Empty;
        Rebuild();
    }

    /// <summary>[S-1979] A key on a focused chip in edit mode.</summary>
    private bool OnChipKey(string viewId, Windows.System.VirtualKey key)
    {
        if (key is Windows.System.VirtualKey.Enter or Windows.System.VirtualKey.Space)
        {
            if (!_items.Any(i => i.Shown && i.Id == viewId))
            {
                _refocusId = viewId;
            }

            _owner.SwitchTo(viewId);
            return true;
        }

        WidgetKey? move = key switch
        {
            Windows.System.VirtualKey.Left or Windows.System.VirtualKey.Up => WidgetKey.Up,
            Windows.System.VirtualKey.Right or Windows.System.VirtualKey.Down => WidgetKey.Down,
            Windows.System.VirtualKey.Home => WidgetKey.Home,
            Windows.System.VirtualKey.End => WidgetKey.End,
            _ => null,
        };

        if (move is not { } widgetKey)
        {
            return false;
        }

        var chips = Chips().ToList();
        var step = ListKeys.Decide(widgetKey, chips.FindIndex(c => (string)c.Tag == viewId), chips.Count);
        if (step.Command == ListCommand.Focus && step.Index >= 0)
        {
            chips[step.Index].Focus(FocusState.Keyboard);
        }

        return true;
    }

    /// <summary>[S-1979] Put the keyboard on the shown view's chip (the surface's F6); false when the bar has none to focus.</summary>
    internal bool FocusFirstChip()
    {
        var chips = Chips().ToList();
        var shown = chips.FindIndex(c => _items.Any(i => i.Shown && i.Id == (string)c.Tag));
        return chips.Count > 0 && chips[Math.Max(0, shown)].Focus(FocusState.Keyboard);
    }

    private IEnumerable<Border> Chips() => _panel.Children.OfType<Border>().Where(b => b.Tag is string);

    private Border? Chip(string viewId) => Chips().FirstOrDefault(c => (string)c.Tag == viewId);
}
