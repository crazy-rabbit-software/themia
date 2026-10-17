using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Windows.Foundation;

namespace Themia.App.Shell;

/// <summary>One chip on the view bar.</summary>
/// <param name="IsDefault">The view new displays start on ([S-847]); its name is drawn bold, as in the original.</param>
/// <param name="Color">[S-3812] The view's own colour for its icon or initials (<c>#RRGGBB</c>), or null for the theme's.</param>
internal sealed record ViewBarItem(string Id, string Name, string? Icon, bool Shown, bool IsDefault, string? Color = null);

/// <summary>How the view bar is drawn: its size, labels, direction, edit-mode state and [S-5400] style.</summary>
internal sealed record ViewBarLook(ViewBarSize Size, bool IconOnly, bool Vertical, bool Editing, bool Preview, ViewBarStyle Style = ViewBarStyle.Chips);

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

    /// <summary>
    /// [S-918] / [S-5402] The overflow chip or the drop-down button was clicked: list
    /// <paramref name="views"/> (the ones that did not fit, or every view on a drop-down).
    /// </summary>
    void ShowViewMenu(FrameworkElement button, IReadOnlyList<ViewBarItem> views);
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

    /// <summary>[S-917] The bar's length budget along its axis, in DIPs; infinite until the surface knows its work area.</summary>
    private double _room = double.PositiveInfinity;

    /// <summary>
    /// [S-5402] The views the bar's menu lists, in bar order: those whose chips did not fit
    /// ([S-918]), or every view on a drop-down.
    /// </summary>
    private IReadOnlyList<ViewBarItem> _menuViews = [];

    /// <summary>The overflow chip while the chip bar has one, or the drop-down button.</summary>
    private Border? _menuButton;

    private bool _menuPressed;

    /// <summary>[S-918] / [S-5401] The overflow chip or the drop-down button; null when every chip fits.</summary>
    internal FrameworkElement? MenuButton => _menuButton;

    /// <summary>[S-5402] The views the menu lists, in bar order.</summary>
    internal IReadOnlyList<ViewBarItem> MenuViews => _menuViews;

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
    /// <param name="room">[S-917] The longest the whole bar may be along its axis (<see cref="ViewBarRules.Room"/>).</param>
    public void Update(IReadOnlyList<ViewBarItem> items, ViewBarLook look, DesignState design, double room)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(look);
        ArgumentNullException.ThrowIfNull(design);

        PaintSurface(look, design);

        // The language is part of what was built: a switch redraws the tooltips and names ([S-1110]).
        // [S-917] So is the room: a resize, a DPI change or the toolbar appearing refits the chips.
        var signature = $"{look}|{string.Join("|", items)}|{FluentType.Body.FontSize}|{Strings.Current}|{Math.Floor(room)}";
        if ((_dragging && _pressedId is not null) || signature == _built)
        {
            return;
        }

        _built = signature;
        _items = items;
        _look = look;
        _room = room;
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
        // [S-6563] The app-wide background: its colour or the Fluent base, at its opacity but never
        // under 20 %, so the bar can always be found (a transparent design draws it at the floor).
        var fill = DesignLook.Card(design.Background ?? new FillChoice());
        _fill.Background = (fill.Colour is { } c ? DesignApplier.UserBrush(HexColor.From(c.R, c.G, c.B)) : null)
            ?? ThemeBrush.Of("SolidBackgroundFillColorBaseBrush", Colors.Black);
        _fill.Opacity = Math.Clamp(fill.Opacity, 0.2, 1);

        _stroke.RadiusX = _stroke.RadiusY = Math.Max(0, radius - (Fluent.Hairline / 2));
        _stroke.Stroke = DesignApplier.UserBrush(design.BorderColor)
            ?? ThemeBrush.Of(look.Preview ? "TextFillColorTertiaryBrush" : "CardStrokeColorDefaultBrush", Colors.Gray);
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

        // [S-917] What the bar spends besides chips: its padding at both ends, and the grip with its gap.
        var fixedLength = 2 * metrics.BarPad;
        if (look.Editing)
        {
            var grip = BuildGrip(look, metrics);
            _panel.Children.Add(grip);
            fixedLength += Length(grip, look.Vertical) + _panel.Spacing;
        }

        if (look.Style == ViewBarStyle.Dropdown)
        {
            // [S-5401] One button for the whole bar; its menu lists every view on it ([S-5402]).
            _menuViews = [.. ViewBarRules.MenuViews(look.Style, _items.Count, new ViewBarFit([], Overflow: false)).Select(i => _items[i])];
            _menuButton = BuildDropdown(look, metrics, _room - fixedLength);
            _panel.Children.Add(_menuButton);
            FinishRebuild(look);
            return;
        }

        var chips = _items.Select(item => BuildChip(item, look, metrics)).ToList();
        var overflow = BuildOverflowChip(look, metrics);
        var fit = ViewBarRules.Fit(
            [.. chips.Select(c => Length(c, look.Vertical))],
            _items.ToList().FindIndex(i => i.Shown),
            _room,
            fixedLength,
            _panel.Spacing,
            Length(overflow, look.Vertical));

        foreach (var index in fit.Shown)
        {
            _panel.Children.Add(chips[index]);
        }

        _menuViews = [.. ViewBarRules.MenuViews(look.Style, _items.Count, fit).Select(i => _items[i])];
        _menuButton = null;
        if (fit.Overflow)
        {
            var more = Str.Shell_ViewBar_More.Format(("count", _menuViews.Count));
            ToolTipService.SetToolTip(overflow, more);
            AutomationProperties.SetName(overflow, more);
            _panel.Children.Add(overflow);
            _menuButton = overflow;
        }

        FinishRebuild(look);
    }

    private void FinishRebuild(ViewBarLook look)
    {
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

    /// <summary>
    /// [S-903] The view's own icon, or its initials, at the step's icon size. [S-3812] In the view's
    /// colour where it has one, shown or not: the raised chip already says which is shown.
    /// </summary>
    private static FrameworkElement BuildMark(ViewBarItem item, Metrics metrics, string brush)
    {
        var tint = DesignApplier.UserBrush(item.Color);
        var fill = item.Shown ? "AccentTextFillColorPrimaryBrush" : brush;
        if (item.Icon is { } glyph)
        {
            var icon = Fluent.Icon(glyph, fill, metrics.Icon);
            if (tint is not null)
            {
                icon.Foreground = tint;
            }

            return icon;
        }

        var initials = Fluent.Text(FluentType.Strong(metrics.Type), fill);
        initials.Text = ViewListRules.Initials(item.Name);
        initials.TextTrimming = TextTrimming.None;
        initials.VerticalAlignment = VerticalAlignment.Center;
        if (tint is not null)
        {
            initials.Foreground = tint;
        }

        return initials;
    }

    private Border BuildChip(ViewBarItem item, ViewBarLook look, Metrics metrics)
    {
        var brush = item.Shown ? "TextFillColorPrimaryBrush" : "TextFillColorSecondaryBrush";
        var mark = BuildMark(item, metrics, brush);

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
            chip.KeyDown += (_, e) => e.Handled = OnKey(chip, item.Id, e.Key);
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

    /// <summary>[S-917] An element's length along the bar's axis, measured unconstrained as the bar is.</summary>
    private static double Length(FrameworkElement element, bool vertical)
    {
        element.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return vertical ? element.DesiredSize.Height : element.DesiredSize.Width;
    }

    /// <summary>
    /// [S-918] The last chip when the views do not all fit: "…", one icon-only square, opening a menu
    /// of the views left out — the overflow button of a Windows command bar. It takes the keyboard
    /// like a chip in edit mode, and is never dragged.
    /// </summary>
    private Border BuildOverflowChip(ViewBarLook look, Metrics metrics)
    {
        var chip = new Border
        {
            Child = Fluent.Icon(Glyphs.More, "TextFillColorSecondaryBrush", metrics.Icon),
            CornerRadius = new CornerRadius(Fluent.ControlRadius),
            BorderThickness = new Thickness(Fluent.Hairline),
            MinWidth = metrics.Square,
            Height = metrics.Square,
            HorizontalAlignment = look.Vertical ? HorizontalAlignment.Stretch : HorizontalAlignment.Left,
        };

        HoverFill(chip, shown: false);
        OpensMenu(chip, look);
        return chip;
    }

    /// <summary>
    /// [S-5401] The drop-down: one raised button with the shown view's icon in its colour, its name
    /// (unless names are off) and a chevron, opening the menu of every view on the bar ([S-5402]).
    /// A display showing a view the bar does not offer reads "Views" with the shared view glyph.
    /// </summary>
    /// <param name="room">The length left for the button along the bar, so a long name is cut rather than running off the display.</param>
    private Border BuildDropdown(ViewBarLook look, Metrics metrics, double room)
    {
        var shown = _items.FirstOrDefault(i => i.Shown)
            ?? new ViewBarItem(string.Empty, Str.Shell_ViewBar_Name.Text(), Glyphs.View, Shown: true, IsDefault: false);
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = Fluent.Space2 * 0.75,
            VerticalAlignment = VerticalAlignment.Center,
        };
        row.Children.Add(BuildMark(shown, metrics, "TextFillColorPrimaryBrush"));

        if (!look.IconOnly)
        {
            var name = Fluent.Text(shown.IsDefault ? FluentType.Strong(metrics.Type) : metrics.Type, "TextFillColorPrimaryBrush");
            name.Text = shown.Name;
            name.VerticalAlignment = VerticalAlignment.Center;
            var spare = room - (2 * metrics.PadX) - (2 * metrics.Icon) - (2 * row.Spacing);
            name.MaxWidth = double.IsFinite(spare) ? Math.Clamp(spare, metrics.Icon, 220) : 220;
            row.Children.Add(name);
        }

        var chevron = Fluent.Icon(Glyphs.ChevronDown, "TextFillColorSecondaryBrush", metrics.Icon * 0.7);
        chevron.VerticalAlignment = VerticalAlignment.Center;
        row.Children.Add(chevron);

        var button = new Border
        {
            Child = row,
            Padding = new Thickness(look.IconOnly ? metrics.PadX * 0.75 : metrics.PadX, metrics.PadY, metrics.PadX * 0.75, metrics.PadY),
            CornerRadius = new CornerRadius(Fluent.ControlRadius),
            BorderThickness = new Thickness(Fluent.Hairline),
            MinHeight = metrics.Square,
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        var tip = Str.Shell_ViewBar_PickTip.Format(("name", shown.Name));
        ToolTipService.SetToolTip(button, tip);
        AutomationProperties.SetName(button, shown.Name);
        AutomationProperties.SetHelpText(button, tip);

        HoverFill(button, shown: true);
        OpensMenu(button, look);
        return button;
    }

    /// <summary>
    /// [S-918] / [S-5401] The overflow chip's and the drop-down's press: its own press and release,
    /// not the bar's capture — it is never dragged, and the surface underneath must not start a
    /// rubber band from it. In edit mode it takes the keyboard like a chip; Enter or Space opens it.
    /// </summary>
    private void OpensMenu(Border button, ViewBarLook look)
    {
        if (look.Editing)
        {
            button.IsTabStop = true;
            button.UseSystemFocusVisuals = true;
            button.KeyDown += (_, e) => e.Handled = OnKey(button, null, e.Key);
        }

        button.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(button).Properties.IsLeftButtonPressed)
            {
                _menuPressed = true;
                e.Handled = true;
            }
        };
        button.PointerReleased += (_, e) =>
        {
            if (_menuPressed)
            {
                _menuPressed = false;
                e.Handled = true;
                OpenMenu(button);
            }
        };
        button.PointerExited += (_, _) => _menuPressed = false;
    }

    private void OpenMenu(Border button)
    {
        if (_menuViews.Count > 0)
        {
            _owner.ShowViewMenu(button, _menuViews);
        }
    }

    /// <summary>
    /// [S-918] / [S-5402] The bar's menu: each view listed, its icon in its colour, in bar order (the
    /// default view's name semi-bold); a click shows it here. The overflow chip lists the views left
    /// off the bar, which a pick brings onto it ([S-917]); the drop-down lists every view, the shown
    /// one ticked. Capped at <paramref name="maxHeight"/> so a long list scrolls inside it; a typed
    /// letter moves to the next view starting with it ([S-5403]).
    /// </summary>
    internal static MenuFlyout ViewMenu(IReadOnlyList<ViewBarItem> views, Action<string> show, double maxHeight)
    {
        ArgumentNullException.ThrowIfNull(views);
        ArgumentNullException.ThrowIfNull(show);

        var menu = new MenuFlyout { MenuFlyoutPresenterStyle = MenuStyle(maxHeight) };
        var ticks = views.Any(v => v.Shown);
        var names = views.Select(v => v.Name).ToList();
        foreach (var view in views)
        {
            MenuFlyoutItem item = ticks
                ? new ToggleMenuFlyoutItem { IsChecked = view.Shown }
                : new MenuFlyoutItem();
            item.Text = view.Name;
            item.Icon = ViewMark.Icon(view.Icon, view.Color);
            if (view.IsDefault)
            {
                item.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
            }

            var id = view.Id;
            item.Click += (_, _) => show(id);
            item.CharacterReceived += (_, e) =>
            {
                var next = ViewBarRules.TypeAhead(names, menu.Items.IndexOf(item), e.Character.ToString());
                if (next >= 0 && menu.Items[next] is Control target)
                {
                    e.Handled = target.Focus(FocusState.Keyboard);
                }
            };
            menu.Items.Add(item);
        }

        // [S-5402] A drop-down opens on the shown view, as a combo box does.
        if (ticks)
        {
            menu.Opened += (_, _) =>
            {
                if (menu.Items.OfType<ToggleMenuFlyoutItem>().FirstOrDefault(i => i.IsChecked) is { } current)
                {
                    current.Focus(FocusState.Programmatic);
                    current.StartBringIntoView();
                }
            };
        }

        return menu;
    }

    /// <summary>[S-918] The menu opens from its button into the display (<see cref="ViewBarRules.MenuSide"/>).</summary>
    internal static FlyoutPlacementMode MenuPlacement(ViewBarMenuSide side) => side switch
    {
        ViewBarMenuSide.Below => FlyoutPlacementMode.Bottom,
        ViewBarMenuSide.Left => FlyoutPlacementMode.Left,
        ViewBarMenuSide.Right => FlyoutPlacementMode.Right,
        _ => FlyoutPlacementMode.Top,
    };

    /// <summary>
    /// [S-918] A menu presenter no taller than <paramref name="maxHeight"/>: its list scrolls past that.
    /// Never below a few lines, so a cramped display still gets a usable menu (which then flips to fit).
    /// </summary>
    internal static Style MenuStyle(double maxHeight) => new(typeof(MenuFlyoutPresenter))
    {
        Setters =
        {
            new Setter(FrameworkElement.MaxHeightProperty, double.IsFinite(maxHeight) ? Math.Max(4 * Fluent.Space6, maxHeight) : double.PositiveInfinity),
        },
    };

    /// <summary>
    /// The shown view is a raised control on the bar, the rest are subtle until the pointer is over
    /// them — the way a Windows 11 selector reads, and the same as the switcher widget.
    /// </summary>
    private static void HoverFill(Border element, bool shown)
    {
        Brush Rest() => ThemeBrush.Of(shown ? "ControlFillColorDefaultBrush" : "SubtleFillColorTransparentBrush", Colors.Transparent);

        element.Background = Rest();
        element.BorderBrush = shown
            ? ThemeBrush.Of("ControlStrokeColorDefaultBrush", Colors.Transparent)
            : new SolidColorBrush(Colors.Transparent);
        element.PointerEntered += (_, _) => element.Background = ThemeBrush.Of(shown ? "ControlFillColorSecondaryBrush" : "SubtleFillColorSecondaryBrush", Colors.Transparent);
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
        // [S-917] The chips drawn, in their slots: a chip only ever lands on a slot it can see.
        _orderAtPress = [.. Chips().Select(c => (string)c.Tag)];

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

        // The chips sit together between the grip (edit mode) and the overflow chip ([S-918]).
        _panel.Children.Move((uint)_panel.Children.IndexOf(chips[from]), (uint)_panel.Children.IndexOf(chips[to]));
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

    /// <summary>
    /// [S-1979] A key on a focused chip in edit mode; <paramref name="viewId"/> is null on the
    /// overflow chip ([S-918]), which the arrows reach last and Enter opens.
    /// </summary>
    private bool OnKey(Border self, string? viewId, Windows.System.VirtualKey key)
    {
        if (key is Windows.System.VirtualKey.Enter or Windows.System.VirtualKey.Space)
        {
            if (viewId is null)
            {
                OpenMenu(self);
                return true;
            }

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

        var stops = Chips().ToList();
        if (_menuButton is { } menuButton)
        {
            stops.Add(menuButton);
        }

        var step = ListKeys.Decide(widgetKey, stops.IndexOf(self), stops.Count);
        if (step.Command == ListCommand.Focus && step.Index >= 0)
        {
            stops[step.Index].Focus(FocusState.Keyboard);
        }

        return true;
    }

    /// <summary>[S-1979] Put the keyboard on the shown view's chip (the surface's F6); false when the bar has none to focus.</summary>
    internal bool FocusFirstChip()
    {
        // [S-5401] A drop-down's one stop is its button.
        if (_look?.Style == ViewBarStyle.Dropdown)
        {
            return _menuButton?.Focus(FocusState.Keyboard) == true;
        }

        var chips = Chips().ToList();
        var shown = chips.FindIndex(c => _items.Any(i => i.Shown && i.Id == (string)c.Tag));
        return chips.Count > 0 && chips[Math.Max(0, shown)].Focus(FocusState.Keyboard);
    }

    private IEnumerable<Border> Chips() => _panel.Children.OfType<Border>().Where(b => b.Tag is string);

    private Border? Chip(string viewId) => Chips().FirstOrDefault(c => (string)c.Tag == viewId);
}
