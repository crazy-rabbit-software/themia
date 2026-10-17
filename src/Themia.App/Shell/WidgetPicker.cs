using DispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue;
using DispatcherQueueTimer = Microsoft.UI.Dispatching.DispatcherQueueTimer;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Themia.App.Interop;
using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Core.Geometry;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Windows.Foundation;
using Windows.System;

namespace Themia.App.Shell;

/// <summary>
/// [S-3501] [S-3510..S-3515] The body of the edit bar's add menu, after Karpo's add list: a search
/// box, then two columns under their headings — <b>Popular</b> and <b>Browse</b>, a row per subject
/// — and the cascade a subject opens beside them, a family inside it one more level. Each row is
/// the widget's icon and name, a folder's with a chevron — no preview. Typing lists what matches in
/// place of the tree, flat, each with where it lives. Built from <see cref="ModuleCatalog.Widgets"/>
/// through Core's <see cref="AddTree"/> each time it opens, so a new kind is listed without touching
/// this, and in the current language ([S-1109]).
/// <para>
/// Its own control rather than code inside the surface, so the debug render can draw it off-screen
/// without a desktop window. A submenu is a card in a popup of its own beside the row that opened
/// it (<see cref="AddCascade"/>), as Karpo's are windows of their own: inside the menu it pushed
/// the columns about and grew the menu. The popups are children of this control, so the flyout
/// counts them as inside it and one light dismiss closes all of it.
/// </para>
/// <para>
/// Keyboard ([S-3514]): the search box keeps the focus, so typing always searches; the highlight is
/// drawn, not focused, and walks the tree by <see cref="AddMenuKeys"/>.
/// </para>
/// </summary>
internal sealed partial class WidgetPicker : Grid
{
    private const string Scope = "picker";

    /// <summary>[S-3512] One column's width, Karpo's 240 DIPs: the longest name with its icon and a chevron fits.</summary>
    private const double ColumnWidth = 240;

    /// <summary>The gap between two columns.</summary>
    private const double ColumnGap = Fluent.Space2;

    /// <summary>[S-3512] One row, heading or entry: Fluent's compact list row.</summary>
    private const double LineHeight = Fluent.RowHeightCompact;

    /// <summary>A line between two runs of rows: a hairline in a small gap.</summary>
    private const double RuleHeight = Fluent.Space2 + Fluent.Hairline;

    /// <summary>[S-3515] A result: its name over where it lives.</summary>
    private const double ResultHeight = Fluent.RowHeight + Fluent.Space2;

    /// <summary>
    /// The search index, built once per language: the catalogue does not change while the app runs,
    /// but its names do when the language does ([S-1109]). UI thread only.
    /// </summary>
    private static (Locale Locale, (IndexedWidget Index, AddHome Home)[] Entries)? _searchIndex;

    private readonly Action<WidgetDescriptor> _pick;
    private readonly Action _close;

    private readonly TextBox _search;
    private readonly StackPanel _tree;
    private readonly ScrollViewer _treeScroll;
    private readonly ScrollViewer _results;
    private readonly StackPanel _resultRows;
    private readonly TextBlock _none;

    /// <summary>The tree as the catalogue resolves it.</summary>
    private readonly IReadOnlyList<AddColumn> _menu;

    /// <summary>The work area, in the coordinates of the window's content (the render's layer).</summary>
    private readonly PixelRect _room;

    /// <summary>The columns drawn, the top ones then each open submenu, each its row buttons (null for a line) by row.</summary>
    private readonly List<Drawn> _columns = [];

    private readonly List<(Button Button, WidgetId Kind)> _hits = [];

    private readonly DispatcherQueueTimer? _hover;
    private AddSpot? _hovered;

    private AddMenuNav _nav = AddMenuNav.Start;

    /// <summary>The open spots the drawn submenu columns stand for.</summary>
    private IReadOnlyList<AddSpot> _drawnOpen = [];

    private int _hit = -1;

    /// <param name="pick">Runs when a widget is chosen.</param>
    /// <param name="close">Runs on Escape with nothing typed and nothing open.</param>
    /// <param name="room">The work area in DIPs, in the coordinates of the window's content: the submenus stay inside it.</param>
    public WidgetPicker(Action<WidgetDescriptor> pick, Action close, PixelRect room)
    {
        _pick = pick;
        _close = close;
        _room = room;
        _menu = AddTree.On([.. ModuleCatalog.Widgets.Select(definition => definition.Id)]);

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

        // [S-3512] The menu is its two top columns, whatever opens beside it.
        var topWidth = (_menu.Count * ColumnWidth) + (Math.Max(0, _menu.Count - 1) * ColumnGap);
        var box = new Grid { Children = { _search }, HorizontalAlignment = HorizontalAlignment.Left, Width = topWidth };
        Children.Add(box);

        // [S-3512] As tall as its taller top column, so the search's results do not resize it.
        var height = Math.Min(TallestColumn(), Math.Max(2 * LineHeight, room.Height * 0.7));

        _tree = new StackPanel { Orientation = Orientation.Horizontal, Spacing = ColumnGap, VerticalAlignment = VerticalAlignment.Top };
        _treeScroll = new ScrollViewer
        {
            Content = _tree,
            Height = height,
            Width = topWidth,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };

        _resultRows = new StackPanel { Width = topWidth };
        _none = Fluent.Text(FluentType.Body, "TextFillColorSecondaryBrush");
        _none.Margin = new Thickness(Fluent.Space3, Fluent.Space2, Fluent.Space3, Fluent.Space2);
        _results = new ScrollViewer
        {
            Content = _resultRows,
            Height = height,
            Width = topWidth,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Visibility = Visibility.Collapsed,
        };

        var body = new Grid { Children = { _treeScroll, _results } };
        SetRow(body, 1);
        Children.Add(body);

        if (DispatcherQueue.GetForCurrentThread() is { } dispatcher)
        {
            _hover = dispatcher.CreateTimer();
            _hover.Interval = Win32.MenuShowDelay();
            _hover.IsRepeating = false;
            _hover.Tick += (_, _) => OnHoverRested();
        }

        for (var c = 0; c < _menu.Count; c++)
        {
            AddColumnView(_menu[c].Shelf.Title.Text(), _menu[c].Rows, c);
        }

        // The flyout closed: its submenus go with it.
        Unloaded += (_, _) => CloseSubmenus(_menu.Count);

        Draw();
    }

    /// <summary>
    /// Debug renders only: draw the submenus on this canvas, which covers the stand-in work area,
    /// rather than in popups, which a render cannot capture. The picker's room is then the canvas's.
    /// </summary>
    internal Canvas? RenderLayer { get; set; }

    /// <summary>Put the caret in the search box. Called once the flyout is open.</summary>
    public void FocusSearch() => _search.Focus(FocusState.Programmatic);

    /// <summary>Type into the search box, for the debug render.</summary>
    internal void SetQuery(string text) => _search.Text = text;

    /// <summary>
    /// Open folders by name, outermost first, as the keys would, for the debug render
    /// (<c>--open System/Processor</c>). Names that are not open-able here are skipped.
    /// </summary>
    internal void OpenPath(IReadOnlyList<AddFolderId> ids)
    {
        foreach (var id in ids)
        {
            var columns = AddMenuKeys.Columns(_menu, _nav);
            var from = _nav.Open.Count == 0 ? 0 : columns.Count - 1;
            for (var c = from; c < columns.Count; c++)
            {
                var row = columns[c].ToList().FindIndex(r => r is AddFolder folder && folder.Shelf.Id == id);
                if (row >= 0)
                {
                    _nav = AddMenuKeys.Move(_menu, AddMenuKeys.Point(_menu, _nav, new AddSpot(c, row)), WidgetKey.Right).Nav;
                    break;
                }
            }
        }

        Draw();
    }

    /// <summary>The height of the taller top column: its heading, its rows and the gap over them.</summary>
    private double TallestColumn()
        => _menu.Max(column => LineHeight + RowsHeight(column.Rows, column.Rows.Count)) + Fluent.Space1 + Fluent.Hairline;

    /// <summary>The height of the first <paramref name="count"/> rows of a column, lines counted.</summary>
    private static double RowsHeight(IReadOnlyList<AddRow> rows, int count)
        => rows.Take(count).Sum(row => row is AddRule ? RuleHeight : LineHeight);

    private static (IndexedWidget Index, AddHome Home)[] SearchIndex(IReadOnlyList<AddColumn> menu)
    {
        var locale = Strings.Current;
        if (_searchIndex is { } built && built.Locale == locale)
        {
            return built.Entries;
        }

        // [S-3513] Each widget once, at its home, its folders' names searched too.
        (IndexedWidget, AddHome)[] entries =
            [.. AddTree.Homes(menu).Select(home => (WidgetSearch.Index(WidgetCatalog.SearchDoc(ModuleCatalog.Of(home.Kind), home.Path)), home))];
        _searchIndex = (locale, entries);
        return entries;
    }

    // ---- the tree -------------------------------------------------------------------------

    /// <summary>A drawn column: its view, its row buttons and rows, and for a submenu its popup and where it stands.</summary>
    private sealed record Drawn(FrameworkElement View, Button?[] Rows, IReadOnlyList<AddRow> Items, Popup? Popup = null, AddCascadeSpot? Spot = null);

    /// <summary>[S-3512] A submenu's frame and padding: how far its first row is under its card's top.</summary>
    private const double CardLead = Fluent.Hairline + Fluent.Space1;

    /// <summary>Draw a top column: a heading, then its rows.</summary>
    private void AddColumnView(string heading, IReadOnlyList<AddRow> rows, int column)
    {
        var stack = new StackPanel { Width = ColumnWidth, Margin = new Thickness(0, Fluent.Space1 + Fluent.Hairline, 0, 0) };
        var title = Fluent.Text(FluentType.Caption, "TextFillColorSecondaryBrush");
        title.Text = heading;
        title.Height = LineHeight;
        title.Padding = new Thickness(Fluent.Space3, Fluent.Space3, Fluent.Space3, 0);
        stack.Children.Add(title);

        var buttons = Rows(stack, rows, column);
        _columns.Add(new Drawn(stack, buttons, rows));
        _tree.Children.Add(stack);
    }

    /// <summary>A column's rows into <paramref name="stack"/>; the buttons by row, null for a line.</summary>
    private Button?[] Rows(StackPanel stack, IReadOnlyList<AddRow> rows, int column)
    {
        var buttons = new Button?[rows.Count];
        for (var r = 0; r < rows.Count; r++)
        {
            if (rows[r] is AddRule)
            {
                stack.Children.Add(Rule());
                continue;
            }

            var button = Row(rows[r], new AddSpot(column, r));
            buttons[r] = button;
            stack.Children.Add(button);
        }

        return buttons;
    }

    /// <summary>
    /// [S-3512] Open a submenu: its rows on a card of the flyout's own chrome (acrylic, stroke,
    /// corners, shadow), in a popup beside the row <paramref name="from"/> that opened it, its first
    /// row level with that row. No heading: the lit row it hangs from names it, as in Windows' menus.
    /// </summary>
    private void AddSubmenu(IReadOnlyList<AddRow> rows, int column, AddSpot from)
    {
        var stack = new StackPanel { Width = ColumnWidth };
        var buttons = Rows(stack, rows, column);

        var width = ColumnWidth + (2 * CardLead);
        var height = RowsHeight(rows, rows.Count) + (2 * CardLead);
        var (left, right, top, backward) = Hanger(from);
        var spot = AddCascade.Beside(left, right, top, CardLead, width, height, backward, Room());

        var card = new Border
        {
            Child = new ScrollViewer { Content = stack, VerticalScrollBarVisibility = ScrollBarVisibility.Auto },
            Padding = new Thickness(Fluent.Space1),
            Width = spot.Box.Width,
            Height = spot.Box.Height,
        };
        EditBar.Frame(card);

        Popup? popup = null;
        if (RenderLayer is { } layer)
        {
            var at = TransformToVisual(layer).TransformPoint(new Point(spot.Box.X, spot.Box.Y));
            Canvas.SetLeft(card, at.X);
            Canvas.SetTop(card, at.Y);
            layer.Children.Add(card);
        }
        else
        {
            // A child of the picker, so the flyout's light dismiss counts a press on it as inside.
            popup = new Popup
            {
                Child = card,
                HorizontalOffset = spot.Box.X,
                VerticalOffset = spot.Box.Y,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
            };
            Children.Add(popup);
            popup.IsOpen = true;
        }

        _columns.Add(new Drawn(card, buttons, rows, popup, spot));
    }

    /// <summary>
    /// The column a row is in (its left and right edge), the row's top, and which way the cascade
    /// runs from there, all in this control's coordinates.
    /// </summary>
    private (double Left, double Right, double Top, bool Backward) Hanger(AddSpot from)
    {
        var parent = _columns[from.Column];
        if (parent.Spot is { } spot)
        {
            return (spot.Box.X, spot.Box.X + spot.Box.Width, spot.Box.Y + CardLead + RowsHeight(parent.Items, from.Row), spot.Backward);
        }

        if (parent.Rows[from.Row] is { } row)
        {
            var box = row.TransformToVisual(this).TransformBounds(new Rect(0, 0, row.ActualWidth, row.ActualHeight));
            return (box.X, box.X + box.Width, box.Y, false);
        }

        return (0, ColumnWidth, 0, false);
    }

    /// <summary>The work area in this control's coordinates (the render's layer is its own work area).</summary>
    private PixelRect Room()
    {
        UIElement? origin = RenderLayer ?? XamlRoot?.Content;
        if (origin is null)
        {
            return _room;
        }

        var box = origin.TransformToVisual(this).TransformBounds(new Rect(_room.X, _room.Y, _room.Width, _room.Height));
        return new PixelRect(box.X, box.Y, box.Width, box.Height);
    }

    /// <summary>Close the submenus from column <paramref name="from"/> on (never a top column).</summary>
    private void CloseSubmenus(int from)
    {
        while (_columns.Count > Math.Max(from, _menu.Count))
        {
            var drawn = _columns[^1];
            _columns.RemoveAt(_columns.Count - 1);
            if (drawn.Popup is { } popup)
            {
                popup.IsOpen = false;
                popup.Child = null;
                Children.Remove(popup);
            }
            else
            {
                RenderLayer?.Children.Remove(drawn.View);
            }
        }
    }

    private static Border Rule() => new()
    {
        Height = Fluent.Hairline,
        Margin = new Thickness(Fluent.Space3, Fluent.Space1, Fluent.Space3, Fluent.Space1),
        Background = FluentLayout.Themed("DividerStrokeColorDefaultBrush", Colors.Gray),
    };

    /// <summary>One row: a widget's icon and name, or a folder's with a chevron. No preview ([S-3501]); a widget's description is its tooltip.</summary>
    private Button Row(AddRow row, AddSpot spot)
    {
        var (glyph, label, help) = row switch
        {
            AddFolder folder => (ModuleCatalog.Of(folder.Shelf.Icon).Glyph, folder.Shelf.Title.Text(), (string?)null),
            AddPick pick => (ModuleCatalog.Of(pick.Kind).Glyph, ModuleCatalog.Of(pick.Kind).Name, ModuleCatalog.Of(pick.Kind).Describe()),
            _ => (string.Empty, string.Empty, null),
        };

        var button = RowButton(glyph, label, help, row is AddFolder, LineHeight, path: null);
        button.PointerEntered += (_, _) => OnHover(spot);
        button.PointerExited += (_, _) => OnHoverLeft(spot);
        button.Click += (_, _) => OnRowClicked(spot);
        return button;
    }

    /// <summary>The shape every row of the menu shares: icon, name (and where it lives, for a result), a folder's chevron.</summary>
    private static Button RowButton(string glyph, string label, string? help, bool folder, double height, string? path)
    {
        var name = Fluent.Text(FluentType.Body);
        name.Text = label;

        // A grid, not a stack: a stack sized the name to the path under it and cut it off.
        var words = new Grid { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Stretch, Children = { name } };
        if (path is not null)
        {
            words.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            words.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var where = Fluent.Text(FluentType.Caption, "TextFillColorSecondaryBrush");
            where.Text = path;
            SetRow(where, 1);
            words.Children.Add(where);
        }

        var icon = Fluent.Icon(glyph, "TextFillColorPrimaryBrush");
        var content = new Grid
        {
            ColumnSpacing = Fluent.Space3,
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto },
            },
            Children = { icon, words },
        };
        SetColumn(words, 1);

        if (folder)
        {
            var chevron = Fluent.Icon(Glyphs.ChevronRight, "TextFillColorSecondaryBrush", Fluent.IconSize * 0.75);
            SetColumn(chevron, 2);
            content.Children.Add(chevron);
        }

        var button = new Button
        {
            Content = content,
            Height = height,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Background = Rest(),
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(Fluent.ControlRadius),
            Padding = new Thickness(Fluent.Space3, 0, Fluent.Space3, 0),

            // The search box keeps the keyboard; the menu is walked with the arrows from there.
            IsTabStop = false,
        };

        if (help is not null)
        {
            ToolTipService.SetToolTip(button, help);
            AutomationProperties.SetHelpText(button, help);
        }

        AutomationProperties.SetName(button, path is null ? label : $"{label}, {path}");
        return button;
    }

    /// <summary>[S-3514] A click opens a folder at once, or adds the widget.</summary>
    private void OnRowClicked(AddSpot spot)
    {
        _hover?.Stop();
        var columns = AddMenuKeys.Columns(_menu, _nav);
        if (spot.Column < columns.Count && spot.Row < columns[spot.Column].Count && columns[spot.Column][spot.Row] is AddPick pick)
        {
            Pick(pick.Kind);
            return;
        }

        _nav = AddMenuKeys.Point(_menu, _nav, spot);
        Draw();
    }

    /// <summary>[S-3514] The pointer rests on a row for Windows' menu delay before the cascade follows it.</summary>
    private void OnHover(AddSpot spot)
    {
        _hovered = spot;
        if (_hover is null)
        {
            return;
        }

        _hover.Stop();
        _hover.Start();
    }

    private void OnHoverLeft(AddSpot spot)
    {
        if (_hovered == spot)
        {
            _hovered = null;
            _hover?.Stop();
        }
    }

    private void OnHoverRested()
    {
        if (_hovered is not { } spot || _search.Text.Trim().Length > 0)
        {
            return;
        }

        var next = AddMenuKeys.Point(_menu, _nav, spot);
        if (!SameOpen(next.Open, _nav.Open))
        {
            _nav = next;
            Draw();
        }
    }

    /// <summary>
    /// Draw the tree for <see cref="_nav"/>: the submenu columns that changed are built again (never
    /// the one under the pointer, which only gains or loses a column beyond it), and the highlight and
    /// the open rows are lit.
    /// </summary>
    private void Draw()
    {
        var open = AddMenuKeys.OpenFolders(_menu, _nav);
        var spots = _nav.Open.Take(open.Count).ToArray();

        var same = 0;
        while (same < spots.Length && same < _drawnOpen.Count && spots[same] == _drawnOpen[same])
        {
            same++;
        }

        CloseSubmenus(_menu.Count + same);

        for (var i = same; i < open.Count; i++)
        {
            AddSubmenu(open[i].Rows, _menu.Count + i, spots[i]);
        }

        _drawnOpen = spots;

        for (var c = 0; c < _columns.Count; c++)
        {
            var rows = _columns[c].Rows;
            for (var r = 0; r < rows.Length; r++)
            {
                if (rows[r] is not { } button)
                {
                    continue;
                }

                var spot = new AddSpot(c, r);
                var lit = spot == _nav.Cursor || spots.Contains(spot);
                button.Background = lit ? Lit() : Rest();
            }
        }

        if (_nav.Cursor is { } cursor && cursor.Column < _columns.Count && _columns[cursor.Column].Rows[cursor.Row] is { } shown)
        {
            shown.StartBringIntoView();
        }
    }

    private static bool SameOpen(IReadOnlyList<AddSpot> a, IReadOnlyList<AddSpot> b) => a.SequenceEqual(b);

    // ---- the search -----------------------------------------------------------------------

    /// <summary>
    /// [S-3515] Typing lists what matches in place of the tree, flat, best first, each with where it
    /// lives; the menu keeps its size. An empty box is the tree, as it was left closed.
    /// </summary>
    private void Refresh()
    {
        var query = _search.Text;
        var searching = WidgetSearch.Tokenize(query).Length > 0;

        _hover?.Stop();
        _results.Visibility = searching ? Visibility.Visible : Visibility.Collapsed;
        _treeScroll.Visibility = searching ? Visibility.Collapsed : Visibility.Visible;

        _hits.Clear();
        _resultRows.Children.Clear();
        _hit = -1;

        if (!searching)
        {
            _nav = AddMenuNav.Start;
            Draw();
            return;
        }

        var ranked = WidgetSearch.Rank(SearchIndex(_menu).Select(entry => (entry.Index, entry.Home)), query);
        if (ranked.Count == 0)
        {
            _none.Text = Str.Shell_Picker_NoMatch.Format(("query", query.Trim()));
            _resultRows.Children.Add(_none);
            return;
        }

        foreach (var home in ranked)
        {
            var definition = ModuleCatalog.Of(home.Kind);
            var path = string.Join(" › ", home.Path.Select(shelf => shelf.Title.Text()));
            var button = RowButton(definition.Glyph, definition.Name, definition.Describe(), folder: false, ResultHeight, path);
            var kind = home.Kind;
            button.Click += (_, _) => Pick(kind);
            _resultRows.Children.Add(button);
            _hits.Add((button, kind));
        }

        LightHit();
    }

    /// <summary>What Enter adds in the results: the highlight, else the best match (drawn highlighted).</summary>
    private int? HitTarget() => _hit >= 0 && _hit < _hits.Count ? _hit : _hits.Count > 0 ? 0 : null;

    private void LightHit()
    {
        var target = HitTarget();
        for (var i = 0; i < _hits.Count; i++)
        {
            _hits[i].Button.Background = i == target ? Lit() : Rest();
        }

        if (target is { } shown)
        {
            _hits[shown].Button.StartBringIntoView();
        }
    }

    // ---- keys -----------------------------------------------------------------------------

    /// <summary>[S-3514] [S-3515] The menu's keys, heard in the search box, which keeps the focus.</summary>
    private void OnSearchKeyDown(object sender, KeyRoutedEventArgs e)
    {
        try
        {
            e.Handled = _results.Visibility == Visibility.Visible ? ResultKey(e.Key) : TreeKey(e.Key);
        }
        catch (Exception ex)
        {
            Log.Error(Scope, $"the add menu's key {e.Key} failed", ex);
        }
    }

    private bool ResultKey(VirtualKey key)
    {
        switch (key)
        {
            case VirtualKey.Enter:
                if (HitTarget() is { } target)
                {
                    Pick(_hits[target].Kind);
                }

                return true;

            case VirtualKey.Escape:
                _search.Text = string.Empty;
                return true;
        }

        if (Map(key, onRow: _hit >= 0) is not { } move || AddMenuKeys.MoveInList(_hits.Count, _hit, move) is not { } next)
        {
            return false; // the caret's
        }

        _hit = next;
        LightHit();
        return true;
    }

    private bool TreeKey(VirtualKey key)
    {
        // Spaces alone search nothing, but Escape empties them before it closes anything.
        if (key == VirtualKey.Escape && _search.Text.Length > 0)
        {
            _search.Text = string.Empty;
            return true;
        }

        if (Map(key, onRow: _nav.Cursor is not null) is not { } move)
        {
            return false;
        }

        var step = AddMenuKeys.Move(_menu, _nav, move);
        switch (step.Action)
        {
            case AddMenuAction.Unhandled:
                return false;
            case AddMenuAction.Pick:
                Pick(step.Kind);
                return true;
            case AddMenuAction.Close:
                _close();
                return true;
            default:
                _hover?.Stop();
                _nav = step.Nav;
                Draw();
                return true;
        }
    }

    /// <summary>
    /// The menu's key for a pressed one. Right to left the cascade opens to the left, so the arrows
    /// swap ([S-1113]). Space is the menu's only on a row: in the box it is typed.
    /// </summary>
    private static WidgetKey? Map(VirtualKey key, bool onRow)
    {
        var rtl = UiLanguage.Flow == FlowDirection.RightToLeft;
        return key switch
        {
            VirtualKey.Up => WidgetKey.Up,
            VirtualKey.Down => WidgetKey.Down,
            VirtualKey.Left => rtl ? WidgetKey.Right : WidgetKey.Left,
            VirtualKey.Right => rtl ? WidgetKey.Left : WidgetKey.Right,
            VirtualKey.Home => WidgetKey.Home,
            VirtualKey.End => WidgetKey.End,
            VirtualKey.Enter => WidgetKey.Enter,
            VirtualKey.Escape => WidgetKey.Escape,
            VirtualKey.Space when onRow => WidgetKey.Space,
            _ => null,
        };
    }

    private void Pick(WidgetId kind)
    {
        try
        {
            _hover?.Stop();
            _pick(ModuleCatalog.Of(kind));
        }
        catch (Exception ex)
        {
            Log.Error(Scope, $"picking {kind} failed", ex);
        }
    }

    private static Microsoft.UI.Xaml.Media.Brush Rest() => FluentLayout.Themed("SubtleFillColorTransparentBrush", Colors.Transparent);

    private static Microsoft.UI.Xaml.Media.Brush Lit() => FluentLayout.Themed("SubtleFillColorSecondaryBrush", Colors.Transparent);
}
