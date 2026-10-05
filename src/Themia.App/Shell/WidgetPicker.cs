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
/// [S-420, S-948, S-949] The body of the Add-widget picker: a search box and the widgets — in
/// category columns while the box is empty, as one flat list ranked by <see cref="WidgetSearch"/>
/// while it is not.
/// <para>
/// Its own control rather than code inside the surface, so the debug render can draw the open
/// picker off-screen without a desktop window, and so the flyout can rebuild it on every open with
/// the names in the current language.
/// </para>
/// <para>
/// Keyboard: the search box has focus as soon as the picker opens, so typing searches. Up and Down
/// move a highlight through what is shown, in reading order; Enter picks the highlighted widget;
/// Escape closes. The highlight is drawn, not focused, so the caret stays in the box and the next
/// letter typed still goes to the search.
/// </para>
/// </summary>
internal sealed partial class WidgetPicker : Grid
{
    private const string Scope = "picker";

    /// <summary>How many columns the categories spread across while nothing is typed.</summary>
    private const int PickerColumns = 4;

    /// <summary>Width of a name in the search results: the longest catalogue label, with room.</summary>
    private const double ResultNameWidth = 150;

    /// <summary>
    /// The search index, built once per language: the catalogue does not change while the app runs,
    /// but its names do when the language does ([S-1109]). UI thread only.
    /// </summary>
    private static (Locale Locale, (IndexedWidget Index, WidgetDefinition Definition)[] Entries)? _searchIndex;

    private static (IndexedWidget Index, WidgetDefinition Definition)[] SearchIndex()
    {
        var locale = Strings.Current;
        if (_searchIndex is { } built && built.Locale == locale)
        {
            return built.Entries;
        }

        var entries = BuildIndex();
        _searchIndex = (locale, entries);
        return entries;
    }

    private readonly IReadOnlyList<WidgetDefinition> _offered;
    private readonly Action<WidgetDefinition> _pick;
    private readonly Action _close;

    private readonly TextBox _search;
    private readonly Grid _columns;
    private readonly ScrollViewer _resultsHost;
    private readonly StackPanel _results;

    /// <summary>What Up, Down and Enter walk: the visible items, in reading order.</summary>
    private readonly List<(Button Button, WidgetDefinition Definition)> _cursorItems = [];
    private readonly List<(Button Button, WidgetDefinition Definition)> _columnItems = [];

    private int _cursor = -1;

    /// <param name="pick">Runs when a widget is chosen.</param>
    /// <param name="close">Runs on Escape.</param>
    public WidgetPicker(Action<WidgetDefinition> pick, Action close)
    {
        _pick = pick;
        _close = close;
        _offered = [.. WidgetCatalog.All];

        RowSpacing = Fluent.Space3;
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        _search = new TextBox
        {
            PlaceholderText = Str.Shell_Picker_Search.Text(),
            MinWidth = 240,
            IsSpellCheckEnabled = false,
            IsTextPredictionEnabled = false,
        };
        AutomationProperties.SetName(_search, Str.Shell_Picker_Search.Text());
        _search.TextChanged += (_, _) => Refresh();
        _search.PreviewKeyDown += OnSearchKeyDown;

        Children.Add(_search);

        _columns = BuildColumns();
        _results = new StackPanel();
        _resultsHost = new ScrollViewer
        {
            Content = _results,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalAlignment = VerticalAlignment.Top,
            Visibility = Visibility.Collapsed,
        };

        var body = new Grid { Children = { _columns, _resultsHost } };
        SetRow(body, 1);
        Children.Add(body);

        Refresh();
    }

    /// <summary>Put the caret in the search box. Called once the flyout is open.</summary>
    public void FocusSearch() => _search.Focus(FocusState.Programmatic);

    /// <summary>Type into the search box, for the debug render.</summary>
    internal void SetQuery(string text) => _search.Text = text;

    private static (IndexedWidget, WidgetDefinition)[] BuildIndex()
        => [.. WidgetCatalog.All.Select(d => (WidgetSearch.Index(WidgetCatalog.SearchDoc(d)), d))];

    /// <summary>[S-420] Every offered widget in category sections, balanced across columns.</summary>
    private Grid BuildColumns()
    {
        var sections = _offered
            .GroupBy(d => d.Category)
            .OrderBy(g => g.Key)
            .Select(g => (Category: g.Key, Items: g.ToList()))
            .ToList();

        // A heading costs about one row, so it counts as one when balancing the columns.
        var columnOf = ColumnLayout.Partition([.. sections.Select(s => s.Items.Count + 1)], PickerColumns);
        var columnCount = columnOf.Length == 0 ? 0 : columnOf.Max() + 1;

        var grid = new Grid { ColumnSpacing = Fluent.Space4 };
        var columns = new StackPanel[columnCount];

        for (var c = 0; c < columnCount; c++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            columns[c] = new StackPanel { MinWidth = 176 };
            SetColumn(columns[c], c);
            grid.Children.Add(columns[c]);
        }

        // Sections fill the columns in reading order, so walking the sections in order is walking
        // the columns top to bottom, left to right — the order Up and Down follow.
        for (var i = 0; i < sections.Count; i++)
        {
            var column = columns[columnOf[i]];

            var heading = Fluent.Text(FluentType.Caption, "TextFillColorSecondaryBrush");
            heading.Text = WidgetCatalog.Title(sections[i].Category);
            heading.Margin = new Thickness(Fluent.Space3, column.Children.Count == 0 ? 0 : Fluent.Space4, 0, Fluent.Space1);
            column.Children.Add(heading);

            foreach (var definition in sections[i].Items)
            {
                var button = Item(definition, detail: false);
                column.Children.Add(button);
                _columnItems.Add((button, definition));
            }
        }

        return grid;
    }

    /// <summary>
    /// [S-948] Show the columns for an empty query, or the ranked flat list for any other, and put
    /// the highlight on the first item of a search so Enter takes the best match.
    /// </summary>
    private void Refresh()
    {
        var query = _search.Text;
        _cursorItems.Clear();

        if (string.IsNullOrWhiteSpace(query))
        {
            _resultsHost.Visibility = Visibility.Collapsed;
            _columns.Visibility = Visibility.Visible;
            _cursorItems.AddRange(_columnItems);
            MoveCursor(-1);
            return;
        }

        // The list takes the columns' place at the columns' size, so the flyout does not jump about
        // while the user types.
        if (_columns.Visibility == Visibility.Visible && _columns.ActualWidth > 0)
        {
            _resultsHost.Width = _columns.ActualWidth;
            _resultsHost.Height = _columns.ActualHeight;
        }

        _columns.Visibility = Visibility.Collapsed;
        _resultsHost.Visibility = Visibility.Visible;
        _results.Children.Clear();

        var offered = SearchIndex().Where(entry => _offered.Contains(entry.Definition));
        var ranked = WidgetSearch.Rank(offered.Select(entry => (entry.Index, entry.Definition)), query);

        if (ranked.Count == 0)
        {
            var none = Fluent.Text(FluentType.Body, "TextFillColorSecondaryBrush");
            none.Text = Str.Shell_Picker_NoMatch.Format(("query", query.Trim()));
            none.Margin = new Thickness(Fluent.Space3, Fluent.Space2, Fluent.Space3, Fluent.Space2);
            _results.Children.Add(none);
            MoveCursor(-1);
            return;
        }

        foreach (var definition in ranked)
        {
            var button = Item(definition, detail: true);
            _results.Children.Add(button);
            _cursorItems.Add((button, definition));
        }

        MoveCursor(0);
    }

    /// <summary>
    /// One widget. In the flat list it also says what the widget is and where it would be in the
    /// columns, because a search result has lost the section heading that said so.
    /// </summary>
    private Button Item(WidgetDefinition definition, bool detail)
    {
        var name = Fluent.Text(FluentType.Body);
        name.Text = definition.Label;
        name.VerticalAlignment = VerticalAlignment.Center;

        // In the flat list the names share one width, so the descriptions start in one column.
        if (detail)
        {
            name.Width = ResultNameWidth;
        }

        var row = new Grid { ColumnSpacing = Fluent.Space3 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        row.Children.Add(new FontIcon { Glyph = definition.Glyph, FontSize = 16 });
        SetColumn(name, 1);
        row.Children.Add(name);

        if (detail)
        {
            var description = Fluent.Text(FluentType.Body, "TextFillColorSecondaryBrush");
            description.Text = definition.Description;
            description.VerticalAlignment = VerticalAlignment.Center;
            SetColumn(description, 2);
            row.Children.Add(description);

            var category = Fluent.Text(FluentType.Caption, "TextFillColorTertiaryBrush");
            category.Text = WidgetCatalog.Title(definition.Category);
            category.VerticalAlignment = VerticalAlignment.Center;
            SetColumn(category, 3);
            row.Children.Add(category);
        }

        var button = new Button
        {
            Content = row,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Background = DesignApplier.Brush("SubtleFillColorTransparentBrush", Colors.Transparent),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(Fluent.Space3, 6, Fluent.Space3, 6),

            // The search box keeps the keyboard; the list is walked with Up and Down from there.
            IsTabStop = false,
        };

        ToolTipService.SetToolTip(button, definition.Description);
        AutomationProperties.SetName(button, definition.Label);
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

    /// <summary>[S-949] Up and Down move the highlight, Enter picks, Escape closes.</summary>
    private void OnSearchKeyDown(object sender, KeyRoutedEventArgs e)
    {
        switch (e.Key)
        {
            case VirtualKey.Down:
            case VirtualKey.Up:
                var step = ListKeys.Decide(e.Key == VirtualKey.Down ? WidgetKey.Down : WidgetKey.Up, _cursor, _cursorItems.Count);
                if (step.Command == ListCommand.Focus)
                {
                    MoveCursor(step.Index);
                }

                break;

            case VirtualKey.Enter:
                if (_cursor >= 0 && _cursor < _cursorItems.Count)
                {
                    Pick(_cursorItems[_cursor].Definition);
                }

                break;

            case VirtualKey.Escape:
                _close();
                break;

            default:
                return;
        }

        e.Handled = true;
    }

    private void MoveCursor(int index)
    {
        for (var i = 0; i < _cursorItems.Count; i++)
        {
            _cursorItems[i].Button.Background = i == index
                ? DesignApplier.Brush("SubtleFillColorSecondaryBrush", Colors.Transparent)
                : DesignApplier.Brush("SubtleFillColorTransparentBrush", Colors.Transparent);
        }

        // Clear a highlight left on a column item from before a search.
        if (index < 0)
        {
            foreach (var (button, _) in _columnItems)
            {
                button.Background = DesignApplier.Brush("SubtleFillColorTransparentBrush", Colors.Transparent);
            }
        }

        _cursor = index >= 0 && index < _cursorItems.Count ? index : -1;

        if (_cursor >= 0)
        {
            _cursorItems[_cursor].Button.StartBringIntoView();
        }
    }
}
