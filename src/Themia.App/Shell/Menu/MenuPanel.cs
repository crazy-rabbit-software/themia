using System.Numerics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Themia.App.Shell.Motion;
using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;

namespace Themia.App.Shell;

/// <summary>What a <see cref="MenuNode"/> is drawn as.</summary>
internal enum MenuNodeKind
{
    Line,
    Separator,

    /// <summary>Something drawn whole, not a row (a colour level's swatches, the icon and font pickers), drawn again when its <see cref="MenuNode.ValueKey"/> changes.</summary>
    Custom,

    /// <summary>[S-3728] Words only, greyed, never lit or picked, skipped by the keys.</summary>
    Note,

    /// <summary>[S-3731] An inline slider: its words, a compact track and its value in one row.</summary>
    Slider,

    /// <summary>[S-3736] A text level's field, filling its row.</summary>
    Field,

    /// <summary>[S-3740] A heading: 12-DIP semibold words in the secondary colour over a group.</summary>
    Heading,
}

/// <summary>
/// [S-3736] What a text level asks for: its name (the head's words), the field's hint and what it
/// starts with, and what is done with what is typed. <see cref="Accept"/> is the words of the row that
/// gives it (Add, Save): with one the text is given only by it or Enter, and going back drops it; with
/// none going back keeps what was typed (a title, a form's field). <see cref="Live"/> hears each letter
/// (a form's field), and Escape hands it <see cref="Initial"/> back. A <see cref="Masked"/> one is a
/// password's ([S-3556]): its words are never in a node, <see cref="Fill"/> gives the box what the form
/// holds.
/// </summary>
internal sealed record MenuEdit(string Title, string Placeholder, string Initial, Action<string> Commit)
{
    public Func<string, string?>? Refuse { get; init; }

    public bool AllowEmpty { get; init; } = true;

    public string? Accept { get; init; }

    public Action<string>? Live { get; init; }

    public bool Masked { get; init; }

    public Func<string>? Fill { get; init; }

    public bool Numeric { get; init; }

    /// <summary>Why <paramref name="typed"/> cannot be given: null when it can, empty when it cannot but needs no words (nothing typed).</summary>
    public string? Reason(string typed)
    {
        var text = (typed ?? string.Empty).Trim();
        return text.Length == 0 ? (AllowEmpty ? null : string.Empty) : Refuse?.Invoke(text);
    }
}

/// <summary>[S-3737] An "are you sure?" asked on a level of the menu's own: the question, what goes with it, the words of the row that does it.</summary>
internal sealed record MenuQuestion(string Question, string Accept, Action Done, bool Danger, string? Detail);

/// <summary>
/// One row of a menu or of one of its levels: its words, the value at its end (words, or a swatch:
/// <see cref="ValueIcon"/>), its icon (and what the icon is, to tell a change), a switch, a tick, a
/// level of its own (rows, a search, or a question its click asks: <see cref="Asks"/>), a link, or what
/// a click does; a danger row is drawn in the critical colour. <see cref="Key"/> names it across
/// rebuilds and languages.
/// </summary>
internal sealed record MenuNode(string Key, MenuNodeKind Kind = MenuNodeKind.Line)
{
    public string Text { get; init; } = string.Empty;

    public string? Value { get; init; }

    /// <summary>[S-3739] A picture in the value's place (the card's own background swatch on Appearance).</summary>
    public Func<UIElement?>? ValueIcon { get; init; }

    public string ValueIconKey { get; init; } = string.Empty;

    public string IconKey { get; init; } = string.Empty;

    public Func<UIElement?>? Icon { get; init; }

    public Action? Run { get; init; }

    /// <summary>A switch's state (<see cref="Radio"/> false) or whether an option holds (<see cref="Radio"/> true).</summary>
    public bool? Checked { get; init; }

    public bool Radio { get; init; }

    public bool Disabled { get; init; }

    public bool Danger { get; init; }

    public IReadOnlyList<MenuNode>? Children { get; init; }

    public MenuSearch? Search { get; init; }

    public Func<FrameworkElement>? Build { get; init; }

    public string ValueKey { get; init; } = string.Empty;

    /// <summary>[S-1862] Where what the widget waits for is set: <see cref="MenuPanel.OpenSetup"/> opens the menu at it.</summary>
    public bool SetsUp { get; init; }

    /// <summary>[S-3731] The slider of a <see cref="MenuNodeKind.Slider"/> row.</summary>
    public MenuSlider? Slider { get; init; }

    /// <summary>[S-3732] An ordered list's item: its row is dragged up and down among the list's.</summary>
    public MenuOrder? Order { get; init; }

    /// <summary>[S-3742] The hint of the filter field a long level of this row's has; a plain "Search" without one.</summary>
    public string? Filter { get; init; }

    /// <summary>[S-3742] Words a filter finds the row by besides its own.</summary>
    public string? Keywords { get; init; }

    /// <summary>[S-3736] [S-3737] Its <see cref="Run"/> asks (a line of text, a question): the question opens as a level of its own.</summary>
    public bool Asks { get; init; }

    /// <summary>[S-3749] A link row: the name of the menu level it opens, its tooltip's ("Opens Accounts").</summary>
    public string? Link { get; init; }

    /// <summary>[S-3556] [S-3557] What the level this row stands on wipes as it closes (a form's password).</summary>
    public Action? Wipe { get; init; }

    /// <summary>[S-3736] The field of a <see cref="MenuNodeKind.Field"/> row.</summary>
    public MenuEdit? Edit { get; init; }

    /// <summary>[S-3561] A note about something being done: a ring before its words.</summary>
    public bool Busy { get; init; }

    /// <summary>[S-3737] A note in the text colour and size: a question's own words.</summary>
    public bool Strong { get; init; }

    public static MenuNode Separator(string key) => new(key, MenuNodeKind.Separator);

    /// <summary>Whether a click on the row opens a level of its own: rows, a search, or the question it asks.</summary>
    public bool Opens => Children is not null || Search is not null || Asks;

    /// <summary>[S-3741] A switch: on or off, drawn as Windows' toggle at the row's end.</summary>
    public bool Switch => Checked is not null && !Radio && !Opens && Link is null;
}

/// <summary>
/// [S-3716] The one menu engine, after Karpo's configuration menus (karpo-app, Flyout.Inline.cs): a
/// list of <see cref="MenuNode"/>s in one fixed rectangle ([S-3734]: 320 × 440 DIPs, the same on every
/// level, never moving or resizing while it is used) on a flyout's surface, its rows Explorer's
/// (32 DIPs apart, a 28-DIP fill inset 5, the icon at 16 and the words at 44). A row with a level of
/// its own opens it **in place** ([S-3718]): its rows give way to the level's under a pinned head (the
/// back arrow and the level's name), sliding in 16 DIPs; going back shows the level under it again,
/// its row highlighted and its scroll where it was. The selection's properties menu
/// (<see cref="PropertiesStrip"/>, with its top row and its pinned Appearance foot) and the edit bar's
/// menus (<see cref="BarMenuView"/>) are this engine with their own rows.
/// <para>
/// The rows are handed in again on every change (<see cref="Present"/>): the open levels are found
/// again in them by their rows' keys, and the deepest is shown in place. It is drawn again only when
/// its shape changed (which rows there are); a change of value (a switch, a tick, the words at the
/// end, an icon) is written into the rows in place, so the focus, a field being typed in and the scroll
/// stay where they are.
/// </para>
/// </summary>
internal partial class MenuPanel : Grid
{
    private const string Scope = "menu";

    /// <summary>[S-3734] The one width of the menu and every level, at a text size of 100 % (Karpo's SettingsWidthDip).</summary>
    internal const double MenuWidth = 320;

    /// <summary>[S-3734] The one height of the menu and every level, at a text size of 100 % (Karpo's SettingsHeightDip).</summary>
    internal const double MenuHeight = 440;

    /// <summary>[S-3734] The rows' pitch: a 28-DIP fill with 2 DIPs above and under it.</summary>
    protected const double LineHeight = 32;

    /// <summary>[S-3734] The fill under a row, as Explorer's menu draws it.</summary>
    protected const double FillHeight = 28;

    /// <summary>[S-3734] Where a row's fill starts in from the menu's edge (5 DIPs), less the rim the frame draws there.</summary>
    protected const double RowInset = 5 - Fluent.Hairline;

    /// <summary>[S-3734] The room over the first row and under the last (Karpo's panel padding).</summary>
    private const double BandPadding = 3;

    /// <summary>[S-3742] A level with more rows to pick than this has a filter field under its head.</summary>
    internal const int FilterFrom = 10;

    /// <summary>[S-3738] How far a level's rows slide in, and for how long (Karpo's 16 DIPs over 167 ms).</summary>
    private const float SlideDip = 16;

    private static readonly TimeSpan SlideTime = TimeSpan.FromMilliseconds(167);

    /// <summary>[S-3734] How far a notch of the mouse wheel scrolls.</summary>
    private const double WheelStep = 48;

    /// <summary>The node a level's head row stands for: a click on it goes back.</summary>
    private static readonly MenuNode HeadNode = new("<back>");

    private static Windows.UI.ViewManagement.UISettings? _ui;

    private readonly Border _frame = new();

    /// <summary>The pinned band over the rows: the menu's own head on its first level, a level's head, rule and field on any other.</summary>
    private readonly StackPanel _top = new();

    private readonly ScrollViewer _scroll = new()
    {
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        VerticalScrollMode = ScrollMode.Enabled,
        HorizontalScrollMode = ScrollMode.Disabled,
    };

    /// <summary>[S-3739] The pinned band under the rows: the rule and the foot rows, on the first level of a menu that has them.</summary>
    private readonly StackPanel _footBand = new();

    /// <summary>The rows shown now, and the foot's.</summary>
    private readonly Level _shown;

    private readonly Level _foot;

    /// <summary>[S-3738] The level's rows and its head band, slid in together.</summary>
    private readonly Grid _body;

    /// <summary>[S-3718] The levels open in place, the first opened from the menu's own rows; empty on its own rows.</summary>
    private readonly List<Step> _path = [];

    /// <summary>What the menu's own rows are drawn for (the selection, the menu's name).</summary>
    private string _headKey = string.Empty;

    private IReadOnlyList<MenuNode>? _root;

    private IReadOnlyList<MenuNode> _footNodes = [];

    private Func<IEnumerable<UIElement>>? _head;

    /// <summary>What the drawn rows are: the open path's keys, so a refresh tells a new level from new values.</summary>
    private string _identity = string.Empty;

    /// <summary>[S-3736] The row whose click is under way: a question it asks opens as its level.</summary>
    private MenuNode? _asking;

    private double _roomHeight = double.PositiveInfinity;

    protected MenuPanel()
    {
        _shown = new Level(NewLines(), Overlay());
        _foot = new Level(NewLines(), Overlay());

        _scroll.Content = new Grid { Children = { _shown.Lines, _shown.Overlay } };
        _scroll.PointerWheelChanged += OnWheel;

        _footBand.Children.Add(Divider(full: false));
        _footBand.Children.Add(new Grid { Children = { _foot.Lines, _foot.Overlay } });
        _footBand.Visibility = Visibility.Collapsed;

        _body = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) },
                new RowDefinition { Height = GridLength.Auto },
            },
            XYFocusKeyboardNavigation = XYFocusKeyboardNavigationMode.Enabled,
        };
        Grid.SetRow(_scroll, 1);
        Grid.SetRow(_footBand, 2);
        _body.Children.Add(_top);
        _body.Children.Add(_scroll);
        _body.Children.Add(_footBand);

        _frame.Child = _body;
        Children.Add(_frame);

        // [S-3715] A flyout's surface: it stands apart from the cards and the wallpaper beside it.
        FlyoutSurface.Apply(this, _frame);
        Size();

        // [S-3707] The keyboard stays in the menu: Tab goes round its rows, the arrows move between
        // them (no wrap), Home and End reach the first and the last ([S-3743]).
        _frame.TabFocusNavigation = KeyboardNavigationMode.Cycle;
        KeyDown += OnKeyDown;

        // A press on the menu is the menu's: it must not reach the surface, whose press on empty space
        // starts a rubber band and clears the selection. [S-3743] The mouse's back button goes back.
        _frame.PointerPressed += (_, e) =>
        {
            e.Handled = true;
            if (e.GetCurrentPoint(_frame).Properties.IsXButton1Pressed)
            {
                Back(keys: false);
            }
        };

        // Put away (a drag, edit mode left): the levels close with it, what they held landing.
        RegisterPropertyChangedCallback(VisibilityProperty, (_, _) =>
        {
            if (Visibility != Visibility.Visible)
            {
                ShowRoot();
            }
        });

        HorizontalAlignment = HorizontalAlignment.Left;
        VerticalAlignment = VerticalAlignment.Top;
        Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// [S-3734] The most the menu may be tall where it stands (the work area beside the selection, under
    /// the edit bar): it is <see cref="MenuHeight"/> unless that is more, and a level's rows scroll inside.
    /// </summary>
    public double RoomHeight
    {
        get => _roomHeight;
        set
        {
            _roomHeight = value;
            Size();
        }
    }

    /// <summary>[S-3734] Windows' text size: the menu grows with it, never under 100 %.</summary>
    private static double TextScale
    {
        get
        {
            try
            {
                _ui ??= new Windows.UI.ViewManagement.UISettings();
                return Math.Max(1, _ui.TextScaleFactor);
            }
            catch (Exception ex)
            {
                Log.Warn(Scope, $"could not read Windows' text size; the menu is drawn at 100 % ({ex.Message})");
                return 1;
            }
        }
    }

    /// <summary>[S-3734] The menu's one width at Windows' text size: what its rows share.</summary>
    protected static double OneWidth => Math.Round(MenuWidth * TextScale);

    /// <summary>[S-3734] The fixed rectangle: its width and height at Windows' text size, the height no more than the room.</summary>
    private void Size()
    {
        _frame.Width = OneWidth;
        var height = Math.Round(MenuHeight * TextScale);
        _frame.Height = double.IsFinite(_roomHeight) ? Math.Max(LineHeight * 6, Math.Min(height, Math.Floor(_roomHeight))) : height;
    }

    /// <summary>The menu's own rows as last shown.</summary>
    protected IReadOnlyList<MenuNode> FirstLevel => _root ?? [];

    /// <summary>[S-3718] Whether a level stands in place of the menu's own rows.</summary>
    public bool OnLevel => _path.Count > 0;

    /// <summary>The element the custom colour dialog stands by: the menu.</summary>
    protected FrameworkElement AskAnchor => _frame;

    /// <summary>[S-3718] Back to the menu's own rows, every open level closing as going back closes it.</summary>
    public void ShowRoot()
    {
        if (_path.Count == 0)
        {
            return;
        }

        CloseLevels();
        if (_root is not null)
        {
            Draw(0, null, 0, FocusHow.Keep);
        }
    }

    private void CloseLevels()
    {
        while (_path.Count > 0)
        {
            Leave(_path[^1], commit: true);
            _path.RemoveAt(_path.Count - 1);
        }
    }

    /// <summary>
    /// Show <paramref name="nodes"/> as the menu's own rows, under the head <paramref name="head"/>
    /// builds (for <paramref name="headKey"/>), with <paramref name="foot"/> pinned under them; null
    /// nodes show nothing. A new head key starts again from the menu's own rows; otherwise the open
    /// levels are found again by their rows' keys and the deepest is shown, drawn again only where its
    /// shape changed (or <paramref name="rebuild"/>, a language switch), else written into in place.
    /// </summary>
    protected void Present(string headKey, List<MenuNode>? nodes, Func<IEnumerable<UIElement>>? head, IReadOnlyList<MenuNode>? foot = null, bool rebuild = false)
    {
        _head = head;
        Size();

        if (nodes is null || !string.Equals(headKey, _headKey, StringComparison.Ordinal))
        {
            CloseLevels();
            _root = nodes;
            _footNodes = foot ?? [];
            _headKey = headKey;
            Draw(0, null, 0, FocusHow.Keep);
            return;
        }

        _root = nodes;
        _footNodes = foot ?? [];
        Resolve();
        var shown = Current();
        if (rebuild || !string.Equals(Identity(), _identity, StringComparison.Ordinal) || !string.Equals(Shape(shown), _shown.Shape, StringComparison.Ordinal)
            || (_path.Count == 0 && !string.Equals(Shape(_footNodes), _foot.Shape, StringComparison.Ordinal)))
        {
            Draw(0, null, _scroll.VerticalOffset, FocusHow.Keep);
            return;
        }

        Update(_shown, shown);
        if (_path.Count == 0)
        {
            Update(_foot, _footNodes);
        }
        else if (_top.Children.Count > 0 && _top.Children[0] is Button { Tag: LineView headView })
        {
            headView.Words.Text = _path[^1].Title;
        }
    }

    /// <summary>[S-3718] Find the open levels again in the rows as they are now; a level whose row is gone closes with those under it.</summary>
    private void Resolve()
    {
        IReadOnlyList<MenuNode> level = [.. _root ?? [], .. _footNodes];
        for (var i = 0; i < _path.Count; i++)
        {
            var step = _path[i];
            if (step.Synthetic)
            {
                level = [];
                continue;
            }

            // A question stands on the row that asked it, whatever that row is (a command that confirms).
            var asked = step.Edit is not null || step.Question is not null;
            if (level.FirstOrDefault(n => (asked || n.Opens) && string.Equals(n.Key, step.Key, StringComparison.Ordinal)) is not { Disabled: false } row)
            {
                while (_path.Count > i)
                {
                    Leave(_path[^1], commit: true);
                    _path.RemoveAt(_path.Count - 1);
                }

                return;
            }

            if (step.Edit is null && step.Question is null)
            {
                step.Title = row.Text;
                step.Holder = row;
                if (row.Search is { } search)
                {
                    step.Search = search;
                }

                level = row.Children ?? [];
            }
            else
            {
                level = [];
            }
        }
    }

    /// <summary>The open path's keys: what the rows drawn now are.</summary>
    private string Identity() => _headKey + "|" + string.Join("/", _path.Select(s => s.Key + (s.Edit is not null ? "#e" : s.Question is not null ? "#q" : string.Empty)));

    /// <summary>The rows of the level shown now: the menu's own, a level's (filtered), a search's, a field's or a question's.</summary>
    private List<MenuNode> Current()
    {
        if (_path.Count == 0)
        {
            return [.. _root ?? []];
        }

        var step = _path[^1];
        if (step.Edit is { } edit)
        {
            return EditNodes(step, edit);
        }

        if (step.Question is { } question)
        {
            return QuestionNodes(question);
        }

        if (step.Search is not null)
        {
            return SearchNodes(step);
        }

        var rows = step.Holder?.Children ?? [];
        return Searchable(rows) ? Filtered(step, rows) : [.. rows];
    }

    /// <summary>How a drawing places the keyboard.</summary>
    private enum FocusHow
    {
        /// <summary>A refresh: the row (or field) that had it keeps it, and nothing takes it otherwise.</summary>
        Keep,

        /// <summary>A level opened or left by a click: its field takes it, else the row it came from or its head, without the keyboard's mark.</summary>
        Pointer,

        /// <summary>A level opened or left from the keyboard: its field, or its first row, or the row it came from.</summary>
        Keys,
    }

    /// <summary>
    /// Draw the level shown now: its head band, its rows and (on the menu's own rows) its foot,
    /// scrolled to <paramref name="scroll"/>; the rows slide in <paramref name="way"/> (+1 a level in,
    /// -1 back out, [S-3738]); <paramref name="focusKey"/> takes the keyboard (the row a level went back to).
    /// </summary>
    private void Draw(int way, string? focusKey, double scroll, FocusHow how)
    {
        EndDrag(drop: false);

        // A refresh keeps the keyboard where it was: on a row by its key, or in the field.
        var had = how == FocusHow.Keep ? FocusedView()?.Node.Key : null;
        var typing = how == FocusHow.Keep && _shown.Field is { } wasField && wasField.Box.FocusState != FocusState.Unfocused;

        _top.Children.Clear();
        var nodes = _root is null ? [] : Current();
        _identity = Identity();

        if (_path.Count == 0)
        {
            foreach (var part in _head?.Invoke() ?? [])
            {
                _top.Children.Add(part);
            }
        }
        else
        {
            var step = _path[^1];
            _top.Children.Add(HeadRow(step));
            _top.Children.Add(Divider(full: false));
            if (step.Search is { } search)
            {
                _top.Children.Add(BoxRow(SearchBox(step, search)));
            }
            else if (step.Edit is null && step.Question is null && Searchable(step.Holder?.Children ?? []))
            {
                _top.Children.Add(BoxRow(FilterBox(step)));
            }
        }

        // [S-3734] Karpo's padding: 3 DIPs from the menu's edge (its rim counted) over the first row and
        // under the last; 3 under the rule of the menu's top row; a level's rows straight under its head's rule.
        IReadOnlyList<MenuNode> foot = _path.Count == 0 && _root is not null ? _footNodes : [];
        var edge = BandPadding - Fluent.Hairline;
        _top.Padding = new Thickness(0, _path.Count > 0 ? edge : 0, 0, 0);
        _shown.Lines.Padding = new Thickness(0, _path.Count > 0 ? 0 : _top.Children.Count > 0 ? BandPadding : edge, 0, foot.Count > 0 ? BandPadding : edge);
        FillLevel(_shown, nodes);

        FillLevel(_foot, foot);
        _foot.Lines.Padding = new Thickness(0, 0, 0, edge);
        _footBand.Visibility = foot.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        UpdateLayout();
        _scroll.ChangeView(null, Math.Max(0, scroll), null, disableAnimation: true);

        if (way != 0)
        {
            Slide(way);
        }

        Focus(how, focusKey ?? had, typing);
    }

    /// <summary>[S-3738] The level's rows and head come in 16 DIPs from the way they go, decelerating; off when Windows' animations are.</summary>
    private void Slide(int way)
    {
        if (!SystemMotion.AnimationsEnabled)
        {
            return;
        }

        try
        {
            var visual = ElementCompositionPreview.GetElementVisual(_body);
            ElementCompositionPreview.SetIsTranslationEnabled(_body, true);
            var compositor = visual.Compositor;
            var move = compositor.CreateVector3KeyFrameAnimation();
            move.InsertKeyFrame(0, new Vector3(SlideDip * way, 0, 0));
            move.InsertKeyFrame(1, Vector3.Zero, compositor.CreateCubicBezierEasingFunction(new Vector2(0, 0), new Vector2(0, 1)));
            move.Duration = SlideTime;
            visual.StartAnimation("Translation", move);
        }
        catch (Exception ex)
        {
            // The rows are where they belong without it: only the motion is lost.
            Log.Warn(Scope, "a level's slide could not start", ex);
        }
    }

    /// <summary>[S-3743] Place the keyboard after a drawing (<see cref="FocusHow"/>).</summary>
    private void Focus(FocusHow how, string? key, bool typing)
    {
        if (how == FocusHow.Keep)
        {
            if (typing && _shown.Field is { } field)
            {
                field.Take(selectAll: false);
            }
            else if (key is not null && Row(key) is { } kept)
            {
                kept.Button.Focus(FocusState.Programmatic);
            }

            return;
        }

        var state = how == FocusHow.Keys ? FocusState.Keyboard : FocusState.Programmatic;
        if (key is not null && Row(key) is { } back)
        {
            Later(() => back.Button.Focus(state));
            return;
        }

        if (_shown.Field is { } box)
        {
            Later(() => box.Take(selectAll: true));
            return;
        }

        if (TopBox() is { } filter)
        {
            Later(() => filter.Focus(FocusState.Keyboard));
            return;
        }

        if (how == FocusHow.Keys && _shown.Views.FirstOrDefault(v => v is { Button.IsEnabled: true }) is { } first)
        {
            Later(() => first.Button.Focus(state));
        }
        else if (_path.Count > 0 && _top.Children.Count > 0 && _top.Children[0] is Control head)
        {
            Later(() => head.Focus(state));
        }
    }

    /// <summary>The search's or the filter's field in the band over the rows, if there is one.</summary>
    private TextBox? TopBox() => _top.Children.OfType<Border>().Select(b => b.Child).OfType<TextBox>().FirstOrDefault();

    /// <summary>The drawn row of <paramref name="key"/>, on the level or its foot.</summary>
    private LineView? Row(string key)
        => _shown.Views.Concat(_foot.Views).FirstOrDefault(v => v is not null && string.Equals(v.Node.Key, key, StringComparison.Ordinal));

    private LineView? FocusedView()
        => _shown.Views.Concat(_foot.Views).FirstOrDefault(v => v is not null && v.Button.FocusState != FocusState.Unfocused);

    /// <summary>
    /// [S-3718] A click on a row (or Enter, Space, the arrow towards a level): a row with a level opens
    /// it in place, and one that also does something ([S-3541]: a view's row shows the view) does it as
    /// it opens; a row that asks asks, its question opening as its level ([S-3736], [S-3737]); anything
    /// else does what it does, and the menu stays as it is.
    /// </summary>
    private void OnLine(LineView view)
    {
        // [S-3732] The release that ends a drag is not a click on the row it started on.
        if (_swallowClick)
        {
            _swallowClick = false;
            return;
        }

        // A click from the keyboard (Enter, Space) keeps the keyboard's mark on the level it opens.
        var keys = view.Button.FocusState == FocusState.Keyboard;
        if (ReferenceEquals(view.Node, HeadNode))
        {
            Back(keys);
            return;
        }

        Enter(view.Node, keys);
    }

    /// <summary>[S-3718] What a row does when it is picked (<see cref="OnLine"/>).</summary>
    private bool Enter(MenuNode node, bool keys)
    {
        if (node.Disabled)
        {
            return false;
        }

        if (node.Asks)
        {
            _askKeys = keys;
            Ask(node, node.Run);
            return true;
        }

        if (node.Children is not null || node.Search is not null)
        {
            if (node.Children is { Count: 0 })
            {
                return false;
            }

            _path.Add(new Step(node.Key, node.Text) { Holder = node, Search = node.Search, Under = _scroll.VerticalOffset });
            Draw(+1, null, 0, keys ? FocusHow.Keys : FocusHow.Pointer);
            if (node.Search is not null)
            {
                Find(_path[^1], string.Empty);
            }

            Run(node.Run);
            return true;
        }

        // A command may ask (Delete view, Sign out): its question stands on this row.
        _askKeys = keys;
        Ask(node, node.Run);
        return true;
    }

    /// <summary>[S-3736] A row's question was opened from the keyboard: its level keeps the keyboard's mark.</summary>
    private bool _askKeys;

    /// <summary>[S-3736] [S-3737] Run a row's click with the row in hand: what it asks opens as its level.</summary>
    protected void Ask(MenuNode node, Action? run)
    {
        var was = _asking;
        _asking = node;
        try
        {
            Run(run);
        }
        finally
        {
            _asking = was;
            _askKeys = false;
        }
    }

    /// <summary>
    /// [S-3718] [S-3743] Back one level (the head, Escape, Backspace, the arrow away, the mouse's back
    /// button): the level under it shows again, the row it came from highlighted and scrolled where it
    /// was. False on the menu's own rows.
    /// </summary>
    protected bool Back(bool keys)
    {
        if (_path.Count == 0)
        {
            return false;
        }

        var step = _path[^1];
        Leave(step, commit: true);
        _path.RemoveAt(_path.Count - 1);
        Draw(-1, step.Synthetic ? null : step.Key, step.Under, keys ? FocusHow.Keys : FocusHow.Pointer);
        return true;
    }

    /// <summary>
    /// A level closes: a search's question is dropped, a field's words land where they keep ([S-3736]),
    /// and what its rows hold that must not outlive it is wiped ([S-3556]: a form's password).
    /// </summary>
    private void Leave(Step step, bool commit)
    {
        step.Pending?.Cancel();
        step.Settle?.Stop();
        step.MaskedStart = null;

        if (step.Edit is { } edit && commit && edit.Accept is null && edit.Live is null && !edit.Masked
            && edit.Reason(step.Text) is null && !string.Equals(step.Text.Trim(), edit.Initial.Trim(), StringComparison.Ordinal))
        {
            var text = step.Text.Trim();
            Later(() => edit.Commit(text));
        }

        IReadOnlyList<MenuNode> rows = step.Edit is null && step.Question is null ? step.Holder?.Children ?? [] : [];
        foreach (var row in rows.Where(r => r.Wipe is not null))
        {
            Run(row.Wipe);
        }
    }

    /// <summary>[S-3743] The menu's keys: back, into a level, the first and the last row; a field keeps its own.</summary>
    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Handled)
        {
            return;
        }

        var inBox = e.OriginalSource is TextBox or PasswordBox;
        var rtl = UiLanguage.Flow == FlowDirection.RightToLeft;
        var away = rtl ? Windows.System.VirtualKey.Right : Windows.System.VirtualKey.Left;
        var towards = rtl ? Windows.System.VirtualKey.Left : Windows.System.VirtualKey.Right;

        if (e.Key == Windows.System.VirtualKey.Escape)
        {
            e.Handled = true;
            if (!Back(keys: true))
            {
                OnEscape();
            }
        }
        else if (inBox)
        {
            return;
        }
        else if (e.Key is Windows.System.VirtualKey.Back or Windows.System.VirtualKey.GoBack || e.Key == away)
        {
            e.Handled = Back(keys: true);
        }
        else if (e.Key == towards)
        {
            if (FocusedView() is { Node.Opens: true } open)
            {
                e.Handled = Enter(open.Node, keys: true);
            }
        }
        else if (e.Key is Windows.System.VirtualKey.Home or Windows.System.VirtualKey.End)
        {
            var rows = _shown.Views.Where(v => v is { Button.IsEnabled: true }).ToList();
            if (rows.Count > 0)
            {
                (e.Key == Windows.System.VirtualKey.Home ? rows[0] : rows[^1])!.Button.Focus(FocusState.Keyboard);
                e.Handled = true;
            }
        }
    }

    /// <summary>[S-3734] A notch of the wheel scrolls 48 DIPs.</summary>
    private void OnWheel(object sender, PointerRoutedEventArgs e)
    {
        var delta = e.GetCurrentPoint(_scroll).Properties.MouseWheelDelta;
        if (delta == 0)
        {
            return;
        }

        _scroll.ChangeView(null, Math.Max(0, _scroll.VerticalOffset - (delta / 120.0 * WheelStep)), null, disableAnimation: false);
        e.Handled = true;
    }

    /// <summary>[S-3707] Escape on the menu's own rows: the menu's own (hand the keyboard back, or close).</summary>
    protected virtual void OnEscape()
    {
    }

    /// <summary>
    /// Open the levels along <paramref name="keys"/> (the rows' keys, the menu's own first), as clicks
    /// would: a level opens (its field or its first row given the keyboard when <paramref name="focus"/>,
    /// the last one only), any other row runs as its click does (a question it asks opens as its level).
    /// The levels open now close first. False when a row on the way is not there or cannot open.
    /// </summary>
    public bool OpenPath(IReadOnlyList<string> keys, bool focus)
    {
        ArgumentNullException.ThrowIfNull(keys);
        if (_root is null)
        {
            return false;
        }

        ShowRoot();
        for (var i = 0; i < keys.Count; i++)
        {
            var key = keys[i];
            if (Picks().FirstOrDefault(n => string.Equals(n.Key, key, StringComparison.Ordinal)) is not { Disabled: false } node
                || !Enter(node, keys: focus && i == keys.Count - 1))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>The rows that can be picked where the menu stands: the level's, and on the menu's own rows its foot's.</summary>
    private IEnumerable<MenuNode> Picks() => Current().Concat(_path.Count == 0 ? _footNodes : []);

    /// <summary>
    /// [S-1862] The card's Set up button: open the menu at the row the widget's module names for what
    /// it waits for (<see cref="WidgetMenuLines.SetsUp"/>), through the levels on the way, as clicks
    /// would (<see cref="OpenPath"/>). A widget that names none keeps the menu as it stands. False when
    /// no row was opened.
    /// </summary>
    public bool OpenSetup()
        => _root is { } root && SetupPath(root) is { } path && OpenPath(path, focus: true);

    /// <summary>[S-1862] The keys of the rows from the menu's own down to the one that sets the widget up, or null.</summary>
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

    /// <summary>[S-3707] The keyboard into the menu: the field of a field's level, else its first row. False when there is none.</summary>
    public bool FocusFirst()
    {
        if (_shown.Field is { } field)
        {
            field.Take(selectAll: true);
            return true;
        }

        if (FocusManager.FindFirstFocusableElement(_path.Count > 0 ? _shown.Lines : _body) is not Control first)
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

    /// <summary>Run <paramref name="action"/> once the event in progress has finished (a move draws the row it is raised on again).</summary>
    private void Later(Action action)
    {
        if (DispatcherQueue is not { } queue || !queue.TryEnqueue(() => Run(action)))
        {
            Log.Error(Scope, "could not queue a menu's step");
        }
    }

    /// <summary>[S-3718] One level open in place: the row it opened from and what it is (rows, a search, a field, a question).</summary>
    private sealed class Step(string key, string title)
    {
        public string Key { get; } = key;

        public string Title { get; set; } = title;

        /// <summary>The row whose rows (or search) the level shows, as the rows last had it.</summary>
        public MenuNode? Holder { get; set; }

        public MenuSearch? Search { get; set; }

        public MenuEdit? Edit { get; init; }

        public MenuQuestion? Question { get; init; }

        /// <summary>A question asked with no row in hand (an import's, once Windows' dialog has gone): no row to find it by.</summary>
        public bool Synthetic { get; init; }

        /// <summary>How far the level under it was scrolled when it opened: where going back scrolls it.</summary>
        public double Under { get; init; }

        /// <summary>[S-3736] What the field holds now (a redraw keeps it); [S-3742] what the filter holds.</summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>[S-3722] A search's answers, and the question still out.</summary>
        public List<MenuNode> Results { get; set; } = [];

        public CancellationTokenSource? Pending { get; set; }

        public Microsoft.UI.Dispatching.DispatcherQueueTimer? Settle { get; set; }

        /// <summary>[S-3556] A password level's: what its form held as it opened, for Escape; dropped as it closes.</summary>
        public string? MaskedStart { get; set; }
    }

#if DEBUG
    /// <summary>
    /// Debug renders only: open the levels along <paramref name="path"/> (the rows' words, "/"
    /// between levels), as clicks would; a last row that opens nothing is clicked. False when a row
    /// on the way is not there.
    /// </summary>
    internal bool DebugOpen(string path)
    {
        foreach (var words in path.Split('/'))
        {
            if (Picks().FirstOrDefault(n => n.Kind == MenuNodeKind.Line && n.Text == words) is not { } node || !Enter(node, keys: false))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Debug renders only: click the shown level's row of <paramref name="words"/>. False when it is not there.</summary>
    internal bool DebugClick(string words)
        => Picks().FirstOrDefault(n => n.Kind == MenuNodeKind.Line && n.Text == words) is { } node && Enter(node, keys: false);

    /// <summary>Debug renders only: click the shown level's row of <paramref name="key"/>. False when it is not there.</summary>
    internal bool DebugClickKey(string key)
        => Picks().FirstOrDefault(n => n.Key == key) is { } node && Enter(node, keys: false);

    /// <summary>Debug renders only: type into the shown level's field (its search, its filter or its text).</summary>
    internal bool DebugType(string text)
    {
        if (_shown.Field is { } field)
        {
            field.Type(text);
            return true;
        }

        if (TopBox() is not { } box)
        {
            return false;
        }

        box.Text = text;
        return true;
    }

    /// <summary>Debug renders only: scroll the shown level's rows to the end.</summary>
    internal void DebugScrollToEnd()
    {
        UpdateLayout();
        _scroll.ChangeView(null, _scroll.ScrollableHeight, null, disableAnimation: true);
    }
#endif
}
