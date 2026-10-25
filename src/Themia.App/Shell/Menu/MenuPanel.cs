using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Core.Geometry;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;

namespace Themia.App.Shell;

/// <summary>
/// [S-3733] What a menu's submenus keep clear of, in the coordinates of the layer the menu stands
/// on: the work area they stay inside, the selected widgets' box (none for a menu that is not the
/// selection's), and the bars.
/// </summary>
internal sealed record MenuRoom(PixelRect Area, PixelRect? Selection, IReadOnlyList<PixelRect> Bars);

/// <summary>What a <see cref="MenuNode"/> is drawn as.</summary>
internal enum MenuNodeKind
{
    Line,
    Separator,

    /// <summary>Something drawn whole, not a line (a colour submenu's swatches), drawn again when its <see cref="MenuNode.ValueKey"/> changes.</summary>
    Custom,

    /// <summary>[S-3728] Words only, greyed and never clickable.</summary>
    Note,

    /// <summary>[S-3725] Room kept free under a search's answers, <see cref="MenuNode.Height"/> tall.</summary>
    Spacer,

    /// <summary>[S-3731] An inline slider: its words, the slider and its value in one line.</summary>
    Slider,

    /// <summary>[S-3561] A line of text typed in the menu: its words over the box ([S-3556]: a password's masked).</summary>
    Field,
}

/// <summary>
/// [S-3561] [S-3556] A field line's box: its hint, what a change does, and for a password's
/// (<see cref="Masked"/>) whether it holds one, what a box built again starts with, and what its
/// submenu closing wipes. A masked field's words are never in its node.
/// </summary>
internal sealed record MenuField(string Placeholder, Action<string> Set)
{
    public bool Masked { get; init; }

    public bool HasValue { get; init; }

    public Func<string>? Fill { get; init; }

    public Action? Wipe { get; init; }

    public bool Numeric { get; init; }
}

/// <summary>
/// One line of a menu or of a submenu: its words, the value at its end, its icon (and what the
/// icon is, to tell a change), a tick or a dot, a submenu or a search, or what a click does; a
/// danger line is drawn in the critical colour. <see cref="Key"/> names it across rebuilds.
/// </summary>
internal sealed record MenuNode(string Key, MenuNodeKind Kind = MenuNodeKind.Line)
{
    public string Text { get; init; } = string.Empty;

    public string? Value { get; init; }

    public string IconKey { get; init; } = string.Empty;

    public Func<UIElement?>? Icon { get; init; }

    public Action? Run { get; init; }

    public bool? Checked { get; init; }

    public bool Radio { get; init; }

    public bool Disabled { get; init; }

    public bool Danger { get; init; }

    public double Height { get; init; }

    public IReadOnlyList<MenuNode>? Children { get; init; }

    public MenuSearch? Search { get; init; }

    public Func<FrameworkElement>? Build { get; init; }

    public string ValueKey { get; init; } = string.Empty;

    /// <summary>[S-1862] Where what the widget waits for is set: <see cref="MenuPanel.OpenSetup"/> opens the menu at it.</summary>
    public bool SetsUp { get; init; }

    /// <summary>[S-3731] The slider of a <see cref="MenuNodeKind.Slider"/> line.</summary>
    public MenuSlider? Slider { get; init; }

    /// <summary>[S-3732] An ordered list's item: its line is dragged up and down among the list's.</summary>
    public MenuOrder? Order { get; init; }

    /// <summary>[S-3537] The hint of a box on top of its submenu that keeps the lines matching what is typed (a long choice: the languages).</summary>
    public string? Filter { get; init; }

    /// <summary>[S-3537] Words a filter box finds the line by besides its own.</summary>
    public string? Keywords { get; init; }

    /// <summary>[S-3561] The box of a <see cref="MenuNodeKind.Field"/> line.</summary>
    public MenuField? Field { get; init; }

    /// <summary>[S-3561] A note about something being done: a ring before its words.</summary>
    public bool Busy { get; init; }

    public static MenuNode Separator(string key) => new(key, MenuNodeKind.Separator);

    /// <summary>Whether a click on the line opens a level of its own (a submenu or a search).</summary>
    public bool Opens => Children is not null || Search is not null;
}

/// <summary>
/// [S-3716] The one menu engine: a list of <see cref="MenuNode"/>s drawn as Windows 11 draws a
/// context menu (the flyout's surface, 32-DIP lines with an icon column, the words, a value column
/// and at the end the mark or the chevron), but one that stays open while it is used, and so do its
/// submenus: a pick in one is drawn where it is, and the menu stays as it was ([S-3718]). The
/// selection's properties menu (<see cref="PropertiesStrip"/>) and the edit bar's settings menu
/// (<see cref="SettingsMenu"/>) are this engine with their own frame and lines.
/// <para>
/// The lines are handed in again on every change (<see cref="Present"/>). They are drawn again only
/// when a list's shape changed (which lines there are); a change of value (a tick, a dot, the words
/// at the end, an icon) is written into the lines in place, so an open submenu, its search box and
/// the focus stay where they are. The submenus are drawn by the menu itself, beside it in the same
/// window, never as light-dismiss popups: a click in one does what it does and leaves it open.
/// </para>
/// </summary>
internal partial class MenuPanel : Grid
{
    private const string Scope = "menu";

    /// <summary>[S-3716] A line's height and its parts, as the Windows 11 menu draws them.</summary>
    protected const double LineHeight = 32;

    /// <summary>A column of a line before its words: an icon (16) and its gap (12).</summary>
    private const double LeadColumn = Fluent.IconSize + Fluent.Space3;

    /// <summary>
    /// [S-3734] The one width of the menu and of every submenu, searches included: room for the
    /// longest common lines (a city and its country, "Item background" and its colour's name), so the
    /// levels line up and the menu does not change size as it is used. Longer words are trimmed,
    /// the whole as their tooltip.
    /// </summary>
    internal const double MenuWidth = 300;

    private readonly Border _frame = new() { Width = MenuWidth };

    private readonly StackPanel _menu = new();

    /// <summary>
    /// [S-3718] The open submenus' layer: a canvas of no size at the menu's corner. What it holds is
    /// drawn beside the menu, outside its box, so the menu's size (and so where it stands, [S-3708])
    /// never counts it, and it is drawn in the window like the menu.
    /// </summary>
    private readonly Canvas _subs = new() { Width = 0, Height = 0, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };

    /// <summary>The menu's list first, then each open submenu, deepest last.</summary>
    private readonly List<Level> _levels = [];

    /// <summary>What the head (the lines over the first level) was drawn for.</summary>
    private string _headKey = string.Empty;

    /// <summary>[S-3733] What the submenus keep clear of; null until the surface has placed the menu (the window then).</summary>
    private MenuRoom? _room;

    /// <summary>[S-3540] The first level's list, scrolled within <see cref="MaxListHeight"/>.</summary>
    private ScrollViewer? _listScroll;

    private double _maxListHeight = double.PositiveInfinity;

    protected MenuPanel()
    {
        _frame.Child = _menu;
        Children.Add(_frame);

        // [S-3715] A flyout's surface: it stands apart from the cards and the wallpaper beside it.
        FlyoutSurface.Apply(this, _frame);
        Children.Add(_subs);

        // [S-3707] The keyboard stays in the menu: Tab goes round its lines and stays in it, Up and
        // Down move between them, and Escape closes the deepest submenu, then the menu's own.
        _frame.TabFocusNavigation = KeyboardNavigationMode.Cycle;
        _menu.XYFocusKeyboardNavigation = XYFocusKeyboardNavigationMode.Enabled;
        KeyDown += OnKeyDown;

        // A press on the menu's padding or words is the menu's: it must not reach the surface,
        // whose press on empty space starts a rubber band and clears the selection.
        _frame.PointerPressed += (_, e) => e.Handled = true;

        // Put away (a drag, edit mode left): its submenus go with it.
        RegisterPropertyChangedCallback(VisibilityProperty, (_, _) =>
        {
            if (Visibility != Visibility.Visible)
            {
                CloseFrom(1);
            }
        });

        HorizontalAlignment = HorizontalAlignment.Left;
        VerticalAlignment = VerticalAlignment.Top;
        Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// [S-3733] What the submenus keep clear of, given by the surface each time it places the menu
    /// (in the coordinates of the layer the menu stands on); the open submenus are placed again.
    /// </summary>
    public MenuRoom? Room
    {
        get => _room;
        set
        {
            _room = value;
            for (var i = 1; i < _levels.Count; i++)
            {
                Place(_levels[i], i);
            }
        }
    }

    /// <summary>
    /// [S-3540] [S-918] The most the first level's list may be tall (the work area under the edit bar,
    /// for a menu dropped from it): a longer list scrolls inside it. The head above it is not counted.
    /// </summary>
    public double MaxListHeight
    {
        get => _maxListHeight;
        set
        {
            _maxListHeight = value;
            if (_listScroll is { } scroll)
            {
                scroll.MaxHeight = ListHeight(value);
            }
        }
    }

    /// <summary>What a list may be tall in <paramref name="room"/>: the frame's stroke off it, never fewer than four lines.</summary>
    private static double ListHeight(double room)
        => double.IsFinite(room) ? Math.Max(LineHeight * 4, room - (2 * Fluent.Hairline)) : double.PositiveInfinity;

    /// <summary>The menu's first level as last shown.</summary>
    protected IReadOnlyList<MenuNode> FirstLevel => _levels.Count > 0 ? _levels[0].Nodes : [];

    /// <summary>Whether a submenu is open.</summary>
    public bool HasOpenSubmenu => _levels.Count > 1;

    /// <summary>The element a question asked beside the menu stands by: the deepest open level, else the menu.</summary>
    protected FrameworkElement AskAnchor => _levels.Count > 1 ? _levels[^1].Root : _frame;

    /// <summary>Close every submenu; the menu's own list stays.</summary>
    public void CloseSubmenus() => CloseFrom(1);

    /// <summary>
    /// Show <paramref name="nodes"/> as the first level, under the head <paramref name="head"/> builds
    /// (for <paramref name="headKey"/>); null nodes show nothing. Drawn again when the head's key or
    /// the first level's shape changed (or <paramref name="rebuild"/>, a language switch), the open
    /// submenus opened again over the new lines by their keys; otherwise a change of value is written
    /// into the lines in place, the open submenus' too, so a submenu left open stays open on the pick
    /// ([S-3718]).
    /// </summary>
    protected void Present(string headKey, List<MenuNode>? nodes, Func<IEnumerable<UIElement>>? head, bool rebuild = false)
    {
        if (rebuild || nodes is null || !string.Equals(headKey, _headKey, StringComparison.Ordinal) || _levels.Count == 0
            || !string.Equals(Shape(nodes), _levels[0].Shape, StringComparison.Ordinal))
        {
            Build(headKey, nodes, head);
            return;
        }

        Update(_levels[0], nodes);

        for (var i = 1; i < _levels.Count; i++)
        {
            var level = _levels[i];
            var key = level.Anchor!.Node.Key;
            if (_levels[i - 1].Views.FirstOrDefault(v => v is not null && v.Node.Opens && v.Node.Key == key) is not { } anchor)
            {
                CloseFrom(i);
                break;
            }

            level.Anchor = anchor;
            Lit(anchor, true);
            if (anchor.Node.Search is { } search)
            {
                level.Search = search;
            }

            var children = LevelNodes(level, anchor.Node);
            if (string.Equals(Shape(children), level.Shape, StringComparison.Ordinal))
            {
                Update(level, children);
            }
            else
            {
                FillLevel(level, children);
                Place(level, i);
            }
        }
    }

    /// <summary>A rebuild is about to replace every level: what a level holds that must land lands now.</summary>
    protected virtual void OnRebuilding()
    {
    }

    /// <summary>A level whose lines are <paramref name="lines"/> is about to close.</summary>
    protected virtual void OnLevelClosing(Panel lines)
    {
    }

    /// <summary>[S-3707] Escape with no submenu open: the menu's own (hand the keyboard back, or close).</summary>
    protected virtual void OnEscape()
    {
    }

    /// <summary>What the menu asked beside itself ([S-3721], [S-3727]) has closed.</summary>
    protected virtual void OnAskClosed()
    {
    }

    private void Build(string headKey, List<MenuNode>? nodes, Func<IEnumerable<UIElement>>? head)
    {
        OnRebuilding();

        // [S-3718] Which submenus were open, by their lines' keys: opened again over the new lines.
        var open = _levels.Skip(1).Select(l => l.Anchor!.Node.Key).ToList();

        // [S-3556] They are opened again at once, so what their fields hold is not wiped on the way.
        _reopening = true;
        try
        {
            CloseFrom(1);
        }
        finally
        {
            _reopening = false;
        }

        _levels.Clear();
        _menu.Children.Clear();
        _headKey = headKey;

        if (nodes is null)
        {
            return;
        }

        foreach (var part in head?.Invoke() ?? [])
        {
            _menu.Children.Add(part);
        }

        var list = new StackPanel { Padding = new Thickness(0, Fluent.Space1, 0, Fluent.Space1), XYFocusKeyboardNavigation = XYFocusKeyboardNavigationMode.Enabled };
        var overlay = Overlay();

        // [S-3540] A long list scrolls inside the room it is given; with no limit it is as tall as its lines.
        _listScroll = new ScrollViewer
        {
            Content = new Grid { Children = { list, overlay } },
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            MaxHeight = ListHeight(_maxListHeight),
        };
        _menu.Children.Add(_listScroll);

        var main = new Level(list, _frame, overlay) { Icons = true };
        _levels.Add(main);
        FillLevel(main, nodes);

        foreach (var key in open)
        {
            if (_levels[^1].Views.FirstOrDefault(v => v is not null && v.Node.Opens && v.Node.Key == key) is not { } anchor
                || !OpenSubmenu(anchor, focus: false))
            {
                break;
            }
        }
    }

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != Windows.System.VirtualKey.Escape || e.Handled)
        {
            return;
        }

        e.Handled = true;
        if (_levels.Count > 1)
        {
            var anchor = _levels[^1].Anchor;
            CloseFrom(_levels.Count - 1);
            anchor?.Button.Focus(FocusState.Keyboard);
            return;
        }

        OnEscape();
    }

    /// <summary>
    /// [S-3718] A click on a line: a submenu line opens its submenu beside it (or closes it again),
    /// and one that also does something ([S-3541]: a view's line shows the view) does it as it opens;
    /// anything else does what it does, and the menu and the submenu it is in stay open.
    /// </summary>
    private void OnLine(Level level, LineView view)
    {
        // [S-3732] The release that ends a drag is not a click on the line it started on.
        if (_swallowClick)
        {
            _swallowClick = false;
            return;
        }

        var at = _levels.IndexOf(level);
        if (at < 0)
        {
            return;
        }

        if (view.Node.Opens)
        {
            var wasOpen = _levels.Count > at + 1 && ReferenceEquals(_levels[at + 1].Anchor, view);
            CloseFrom(at + 1);
            if (!wasOpen)
            {
                OpenSubmenu(view, focus: false);
                Run(view.Node.Run);
            }

            return;
        }

        CloseFrom(at + 1);
        Run(view.Node.Run);
    }

    /// <summary>[S-3718] Open the submenu of <paramref name="anchor"/>'s line beside the level it is in; false when it has nothing.</summary>
    private bool OpenSubmenu(LineView anchor, bool focus)
    {
        if (!anchor.Node.Opens || !anchor.Button.IsEnabled || anchor.Node.Children is { Count: 0 })
        {
            return false;
        }

        var lines = new StackPanel { Padding = new Thickness(0, Fluent.Space1, 0, Fluent.Space1), XYFocusKeyboardNavigation = XYFocusKeyboardNavigationMode.Enabled };
        var overlay = Overlay();
        var scroll = new ScrollViewer
        {
            Content = new Grid { Children = { lines, overlay } },
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,

            // [S-3733] Never taller than the work area: a long list scrolls inside it.
            MaxHeight = Math.Max(LineHeight * 4, RoomArea().Height - (SubmenuPlacement.Gap * 2) - (Fluent.Space1 * 2) - LineHeight),
        };
        var body = new Grid { RowDefinitions = { new RowDefinition { Height = GridLength.Auto }, new RowDefinition { Height = GridLength.Auto } } };
        Grid.SetRow(scroll, 1);
        body.Children.Add(scroll);

        var search = anchor.Node.Search;
        var frame = new Border { Child = body, Width = MenuWidth, TabFocusNavigation = KeyboardNavigationMode.Cycle };
        var host = new Grid { Children = { frame } };
        FlyoutSurface.Apply(host, frame);
        host.PointerPressed += (_, e) => e.Handled = true;

        var level = new Level(lines, host, overlay) { Anchor = anchor, Search = search };
        if (search is not null)
        {
            var box = SearchBox(level, search);
            body.Children.Add(box);
            level.Box = box;
        }
        else if (anchor.Node.Filter is { } hint)
        {
            var box = FilterBox(level, hint);
            body.Children.Add(box);
            level.Box = box;
        }

        FillLevel(level, LevelNodes(level, anchor.Node));
        _subs.Children.Add(host);
        _levels.Add(level);
        Lit(anchor, true);
        Place(level, _levels.Count - 1);

        if (search is not null)
        {
            Ask(level, string.Empty);
            if (focus)
            {
                level.Box!.Focus(FocusState.Keyboard);
            }
        }
        else if (focus && level.Box is { } filter)
        {
            filter.Focus(FocusState.Keyboard);
        }
        else if (focus && FocusManager.FindFirstFocusableElement(lines) is Control first)
        {
            first.Focus(FocusState.Keyboard);
        }

        return true;
    }

    /// <summary>Close the submenus from level <paramref name="at"/> down; the menu's own list (level 0) stays.</summary>
    private void CloseFrom(int at)
    {
        for (var i = _levels.Count - 1; i >= Math.Max(1, at); i--)
        {
            var level = _levels[i];
            level.Pending?.Cancel();
            level.Settle?.Stop();

            OnLevelClosing(level.Lines);
            CloseFields(level);

            if (_drag is { } drag && ReferenceEquals(drag.Level, level))
            {
                EndDrag(drop: false);
            }

            foreach (var slider in level.Sliders.Values.Where(s => s.Pending))
            {
                slider.Pending = false;
                Run(slider.Node.Slider!.Cancel);
            }

            _subs.Children.Remove(level.Root);
            if (level.Anchor is { } anchor)
            {
                Lit(anchor, false);
            }

            _levels.RemoveAt(i);
        }
    }

    /// <summary>
    /// Open the submenus along <paramref name="keys"/> (the lines' keys, first level first), as
    /// clicks would: a submenu or a search opens (its first line or its box focused when
    /// <paramref name="focus"/>, the last one only), any other line runs as its click does (a question
    /// asked beside the menu). The submenus open now are closed first. False when a line on the way is
    /// not there or cannot open.
    /// </summary>
    public bool OpenPath(IReadOnlyList<string> keys, bool focus)
    {
        ArgumentNullException.ThrowIfNull(keys);
        if (_levels.Count == 0)
        {
            return false;
        }

        CloseFrom(1);
        UpdateLayout();
        for (var i = 0; i < keys.Count; i++)
        {
            var key = keys[i];
            if (_levels[^1].Views.FirstOrDefault(v => v is not null && v.Node.Key == key) is not { } line || !line.Button.IsEnabled)
            {
                return false;
            }

            if (line.Node.Opens)
            {
                if (!OpenSubmenu(line, focus: focus && i == keys.Count - 1))
                {
                    return false;
                }
            }
            else
            {
                line.Button.Focus(FocusState.Programmatic);
                Run(line.Node.Run);
            }
        }

        return true;
    }

    /// <summary>
    /// [S-1862] The card's Set up button: open the menu at the line the widget's module names for what
    /// it waits for (<see cref="WidgetMenuLines.SetsUp"/>), through the submenus on the way, as clicks
    /// would (<see cref="OpenPath"/>). A widget that names none keeps the menu as it stands. False when
    /// no line was opened.
    /// </summary>
    public bool OpenSetup()
        => _levels.Count > 0 && SetupPath(_levels[0].Nodes) is { } path && OpenPath(path, focus: true);

    /// <summary>[S-1862] The keys of the lines from the first level down to the one that sets the widget up, or null.</summary>
    private static List<string>? SetupPath(IReadOnlyList<MenuNode> nodes)
    {
        foreach (var node in nodes)
        {
            if (node.SetsUp)
            {
                return [node.Key];
            }

            if (node.Children is { } children && SetupPath(children) is { } below)
            {
                below.Insert(0, node.Key);
                return below;
            }
        }

        return null;
    }

    /// <summary>[S-3707] The keyboard into the menu, on the first line of its deepest open level. False when there is none.</summary>
    public bool FocusFirst()
    {
        if (_levels.Count == 0 || FocusManager.FindFirstFocusableElement(_levels[^1].Lines) is not Control first)
        {
            return false;
        }

        first.Focus(FocusState.Keyboard);
        return true;
    }

    /// <summary>Run a command from inside a XAML event: a throw is logged, never left to escape.</summary>
    protected static void Run(Action? action)
    {
        try
        {
            action?.Invoke();
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "a menu command failed", ex);
        }
    }

    /// <summary>Run <paramref name="action"/> once the event in progress has finished: a move draws the line it is raised on again.</summary>
    private void Later(Action action)
    {
        if (DispatcherQueue is not { } queue || !queue.TryEnqueue(() => Run(action)))
        {
            Log.Error(Scope, "could not queue a menu list's move");
        }
    }

#if DEBUG
    /// <summary>
    /// Debug renders only: open the submenus along <paramref name="path"/> (the lines' words, "/"
    /// between levels), as clicks would; a last line that opens nothing is clicked (what it asks
    /// beside the menu shows). False when a line on the way is not there.
    /// </summary>
    internal bool DebugOpen(string path)
    {
        var parts = path.Split('/');
        for (var i = 0; i < parts.Length; i++)
        {
            var words = parts[i];
            if (_levels.Count == 0)
            {
                return false;
            }

            var level = _levels[^1];
            if (level.Views.FirstOrDefault(v => v is not null && v.Node.Opens && v.Node.Text == words) is { } line)
            {
                if (!OpenSubmenu(line, focus: false))
                {
                    return false;
                }

                continue;
            }

            if (i < parts.Length - 1 || !DebugClick(words))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Debug renders only: click the deepest open level's line of <paramref name="words"/> (Show all, a line that asks). False when it is not there.</summary>
    internal bool DebugClick(string words)
    {
        if (_levels.Count == 0 || _levels[^1].Views.FirstOrDefault(v => v is not null && v.Node.Text == words) is not { } line)
        {
            return false;
        }

        OnLine(_levels[^1], line);
        return true;
    }

    /// <summary>Debug renders only: click the deepest open level's line of <paramref name="key"/> (a line that asks beside the menu). False when it is not there.</summary>
    internal bool DebugClickKey(string key)
    {
        if (_levels.Count == 0 || _levels[^1].Views.FirstOrDefault(v => v is not null && v.Node.Key == key) is not { } line)
        {
            return false;
        }

        OnLine(_levels[^1], line);
        return true;
    }

    /// <summary>Debug renders only: type into the deepest open submenu's search box.</summary>
    internal bool DebugType(string text)
    {
        if (_levels.Count < 2 || _levels[^1].Box is not { } box)
        {
            return false;
        }

        box.Text = text;
        return true;
    }
#endif
}
