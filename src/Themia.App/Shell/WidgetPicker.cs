using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Windows.System;

namespace Themia.App.Shell;

/// <summary>
/// [S-3501..S-3505] The body of the edit bar's add menu: a search box over every widget kind, in its
/// catalogue sections flowing through columns as a newspaper's do ([S-3502]). Each entry is the
/// widget's icon and name — no preview. Built from <see cref="WidgetCatalog.All"/> each time it opens,
/// so a new kind is listed without touching this, and in the current language ([S-1109]).
/// <para>
/// Its own control rather than code inside the surface, so the debug render can draw it off-screen
/// without a desktop window.
/// </para>
/// <para>
/// Keyboard ([S-3505]): the search box keeps the focus, so typing always searches; the highlight is
/// drawn, not focused. Arrows move it through the columns (<see cref="AddMenuKeys"/>), Enter places
/// the highlighted widget (or, while a search has no highlight, its best match), Escape empties the
/// box and, empty, closes the menu.
/// </para>
/// </summary>
internal sealed partial class WidgetPicker : Grid
{
    private const string Scope = "picker";

    /// <summary>[S-3502] One column's width: the longest catalogue name with its icon fits, as in Karpo's 216-DIP columns.</summary>
    private const double ColumnWidth = 216;

    /// <summary>The gap between two columns.</summary>
    private const double ColumnGap = Fluent.Space2;

    /// <summary>[S-3502] One line, heading or entry: Fluent's compact list row.</summary>
    private const double LineHeight = Fluent.RowHeightCompact;

    /// <summary>
    /// The search index, built once per language: the catalogue does not change while the app runs,
    /// but its names do when the language does ([S-1109]). UI thread only.
    /// </summary>
    private static (Locale Locale, (IndexedWidget Index, WidgetDefinition Definition, WidgetCategory Category)[] Entries)? _searchIndex;

    private readonly Action<WidgetDefinition> _pick;
    private readonly Action _close;

    private readonly TextBox _search;
    private readonly Grid _columns;

    /// <summary>[S-3503] The column count and height of the full menu; a search keeps them, so the menu does not jump.</summary>
    private readonly int _columnCount;
    private readonly int _rows;

    /// <summary>What the arrows walk: the shown entries in reading order, and how they stand.</summary>
    private readonly List<(Button Button, WidgetDefinition Definition)> _entries = [];
    private AddMenuGrid _grid = AddMenuLayout.Flow([], 2);

    private int _cursor = -1;

    /// <param name="pick">Runs when a widget is chosen.</param>
    /// <param name="close">Runs on Escape from an empty search box.</param>
    /// <param name="areaWidth">The work area's width in DIPs, which decides the columns ([S-3503]).</param>
    /// <param name="areaHeight">The work area's height in DIPs.</param>
    public WidgetPicker(Action<WidgetDefinition> pick, Action close, double areaWidth, double areaHeight)
    {
        _pick = pick;
        _close = close;

        RowSpacing = Fluent.Space3;
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        _search = new TextBox
        {
            PlaceholderText = Str.Shell_Picker_Search.Text(),
            IsSpellCheckEnabled = false,
            IsTextPredictionEnabled = false,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        AutomationProperties.SetName(_search, Str.Shell_Picker_Search.Text());
        _search.TextChanged += (_, _) => Refresh();
        _search.PreviewKeyDown += OnSearchKeyDown;
        Children.Add(_search);

        var all = AddMenuLayout.Filter(SearchIndex(), null);
        (_columnCount, _rows) = AddMenuLayout.Fit([.. all.Select(s => s.Items.Count)], areaWidth, areaHeight, ColumnWidth, ColumnGap, LineHeight);

        _columns = new Grid
        {
            ColumnSpacing = ColumnGap,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Width = Math.Max(1, (_columnCount * ColumnWidth) + (Math.Max(0, _columnCount - 1) * ColumnGap)),
            Height = _rows * LineHeight,
        };

        // [S-3503] Past the most the screen allows the menu scrolls rather than leaving the screen.
        var body = new ScrollViewer
        {
            Content = _columns,
            MaxHeight = Math.Max(2 * LineHeight, areaHeight * AddMenuLayout.MostHeightShare),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
        SetRow(body, 1);
        Children.Add(body);

        Refresh();
    }

    /// <summary>Put the caret in the search box. Called once the flyout is open.</summary>
    public void FocusSearch() => _search.Focus(FocusState.Programmatic);

    /// <summary>Type into the search box, for the debug render.</summary>
    internal void SetQuery(string text) => _search.Text = text;

    private static (IndexedWidget Index, WidgetDefinition Definition, WidgetCategory Category)[] SearchIndex()
    {
        var locale = Strings.Current;
        if (_searchIndex is { } built && built.Locale == locale)
        {
            return built.Entries;
        }

        // In section order, the catalogue's order inside each: what an empty search lists.
        (IndexedWidget, WidgetDefinition, WidgetCategory)[] entries =
            [.. WidgetCatalog.All.OrderBy(d => d.Category).Select(d => (WidgetSearch.Index(WidgetCatalog.SearchDoc(d)), d, d.Category))];
        _searchIndex = (locale, entries);
        return entries;
    }

    /// <summary>
    /// [S-3504] Lay out what the query leaves through the columns. A search keeps the full menu's
    /// size and column count and starts the reading order on its best match.
    /// </summary>
    private void Refresh()
    {
        var query = _search.Text;
        var sections = AddMenuLayout.Filter(SearchIndex(), query);
        var sizes = sections.Select(s => s.Items.Count).ToArray();

        _grid = AddMenuLayout.Flow(sizes, Math.Max(_rows, AddMenuLayout.RowsFor(sizes, _columnCount)));
        _entries.Clear();
        _columns.Children.Clear();
        _columns.ColumnDefinitions.Clear();

        if (_grid.Columns.Count == 0)
        {
            var none = Fluent.Text(FluentType.Body, "TextFillColorSecondaryBrush");
            none.Text = Str.Shell_Picker_NoMatch.Format(("query", query.Trim()));
            none.Margin = new Thickness(Fluent.Space3, Fluent.Space2, Fluent.Space3, Fluent.Space2);
            _columns.Children.Add(none);
            MoveCursor(-1);
            return;
        }

        for (var c = 0; c < _grid.Columns.Count; c++)
        {
            _columns.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(ColumnWidth) });
            var column = new StackPanel { Width = ColumnWidth };
            SetColumn(column, c);
            _columns.Children.Add(column);

            foreach (var line in _grid.Columns[c])
            {
                var (category, items) = sections[line.Section];
                if (line.IsHeading)
                {
                    column.Children.Add(Heading(category));
                    continue;
                }

                var definition = items[line.Entry];
                var button = Entry(definition);
                column.Children.Add(button);
                _entries.Add((button, definition));
            }
        }

        MoveCursor(-1);
    }

    private static TextBlock Heading(WidgetCategory category)
    {
        var heading = Fluent.Text(FluentType.Caption, "TextFillColorSecondaryBrush");
        heading.Text = WidgetCatalog.Title(category);
        heading.Height = LineHeight;
        heading.Padding = new Thickness(Fluent.Space3, Fluent.Space3, Fluent.Space3, 0);
        return heading;
    }

    /// <summary>One widget: its icon and its name. No preview ([S-3501]); the description is the tooltip.</summary>
    private Button Entry(WidgetDefinition definition)
    {
        var name = Fluent.Text(FluentType.Body);
        name.Text = definition.Label;
        name.VerticalAlignment = VerticalAlignment.Center;

        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = Fluent.Space3,
            Children = { Fluent.Icon(definition.Glyph, "TextFillColorPrimaryBrush"), name },
        };

        var button = new Button
        {
            Content = row,
            Height = LineHeight,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Background = Rest(),
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(Fluent.ControlRadius),
            Padding = new Thickness(Fluent.Space3, 0, Fluent.Space3, 0),

            // The search box keeps the keyboard; the menu is walked with the arrows from there.
            IsTabStop = false,
        };

        ToolTipService.SetToolTip(button, definition.Description);
        AutomationProperties.SetName(button, definition.Label);
        AutomationProperties.SetHelpText(button, definition.Description);
        button.Click += (_, _) => Pick(definition);
        return button;
    }

    private void Pick(WidgetDefinition definition)
    {
        try
        {
            _pick(definition);
        }
        catch (Exception ex)
        {
            Log.Error(Scope, $"picking {definition.Kind} failed", ex);
        }
    }

    /// <summary>[S-3505] The arrows, Home and End move the highlight; Enter places; Escape empties, then closes.</summary>
    private void OnSearchKeyDown(object sender, KeyRoutedEventArgs e)
    {
        WidgetKey? key = e.Key switch
        {
            VirtualKey.Up => WidgetKey.Up,
            VirtualKey.Down => WidgetKey.Down,
            VirtualKey.Left => WidgetKey.Left,
            VirtualKey.Right => WidgetKey.Right,
            VirtualKey.Home => WidgetKey.Home,
            VirtualKey.End => WidgetKey.End,
            _ => null,
        };

        if (key is { } move)
        {
            if (AddMenuKeys.Move(_grid, _cursor, move) is not { } next)
            {
                return; // the caret's
            }

            MoveCursor(next);
            e.Handled = true;
            return;
        }

        switch (e.Key)
        {
            case VirtualKey.Enter:
                if (Target() is { } target)
                {
                    Pick(_entries[target].Definition);
                }

                break;

            case VirtualKey.Escape when _search.Text.Length > 0:
                _search.Text = string.Empty;
                break;

            case VirtualKey.Escape:
                _close();
                break;

            default:
                return;
        }

        e.Handled = true;
    }

    /// <summary>What Enter places: the highlight, else during a search its best match (the first entry).</summary>
    private int? Target()
    {
        if (_cursor >= 0 && _cursor < _entries.Count)
        {
            return _cursor;
        }

        return !string.IsNullOrWhiteSpace(_search.Text) && _entries.Count > 0 ? 0 : null;
    }

    private void MoveCursor(int index)
    {
        _cursor = index >= 0 && index < _entries.Count ? index : -1;
        var target = Target();

        for (var i = 0; i < _entries.Count; i++)
        {
            _entries[i].Button.Background = i == target
                ? FluentLayout.Themed("SubtleFillColorSecondaryBrush", Colors.Transparent)
                : Rest();
        }

        if (target is { } shown)
        {
            _entries[shown].Button.StartBringIntoView();
        }
    }

    private static Microsoft.UI.Xaml.Media.Brush Rest() => FluentLayout.Themed("SubtleFillColorTransparentBrush", Colors.Transparent);
}
