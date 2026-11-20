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
using Themia.Core.Shell;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;
using Windows.Foundation;

namespace Themia.App.Shell;

/// <summary>One chip on the view bar.</summary>
/// <param name="IsDefault">The view new displays start on ([S-847]); its name is drawn bold, as in the original.</param>
/// <param name="Color">[S-3812] The view's own colour for its icon or initials (<c>#RRGGBB</c>), or null for the theme's.</param>
internal sealed record ViewBarItem(string Id, string Name, string? Icon, bool Shown, bool IsDefault, string? Color = null);

/// <summary>How the view bar is drawn: its size, [S-5405] labels, direction, edit-mode state and [S-5400] type.</summary>
internal sealed record ViewBarLook(ViewBarSize Size, ViewBarLabels Labels, bool Vertical, bool Editing, bool Preview, ViewBarType Type = ViewBarType.Inline)
{
    /// <summary>[S-5405] Icon only: a chip is one square, its name in its tooltip.</summary>
    public bool IconOnly => Labels == ViewBarLabels.IconOnly;
}

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
    /// [S-918] / [S-5402] The overflow chip or the select button was clicked: list
    /// <paramref name="views"/> (the ones that did not fit, or every view on a select bar), labelled
    /// as the bar is ([S-5405]).
    /// </summary>
    void ShowViewMenu(FrameworkElement button, IReadOnlyList<ViewBarItem> views, ViewBarLabels labels);
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
    /// ([S-918]), or every view on a select bar.
    /// </summary>
    private IReadOnlyList<ViewBarItem> _menuViews = [];

    /// <summary>The overflow chip while the inline bar has one, or the select button.</summary>
    private Border? _menuButton;

    private bool _menuPressed;

    /// <summary>
    /// [S-13400] The buttons of the styles that show the shown view alone (Previous, its label, Next; the one
    /// button), by name, so a rebuild an Enter caused gives the focus back to the same one.
    /// </summary>
    private readonly Dictionary<string, Border> _buttons = new(StringComparer.Ordinal);

    /// <summary>The button pressed and not yet let go: a click lands only on the one it started on.</summary>
    private Border? _clickPressed;

    /// <summary>[S-13404] The wheel's part of a notch kept from the last event (<see cref="ViewBarRules.Wheel"/>).</summary>
    private double _wheelKept;

    /// <summary>[S-918] / [S-5401] The overflow chip or the select button; null when every chip fits.</summary>
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

    /// <summary>[S-13400] The button (<see cref="_buttons"/>) that takes the focus back after the rebuild a key caused.</summary>
    private string? _refocusButton;

    public ViewBar(IViewBarOwner owner)
    {
        _owner = owner;

        // Transparent, not null: the gaps between chips are still the bar, not the desktop.
        Background = Family.Brush(ColourRole.Clear);
        HorizontalAlignment = HorizontalAlignment.Left;
        VerticalAlignment = VerticalAlignment.Top;
        Children.Add(_fill);
        Children.Add(_panel);
        Children.Add(_stroke);

        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        PointerCaptureLost += OnPointerCaptureLost;
        PointerWheelChanged += OnWheel;
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
        // [S-917] So is the room: a resize, a DPI change or the edit bar appearing refits the chips.
        var signature = $"{look}|{string.Join("|", items)}|{FamilyType.Body.FontSize}|{Strings.Current}|{Math.Floor(room)}";
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

    /// <summary>
    /// [S-911] The sizes of one step, M, L and XL, on the family's tokens: Caption, Body and Title
    /// (regular) words, the 16 icon (Windows Settings' 20 icon box beside XL's 20 words), paddings on
    /// the scale, a chip at least its square tall.
    /// </summary>
    private readonly record struct Metrics(TypeStep Type, double Icon, double PadX, double PadY, double BarPad, double Square);

    private static Metrics MetricsFor(ViewBarSize size) => size switch
    {
        ViewBarSize.Large => new(FamilyType.Body, FamilyShape.Icon, FamilySpace.S3, FamilySpace.S1, FamilySpace.S1, ShellMetrics.ViewBar.SquareL),
        ViewBarSize.ExtraLarge => new(FamilyType.Regular(FamilyType.Title), ShellMetrics.IconBox, FamilySpace.S4, FamilySpace.S1, FamilySpace.S2, ShellMetrics.ViewBar.SquareXl),
        _ => new(FamilyType.Caption, FamilyShape.Icon, FamilySpace.S2, FamilySpace.S1, FamilySpace.S1, ShellMetrics.ViewBar.SquareM),
    };

    /// <summary>A step's words, semibold (the initials, the default view's name).</summary>
    private static TypeStep Strong(TypeStep step) => step with { SemiBold = true };

    /// <summary>
    /// The bar reads as a widget: the card's own fill, colour and opacity, and its stroke. In edit
    /// mode on a display where the user hid it, it is drawn faded with a dashed edge ([S-907]).
    /// </summary>
    private void PaintSurface(ViewBarLook look, DesignState design)
    {
        var metrics = MetricsFor(look.Size);
        var radius = Fluent.ControlRadius + metrics.BarPad;

        _fill.CornerRadius = new CornerRadius(radius);
        // [S-6563] The app-wide background: its colour (Themia's card for Default, unfrosted: the bar
        // has no backdrop, [S-2110]), at its opacity but never under 20 %, so the bar can always be
        // found (a transparent design draws it at the floor).
        var fill = DesignLook.Card(design.Background ?? new FillChoice(), DesignApplier.IsDark, frosted: false);
        _fill.Background = (fill.Colour is { } c ? DesignApplier.UserBrush(HexColor.From(c.R, c.G, c.B)) : null)
            ?? ShellBrush.Of(ShellRole.CardBase);
        _fill.Opacity = Math.Clamp(fill.Opacity, 0.2, 1);

        _stroke.RadiusX = _stroke.RadiusY = Math.Max(0, radius - (Fluent.Hairline / 2));
        _stroke.Stroke = DesignApplier.UserBrush(design.BorderColor)
            ?? (look.Preview ? Family.Brush(ColourRole.TextTertiary) : ShellBrush.Of(ShellRole.CardStroke));
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
        _buttons.Clear();
        _clickPressed = null;
        _panel.Orientation = look.Vertical ? Orientation.Vertical : Orientation.Horizontal;
        // [S-13401] The dots' slots carry their own room either side, so the dots sit evenly.
        _panel.Spacing = look.Type == ViewBarType.Dots ? 0 : Fluent.Space1;
        _panel.Margin = new Thickness(metrics.BarPad);

        // [S-917] What the bar spends besides chips: its padding at both ends, and the grip with its gap.
        var fixedLength = 2 * metrics.BarPad;
        if (look.Editing)
        {
            var grip = BuildGrip(look, metrics);
            _panel.Children.Add(grip);
            fixedLength += Length(grip, look.Vertical) + _panel.Spacing;
        }

        if (look.Type == ViewBarType.Select)
        {
            // [S-5401] One button for the whole bar; its menu lists every view on it ([S-5402]).
            _menuViews = [.. ViewBarRules.MenuViews(look.Type, _items.Count, new ViewBarFit([], Overflow: false)).Select(i => _items[i])];
            _menuButton = BuildSelect(look, metrics, _room - fixedLength);
            _panel.Children.Add(_menuButton);
            FinishRebuild(look);
            return;
        }

        if (look.Type is ViewBarType.Arrows or ViewBarType.Cycle)
        {
            // [S-13402] [S-13403] The shown view alone, stepped through; the menu lists every view on the bar.
            _menuViews = [.. ViewBarRules.MenuViews(look.Type, _items.Count, new ViewBarFit([], Overflow: false)).Select(i => _items[i])];
            if (look.Type == ViewBarType.Arrows)
            {
                BuildArrows(look, metrics, _room - fixedLength);
            }
            else
            {
                _menuButton = BuildCycle(look, metrics, _room - fixedLength);
                _panel.Children.Add(_menuButton);
            }

            FinishRebuild(look);
            return;
        }

        var anyColour = _items.Any(i => DesignApplier.UserBrush(i.Color) is not null);
        var chips = _items.Select(item => look.Type == ViewBarType.Dots ? BuildDotChip(item, look, metrics) : BuildChip(item, look, metrics, anyColour)).ToList();
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

        _menuViews = [.. ViewBarRules.MenuViews(look.Type, _items.Count, fit).Select(i => _items[i])];
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

        if (_refocusButton is { } name && _buttons.TryGetValue(name, out var button))
        {
            _refocusButton = null;
            button.Loaded += (_, _) => button.Focus(FocusState.Keyboard);
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
            Background = Family.Brush(ColourRole.Clear),
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
        var fill = item.Shown ? ShellRoles.Key(ShellRole.AccentText) : brush;
        if (item.Icon is { } glyph)
        {
            var icon = Fluent.Icon(glyph, fill, metrics.Icon);
            if (tint is not null)
            {
                icon.Foreground = tint;
            }

            return icon;
        }

        var initials = Fluent.Text(Strong(metrics.Type), fill);
        initials.Text = ViewListRules.Initials(item.Name);
        initials.TextTrimming = TextTrimming.None;
        initials.VerticalAlignment = VerticalAlignment.Center;
        if (tint is not null)
        {
            initials.Foreground = tint;
        }

        return initials;
    }

    /// <summary>
    /// [S-5406] Text only: the dot in the view's colour before its name, the cue its icon carried,
    /// or the dot's room kept empty on a column so the names start on one line; null for neither.
    /// </summary>
    private static FrameworkElement? BuildDot(ViewBarItem item, Metrics metrics, ViewBarLabels labels, bool vertical, bool anyColour)
    {
        var tint = DesignApplier.UserBrush(item.Color);
        var dot = ViewBarRules.Dot(labels, vertical, tint is not null, anyColour);
        if (dot == ViewBarDot.None)
        {
            return null;
        }

        var size = Math.Round(metrics.Icon / 2);
        return new Ellipse
        {
            Width = size,
            Height = size,
            Fill = dot == ViewBarDot.Drawn ? tint : null,
            VerticalAlignment = VerticalAlignment.Center,
        };
    }

    /// <summary>
    /// [S-911] [S-5405] A view's name on the bar, bold for the default view. Text only, its colour is
    /// the dot's ([S-5406]); the name stays in the theme's text, so it reads on any card.
    /// </summary>
    private static TextBlock BuildName(ViewBarItem item, Metrics metrics, string brush)
    {
        var name = Fluent.Text(item.IsDefault ? Strong(metrics.Type) : metrics.Type, brush);
        name.Text = item.Name;
        name.VerticalAlignment = VerticalAlignment.Center;
        name.MaxWidth = ShellMetrics.ViewBar.NameMaxWidth;
        return name;
    }

    private Border BuildChip(ViewBarItem item, ViewBarLook look, Metrics metrics, bool anyColour)
    {
        var brush = item.Shown ? "TextFillColorPrimaryBrush" : "TextFillColorSecondaryBrush";
        var parts = new List<FrameworkElement>();
        if (ViewBarRules.ShowsMark(look.Labels))
        {
            parts.Add(BuildMark(item, metrics, brush));
        }
        else if (BuildDot(item, metrics, look.Labels, look.Vertical, anyColour) is { } dot)
        {
            parts.Add(dot);
        }

        if (ViewBarRules.ShowsName(look.Labels))
        {
            parts.Add(BuildName(item, metrics, brush));
        }

        FrameworkElement content = parts[0];
        if (parts.Count > 1)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = FamilySpace.GlyphGap };
            foreach (var part in parts)
            {
                row.Children.Add(part);
            }

            content = row;
        }

        content.HorizontalAlignment = look.Vertical && !look.IconOnly ? HorizontalAlignment.Left : HorizontalAlignment.Center;
        content.VerticalAlignment = VerticalAlignment.Center;
        // [S-13405] A row's chips keep the pill's room under their words, every chip alike, so the names stay on one line.
        var foot = look.Vertical ? 0 : ShellMetrics.Bar.PillHeight;
        content.Margin = look.IconOnly ? new Thickness(0, 0, 0, foot) : new Thickness(metrics.PadX, metrics.PadY, metrics.PadX, metrics.PadY + foot);

        // [S-13405] The shown view's chip carries the accent pill Windows' selectors mark the picked item with: at
        // its foot on a row, at its start on a column (the edit bar's open entry has the same, [S-2321]).
        var face = new Grid();
        face.Children.Add(content);
        if (item.Shown)
        {
            face.Children.Add(Pill(look.Vertical));
        }

        var chip = new Border
        {
            Child = face,
            Tag = item.Id,
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

        // [S-5405] The name as a tooltip only where the chip does not say it; the default view's note
        // and, in edit mode, what a click, a drag and a right-click do, always.
        var note = item.IsDefault ? Str.Shell_ViewBar_DefaultTip.Format(("name", item.Name)) : null;
        var tip = look.Editing
            ? Str.Shell_ViewBar_EditTip.Format(("name", note ?? item.Name))
            : ViewBarRules.ChipTip(look.Labels, item.Name, note);
        ToolTipService.SetToolTip(chip, tip);
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
    /// [S-13405] The accent pill of the shown view: <see cref="ShellMetrics.Bar.PillWidth"/> across and
    /// <see cref="ShellMetrics.Bar.PillHeight"/> thick, centred at the chip's foot on a row, at its start on a column.
    /// </summary>
    private static Border Pill(bool vertical) => new()
    {
        Width = vertical ? ShellMetrics.Bar.PillHeight : ShellMetrics.Bar.PillWidth,
        Height = vertical ? ShellMetrics.Bar.PillWidth : ShellMetrics.Bar.PillHeight,
        CornerRadius = new CornerRadius(ShellMetrics.Bar.PillRadius),
        Background = Family.Brush(ColourRole.Accent),
        HorizontalAlignment = vertical ? HorizontalAlignment.Left : HorizontalAlignment.Center,
        VerticalAlignment = vertical ? VerticalAlignment.Center : VerticalAlignment.Bottom,
        Margin = vertical ? new Thickness(Fluent.Hairline, 0, 0, 0) : new Thickness(0, 0, 0, Fluent.Hairline),
        IsHitTestVisible = false,
    };

    /// <summary>[S-13401] The dots style's dot at the bar's size.</summary>
    private static double DotSize(ViewBarSize size) => size switch
    {
        ViewBarSize.Large => ShellMetrics.ViewBar.DotL,
        ViewBarSize.ExtraLarge => ShellMetrics.ViewBar.DotXl,
        _ => ShellMetrics.ViewBar.DotM,
    };

    /// <summary>
    /// [S-13401] One view as a dot, the pager's look: a round dot in the view's colour (the tertiary text where it
    /// has none), the shown view a pill three dots long in its colour or the accent. Its slot is the step's square
    /// across and the dot plus room either side along, so it is easy to hit; its name is its tooltip. A chip like
    /// any other: a click shows its view, in edit mode it is dragged to reorder ([S-914]) and right-clicked for the
    /// chip menu ([S-915]); outside edit mode a right-click lists the views by name.
    /// </summary>
    private Border BuildDotChip(ViewBarItem item, ViewBarLook look, Metrics metrics)
    {
        var dot = DotSize(look.Size);
        var length = item.Shown ? dot * ShellMetrics.ViewBar.DotPill : dot;
        var tint = DesignApplier.UserBrush(item.Color);
        var round = dot / 2;
        var mark = new Border
        {
            Width = look.Vertical ? dot : length,
            Height = look.Vertical ? length : dot,
            CornerRadius = new CornerRadius(round),
            Background = tint ?? Family.Brush(item.Shown ? ColourRole.Accent : ColourRole.TextTertiary),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false,
        };

        var along = length + (metrics.Square / 2);
        var chip = new Border
        {
            Child = mark,
            Tag = item.Id,
            CornerRadius = new CornerRadius(Fluent.ControlRadius),
            BorderThickness = new Thickness(Fluent.Hairline),
            Width = look.Vertical ? metrics.Square : along,
            Height = look.Vertical ? along : metrics.Square,
        };

        var note = item.IsDefault ? Str.Shell_ViewBar_DefaultTip.Format(("name", item.Name)) : null;
        ToolTipService.SetToolTip(chip, look.Editing ? Str.Shell_ViewBar_EditTip.Format(("name", note ?? item.Name)) : note ?? item.Name);
        AutomationProperties.SetName(chip, item.Name);
        AutomationProperties.SetItemStatus(chip, item.Shown ? Str.Shell_ViewBar_Showing.Text() : string.Empty);
        HoverFill(chip, shown: false);

        if (look.Editing)
        {
            chip.IsTabStop = true;
            chip.UseSystemFocusVisuals = true;
            chip.KeyDown += (_, e) => e.Handled = OnKey(chip, item.Id, e.Key);
        }

        chip.PointerPressed += (_, e) => OnPressed(e, item.Id);
        chip.RightTapped += (_, e) =>
        {
            e.Handled = true;
            if (_look?.Editing == true)
            {
                _owner.ShowChipMenu(chip, item.Id, e);
            }
            else
            {
                _owner.ShowViewMenu(chip, _items, ViewBarLabels.IconAndText);
            }
        };

        return chip;
    }

    /// <summary>
    /// The shown view's face on a one-view button (select, the arrows' label, the one button): its mark, or its
    /// colour dot with text only, and its name, as the labels say ([S-5405]); the name cut to what the bar has
    /// left after <paramref name="besides"/> (the button's own glyphs and paddings).
    /// </summary>
    private static StackPanel Face(ViewBarItem shown, ViewBarLook look, Metrics metrics, double room, double besides)
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = FamilySpace.GlyphGap,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
        };

        if (ViewBarRules.ShowsMark(look.Labels))
        {
            row.Children.Add(BuildMark(shown, metrics, "TextFillColorPrimaryBrush"));
        }
        else if (BuildDot(shown, metrics, look.Labels, vertical: false, anyColour: false) is { } dot)
        {
            row.Children.Add(dot);
        }

        if (ViewBarRules.ShowsName(look.Labels))
        {
            var name = BuildName(shown, metrics, "TextFillColorPrimaryBrush");
            var spare = room - besides - metrics.Icon - row.Spacing;
            name.MaxWidth = double.IsFinite(spare) ? Math.Clamp(spare, metrics.Icon, ShellMetrics.ViewBar.NameMaxWidth) : ShellMetrics.ViewBar.NameMaxWidth;
            row.Children.Add(name);
        }

        return row;
    }

    /// <summary>The shown view, or "Views" with the shared view glyph on a display showing a view the bar does not offer.</summary>
    private ViewBarItem ShownItem()
        => _items.FirstOrDefault(i => i.Shown)
            ?? new ViewBarItem(string.Empty, Str.Shell_ViewBar_Name.Text(), Glyphs.View, Shown: true, IsDefault: false);

    /// <summary>The view <paramref name="delta"/> steps away on the bar ([S-13404]); null when there is nowhere to go.</summary>
    private ViewBarItem? StepTarget(int delta)
    {
        var id = ViewBarRules.Step([.. _items.Select(i => i.Id)], _items.FirstOrDefault(i => i.Shown)?.Id, delta);
        return id is null ? null : _items.FirstOrDefault(i => i.Id == id);
    }

    /// <summary>[S-13404] Show the view <paramref name="delta"/> steps away on the bar, round from the last to the first.</summary>
    private void Step(int delta)
    {
        if (StepTarget(delta) is { } target)
        {
            _owner.SwitchTo(target.Id);
        }
    }

    /// <summary>
    /// [S-13402] Previous / Next: the shown view between two arrow buttons, a row at every anchor. The arrows are
    /// subtle squares (the chevrons mirror in a right-to-left language), their tooltips naming the view they go
    /// to; the label is the raised control and opens the menu of every view on the bar, as the select bar's
    /// button does ([S-5402]), to jump straight to one.
    /// </summary>
    private void BuildArrows(ViewBarLook look, Metrics metrics, double room)
    {
        var shown = ShownItem();
        var previous = Arrow(look, metrics, "previous", Glyphs.ChevronLeft, -1);
        var next = Arrow(look, metrics, "next", Glyphs.ChevronRight, 1);

        var besides = (2 * metrics.Square) + (2 * _panel.Spacing) + (2 * metrics.PadX);
        var label = new Border
        {
            Child = Face(shown, look, metrics, room, besides),
            Padding = look.IconOnly ? new Thickness(0) : new Thickness(metrics.PadX, metrics.PadY, metrics.PadX, metrics.PadY),
            CornerRadius = new CornerRadius(Fluent.ControlRadius),
            BorderThickness = new Thickness(Fluent.Hairline),
            MinHeight = metrics.Square,
            MinWidth = metrics.Square,
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        if (look.IconOnly)
        {
            label.Height = metrics.Square;
        }

        var tip = Str.Shell_ViewBar_PickTip.Format(("name", shown.Name));
        ToolTipService.SetToolTip(label, tip);
        AutomationProperties.SetName(label, shown.Name);
        AutomationProperties.SetHelpText(label, tip);
        HoverFill(label, shown: true);
        Clicks(label, look, "label", _ => OpenMenu(label));
        _menuButton = label;
        _buttons["label"] = label;

        _panel.Children.Add(previous);
        _panel.Children.Add(label);
        _panel.Children.Add(next);
    }

    /// <summary>[S-13402] One of the arrows: a subtle square with its chevron, stepping <paramref name="delta"/> views.</summary>
    private Border Arrow(ViewBarLook look, Metrics metrics, string name, string glyph, int delta)
    {
        var button = new Border
        {
            Child = Fluent.Icon(glyph, "TextFillColorSecondaryBrush", metrics.Icon - FamilySpace.S1),
            CornerRadius = new CornerRadius(Fluent.ControlRadius),
            BorderThickness = new Thickness(Fluent.Hairline),
            Width = metrics.Square,
            Height = metrics.Square,
        };

        var target = StepTarget(delta) ?? ShownItem();
        ToolTipService.SetToolTip(button, (delta < 0 ? Str.Shell_ViewBar_PreviousTip : Str.Shell_ViewBar_NextTip).Format(("name", target.Name)));
        AutomationProperties.SetName(button, delta < 0 ? Str.Shell_ViewBar_Previous.Text() : Str.Shell_ViewBar_Next.Text());
        HoverFill(button, shown: false);
        Clicks(button, look, name, _ => Step(delta));
        _buttons[name] = button;
        return button;
    }

    /// <summary>
    /// [S-13403] The one button: the shown view, raised, labelled as the bar is, with a chevron pointing on. A
    /// click shows the next view on the bar, after the last the first; Shift+click the previous. Its tooltip names
    /// the view a click goes to; a right-click opens the menu of every view on the bar to jump straight to one.
    /// </summary>
    private Border BuildCycle(ViewBarLook look, Metrics metrics, double room)
    {
        var shown = ShownItem();
        var chevronSize = ShellMetrics.Chevron;
        // Where the shown view stands among the bar's ("2/4"), in the tertiary words: there is more to step to.
        var position = ViewBarRules.Position([.. _items.Select(i => i.Id)], _items.FirstOrDefault(i => i.Shown)?.Id);
        var count = position is { } at
            ? LocaleFormat.Integer(at.Index + 1) + "/" + LocaleFormat.Integer(at.Count)
            : null;
        var counter = Fluent.Text(FamilyType.Caption, "TextFillColorTertiaryBrush");
        counter.Text = count ?? string.Empty;
        counter.VerticalAlignment = VerticalAlignment.Center;
        counter.TextTrimming = TextTrimming.None;
        counter.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var counterLength = count is null ? 0 : counter.DesiredSize.Width + FamilySpace.GlyphGap;

        var face = Face(shown, look, metrics, room, (2 * metrics.PadX) + chevronSize + FamilySpace.GlyphGap + counterLength);
        if (count is not null)
        {
            face.Children.Add(counter);
        }

        var chevron = Fluent.Icon(Glyphs.ChevronRight, "TextFillColorSecondaryBrush", chevronSize);
        chevron.VerticalAlignment = VerticalAlignment.Center;
        face.Children.Add(chevron);

        var button = new Border
        {
            Child = face,
            Padding = new Thickness(look.IconOnly ? metrics.PadX - FamilySpace.S1 : metrics.PadX, metrics.PadY, metrics.PadX - FamilySpace.S1, metrics.PadY),
            CornerRadius = new CornerRadius(Fluent.ControlRadius),
            BorderThickness = new Thickness(Fluent.Hairline),
            MinHeight = metrics.Square,
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        var next = StepTarget(1) ?? shown;
        var tip = Str.Shell_ViewBar_CycleTip.Format(("name", shown.Name), ("next", next.Name));
        ToolTipService.SetToolTip(button, tip);
        AutomationProperties.SetName(button, shown.Name);
        AutomationProperties.SetHelpText(button, tip);
        HoverFill(button, shown: true);
        Clicks(button, look, "cycle", back => Step(back ? -1 : 1));
        _buttons["cycle"] = button;
        return button;
    }

    /// <summary>
    /// [S-13402] [S-13403] A step button's press: its own press and release (never the bar's capture or drag, and
    /// the surface underneath starts no rubber band from it), <paramref name="click"/> told whether Shift was
    /// held. A right-click opens the menu of every view on the bar. In edit mode it takes the keyboard: Enter or
    /// Space clicks it, the arrow keys step back (Left, Up) and on (Right, Down), and the focus comes back to it
    /// after the switch redraws the bar.
    /// </summary>
    private void Clicks(Border button, ViewBarLook look, string name, Action<bool> click)
    {
        if (look.Editing)
        {
            button.IsTabStop = true;
            button.UseSystemFocusVisuals = true;
            button.KeyDown += (_, e) =>
            {
                var delta = e.Key switch
                {
                    Windows.System.VirtualKey.Left or Windows.System.VirtualKey.Up => -1,
                    Windows.System.VirtualKey.Right or Windows.System.VirtualKey.Down => 1,
                    _ => 0,
                };

                if (e.Key is Windows.System.VirtualKey.Enter or Windows.System.VirtualKey.Space)
                {
                    _refocusButton = name;
                    click(false);
                    e.Handled = true;
                }
                else if (delta != 0)
                {
                    _refocusButton = name;
                    Step(delta);
                    e.Handled = true;
                }
            };
        }

        button.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(button).Properties.IsLeftButtonPressed)
            {
                _clickPressed = button;
                e.Handled = true;
            }
        };
        button.PointerReleased += (_, e) =>
        {
            if (ReferenceEquals(_clickPressed, button))
            {
                _clickPressed = null;
                e.Handled = true;
                click((e.KeyModifiers & Windows.System.VirtualKeyModifiers.Shift) != 0);
            }
        };
        button.PointerExited += (_, _) =>
        {
            if (ReferenceEquals(_clickPressed, button))
            {
                _clickPressed = null;
            }
        };
        button.RightTapped += (_, e) =>
        {
            e.Handled = true;
            if (_menuViews.Count > 0)
            {
                _owner.ShowViewMenu(button, _menuViews, look.Labels);
            }
        };
    }

    /// <summary>
    /// [S-13404] The wheel over the bar, every style, in and out of edit mode: a notch towards the user shows the
    /// next view on the bar, away the previous, round from the last to the first; a horizontal wheel steps right
    /// to Next. Handled, so nothing under the bar scrolls; not during a press on a chip or the grip.
    /// </summary>
    private void OnWheel(object sender, PointerRoutedEventArgs e)
    {
        if (_pressing)
        {
            return;
        }

        var properties = e.GetCurrentPoint(this).Properties;
        var delta = properties.IsHorizontalMouseWheel ? -properties.MouseWheelDelta : properties.MouseWheelDelta;
        var (steps, kept) = ViewBarRules.Wheel(_wheelKept, delta);
        _wheelKept = kept;
        e.Handled = true;
        if (steps != 0)
        {
            Step(steps);
        }
    }


    /// <summary>
    /// [S-5401] The select bar: one raised button with the shown view alone, labelled as the bar is
    /// ([S-5405]: its icon in its colour, its name, or both; text only with the colour dot), and a
    /// chevron, opening the menu of every view on the bar ([S-5402]). A display showing a view the
    /// bar does not offer reads "Views" with the shared view glyph.
    /// </summary>
    /// <param name="room">The length left for the button along the bar, so a long name is cut rather than running off the display.</param>
    private Border BuildSelect(ViewBarLook look, Metrics metrics, double room)
    {
        var shown = ShownItem();
        var row = Face(shown, look, metrics, room, (2 * metrics.PadX) + metrics.Icon + FamilySpace.GlyphGap);

        var chevron = Fluent.Icon(Glyphs.ChevronDown, "TextFillColorSecondaryBrush", ShellMetrics.Chevron);
        chevron.VerticalAlignment = VerticalAlignment.Center;
        row.Children.Add(chevron);

        var button = new Border
        {
            Child = row,
            Padding = new Thickness(look.IconOnly ? metrics.PadX - FamilySpace.S1 : metrics.PadX, metrics.PadY, metrics.PadX - FamilySpace.S1, metrics.PadY),
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
    /// [S-918] / [S-5401] The overflow chip's and the select button's press: its own press and release,
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
            _owner.ShowViewMenu(button, _menuViews, _look?.Labels ?? ViewBarLabels.IconAndText);
        }
    }

    /// <summary>
    /// [S-918] / [S-5402] The bar's menu: each view listed, its icon in its colour, in bar order (the
    /// default view's name semi-bold); a click shows it here. The overflow chip lists the views left
    /// off the bar, which a pick brings onto it ([S-917]); the select bar lists every view, the shown
    /// one marked by the choice's dot in its one leading slot ([S-12952], <see cref="MenuMarks.Slot"/>: no second
    /// column for a tick). Every row has the view's name; text only ([S-5405]) puts the colour dot where the
    /// icon was ([S-5406]). Capped at <paramref name="maxHeight"/> so a long list scrolls inside it; a
    /// typed letter moves to the next view starting with it ([S-5403]).
    /// </summary>
    internal static MenuFlyout ViewMenu(IReadOnlyList<ViewBarItem> views, ViewBarLabels labels, Action<string> show, double maxHeight)
    {
        ArgumentNullException.ThrowIfNull(views);
        ArgumentNullException.ThrowIfNull(show);

        var menu = new MenuFlyout { MenuFlyoutPresenterStyle = MenuStyle(maxHeight) };
        var ticks = views.Any(v => v.Shown);
        var names = views.Select(v => v.Name).ToList();
        MenuFlyoutItem? shown = null;
        foreach (var view in views)
        {
            var item = new MenuFlyoutItem { Text = view.Name };
            if (ticks && view.Shown)
            {
                // [S-12952] The shown view: the choice's dot in the slot its mark stands in on the others.
                item.Icon = new FontIcon { Glyph = MenuMarks.Slot(true, choice: true, null) };
                shown = item;
            }
            else if (ViewBarRules.ShowsMark(labels))
            {
                item.Icon = ViewMark.Icon(view.Icon, view.Color);
            }
            else if (DesignApplier.UserBrush(view.Color) is { } tint
                && ViewBarRules.Dot(labels, vertical: false, hasColour: true, anyHasColour: false) == ViewBarDot.Drawn)
            {
                item.Icon = ViewMark.Dot(tint, Math.Round(Fluent.IconSize / 2));
            }
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

        // [S-5402] A select bar's menu opens on the shown view, as a combo box does.
        if (shown is { } current)
        {
            menu.Opened += (_, _) =>
            {
                current.Focus(FocusState.Programmatic);
                current.StartBringIntoView();
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
            : Family.Brush(ColourRole.Clear);
        element.PointerEntered += (_, _) => element.Background = shown ? ShellBrush.Of(ShellRole.ControlHover) : Family.Brush(ColourRole.Subtle);
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
                lifted.Opacity = SelectionLook.Faded;
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
        // [S-5401] A select bar's one stop is its button; [S-13402] the arrows' is their label, [S-13403] the one button.
        if (_look is { } look && !ViewBarRules.HasChips(look.Type))
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
