using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Windows.ApplicationModel.DataTransfer;

namespace Themia.App.Widgets;

/// <summary>
/// A colour swatch: by default the original's block of colour — the whole tile in one colour or a
/// gradient, at an opacity, used as a backdrop or a coloured panel — or a palette you can copy
/// colours out of. [S-1500]
/// <para>
/// The <b>colours it shows are content</b> and are therefore literal: they are the user's, and a
/// theme brush would be the wrong answer by definition (except the default, which is the accent,
/// as the original's was). Everything the widget is made of — text, a chip's hairline, the fill
/// under the pointer — comes from named brushes, so the chrome follows light, dark and high contrast.
/// </para>
/// </summary>
internal sealed partial class ColorSwatchView : Grid, IWidgetView, IWidgetSizeAware, IWidgetKeys
{
    private const string Scope = "color-swatch";

    /// <summary>Round chips and tiles and the gap round the chip grid.</summary>
    private const double GridInset = Fluent.Space2;

    /// <summary>[S-1504] The block: the whole slot, edge to edge, no inset — the card clips its corners.</summary>
    private readonly Border _fill = new();

    private readonly Grid _columns = new();
    private readonly ScrollViewer _scroller;

    private readonly List<SwatchItemView> _items = [];

    private ColorSwatchConfig _config = new();
    private WidgetArea _area;
    private LinearGradientBrush? _gradient;

    /// <summary>What the palette was last built as, so a resize that changes nothing rebuilds nothing.</summary>
    private (SwatchPlan Plan, int Count)? _built;

    /// <summary>[S-1507] The palette entry the keys are on, or null.</summary>
    private int? _cursor;

    private bool _detached;

    public ColorSwatchView()
    {
        _scroller = new ScrollViewer
        {
            Content = _columns,
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            HorizontalScrollMode = ScrollMode.Disabled,
        };

        Children.Add(_fill);
        Children.Add(_scroller);
    }

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        // Nothing to tick: a swatch changes when it is edited, not when time passes.
        Strings.Changed += OnLanguage;
        ApplyConfig(config);
    }

    /// <summary>[S-1109] The copy tooltips follow the language.</summary>
    private void OnLanguage(Locale locale) => ApplyConfig(_config);

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as ColorSwatchConfig ?? new ColorSwatchConfig();

        // The colours or the palette changed, so the palette is rebuilt whatever the size is doing.
        _built = null;
        Lay();
    }

    /// <inheritdoc />
    public void ApplySize(WidgetArea area)
    {
        _area = area;
        Lay();
    }

    private void Lay()
    {
        if (_detached)
        {
            return;
        }

        var palette = _config.Mode == SwatchMode.Palette;
        _fill.Visibility = palette ? Visibility.Collapsed : Visibility.Visible;

        if (!palette)
        {
            _scroller.Visibility = Visibility.Collapsed;
            ClearItems();
            _built = null;
            _cursor = null;
            PaintBlock();
            return;
        }

        _fill.Background = null;
        _gradient = null;
        LayPalette();
    }

    // -----------------------------------------------------------------------------------------
    // Solid and gradient
    // -----------------------------------------------------------------------------------------

    /// <summary>
    /// [S-1501]..[S-1504] Paint the block. A solid colour is a brush at the widget's opacity (the
    /// default is the theme's own accent brush, so it follows an accent change in place); a gradient
    /// is two stops on an axis computed for the slot's actual size, as CSS draws it.
    /// </summary>
    private void PaintBlock()
    {
        var accentBrush = FluentLayout.Themed("AccentFillColorDefaultBrush", Colors.DodgerBlue);
        var accent = accentBrush is SolidColorBrush { Color: var a } ? new RgbColor(a.R, a.G, a.B) : new RgbColor(0x60, 0xCD, 0xFF);

        if (_config.Mode == SwatchMode.Solid)
        {
            _gradient = null;
            var fill = SwatchFill.Resolve(_config.Color, _config.Alpha, accent);

            _fill.Background = HexColor.IsOverridden(_config.Color)
                ? new SolidColorBrush(Windows.UI.Color.FromArgb(255, fill.Color.R, fill.Color.G, fill.Color.B))
                : accentBrush;

            // The opacity on the element rather than on the brush: the default brush is the theme's
            // shared one, and fading it would fade every accent in the app.
            _fill.Opacity = SwatchFill.ClampAlpha(fill.Alpha);
            return;
        }

        var start = SwatchFill.Resolve(_config.StartColor, _config.StartAlpha, accent);
        var end = SwatchFill.Resolve(_config.EndColor, _config.EndAlpha, accent);

        _gradient = new LinearGradientBrush { MappingMode = BrushMappingMode.Absolute };
        _gradient.GradientStops.Add(new GradientStop { Color = ToColor(start), Offset = 0 });
        _gradient.GradientStops.Add(new GradientStop { Color = ToColor(end), Offset = 1 });

        var axis = GradientAxis.For(_config.Angle, _area.Width, _area.Height);
        _gradient.StartPoint = new Windows.Foundation.Point(axis.StartX, axis.StartY);
        _gradient.EndPoint = new Windows.Foundation.Point(axis.EndX, axis.EndY);

        _fill.Opacity = 1;
        _fill.Background = _gradient;
    }

    private static Windows.UI.Color ToColor(FillColor fill) => Windows.UI.Color.FromArgb(fill.A, fill.Color.R, fill.Color.G, fill.Color.B);

    // -----------------------------------------------------------------------------------------
    // Palette
    // -----------------------------------------------------------------------------------------

    /// <summary>
    /// [S-1506] Lay the palette out by <see cref="SwatchLayout"/> for this size, and only rebuild
    /// when that changed something — a resize drag raises this on every frame, and tearing down and
    /// rebuilding forty rows per frame is how a widget layer starts costing a core.
    /// </summary>
    private void LayPalette()
    {
        var swatches = _config.EffectiveSwatches;

        if (swatches.Count == 0)
        {
            ClearItems();
            // [S-1862] An empty palette: the card shows its Set up button over this.
            _scroller.Visibility = Visibility.Collapsed;
            _built = null;
            _cursor = null;
            return;
        }

        _scroller.Visibility = Visibility.Visible;

        var listTop = WidgetLayout.ListInset.Top + WidgetLayout.ListInset.Bottom;
        var caption = (_config.ShowNames ? FluentType.Body.LineHeight : 0)
            + (_config.ShowValues ? FluentType.Caption.LineHeight : 0)
            + (_config.ShowNames || _config.ShowValues ? Fluent.Space1 : 0);

        var plan = SwatchLayout.Plan(_area.Width, _area.Height - listTop, swatches.Count, _config.ShowValues, caption);

        // Chips and tiles sit inside an inset of their own; re-fit them to it.
        if (plan.Arrangement == SwatchArrangement.Chips)
        {
            var (columns, side) = SwatchLayout.ChipGrid(_area.Width - (2 * GridInset), _area.Height - (2 * GridInset), swatches.Count);
            plan = plan with { Columns = columns, Cell = side };
        }
        else if (plan.Arrangement == SwatchArrangement.Tiles)
        {
            plan = SwatchLayout.TileGrid(_area.Width - (2 * WidgetLayout.ContentInset), _area.Height - (2 * WidgetLayout.ContentInset), swatches.Count, caption) is { } tiles
                ? plan with { Columns = tiles.Columns, Cell = tiles.Height }
                : SwatchLayout.Plan(_area.Width, _area.Height - listTop, swatches.Count, _config.ShowValues, double.PositiveInfinity);
        }

        if (_built is { } built && built.Plan == plan && built.Count == swatches.Count)
        {
            return;
        }

        _built = (plan, swatches.Count);
        ClearItems();

        switch (plan.Arrangement)
        {
            case SwatchArrangement.Chips:
                BuildGrid(swatches, plan, GridInset, (swatch, _) => SwatchItemView.Chip(swatch, _config.CopyAs, plan.Cell));
                break;

            case SwatchArrangement.Tiles:
                BuildGrid(swatches, plan, WidgetLayout.ContentInset, (swatch, _) => SwatchItemView.Tile(swatch, _config.CopyAs, plan.Cell, _config.ShowNames, _config.ShowValues));
                break;

            default:
                BuildRows(swatches, plan);
                break;
        }

        if (_cursor is { } cursor)
        {
            _cursor = cursor < _items.Count ? cursor : null;
            ShowCursor();
        }
    }

    /// <summary>Rows, in columns that fill top to bottom, edge to edge of the card. [S-213]</summary>
    private void BuildRows(IReadOnlyList<Swatch> swatches, SwatchPlan plan)
    {
        _columns.Margin = WidgetLayout.ListInset;
        _columns.ColumnSpacing = 0;
        _columns.RowSpacing = 0;

        var columns = Math.Max(1, plan.Columns);

        for (var column = 0; column < columns; column++)
        {
            _columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        var perColumn = (int)Math.Ceiling(swatches.Count / (double)columns);
        var stacks = new StackPanel[columns];

        for (var column = 0; column < columns; column++)
        {
            stacks[column] = new StackPanel();
            SetColumn(stacks[column], column);
            _columns.Children.Add(stacks[column]);
        }

        for (var i = 0; i < swatches.Count; i++)
        {
            var item = SwatchItemView.Row(swatches[i], _config.CopyAs, plan.Cell, _config.ShowNames, plan.ShowValue);
            _items.Add(item);
            stacks[Math.Min(i / perColumn, columns - 1)].Children.Add(item.Root);
        }
    }

    /// <summary>Chips or tiles: a grid, row by row, inside an inset.</summary>
    private void BuildGrid(IReadOnlyList<Swatch> swatches, SwatchPlan plan, double inset, Func<Swatch, int, SwatchItemView> build)
    {
        _columns.Margin = new Thickness(inset);
        _columns.ColumnSpacing = SwatchLayout.Gap;
        _columns.RowSpacing = SwatchLayout.Gap;

        var columns = Math.Max(1, plan.Columns);
        var rows = (int)Math.Ceiling(swatches.Count / (double)columns);

        for (var column = 0; column < columns; column++)
        {
            // Chips are square at their planned side; tiles share the width.
            _columns.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = plan.Arrangement == SwatchArrangement.Chips ? new GridLength(plan.Cell) : new GridLength(1, GridUnitType.Star),
            });
        }

        for (var row = 0; row < rows; row++)
        {
            _columns.RowDefinitions.Add(new RowDefinition { Height = new GridLength(plan.Cell) });
        }

        // A chip grid narrower than the slot is centred, so its spare width is even on both sides.
        _columns.HorizontalAlignment = plan.Arrangement == SwatchArrangement.Chips ? HorizontalAlignment.Center : HorizontalAlignment.Stretch;

        for (var i = 0; i < swatches.Count; i++)
        {
            var item = build(swatches[i], i);
            SetRow(item.Root, i / columns);
            SetColumn(item.Root, i % columns);
            _columns.Children.Add(item.Root);
            _items.Add(item);
        }
    }

    private void ClearItems()
    {
        foreach (var item in _items)
        {
            item.Dispose();
        }

        _items.Clear();
        _columns.Children.Clear();
        _columns.ColumnDefinitions.Clear();
        _columns.RowDefinitions.Clear();
        _columns.HorizontalAlignment = HorizontalAlignment.Stretch;
    }

    // -----------------------------------------------------------------------------------------
    // Keys
    // -----------------------------------------------------------------------------------------

    /// <summary>[S-1507] Only a palette with colours in it has anything to navigate.</summary>
    public bool WantsKeys => _config.Mode == SwatchMode.Palette && _items.Count > 0;

    /// <summary>[S-1507] Arrows walk the palette, Enter copies the colour the cursor is on, Escape lets go.</summary>
    public bool HandleKey(WidgetKey key)
    {
        if (_detached)
        {
            return false;
        }

        var decision = ListKeys.Decide(key, _cursor ?? -1, _items.Count);

        switch (decision.Command)
        {
            case ListCommand.Focus:
                _cursor = decision.Index;
                ShowCursor();
                return true;

            case ListCommand.Open:
                _items[decision.Index].Copy();
                return true;

            case ListCommand.Release:
                _cursor = null;
                ShowCursor();
                return false;

            default:
                return false;
        }
    }

    private void ShowCursor()
    {
        for (var i = 0; i < _items.Count; i++)
        {
            RowFocus.Mark(_items[i].Root, i == _cursor);
        }
    }

    public void Detach()
    {
        if (_detached)
        {
            return;
        }

        Strings.Changed -= OnLanguage;

        _detached = true;
        ClearItems();
    }

    // -----------------------------------------------------------------------------------------

    /// <summary>
    /// One colour in the palette — a row (chip, name, value), a chip, or a tile (block over name
    /// and value) — and what a click on it does: copy, and say so.
    /// <para>
    /// A class rather than a method because an item owns three handlers and a hover subscription,
    /// and every one of them has to come off again — a view that rebuilds on resize would otherwise
    /// leak a set per drag.
    /// </para>
    /// </summary>
    private sealed class SwatchItemView : IDisposable
    {
        private readonly TextBlock? _value;
        private readonly IDisposable? _hover;
        private readonly string _copied;

        private bool _disposed;

        private SwatchItemView(Border root, TextBlock? value, string copied, bool hover)
        {
            Root = root;
            _value = value;
            _copied = copied;
            RowFocus.Prepare(root);
            _hover = hover ? Fluent.Hover(root) : null;

            ToolTipService.SetToolTip(root, Str.WidgetsA_ColorSwatch_CopyValue.Format(("value", copied)));

            // Tapped, not PointerPressed: in edit mode the card captures the pointer on press to
            // drag the widget, the gesture never completes here, and nothing is copied while a
            // widget is being moved.
            root.Tapped += OnTapped;
            root.PointerExited += OnLeft;
            root.PointerCaptureLost += OnLeft;
        }

        public Border Root { get; }

        /// <summary>A list row: the chip is sized from the row, and the row from the plan.</summary>
        public static SwatchItemView Row(Swatch swatch, ColorNotation notation, double height, bool showName, bool showValue)
        {
            var copied = ColorFormats.Format(swatch.Color, notation);

            var content = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = GridLength.Auto },
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                    new ColumnDefinition { Width = GridLength.Auto },
                },
            };

            var chip = Block(swatch, height - (Fluent.Space2 * 2), Fluent.ControlRadius);
            chip.Margin = new Thickness(0, 0, Fluent.Space3, 0);
            chip.VerticalAlignment = VerticalAlignment.Center;
            content.Children.Add(chip);

            if (showName)
            {
                var name = Fluent.Text(FluentType.Body);
                name.Text = swatch.Name;
                name.TextTrimming = TextTrimming.CharacterEllipsis;
                name.VerticalAlignment = VerticalAlignment.Center;
                SetColumn(name, 1);
                content.Children.Add(name);
            }

            TextBlock? value = null;

            if (showValue)
            {
                value = Fluent.Text(FluentType.Caption, "TextFillColorTertiaryBrush");
                value.Text = copied;
                value.VerticalAlignment = VerticalAlignment.Center;
                value.Margin = new Thickness(Fluent.Space2, 0, 0, 0);
                SetColumn(value, 2);
                content.Children.Add(value);
            }

            var root = Fluent.Row(content);
            root.Height = height;
            return new SwatchItemView(root, value, copied, hover: true);
        }

        /// <summary>A chip in the grid a narrow tile shows: the colour only; the name is in the tooltip.</summary>
        public static SwatchItemView Chip(Swatch swatch, ColorNotation notation, double side)
        {
            var copied = ColorFormats.Format(swatch.Color, notation);
            // In a holder of its own, so the keyboard ring goes round the chip rather than replacing
            // the hairline that gives a card-coloured chip its edge.
            var holder = new Border
            {
                Child = Block(swatch, side - (2 * RowFocus.Ring), Fluent.ControlRadius),
                CornerRadius = new CornerRadius(Fluent.ControlRadius + RowFocus.Ring),
            };

            var view = new SwatchItemView(holder, null, copied, hover: false);
            ToolTipService.SetToolTip(holder, Str.WidgetsA_ColorSwatch_CopyNamed.Format(("name", swatch.Name), ("value", copied)));
            return view;
        }

        /// <summary>A tile a big tile shows: a large block, with the name and value under it.</summary>
        public static SwatchItemView Tile(Swatch swatch, ColorNotation notation, double height, bool showName, bool showValue)
        {
            var copied = ColorFormats.Format(swatch.Color, notation);

            var block = Block(swatch, double.NaN, Fluent.OverlayRadius);
            block.HorizontalAlignment = HorizontalAlignment.Stretch;
            block.VerticalAlignment = VerticalAlignment.Stretch;

            var layout = new Grid
            {
                RowSpacing = Fluent.Space1,
                RowDefinitions =
                {
                    new RowDefinition { Height = new GridLength(1, GridUnitType.Star) },
                    new RowDefinition { Height = GridLength.Auto },
                    new RowDefinition { Height = GridLength.Auto },
                },
                Children = { block },
            };

            if (showName)
            {
                var name = Fluent.Text(FluentType.Body);
                name.Text = swatch.Name;
                name.TextTrimming = TextTrimming.CharacterEllipsis;
                SetRow(name, 1);
                layout.Children.Add(name);
            }

            TextBlock? value = null;

            if (showValue)
            {
                value = Fluent.Text(FluentType.Caption, "TextFillColorTertiaryBrush");
                value.Text = copied;
                SetRow(value, 2);
                layout.Children.Add(value);
            }

            var root = new Border
            {
                Child = layout,
                Height = height,
                CornerRadius = new CornerRadius(Fluent.OverlayRadius),
                Background = FluentLayout.Themed("SubtleFillColorTransparentBrush", Colors.Transparent),
            };

            return new SwatchItemView(root, value, copied, hover: false);
        }

        /// <summary>
        /// The colour itself: the one literal colour in the widget, and the only one that should be —
        /// this IS the content. A hairline so a colour the same as the card still has an edge.
        /// </summary>
        private static Border Block(Swatch swatch, double side, double radius) => new()
        {
            Width = side,
            Height = side,
            CornerRadius = new CornerRadius(radius),
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, swatch.Color.R, swatch.Color.G, swatch.Color.B)),
            BorderBrush = FluentLayout.Themed("ControlStrokeColorDefaultBrush", Colors.Gray),
            BorderThickness = new Thickness(Fluent.Hairline),
        };

        private void OnTapped(object sender, TappedRoutedEventArgs args)
        {
            // Not marked handled: the card's double-click, which is how the desktop enters edit
            // mode, still has to reach it. Copying the same colour twice costs nothing.
            Copy();
        }

        /// <summary>
        /// Copy, and say so in place of the value until the pointer leaves.
        /// <para>
        /// Event-driven rather than a timer: "Copied" that clears itself after a second and a half
        /// needs a wall clock, a dispatcher and something to dispose, and the pointer leaving the
        /// item is both a better signal and free.
        /// </para>
        /// </summary>
        public void Copy()
        {
            try
            {
                var package = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
                package.SetText(_copied);
                Clipboard.SetContent(package);
            }
            catch (Exception ex)
            {
                // Another process can hold the clipboard open; Windows fails the call rather than
                // waiting. Say so instead of leaving the item claiming it copied something.
                Log.Error(Scope, $"could not copy {_copied} to the clipboard", ex);
                return;
            }

            try
            {
                // Without this the colour is gone from the clipboard the moment Themia exits, which
                // for a widget that is running all day is exactly when it would be missed.
                Clipboard.Flush();
            }
            catch (Exception ex)
            {
                Log.Warn(Scope, "the clipboard was copied but could not be flushed; it will not outlive the app", ex);
            }

            if (_value is not null)
            {
                _value.Text = Str.Toast_Copied.Text();
            }
        }

        private void OnLeft(object sender, PointerRoutedEventArgs args)
        {
            if (_value is not null)
            {
                _value.Text = _copied;
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Root.Tapped -= OnTapped;
            Root.PointerExited -= OnLeft;
            Root.PointerCaptureLost -= OnLeft;
            _hover?.Dispose();
        }
    }
}
