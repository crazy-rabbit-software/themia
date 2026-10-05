using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Core.Shell;

namespace Themia.App.Widgets;

/// <summary>
/// The views as buttons; a click puts that view on the display the widget is on. [S-487]
/// <para>
/// The display, not the app: every display has a view of its own ([S-18]), so "switch the view"
/// only means something once it says where. The widget asks its card for the display it sits on
/// (<see cref="IWidgetSurface.Display"/>) and switches that one through the same path as the
/// toolbar's View menu ([S-19]). Copy it to another screen and it switches that screen instead.
/// </para>
/// <para>
/// [S-1653] The buttons climb a ladder with the room there is — initials, names, a "3 widgets"
/// caption, then view cards with a sketch of each layout — arranged by
/// <see cref="ViewSwitcherLayout.Plan"/>. Each button has the view's own menu ([S-1650]) and the
/// widget takes the arrows when clicked ([S-1652]).
/// </para>
/// <para>
/// The card is the track. The original drew a segmented control on the header-coloured surface; a
/// track with its own fill inside a card that already has one is a box inside a box, so the buttons
/// sit straight on the card, inset by one step so their corners nest inside the card's.
/// </para>
/// </summary>
internal sealed partial class ViewSwitcherView : Grid, IWidgetView, IWidgetSurfaceAware, IWidgetSizeAware, IWidgetKeys
{
    private const string Scope = "view-switcher";

    /// <summary>The cursor's key for the "+N" cell, which is no view.</summary>
    private const string OverflowKey = "\u0000overflow";

    private readonly Grid _track = new()
    {
        Padding = new Thickness(ViewSwitcherLayout.Inset),
        RowSpacing = ViewSwitcherLayout.Gap,
    };

    private readonly StackPanel _empty;
    private readonly TextBlock _emptyText = new() { Text = Str.WidgetsC_ViewSwitcher_Empty.Text() };

    /// <summary>The drawn cells in reading order: the button and its view (or <see cref="OverflowKey"/>).</summary>
    private readonly List<(Button Button, string Key)> _cells = [];

    private readonly List<IDisposable> _menus = [];

    private ViewSwitcherConfig _config = new();
    private IWidgetSurface? _surface;
    private IReadOnlyList<ViewChoice> _choices = [];
    private WidgetArea _area;
    private SwitcherLayout _plan;
    private string _built = string.Empty;

    /// <summary>[S-1652] The cell the keyboard is on, by view id so it survives a rebuild; null for none.</summary>
    private string? _cursor;

    private bool _detached;

    public ViewSwitcherView()
    {
        _empty = Fluent.EmptyState(Glyphs.View, _emptyText, out _);
        _empty.Visibility = Visibility.Collapsed;

        Children.Add(_track);
        Children.Add(_empty);
    }

    public void Bind(IWidgetSurface surface) => _surface = surface;

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _detached = false;
        Strings.Changed -= OnLanguage;
        Strings.Changed += OnLanguage;
        ApplyConfig(config);
    }

    /// <summary>The language changed: the empty text, and the buttons rebuilt for their tooltips and counts.</summary>
    private void OnLanguage(Locale locale)
    {
        if (_detached)
        {
            return;
        }

        _emptyText.Text = Str.WidgetsC_ViewSwitcher_Empty.Text();
        Lay(force: true);
    }

    /// <summary>
    /// Re-read the views as well as the config. The card calls this on every state change it
    /// survives, which is exactly when a view may have been renamed, added, moved or deleted — so
    /// the switcher follows the settings window without subscribing to anything of its own.
    /// </summary>
    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as ViewSwitcherConfig ?? new ViewSwitcherConfig();
        Refresh();
    }

    public void ApplySize(WidgetArea area)
    {
        _area = area;
        Lay(force: false);
    }

    private void Refresh()
    {
        if (_detached)
        {
            return;
        }

        if (_surface?.Display is not { } display)
        {
            _choices = [];
            _track.Visibility = Visibility.Collapsed;
            _empty.Visibility = Visibility.Visible;
            return;
        }

        _track.Visibility = Visibility.Visible;
        _empty.Visibility = Visibility.Collapsed;
        _choices = ViewSwitcherRules.Choices(display.Views, _config.VisibleViews, display.ShownViewId);
        Lay(force: false);
    }

    // ---- layout ------------------------------------------------------------------------------

    /// <summary>
    /// Build the cells for the views on offer and the room there is. Skipped when nothing that
    /// shows has changed, because the card calls in on every state change on the desktop and a
    /// rebuild would drop the pointer-over state under the cursor.
    /// </summary>
    private void Lay(bool force)
    {
        if (_detached || _choices.Count == 0 || !_area.IsMeasured || _surface?.Display is not { } display)
        {
            return;
        }

        var plan = ViewSwitcherLayout.Plan(_area, _choices.Count, _config.IconOnly);
        var sketches = plan.Face == SwitcherFace.Card ? SketchSignature(display) : string.Empty;
        var signature = $"{_area.Width:0}x{_area.Height:0}|{plan}|{FluentType.Body.FontSize}|{string.Join("|", _choices)}|{sketches}";

        if (!force && signature == _built)
        {
            return;
        }

        _built = signature;
        _plan = plan;
        Clear();

        for (var row = 0; row < plan.Rows; row++)
        {
            _track.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var across = plan.CellsInRow(row);
            var line = new Grid { ColumnSpacing = ViewSwitcherLayout.Gap };
            var cell = ViewSwitcherLayout.Cell(_area, plan, row);

            for (var column = 0; column < across; column++)
            {
                line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var index = (row * plan.Columns) + column;
                var (button, key) = plan.Overflow && index == plan.Cells - 1
                    ? (BuildOverflow(_choices.Count - plan.Views, cell), OverflowKey)
                    : (BuildButton(_choices[index], plan.Face, cell, display), _choices[index].Id);

                SetColumn(button, column);
                line.Children.Add(button);
                _cells.Add((button, key));
            }

            SetRow(line, row);
            _track.Children.Add(line);
        }

        // A rebuild under a keyboard cursor keeps it on its view.
        if (_cursor is { } cursor && IndexOf(cursor) is { } at)
        {
            _cells[at].Button.Focus(FocusState.Keyboard);
        }
        else
        {
            _cursor = null;
        }
    }

    private void Clear()
    {
        foreach (var menu in _menus)
        {
            menu.Dispose();
        }

        _menus.Clear();
        _cells.Clear();
        _track.Children.Clear();
        _track.RowDefinitions.Clear();
    }

    /// <summary>What the sketches show, so a widget moved in a view redraws its card and nothing else does.</summary>
    private string SketchSignature(IWidgetDisplay display)
    {
        var parts = _choices.Select(choice => string.Join(
            ";",
            display.Tiles(choice.Id).Select(t => FormattableString.Invariant($"{t.X:0.###},{t.Y:0.###},{t.W:0.###},{t.H:0.###}"))));
        return FormattableString.Invariant($"{display.Aspect:0.###}|") + string.Join("/", parts);
    }

    // ---- cells -------------------------------------------------------------------------------

    private Button BuildButton(ViewChoice choice, SwitcherFace face, CellSize cell, IWidgetDisplay display)
    {
        var content = face switch
        {
            SwitcherFace.Card => CardContent(choice, cell, display),
            SwitcherFace.Detailed => DetailedContent(choice),
            SwitcherFace.Named => NamedContent(choice, cell),
            _ => Mark(choice, MarkStep(cell)),
        };

        var roomy = face >= SwitcherFace.Detailed;
        var button = NewButton(content, roomy);

        // A row of named buttons centres its label; a column, a detailed button or a card reads
        // from the left.
        button.HorizontalContentAlignment = face == SwitcherFace.Mark || (face == SwitcherFace.Named && !_plan.Vertical)
            ? HorizontalAlignment.Center
            : HorizontalAlignment.Stretch;

        Paint(button, choice.Shown);

        ToolTipService.SetToolTip(button, choice.Shown
            ? Str.WidgetsC_ViewSwitcher_Tooltip_Showing.Format(("name", choice.Name))
            : Str.WidgetsC_ViewSwitcher_Tooltip_Show.Format(("name", choice.Name)));
        AutomationProperties.SetName(button, choice.Name);
        AutomationProperties.SetItemStatus(button, choice.Shown ? Str.WidgetsC_ViewSwitcher_Status_Showing.Text() : string.Empty);

        var id = choice.Id;
        button.Click += (_, _) => Switch(id);
        _menus.Add(ItemMenu.Attach(button, () => MenuFor(id)));
        return button;
    }

    /// <summary>[S-1655] The last cell when not every view has one: "+N", a menu of the rest.</summary>
    private Button BuildOverflow(int hidden, CellSize cell)
    {
        var text = Fluent.Text(MarkStep(cell), "TextFillColorSecondaryBrush");
        text.Text = "+" + LocaleFormat.Integer(hidden);
        text.TextTrimming = TextTrimming.None;
        text.VerticalAlignment = VerticalAlignment.Center;

        var button = NewButton(text, roomy: false);
        button.HorizontalContentAlignment = HorizontalAlignment.Center;
        Paint(button, shown: false);

        var label = Str.WidgetsC_ViewSwitcher_MoreViews.Plural(hidden);
        ToolTipService.SetToolTip(button, label);
        AutomationProperties.SetName(button, label);
        button.Click += (_, _) => ShowOverflow(button);
        return button;
    }

    private static Button NewButton(FrameworkElement content, bool roomy) => new()
    {
        Content = content,
        HorizontalAlignment = HorizontalAlignment.Stretch,
        VerticalAlignment = VerticalAlignment.Stretch,
        VerticalContentAlignment = roomy ? VerticalAlignment.Stretch : VerticalAlignment.Center,
        Padding = new Thickness(0),
        MinWidth = 0,
        MinHeight = 0,
        CornerRadius = new CornerRadius(Fluent.ControlRadius),
        BorderThickness = new Thickness(Fluent.Hairline),
    };

    /// <summary>The initials step for a cell: bigger letters in a bigger square, always on the ramp.</summary>
    private static TypeStep MarkStep(CellSize cell)
        => cell.Height >= 120 && cell.Width >= 120 ? FluentType.Title
            : cell.Height >= 72 && cell.Width >= 64 ? FluentType.Subtitle
            : FluentType.BodyStrong;

    /// <summary>[S-903] The view's own icon, or its initials, at the size of <paramref name="step"/>.</summary>
    private static FrameworkElement Mark(ViewChoice choice, TypeStep step)
    {
        var brush = choice.Shown ? "AccentTextFillColorPrimaryBrush" : "TextFillColorSecondaryBrush";

        if (choice.Icon is { } glyph)
        {
            var icon = Fluent.Icon(glyph, brush, step.FontSize);
            icon.VerticalAlignment = VerticalAlignment.Center;
            return icon;
        }

        var initials = Fluent.Text(step, brush);
        initials.Text = choice.Initials;
        initials.TextTrimming = TextTrimming.None;
        initials.VerticalAlignment = VerticalAlignment.Center;
        return initials;
    }

    private static TextBlock NameText(ViewChoice choice, TypeStep step)
    {
        var name = Fluent.Text(choice.Shown ? FluentType.Strong(step) : step, choice.Shown ? "TextFillColorPrimaryBrush" : "TextFillColorSecondaryBrush");
        name.Text = choice.Name;
        name.VerticalAlignment = VerticalAlignment.Center;
        return name;
    }

    private static TextBlock Count(ViewChoice choice)
    {
        var count = Fluent.Text(FluentType.Caption, "TextFillColorTertiaryBrush");
        count.Text = ViewSwitcherRules.CountText(choice.WidgetCount);
        return count;
    }

    /// <summary>Icon or initials, and the name beside them; a step up the ramp in a tall button.</summary>
    private FrameworkElement NamedContent(ViewChoice choice, CellSize cell)
    {
        var large = cell.Height >= 72;
        var mark = Mark(choice, large ? FluentType.Subtitle : FluentType.Strong(FluentType.Caption));
        if (choice.Icon is not null)
        {
            ((FontIcon)mark).FontSize = large ? FluentType.Subtitle.FontSize : Fluent.IconSize;
        }

        // A Grid rather than a horizontal StackPanel, so the name learns its width and trims
        // instead of running out of the button.
        var name = NameText(choice, large ? FluentType.BodyLarge : FluentType.Body);
        var row = new Grid
        {
            ColumnSpacing = Fluent.Space2,
            Margin = new Thickness(Fluent.Space3, 0, Fluent.Space3, 0),
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
            },
            Children = { mark, name },
        };

        SetColumn(name, 1);

        // A grid column hands the content its share of the width only when the content is told to
        // stretch; centred, the name never learns how much room it has.
        row.HorizontalAlignment = _plan.Vertical ? HorizontalAlignment.Stretch : HorizontalAlignment.Center;
        return row;
    }

    /// <summary>[S-1657] The mark, then the name over "3 widgets".</summary>
    private static FrameworkElement DetailedContent(ViewChoice choice)
    {
        var mark = Mark(choice, FluentType.Subtitle);
        var text = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Children = { NameText(choice, FluentType.Body), Count(choice) },
        };

        var row = new Grid
        {
            ColumnSpacing = Fluent.Space3,
            Margin = new Thickness(Fluent.Space3, ViewSwitcherLayout.CellPad, Fluent.Space3, ViewSwitcherLayout.CellPad),
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
            },
            Children = { mark, text },
        };

        SetColumn(text, 1);
        return WithPill(row, choice.Shown);
    }

    /// <summary>
    /// [S-1656] A view card: a sketch of the view's layout, and the name and widget count — the
    /// sketch over the text in a squarish card, beside it in a wide one.
    /// </summary>
    private static FrameworkElement CardContent(ViewChoice choice, CellSize cell, IWidgetDisplay display)
    {
        var shape = ViewSwitcherLayout.CardShapeFor(cell);
        var padX = Fluent.Space3;
        var padY = ViewSwitcherLayout.CellPad;

        var title = new Grid
        {
            ColumnSpacing = Fluent.Space2,
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
            },
        };

        var mark = Mark(choice, FluentType.Strong(FluentType.Body));
        if (choice.Icon is not null)
        {
            ((FontIcon)mark).FontSize = Fluent.IconSize;
        }

        var name = NameText(choice, FluentType.Body);
        title.Children.Add(mark);
        title.Children.Add(name);
        SetColumn(name, 1);

        var text = new StackPanel { Children = { title, Count(choice) } };

        double boxWidth, boxHeight;
        if (shape == CardShape.Above)
        {
            boxWidth = cell.Width - (2 * padX);
            boxHeight = cell.Height - (2 * padY) - ViewSwitcherLayout.Gap - FluentType.Body.LineHeight - FluentType.Caption.LineHeight;
        }
        else
        {
            boxHeight = cell.Height - (2 * padY);
            boxWidth = cell.Width - (3 * padX) - 96;
        }

        var sketch = Sketch(ViewSwitcherLayout.Sketch(display.Tiles(choice.Id), boxWidth, boxHeight, display.Aspect), choice.Shown);
        var body = new Grid { Margin = new Thickness(padX, padY, padX, padY) };

        if (shape == CardShape.Above)
        {
            body.RowSpacing = ViewSwitcherLayout.Gap;
            body.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            sketch.HorizontalAlignment = HorizontalAlignment.Center;
            sketch.VerticalAlignment = VerticalAlignment.Center;
            SetRow(text, 1);
        }
        else
        {
            body.ColumnSpacing = padX;
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            sketch.VerticalAlignment = VerticalAlignment.Center;
            text.VerticalAlignment = VerticalAlignment.Center;
            SetColumn(text, 1);
        }

        body.Children.Add(sketch);
        body.Children.Add(text);
        return WithPill(body, choice.Shown);
    }

    /// <summary>
    /// [S-1656] The screen as a rounded box and each widget as a smaller one inside it, drawn from
    /// the stored rects — nothing of the live view is rendered. The shown view's widgets take the
    /// accent, so the card says which layout is on this display at a glance.
    /// </summary>
    private static FrameworkElement Sketch(ViewSketch sketch, bool shown)
    {
        var canvas = new Canvas { Width = sketch.Width, Height = sketch.Height };

        foreach (var tile in sketch.Tiles)
        {
            // One DIP in from each side, so neighbouring widgets read as two.
            var part = new Border
            {
                Width = Math.Max(1, tile.Width - 2),
                Height = Math.Max(1, tile.Height - 2),
                CornerRadius = new CornerRadius(2),
                Background = FluentLayout.Themed(shown ? "AccentFillColorDefaultBrush" : "ControlStrongFillColorDefaultBrush", Colors.Gray),
                Opacity = shown ? 0.7 : 0.45,
            };

            Canvas.SetLeft(part, tile.X + 1);
            Canvas.SetTop(part, tile.Y + 1);
            canvas.Children.Add(part);
        }

        return new Border
        {
            Width = sketch.Width,
            Height = sketch.Height,
            CornerRadius = new CornerRadius(Fluent.ControlRadius),
            BorderThickness = new Thickness(Fluent.Hairline),
            BorderBrush = FluentLayout.Themed("ControlStrokeColorDefaultBrush", Colors.Gray),
            Background = FluentLayout.Themed("SubtleFillColorSecondaryBrush", Colors.Transparent),
            Child = canvas,
        };
    }

    /// <summary>
    /// The shown view's selection pill on the left edge, as a Windows 11 list marks its selection —
    /// the cue that carries when a card's raised fill is subtle.
    /// </summary>
    private static Grid WithPill(FrameworkElement content, bool shown)
    {
        var root = new Grid { Children = { content } };

        if (shown)
        {
            root.Children.Add(new Border
            {
                Width = 3,
                Height = 16,
                CornerRadius = new CornerRadius(1.5),
                Margin = new Thickness(Fluent.Hairline, 0, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Background = FluentLayout.Themed("AccentFillColorDefaultBrush", Colors.DodgerBlue),
            });
        }

        return root;
    }

    /// <summary>
    /// Lightweight styling: the stock button template, with the fills it reads swapped for the ones
    /// a segmented choice uses. The shown view is a raised control on the card; the rest are subtle
    /// until the pointer is over them, the way a Windows 11 selector reads.
    /// </summary>
    private static void Paint(Button button, bool shown)
    {
        void Set(string key, string brush) => button.Resources[key] = FluentLayout.Themed(brush, Colors.Transparent);

        if (shown)
        {
            Set("ButtonBackground", "ControlFillColorDefaultBrush");
            Set("ButtonBackgroundPointerOver", "ControlFillColorSecondaryBrush");
            Set("ButtonBackgroundPressed", "ControlFillColorTertiaryBrush");
            Set("ButtonBorderBrush", "ControlStrokeColorDefaultBrush");
            Set("ButtonBorderBrushPointerOver", "ControlStrokeColorDefaultBrush");
            Set("ButtonBorderBrushPressed", "ControlStrokeColorDefaultBrush");
        }
        else
        {
            Set("ButtonBackground", "SubtleFillColorTransparentBrush");
            Set("ButtonBackgroundPointerOver", "SubtleFillColorSecondaryBrush");
            Set("ButtonBackgroundPressed", "SubtleFillColorTertiaryBrush");
            Set("ButtonBorderBrush", "SubtleFillColorTransparentBrush");
            Set("ButtonBorderBrushPointerOver", "SubtleFillColorTransparentBrush");
            Set("ButtonBorderBrushPressed", "SubtleFillColorTransparentBrush");
        }
    }

    // ---- menus -------------------------------------------------------------------------------

    /// <summary>
    /// [S-1650] A view's own menu: show it here, move it, duplicate it, its settings (name and
    /// icon), delete it. Every command goes through the host's view mutations — the same ones the
    /// View page and the toolbar's View menu use — so undo, the licence and the repairs of [S-902]
    /// come with them. Delete is never offered for the last view and asks nothing: the notice it
    /// raises is the way back ([S-881]).
    /// </summary>
    private IEnumerable<ItemMenuEntry> MenuFor(string viewId)
    {
        if (_detached || _surface?.Display is not { } display)
        {
            return [];
        }

        var views = display.Views;
        var index = -1;
        for (var i = 0; i < views.Count; i++)
        {
            if (views[i].Id == viewId)
            {
                index = i;
            }
        }

        if (index < 0)
        {
            return [];
        }

        var shown = display.ShownViewId == viewId;

        return
        [
            new ItemMenuEntry(Glyphs.View, Str.WidgetsC_ViewSwitcher_Menu_Show.Text(), () => Switch(viewId), Disabled: shown),
            ItemMenuEntry.Separator,
            new ItemMenuEntry(Glyphs.MoveUp, Str.WidgetsC_ViewSwitcher_Menu_MoveEarlier.Text(), () => display.MoveView(viewId, -1), Disabled: index == 0),
            new ItemMenuEntry(Glyphs.MoveDown, Str.WidgetsC_ViewSwitcher_Menu_MoveLater.Text(), () => display.MoveView(viewId, +1), Disabled: index == views.Count - 1),
            new ItemMenuEntry(Glyphs.Duplicate, Str.App_DuplicateView.Text(), () => display.DuplicateView(viewId), Quick: QuickCommand.Duplicate),
            new ItemMenuEntry(Glyphs.Settings, Str.App_ViewSettingsBtn.Text(), () => display.OpenViewSettings(viewId), Quick: QuickCommand.Settings),
            ItemMenuEntry.Separator,
            new ItemMenuEntry(Glyphs.Delete, Str.App_DeleteView.Text(), () => display.DeleteView(viewId), Danger: true, Disabled: views.Count <= 1, Quick: QuickCommand.Delete),
        ];
    }

    /// <summary>[S-1655] The views without a cell of their own; the one shown here is ticked.</summary>
    private void ShowOverflow(FrameworkElement anchor)
    {
        var rest = _choices.Skip(_plan.Views)
            .Select(choice =>
            {
                var id = choice.Id;
                return new ItemMenuEntry(choice.Icon, choice.Name, () => Switch(id), Checked: choice.Shown);
            })
            .ToList();

        if (rest.Count == 0)
        {
            return;
        }

        try
        {
            ItemMenu.Open(anchor, rest);
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "could not open the overflow menu", ex);
        }
    }

    // ---- keys --------------------------------------------------------------------------------

    /// <summary>
    /// [S-1652] Arrows and Home/End move the keyboard cursor over the cells (it starts on the view
    /// this display shows); Enter or Space shows the view under it — or opens the "+N" menu — and
    /// the Menu key opens that view's menu. Escape, or the lease ending, lets go.
    /// </summary>
    public bool HandleKey(WidgetKey key)
    {
        if (_detached || _cells.Count == 0)
        {
            _cursor = null;
            return false;
        }

        var home = _choices.Take(_plan.Views).ToList().FindIndex(c => c.Shown);
        var step = ViewSwitcherLayout.Step(_plan, _cursor is { } c ? IndexOf(c) : null, home >= 0 ? home : null, key);

        _cursor = step.Index is { } i ? _cells[i].Key : null;
        if (!step.Used || step.Index is not { } at)
        {
            return step.Used;
        }

        var (button, cellKey) = _cells[at];

        switch (key)
        {
            case WidgetKey.Enter or WidgetKey.Space when cellKey == OverflowKey:
                ShowOverflow(button);
                break;

            case WidgetKey.Enter or WidgetKey.Space:
                Switch(cellKey);
                break;

            case WidgetKey.Menu when cellKey != OverflowKey:
                try
                {
                    ItemMenu.Open(button, MenuFor(cellKey));
                }
                catch (Exception ex)
                {
                    Log.Error(Scope, "could not open a view's menu", ex);
                }

                break;

            default:
                button.Focus(FocusState.Keyboard);
                break;
        }

        return true;
    }

    private int? IndexOf(string key)
    {
        for (var i = 0; i < _cells.Count; i++)
        {
            if (_cells[i].Key == key)
            {
                return i;
            }
        }

        return null;
    }

    // ---- switching ---------------------------------------------------------------------------

    /// <summary>
    /// Show a view on this widget's display. On the desktop the switch replaces this card with the
    /// other view's widgets; the refresh afterwards is for a display that keeps this card — a
    /// render's pretend one, or a view that has a switcher at the same place — and does nothing on
    /// a card that has gone.
    /// </summary>
    private void Switch(string viewId)
    {
        if (_detached || _surface?.Display is not { } display || viewId == display.ShownViewId)
        {
            return;
        }

        display.ShowView(viewId);
        Refresh();
    }

    public void Detach()
    {
        _detached = true;
        Strings.Changed -= OnLanguage;
        _cursor = null;
        Clear();
        _built = string.Empty;
    }
}
