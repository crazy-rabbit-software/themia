using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Settings;
using Themia.Core.Shell;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Themia.Sdk;
using Windows.Foundation;

namespace Themia.App.Shell;

/// <summary>What the properties panel's choices do, given by the surface (or a render's stand-in).</summary>
/// <param name="SetHeader">[S-3703] The selected widgets' header placement.</param>
/// <param name="SetBackground">[S-3703] [S-6566] The selected widgets' background.</param>
/// <param name="SetTitle">[S-3704] One widget's title, by id, as typed.</param>
/// <param name="ReleaseFocus">[S-3704] Hands the keyboard back to the surface after the title box, so the edit keys work again.</param>
/// <param name="SetStyle">[S-6008] The selected widgets' style, by the style enum's value.</param>
/// <param name="SetItemBackground">[S-6521] The selected widgets' own item background (those with items, [S-6571]).</param>
/// <param name="SetItemTextColor">[S-6544] The selected widgets' own item text colour, or empty for the app-wide one.</param>
/// <param name="SetScale">[S-6531] The selected widgets' own content scale, as a factor.</param>
/// <param name="SetAlign">[S-6584] Where the selected widgets that can align draw their content. A render's stand-in may leave it out.</param>
/// <param name="Cut">[S-3717] Cut the selection, as Ctrl+X does.</param>
/// <param name="Copy">[S-3717] Copy the selection, as Ctrl+C does.</param>
/// <param name="Delete">[S-3717] Delete the selection, as the Delete key does.</param>
/// <param name="WidgetLines">[S-3719] One widget's own lines, by id, built over its live config (asking for text and confirmations beside the menu); null for none.</param>
internal sealed record PropertiesCommands(
    Action<HeaderPlacement> SetHeader,
    Action<FillChoice> SetBackground,
    Action<string, string?> SetTitle,
    Action ReleaseFocus,
    Action<int> SetStyle,
    Action<FillChoice> SetItemBackground,
    Action<string> SetItemTextColor,
    Action<double> SetScale,
    Action<ContentAlign>? SetAlign = null,
    Action? Cut = null,
    Action? Copy = null,
    Action? Delete = null,
    Func<string, Action<WidgetTextAsk>, Action<WidgetConfirm>, WidgetMenuLines?>? WidgetLines = null);

/// <summary>
/// [S-3716] The selected widgets' properties, as a menu that stands beside the selection: drawn as
/// Windows 11 draws a context menu (the flyout's surface, the icon row on top, 32-DIP lines with an
/// icon column, the words, a value column and at the end the mark or the chevron), but it stays open
/// while it is used, and so do its submenus: a pick in one is drawn where it is, and the menu stays as
/// it was ([S-3718]). The icon row is Cut and Copy, Delete at its far end ([S-3717]); then the title
/// (one widget) or "N selected", Style, the widget's own lines ([S-3719]), and the card's own (Header,
/// Alignment, Background, Item background, Item text, Scale) as one Appearance submenu: where the
/// widget places it, else right after Style ([S-3723]). It is where a widget's options are: there is
/// no settings page for a widget ([S-6536]). After Karpo's properties panel (docs/widgets.md §145).
/// <para>
/// The menu is a list of <see cref="Node"/>s, built again from the offer and the widget's lines on
/// every change. Lines are drawn again only when a list's shape changed (which lines there are); a
/// change of value (a tick, a dot, the words at the end, an icon) is written into the lines in
/// place, so an open submenu, its search box and the focus stay where they are. The submenus are
/// drawn by the panel itself, beside it in the same window, never as light-dismiss popups: a click in
/// one does what it does and leaves it open.
/// </para>
/// </summary>
internal sealed partial class PropertiesStrip : Grid
{
    private const string Scope = "properties";

    /// <summary>[S-3716] A line's height and its parts, as the Windows 11 menu draws them.</summary>
    private const double LineHeight = 32;

    /// <summary>The icon row's buttons: the size of Explorer's.</summary>
    private const double RowButton = 40;

    /// <summary>A column of a line before its words: an icon (16) and its gap (12).</summary>
    private const double LeadColumn = Fluent.IconSize + Fluent.Space3;

    /// <summary>The narrowest the menu is: Explorer's menus are never a sliver.</summary>
    private const double MinMenuWidth = 256;

    /// <summary>The narrowest a submenu is; a search's is wider, for a city and its country.</summary>
    private const double MinSubmenuWidth = 200;

    private const double SearchWidth = 300;

    /// <summary>The widest a submenu is: a long line (a zone's cities) is trimmed, not a menu across the screen.</summary>
    private const double MaxSubmenuWidth = 400;

    /// <summary>[S-3723] A swatch of a colour submenu's grid, eight to a row as Windows lays its palette out.</summary>
    private const double SwatchSize = 24;

    /// <summary>The chevron of a line with a submenu: Windows' "ChevronRightMed", at the menu's size.</summary>
    private static readonly string Chevron = char.ConvertFromUtf32(0xE974);

    /// <summary>[S-3725] The answers a search shows before its Show all line, and the lines its answers' room never shrinks below.</summary>
    private const int SearchAnswers = 5;

    /// <summary>[S-3728] The most lines a note wraps to; the rest is trimmed, the whole in its tooltip.</summary>
    private const int NoteLines = 3;

    private readonly Border _frame = new() { MinWidth = MinMenuWidth };

    private readonly StackPanel _menu = new();

    /// <summary>
    /// [S-3718] The open submenus' layer: a canvas of no size at the panel's corner. What it holds is
    /// drawn beside the panel, outside its box, so the panel's size (and so where it stands, [S-3708])
    /// never counts it, and it is drawn in the window like the panel.
    /// </summary>
    private readonly Canvas _subs = new() { Width = 0, Height = 0, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };

    /// <summary>The panel's list first, then each open submenu, deepest last.</summary>
    private readonly List<Level> _levels = [];

    private PropertiesCommands _commands;

    private QuickSetOffer _offer = QuickSetOffer.None;

    /// <summary>What the frame (the icon row, the title or count) was drawn for.</summary>
    private string _frameKey = string.Empty;

    /// <summary>[S-3719] The one selected widget's own lines as last built.</summary>
    private WidgetMenuLines? _widget;

    /// <summary>The title box while one widget is selected, the widget it names and what it last committed.</summary>
    private TextBox? _title;

    private string? _titleId;

    private string _titleCommitted = string.Empty;

    public PropertiesStrip(PropertiesCommands commands)
    {
        _commands = commands;
        _frame.Child = _menu;
        Children.Add(_frame);

        // [S-3715] A flyout's surface: it stands apart from the cards and the wallpaper beside it.
        FlyoutSurface.Apply(this, _frame);
        Children.Add(_subs);

        // [S-3707] The keyboard stays in the menu: Tab goes round its lines and stays in it, Up and
        // Down move between them, and Escape closes the deepest submenu, then hands the keyboard back.
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

    /// <summary>What the panel offers now; <see cref="QuickSetOffer.Shows"/> false when it has nothing to show.</summary>
    public QuickSetOffer Offer => _offer;

    /// <summary>Rebuild the choices in the current language. Called again on a language switch.</summary>
    public void Fill(PropertiesCommands commands)
    {
        ArgumentNullException.ThrowIfNull(commands);
        _commands = commands;
        Build(Nodes());
    }

    /// <summary>
    /// [S-3701]..[S-3702] Show the choices for what is selected on this display. A change of value
    /// is written into the lines in place, so a submenu left open stays open on the pick ([S-3718]);
    /// the title box is left alone while it is typed in for the same widget.
    /// </summary>
    public void ShowSelection(QuickSetOffer offer)
    {
        ArgumentNullException.ThrowIfNull(offer);
        _offer = offer;

        // [S-3719] The widget's own lines are built again on every change: their closures hold the
        // config as it is now (an undo replaces it), and their ticks say what it holds now.
        _widget = WidgetLines();
        if (_widget is { } built)
        {
            // [S-3746] A line that was still reading (an account's calendars) asks to be built again
            // when its answer has come; only while these lines are still the ones shown.
            built.Outdated += () => DispatcherQueue?.TryEnqueue(() =>
            {
                try
                {
                    if (ReferenceEquals(_widget, built))
                    {
                        ShowSelection(_offer);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(Scope, "a widget's menu lines could not be built again", ex);
                }
            });
        }

        var nodes = Nodes();
        if (!string.Equals(FrameKey(), _frameKey, StringComparison.Ordinal) || _levels.Count == 0
            || !string.Equals(Shape(nodes), _levels[0].Shape, StringComparison.Ordinal))
        {
            Build(nodes);
            return;
        }

        Update(_levels[0], nodes);

        for (var i = 1; i < _levels.Count; i++)
        {
            var level = _levels[i];
            var key = level.Anchor!.Node.Key;
            if (_levels[i - 1].Views.FirstOrDefault(v => v is not null && Opens(v.Node) && v.Node.Key == key) is not { } anchor)
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

        // The same widget: its title may have changed (an undo, the box's own commit).
        if (_title is { } title && title.FocusState == FocusState.Unfocused)
        {
            _titleCommitted = offer.Label ?? string.Empty;
            title.Text = _titleCommitted;
        }
    }

    /// <summary>What the frame shows: the selection's count or the one widget, whether there is a menu at all.</summary>
    private string FrameKey()
        => _offer.Shows + "|" + _offer.TitleWidgetId + "|" + (_offer.HasTitle ? 0 : _offer.Count);

    private WidgetMenuLines? WidgetLines()
    {
        if (_offer.TitleWidgetId is not { } id || _commands.WidgetLines is not { } lines)
        {
            return null;
        }

        try
        {
            return lines(id, Ask, Confirm);
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "a widget's menu lines could not be built", ex);
            return null;
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

        _commands.ReleaseFocus();
    }

    // ---- the menu as lines ---------------------------------------------------------------------

    private enum NodeKind
    {
        Line,
        Separator,

        /// <summary>Something drawn whole, not a line (a colour submenu's swatches), drawn again when its <see cref="Node.ValueKey"/> changes.</summary>
        Custom,

        /// <summary>[S-3728] Words only, greyed and never clickable.</summary>
        Note,

        /// <summary>[S-3725] Room kept free under a search's answers, <see cref="Node.Height"/> tall.</summary>
        Spacer,
    }

    /// <summary>
    /// One line of the menu or of a submenu: its words, the value at its end, its icon (and what the
    /// icon is, to tell a change), a tick or a dot, a submenu or a search, or what a click does; a
    /// danger line is drawn in the critical colour.
    /// <see cref="Key"/> names it across rebuilds.
    /// </summary>
    private sealed record Node(string Key, NodeKind Kind = NodeKind.Line)
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

        public IReadOnlyList<Node>? Children { get; init; }

        public MenuSearch? Search { get; init; }

        public Func<FrameworkElement>? Build { get; init; }

        public string ValueKey { get; init; } = string.Empty;

        /// <summary>[S-1862] Where what the widget waits for is set: <see cref="OpenSetup"/> opens the menu at it.</summary>
        public bool SetsUp { get; init; }

        public static Node Separator(string key) => new(key, NodeKind.Separator);
    }

    private static bool Opens(Node node) => node.Children is not null || node.Search is not null;

    /// <summary>A drawn line: its node now, and the parts a change of value is written into.</summary>
    private sealed class LineView(Node node, Button button, FrameworkElement root, Border lead, Border trail, TextBlock words, TextBlock value)
    {
        public Node Node { get; set; } = node;

        public string IconKey { get; set; } = node.IconKey;

        public Button Button { get; } = button;

        public FrameworkElement Root { get; } = root;

        public Border Lead { get; } = lead;

        /// <summary>[S-3716] The end column: the tick or the dot, or the chevron of a line that opens a submenu.</summary>
        public Border Trail { get; } = trail;

        public TextBlock Words { get; } = words;

        public TextBlock Value { get; } = value;
    }

    /// <summary>The panel's list or one open submenu: its lines, the surface they are on, and the line that opened it.</summary>
    private sealed class Level(StackPanel lines, FrameworkElement root)
    {
        public StackPanel Lines { get; } = lines;

        public FrameworkElement Root { get; } = root;

        public List<LineView?> Views { get; } = [];

        public List<Node> Nodes { get; set; } = [];

        public string Shape { get; set; } = string.Empty;

        public LineView? Anchor { get; set; }

        /// <summary>[S-3716] Some line has a mark or a chevron: every line keeps the end column, so the values stand in one column.</summary>
        public bool Trail { get; set; }

        public bool Icons { get; set; }

        /// <summary>[S-3722] A search's box, what it was asked and found, and the question still out.</summary>
        public TextBox? Box { get; set; }

        public MenuSearch? Search { get; set; }

        public List<Node> Results { get; set; } = [];

        /// <summary>[S-3725] Show all was clicked: every answer shows until the next question.</summary>
        public bool ShowAll { get; set; }

        public CancellationTokenSource? Pending { get; set; }

        public Microsoft.UI.Dispatching.DispatcherQueueTimer? Settle { get; set; }
    }

    /// <summary>Which lines there are, as one string: values (ticks, words at the end, icons) are not in it.</summary>
    private static string Shape(IReadOnlyList<Node> nodes)
        => string.Join(";", nodes.Select(n => n.Key + "|" + (int)n.Kind + "|" + (n.Disabled ? 'd' : 'e') + "|"
            + (Opens(n) ? 's' : '-') + "|" + (n.Checked is null ? '-' : 'c') + "|" + (n.Icon is null ? '-' : 'i') + "|" + (n.Danger ? '!' : '-')));

    /// <summary>
    /// The lines of the level a node opens: its submenu, or a search's answers and its fixed lines.
    /// [S-3725] A search shows its first five answers and Show all (N) under them until that is
    /// clicked, and the answers' room is never less than five lines (kept free under a note or a few
    /// answers), so the fixed lines do not jump while it is typed into.
    /// </summary>
    private List<Node> LevelNodes(Level level, Node owner)
    {
        if (owner.Search is not { } search)
        {
            return [.. owner.Children ?? []];
        }

        var answers = level.Results;
        var cut = !level.ShowAll && answers.Count > SearchAnswers;
        var nodes = new List<Node>(cut ? answers.Take(SearchAnswers) : answers);
        if (cut)
        {
            nodes.Add(new Node(owner.Key + "/all")
            {
                Text = Str.Menu_ShowAll.Format(("count", LocaleFormat.Integer(answers.Count))),
                Run = () => ShowAllAnswers(level),
            });
        }

        if (nodes.Count < SearchAnswers)
        {
            var free = SearchAnswers - nodes.Count;
            nodes.Add(new Node(owner.Key + "/pad" + free, NodeKind.Spacer) { Height = free * LineHeight });
        }

        if (search.Fixed.Count > 0)
        {
            nodes.Add(Node.Separator(owner.Key + "/-fixed"));
            nodes.AddRange(FromEntries(search.Fixed, owner.Key + "/fixed"));
        }

        return nodes;
    }

    /// <summary>The menu's first level, from the offer, the widget's lines and the commands.</summary>
    private List<Node> Nodes()
    {
        var nodes = new List<Node>();
        if (!_offer.Shows)
        {
            return nodes;
        }

        var commands = _commands;

        // [S-6007] The look first: it changes the widget most. Only for one kind with styles; the
        // options only that look draws with are under it ([S-3719]).
        if (_offer.StyleKind is { } styled && WidgetStyles.For(styled) is { Count: > 0 } styles)
        {
            var now = _offer.Style;
            var current = styles.FirstOrDefault(s => s.Value == now);
            var shown = now is null || current.Glyph == 0 ? styles[0] : current;
            var options = new List<Node>();
            foreach (var style in styles)
            {
                var value = style.Value;
                var glyph = char.ConvertFromUtf32(style.Glyph);
                options.Add(Option("style/" + value, style.Label.Text(), glyph, () => EditBar.Icon(glyph, false, Fluent.IconSize), now == value, () => commands.SetStyle(value)));
            }

            if (_widget is { StyleLines.Count: > 0 } widget)
            {
                options.Add(Node.Separator("style/-"));
                options.AddRange(FromEntries(widget.StyleLines, "style/w"));
            }

            var icon = char.ConvertFromUtf32(shown.Glyph);
            nodes.Add(new Node("style")
            {
                Text = Str.Styles_Label.Text(),
                Value = now is null ? Mixed : shown.Label.Text(),
                IconKey = icon,
                Icon = () => Glyph(icon),
                Children = options,
            });
        }

        // [S-3719] The widget's own lines, in its order; [S-3723] the card's as one Appearance
        // submenu among them where the widget places it, else first, right after Style: for every
        // widget and for several.
        var lines = _widget is { } own ? FromEntries(own.Lines, "w") : [];
        lines.Insert(Math.Clamp(_widget?.CardAt ?? 0, 0, lines.Count), new Node("appearance")
        {
            Text = Str.Settings_Section_Appearance.Text(),
            IconKey = "appearance",
            Icon = () => Glyph(Glyphs.Palette),
            Children = CardNodes(commands),
        });
        nodes.AddRange(lines);
        return nodes;
    }

    /// <summary>
    /// [S-3723] The card's own lines, submenus throughout: Header, Alignment, Background, Item
    /// background, Item text and Scale. A line that cannot apply is not there ([S-3718]): Header for
    /// a kind that never has one ([S-3711]), Alignment where nothing selected aligns ([S-6584]), the
    /// item lines where nothing selected has items ([S-6571]). The colours are the menu's one colour
    /// vocabulary ([S-3724]), the same lines a widget's own colour gets.
    /// </summary>
    private List<Node> CardNodes(PropertiesCommands commands)
    {
        var nodes = new List<Node>();

        if (_offer.HasHeader)
        {
            var header = Array.Find(HeaderOptions(), o => o.Value == _offer.Header);
            var (glyph, turned) = header.Icon ?? (Glyphs.HeaderBottom, true);
            nodes.Add(new Node("header")
            {
                Text = Str.App_ConfigHeaderLabel.Text(),
                Value = _offer.Header is null ? Mixed : header.Text,
                IconKey = glyph + turned,
                Icon = () => EditBar.Icon(glyph, turned, Fluent.IconSize),
                Children = [.. HeaderOptions().Select(o => Option("header/" + o.Value, o.Text, o.Icon!.Value.Glyph + o.Icon.Value.Turned,
                    () => EditBar.Icon(o.Icon!.Value.Glyph, o.Icon.Value.Turned, Fluent.IconSize), _offer.Header == o.Value, () => commands.SetHeader(o.Value)))],
            });
        }

        // [S-6584] Where the content sits across the card, for the widgets that align in their style ([S-6581]).
        if (_offer.CanAlign)
        {
            var align = AlignOptions();
            var now = _offer.Align;
            var at = Array.FindIndex(align, o => o.Value == now);
            var glyph = at >= 0 ? align[at].Glyph : Glyphs.AlignLeft;
            nodes.Add(new Node("align")
            {
                Text = Str.Widget_Align_Label.Text(),
                Value = at >= 0 ? align[at].Name.Text() : Mixed,
                IconKey = glyph,
                Icon = () => Glyph(glyph),
                Children = [.. align.Select(o => Option("align/" + o.Value, o.Name.Text(), o.Glyph, () => Glyph(o.Glyph), now == o.Value, () => commands.SetAlign?.Invoke(o.Value)))],
            });
        }

        // [S-6566] The widgets' own background and item background: one fill each, Default for the
        // app-wide one; [S-6544] the colour of the text on their items, Default for the app-wide one.
        // A mixed selection's value is null: the line reads as a dash and nothing holds.
        var colours = new List<ItemMenuEntry>
        {
            new(null, Str.Settings_Style_BgLabel.Text(), null, Colour: MenuColour.OfFill(_offer.Background, commands.SetBackground,
                new FillChoiceOptions { DefaultLabel = Str.Common_Default.Text(), DefaultColor = AppBackgroundColour })),
        };

        if (_offer.HasItems)
        {
            colours.Add(new(null, Str.Shell_Items_Background.Text(), null, Colour: MenuColour.OfFill(_offer.Items, commands.SetItemBackground,
                new FillChoiceOptions { DefaultLabel = Str.Common_Default.Text(), DefaultColor = AppItemColour })));
            colours.Add(new(null, Str.Shell_Items_TextShort.Text(), null, Colour: MenuColour.Of(_offer.ItemText, commands.SetItemTextColor,
                new ColorChoiceOptions { DefaultLabel = Str.Common_Default.Text(), Presets = ColorPresets.Surfaces, DefaultColor = AppItemTextColour })));
        }

        nodes.AddRange(FromEntries(colours, "card"));

        // [S-6531] The widgets' own content scale, in steps, after the look of the card it magnifies.
        {
            var now = _offer.Scale;
            nodes.Add(new Node("scale")
            {
                Text = Str.Widget_Scale_Label.Text(),
                Value = now is { } scale ? ScaleText(scale) : Mixed,
                IconKey = "scale",
                Icon = () => Glyph(Glyphs.Scale),
                Children = [.. WidgetScale.Offered(now).Select(step => Option("scale/" + step, ScaleText(step), string.Empty, null,
                    now is { } v && v.Equals(step), () => commands.SetScale(step)))],
            });
        }

        return nodes;
    }

    /// <summary>
    /// [S-3724] A colour line and its submenu, the one path every colour of the menu takes (the
    /// card's and a widget's own): Default (where the setting has one) and a fill's Transparent with
    /// the dot, the swatches, a fill's Opacity in steps (a colour only), Custom colour… (the picker's
    /// dialog) and Pick a colour from the screen (the eyedropper), those two where custom colours are
    /// allowed. The line shows the colour's swatch and its words; a mixed selection reads as a dash.
    /// </summary>
    private Node ColourNode(string key, string text, MenuColour colour, bool disabled)
    {
        var options = colour.Options;
        var chosen = colour.Chosen;

        // Every part of the value, so the swatches are drawn again (with a pick's opacity) when any changes.
        var held = colour.Mixed ? "mixed" : colour.IsFill ? colour.Fill!.Kind + "|" + chosen + "|" + colour.Opacity : "c|" + chosen;

        void Paint(Border swatch)
        {
            if (colour.Mixed)
            {
                return;
            }

            if (colour.IsFill)
            {
                ColorChoice.PaintFill(swatch, colour.Fill!, options);
            }
            else
            {
                ColorChoice.PaintSwatch(swatch, chosen, options);
            }
        }

        var children = new List<Node>();
        if (options.DefaultLabel is { } defaultLabel)
        {
            children.Add(Option(key + "/default", defaultLabel, "default", () => Swatch(b =>
            {
                if (colour.IsFill)
                {
                    ColorChoice.PaintFill(b, FillChoice.Default(), options);
                }
                else
                {
                    ColorChoice.PaintSwatch(b, string.Empty, options);
                }
            }), colour.DefaultHolds, colour.ChooseDefault));
        }

        if (colour.IsFill)
        {
            children.Add(Option(key + "/transparent", Str.Shell_Fill_Transparent.Text(), "transparent", () => Swatch(b => ColorChoice.PaintFill(b, FillChoice.Transparent(), options)),
                colour.TransparentHolds, colour.ChooseTransparent));
        }

        children.Add(Node.Separator(key + "/-swatches"));
        children.Add(new Node(key + "/swatches", NodeKind.Custom)
        {
            ValueKey = held,
            Build = () => Swatches(options.Presets, chosen, colour.Choose),
        });
        children.Add(Node.Separator(key + "/-more"));

        if (colour.IsFill)
        {
            children.Add(new Node(key + "/opacity")
            {
                Text = Str.Shell_Fill_Opacity.Text(),
                Value = colour.HasOpacity ? Percent(colour.Opacity) : null,
                Disabled = !colour.HasOpacity,
                Children = [.. MenuColour.OpacitySteps.Select(step => Option(key + "/opacity/" + step, Percent(step), string.Empty, null,
                    colour.HasOpacity && Math.Abs(colour.Opacity - step) < 0.005, () => colour.ChooseOpacity(step)))],
            });
        }

        if (options.AllowCustom)
        {
            children.Add(new Node(key + "/custom")
            {
                Text = Str.Shell_Color_Custom.Text(),
                IconKey = "custom",
                Icon = () => Glyph(Glyphs.Palette),
                Run = () => Custom(key, chosen, options, colour.IsFill ? colour.Opacity : null, colour.ChooseCustom),
            });
            children.Add(new Node(key + "/screen")
            {
                Text = Str.Shell_Color_PickFromScreen.Text(),
                IconKey = "screen",
                Icon = () => Glyph(Glyphs.Eyedropper),
                Run = () => ColorChoice.PickFromScreen(colour.Choose),
            });
        }

        return new Node(key)
        {
            Text = text,
            Value = colour.Mixed ? Mixed : colour.IsFill ? ColorChoice.DescribeFill(colour.Fill!, options) : ColorChoice.Describe(chosen, options),
            IconKey = held,
            Icon = () => Swatch(Paint),
            Disabled = disabled,
            Children = children,
        };
    }

    private static string Percent(double fraction) => LocaleFormat.Percent(Strings.Current, (int)Math.Round(fraction * 100));

    /// <summary>[S-3723] The custom colour dialog, from the line that asked for it.</summary>
    private void Custom(string key, string current, ColorChoiceOptions options, double? opacity, Action<string, double> choose)
    {
        FrameworkElement source = _levels.Count > 1 ? _levels[^1].Root : _frame;
        _ = ColorChoice.CustomAsync(source, current, options, opacity, choose);
        Log.Debug(Scope, $"custom colour asked from {key}");
    }

    /// <summary>
    /// [S-3723] A colour submenu's swatches, eight to a row: the chosen one with a tick in a colour
    /// that reads on it and the accent ring, each one's name as its tooltip. A click picks it, and the
    /// submenu stays open on the pick.
    /// </summary>
    private static FrameworkElement Swatches(IReadOnlyList<ColorPreset> presets, string current, Action<string> pick)
    {
        var grid = new Grid
        {
            RowSpacing = Fluent.Space1,
            ColumnSpacing = Fluent.Space1,
            Margin = new Thickness(Fluent.Space3, Fluent.Space1, Fluent.Space3, Fluent.Space1),
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        for (var c = 0; c < ColorPresets.Columns; c++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(SwatchSize) });
        }

        for (var r = 0; r < (presets.Count + ColorPresets.Columns - 1) / ColorPresets.Columns; r++)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(SwatchSize) });
        }

        for (var i = 0; i < presets.Count; i++)
        {
            var preset = presets[i];
            var hex = HexColor.Normalise(preset.Hex);
            HexColor.TryParse(hex, out var r, out var g, out var b);
            var chosen = hex == current;

            var swatch = new Button
            {
                Width = SwatchSize,
                Height = SwatchSize,
                Padding = new Thickness(0),
                MinWidth = 0,
                MinHeight = 0,
                CornerRadius = new CornerRadius(Fluent.ControlRadius),
                Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, r, g, b)),
                BorderThickness = new Thickness(chosen ? 2 : Fluent.Hairline),
                BorderBrush = chosen ? ThemeBrush.Of("AccentFillColorDefaultBrush", Colors.DodgerBlue) : ThemeBrush.Of("ControlStrokeColorDefaultBrush", Colors.Gray),
                Content = chosen
                    ? new FontIcon
                    {
                        Glyph = Glyphs.Done,
                        FontSize = 12,

                        // The tick is drawn ON the colour, so its colour is the colour's contrast, not a theme brush.
                        Foreground = new SolidColorBrush(ScreenPick.WantsDarkInk(r, g, b) ? Colors.Black : Colors.White),
                    }
                    : null,
            };

            // The colour stays itself under the pointer: only the ring says it is pointed at.
            swatch.Resources["ButtonBackgroundPointerOver"] = swatch.Background;
            swatch.Resources["ButtonBackgroundPressed"] = swatch.Background;
            swatch.Resources["ButtonBorderBrushPointerOver"] = ThemeBrush.Of("ControlStrokeColorSecondaryBrush", Colors.Gray);

            ToolTipService.SetToolTip(swatch, preset.Name);
            AutomationProperties.SetName(swatch, preset.Name);
            swatch.Click += (_, _) => Run(() => pick(hex));

            Grid.SetRow(swatch, i / ColorPresets.Columns);
            Grid.SetColumn(swatch, i % ColorPresets.Columns);
            grid.Children.Add(swatch);
        }

        return grid;
    }

    /// <summary>A colour's swatch the size of a menu icon, framed as Settings frames its swatches.</summary>
    private static Border Swatch(Action<Border> paint)
    {
        var swatch = new Border
        {
            Width = Fluent.IconSize,
            Height = Fluent.IconSize,
            CornerRadius = new CornerRadius(Fluent.ControlRadius),
            BorderThickness = new Thickness(Fluent.Hairline),
            BorderBrush = ThemeBrush.Of("ControlStrokeColorDefaultBrush", Colors.Gray),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
        };
        paint(swatch);
        return swatch;
    }

    /// <summary>One option of a choice of one: the dot while it holds.</summary>
    private static Node Option(string key, string text, string iconKey, Func<UIElement?>? icon, bool holds, Action pick)
        => new(key) { Text = text, IconKey = iconKey, Icon = icon, Checked = holds, Radio = true, Run = pick };

    /// <summary>
    /// [S-3719] The widget's own lines (and the card's colours) as the menu's: its submenus,
    /// searches, switches, choices, commands, colours ([S-3724]) and notes ([S-3728]).
    /// </summary>
    private List<Node> FromEntries(IReadOnlyList<ItemMenuEntry> entries, string prefix)
    {
        var nodes = new List<Node>(entries.Count);
        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (entry.IsSeparator)
            {
                nodes.Add(Node.Separator(prefix + "/-" + i));
                continue;
            }

            var key = prefix + "/" + entry.Text;
            if (entry.Colour is { } colour)
            {
                nodes.Add(ColourNode(key, entry.Text, colour, entry.Disabled));
                continue;
            }

            if (entry.Note)
            {
                nodes.Add(new Node(key, NodeKind.Note) { Text = entry.Text });
                continue;
            }

            var value = entry.Value ?? (entry.Shortcut is { } chord ? KeyChordText.Format(chord) : entry.Search?.Value);
            List<Node>? children = null;
            if (entry.Children is { } kids)
            {
                children = FromEntries(kids, key);

                // [S-3718] A choice of one shows the option that holds, as the card's own choices do.
                var chosen = kids.FirstOrDefault(k => k is { Checked: true, Group: not null });
                if (chosen.Group is not null)
                {
                    value ??= chosen.Text;
                }
            }

            nodes.Add(new Node(key)
            {
                Text = entry.Text,
                Value = value,
                IconKey = entry.IconFile ?? entry.Glyph ?? string.Empty,
                Icon = entry.Glyph is null && entry.IconFile is null ? null : () => ItemMenu.IconFor(entry),
                Run = entry.Action,
                Checked = entry.Checked,
                Radio = entry.Group is not null,
                Disabled = entry.Disabled,
                Danger = entry.Danger,
                Children = children,
                Search = entry.Search,
                SetsUp = entry.SetsUp,
            });
        }

        return nodes;
    }

    // ---- drawing -------------------------------------------------------------------------------

    private void Build(List<Node> nodes)
    {
        CommitTitleLater();

        // [S-3718] Which submenus were open, by their lines' keys: opened again over the new lines.
        var open = _levels.Skip(1).Select(l => l.Anchor!.Node.Key).ToList();
        CloseFrom(1);
        _levels.Clear();
        _menu.Children.Clear();
        _title = null;
        _titleId = null;
        _frameKey = FrameKey();

        if (!_offer.Shows)
        {
            return;
        }

        var commands = _commands;
        _menu.Children.Add(QuickRow(commands));
        _menu.Children.Add(Divider(full: true));

        if (_offer is { TitleWidgetId: { } id, TitleKind: { } kind })
        {
            _menu.Children.Add(TitleBox(commands, id, ModuleCatalog.Of(kind).Name, _offer.Label));
        }
        else
        {
            // [S-3702] Several: no title, but how many the choices reach, over the lines.
            var count = Fluent.Text(FluentType.BodyStrong);
            count.Text = Str.Shell_EditBar_Selected.Plural(_offer.Count);
            count.Margin = new Thickness(Fluent.Space4, Fluent.Space2, Fluent.Space4, 0);
            _menu.Children.Add(count);
        }

        var list = new StackPanel { Padding = new Thickness(0, Fluent.Space1, 0, Fluent.Space1), XYFocusKeyboardNavigation = XYFocusKeyboardNavigationMode.Enabled };
        _menu.Children.Add(list);

        var main = new Level(list, _frame) { Icons = true };
        _levels.Add(main);
        FillLevel(main, nodes);

        foreach (var key in open)
        {
            if (_levels[^1].Views.FirstOrDefault(v => v is not null && Opens(v.Node) && v.Node.Key == key) is not { } anchor
                || !OpenSubmenu(anchor, focus: false))
            {
                break;
            }
        }
    }

    /// <summary>Draw <paramref name="nodes"/> into <paramref name="level"/>, replacing what it held.</summary>
    private void FillLevel(Level level, IReadOnlyList<Node> nodes)
    {
        level.Lines.Children.Clear();
        level.Views.Clear();
        level.Nodes = [.. nodes];
        level.Shape = Shape(nodes);
        level.Trail = nodes.Any(n => n.Kind == NodeKind.Line && (n.Checked is not null || Opens(n)));
        level.Icons |= nodes.Any(n => n.Kind == NodeKind.Line && n.Icon is not null);

        foreach (var node in nodes)
        {
            switch (node.Kind)
            {
                case NodeKind.Separator:
                    level.Lines.Children.Add(Divider(full: false));
                    level.Views.Add(null);
                    break;
                case NodeKind.Note:
                    level.Lines.Children.Add(NoteLine(node.Text, level.Icons));
                    level.Views.Add(null);
                    break;
                case NodeKind.Spacer:
                    level.Lines.Children.Add(new Border { Height = node.Height, IsHitTestVisible = false });
                    level.Views.Add(null);
                    break;
                case NodeKind.Custom:
                    level.Lines.Children.Add(node.Build!());
                    level.Views.Add(null);
                    break;
                default:
                    var view = MakeLine(level, node);
                    level.Lines.Children.Add(view.Root);
                    level.Views.Add(view);
                    break;
            }
        }

        // Leading, trailing and doubled dividers go, as the card menu drops them ([S-1742]).
        HideStrayDividers(level.Lines);
    }

    private static void HideStrayDividers(StackPanel lines)
    {
        var last = -1;
        var previousWasDivider = true;
        for (var i = 0; i < lines.Children.Count; i++)
        {
            var isDivider = lines.Children[i] is Rectangle;
            lines.Children[i].Visibility = Visibility.Visible;
            if (isDivider && previousWasDivider)
            {
                lines.Children[i].Visibility = Visibility.Collapsed;
                continue;
            }

            previousWasDivider = isDivider;
            if (!isDivider)
            {
                last = i;
            }
        }

        for (var i = last + 1; i < lines.Children.Count; i++)
        {
            lines.Children[i].Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>Write the values of <paramref name="nodes"/> (same shape) into the lines drawn for the ones before.</summary>
    private static void Update(Level level, IReadOnlyList<Node> nodes)
    {
        for (var i = 0; i < nodes.Count && i < level.Views.Count; i++)
        {
            var node = nodes[i];
            if (level.Views[i] is { } view)
            {
                Apply(level, view, node);
            }
            else if (node.Kind is NodeKind.Custom && i < level.Nodes.Count
                && !string.Equals(level.Nodes[i].ValueKey, node.ValueKey, StringComparison.Ordinal))
            {
                level.Lines.Children[i] = node.Build!();
            }
        }

        level.Nodes = [.. nodes];
    }

    private static void Apply(Level level, LineView view, Node node)
    {
        view.Node = node;
        view.Words.Text = node.Text;
        view.Value.Text = node.Value ?? string.Empty;

        if (!string.Equals(view.IconKey, node.IconKey, StringComparison.Ordinal))
        {
            view.IconKey = node.IconKey;
            view.Lead.Child = node.Icon?.Invoke();
        }

        // [S-3716] The mark at the end of the line, where a line that opens a submenu has its chevron.
        if (!Opens(node))
        {
            view.Trail.Child = node.Checked == true ? Mark(node.Radio) : null;
        }

        AutomationProperties.SetName(view.Button, node.Value is { Length: > 0 } value ? node.Text + ", " + value : node.Text);
        if (node.Checked is { } on)
        {
            AutomationProperties.SetItemStatus(view.Button, on ? "✓" : string.Empty);
        }
    }

    /// <summary>A tick for a switch that is on, the small dot for the option that holds, as Windows draws them.</summary>
    private static FrameworkElement Mark(bool radio) => radio
        ? new Ellipse
        {
            Width = 6,
            Height = 6,
            Fill = ThemeBrush.Of("TextFillColorPrimaryBrush", Colors.White),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        }
        : new FontIcon { Glyph = Glyphs.Done, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };

    /// <summary>
    /// [S-3716] One line: an icon column (where the level has icons), its words, a value (or a
    /// shortcut) in the secondary colour where a menu writes a shortcut, and at the end, right-aligned
    /// in its own column, the tick or the dot of a switch or a choice, or the chevron of a line that
    /// opens a submenu (a line is never both). A danger line's words and icon are the critical colour.
    /// </summary>
    private LineView MakeLine(Level level, Node node)
    {
        var lead = new Border { Width = Fluent.IconSize, HorizontalAlignment = HorizontalAlignment.Left, Child = node.Icon?.Invoke() };
        var trail = new Border { Width = Fluent.IconSize, HorizontalAlignment = HorizontalAlignment.Right };
        if (Opens(node))
        {
            trail.Child = new FontIcon
            {
                Glyph = Chevron,
                FontSize = 12,
                MirroredWhenRightToLeft = true,
                Foreground = ThemeBrush.Of("TextFillColorSecondaryBrush", Colors.Gray),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
        }

        var words = new TextBlock
        {
            FontSize = FluentType.Body.FontSize,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        var value = Fluent.Text(FluentType.Caption, "TextFillColorSecondaryBrush");

        if (node.Danger && !node.Disabled)
        {
            var critical = ThemeBrush.Of("SystemFillColorCriticalBrush", Colors.IndianRed);
            words.Foreground = critical;
            if (lead.Child is IconElement icon)
            {
                icon.Foreground = critical;
            }
        }

        var button = LineButton(level.Icons ? lead : null, words, value, level.Trail ? trail : null);
        button.IsEnabled = !node.Disabled;

        var view = new LineView(node, button, button, lead, trail, words, value);
        Apply(level, view, node);

        button.Click += (_, _) => OnLine(level, view);
        button.KeyDown += (_, e) =>
        {
            var rtl = UiLanguage.Flow == FlowDirection.RightToLeft;
            var towards = rtl ? Windows.System.VirtualKey.Left : Windows.System.VirtualKey.Right;
            var away = rtl ? Windows.System.VirtualKey.Right : Windows.System.VirtualKey.Left;
            if (e.Key == towards && Opens(view.Node))
            {
                CloseFrom(_levels.IndexOf(level) + 1);
                OpenSubmenu(view, focus: true);
                e.Handled = true;
            }
            else if (e.Key == away && _levels.IndexOf(level) is var at && at > 0)
            {
                var anchor = level.Anchor;
                CloseFrom(at);
                anchor?.Button.Focus(FocusState.Keyboard);
                e.Handled = true;
            }
        };

        return view;
    }

    /// <summary>The button of a line, its parts in their columns (the ones given), 32 DIPs, inset 4.</summary>
    private static Button LineButton(UIElement? lead, TextBlock words, TextBlock value, UIElement? trail)
    {
        var parts = new Grid();
        var column = 0;

        void Add(UIElement element, GridLength width)
        {
            parts.ColumnDefinitions.Add(new ColumnDefinition { Width = width });
            Grid.SetColumn((FrameworkElement)element, column++);
            parts.Children.Add(element);
        }

        if (lead is not null)
        {
            Add(lead, new GridLength(LeadColumn));
        }

        Add(words, new GridLength(1, GridUnitType.Star));

        // The value and the end column never meet: the value keeps a gap before the mark or chevron.
        value.VerticalAlignment = VerticalAlignment.Center;
        value.Margin = new Thickness(Fluent.Space6, 0, trail is null ? 0 : Fluent.Space2, 0);
        value.MaxWidth = 140;
        value.TextTrimming = TextTrimming.CharacterEllipsis;
        Add(value, GridLength.Auto);

        if (trail is not null)
        {
            Add(trail, new GridLength(Fluent.IconSize));
        }

        var line = new Button
        {
            Content = parts,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(Fluent.Space1, 0, Fluent.Space1, 0),
        };
        EditBar.Subtle(line);
        line.Height = LineHeight;
        line.Padding = new Thickness(Fluent.Space3, 0, Fluent.Space3, 0);
        return line;
    }

    /// <summary>
    /// [S-3718] A click on a line: a submenu line opens its submenu beside it (or closes it again);
    /// anything else does what it does, and the menu and the submenu it is in stay open.
    /// </summary>
    private void OnLine(Level level, LineView view)
    {
        var at = _levels.IndexOf(level);
        if (at < 0)
        {
            return;
        }

        if (Opens(view.Node))
        {
            var wasOpen = _levels.Count > at + 1 && ReferenceEquals(_levels[at + 1].Anchor, view);
            CloseFrom(at + 1);
            if (!wasOpen)
            {
                OpenSubmenu(view, focus: false);
            }

            return;
        }

        CloseFrom(at + 1);
        Run(view.Node.Run);
    }

    /// <summary>[S-3718] Open the submenu of <paramref name="anchor"/>'s line beside the level it is in; false when it has nothing.</summary>
    private bool OpenSubmenu(LineView anchor, bool focus)
    {
        if (!Opens(anchor.Node) || !anchor.Button.IsEnabled || anchor.Node.Children is { Count: 0 })
        {
            return false;
        }

        var lines = new StackPanel { Padding = new Thickness(0, Fluent.Space1, 0, Fluent.Space1), XYFocusKeyboardNavigation = XYFocusKeyboardNavigationMode.Enabled };
        var scroll = new ScrollViewer
        {
            Content = lines,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            MaxHeight = Math.Max(LineHeight * 4, (XamlRoot?.Size.Height ?? 800) - (Fluent.Space6 * 3)),
        };
        var body = new Grid { RowDefinitions = { new RowDefinition { Height = GridLength.Auto }, new RowDefinition { Height = GridLength.Auto } } };
        Grid.SetRow(scroll, 1);
        body.Children.Add(scroll);

        var search = anchor.Node.Search;
        var frame = new Border { Child = body, MinWidth = search is null ? MinSubmenuWidth : SearchWidth, MaxWidth = MaxSubmenuWidth, TabFocusNavigation = KeyboardNavigationMode.Cycle };
        var host = new Grid { Children = { frame } };
        FlyoutSurface.Apply(host, frame);
        host.PointerPressed += (_, e) => e.Handled = true;

        var level = new Level(lines, host) { Anchor = anchor, Search = search };
        if (search is not null)
        {
            var box = SearchBox(level, search);
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
        else if (focus && level.Views.FirstOrDefault(v => v is { Button.IsEnabled: true }) is { } first)
        {
            first.Button.Focus(FocusState.Keyboard);
        }

        return true;
    }

    /// <summary>
    /// [S-3722] A search's box, on top of its submenu: what is typed is asked once typing settles (at
    /// once for an answer in hand), each answer replacing the one before; Enter picks the first line,
    /// Down moves to it.
    /// </summary>
    private TextBox SearchBox(Level level, MenuSearch search)
    {
        var box = new TextBox
        {
            PlaceholderText = search.Placeholder,
            Height = LineHeight,
            MinHeight = LineHeight,
            Margin = new Thickness(Fluent.Space2, Fluent.Space2, Fluent.Space2, 0),
            IsSpellCheckEnabled = false,
            IsTextPredictionEnabled = false,
        };
        AutomationProperties.SetName(box, search.Placeholder);

        box.TextChanged += (_, _) =>
        {
            var settle = level.Search?.Settle ?? TimeSpan.Zero;
            if (settle <= TimeSpan.Zero)
            {
                Ask(level, box.Text);
                return;
            }

            // A pause in typing, not a guess at readiness: the geocoder is asked for what was meant, once.
            level.Settle ??= DispatcherQueue.CreateTimer();
            level.Settle.Stop();
            level.Settle.Interval = settle;
            level.Settle.IsRepeating = false;
            level.Settle.Tick -= OnSettled;
            level.Settle.Tick += OnSettled;
            level.Settle.Start();
        };

        void OnSettled(Microsoft.UI.Dispatching.DispatcherQueueTimer timer, object _) => Ask(level, box.Text);

        box.KeyDown += (_, e) =>
        {
            if (e.Key == Windows.System.VirtualKey.Enter
                && level.Views.FirstOrDefault(v => v is { Button.IsEnabled: true } && v.Node.Key.Contains("/q/", StringComparison.Ordinal)) is { } first)
            {
                OnLine(level, first);
                e.Handled = true;
            }
            else if (e.Key == Windows.System.VirtualKey.Down && level.Views.FirstOrDefault(v => v is { Button.IsEnabled: true }) is { } next)
            {
                next.Button.Focus(FocusState.Keyboard);
                e.Handled = true;
            }
        };

        box.GotFocus += (_, _) => CloseFrom(_levels.IndexOf(level) + 1);
        return box;
    }

    /// <summary>[S-3722] Ask the search for <paramref name="typed"/>; a newer question drops an older one's answer.</summary>
    private async void Ask(Level level, string typed)
    {
        try
        {
            if (level.Search is not { } search || level.Anchor is not { } anchor)
            {
                return;
            }

            level.Pending?.Cancel();
            using var cts = new CancellationTokenSource();
            level.Pending = cts;

            // A question that goes out to the network says so while it is out.
            level.ShowAll = false;
            if (search.Settle > TimeSpan.Zero && typed.Trim().Length > 0)
            {
                level.Results = [new Node(anchor.Node.Key + "/q/note") { Text = Str.Shell_Place_Searching.Text(), Disabled = true }];
                FillLevel(level, LevelNodes(level, anchor.Node));
            }

            var answer = await search.Find(typed.Trim(), cts.Token).ConfigureAwait(true);
            if (!ReferenceEquals(level.Pending, cts) || !_levels.Contains(level))
            {
                return;
            }

            level.Pending = null;
            level.Results = answer.Note is { } note
                ? [new Node(anchor.Node.Key + "/q/note") { Text = note, Disabled = true }]
                : FromEntries(answer.Lines, anchor.Node.Key + "/q");
            FillLevel(level, LevelNodes(level, anchor.Node));
            Place(level, _levels.IndexOf(level));
        }
        catch (OperationCanceledException)
        {
            // ignore: a newer question is out; its answer is the one shown.
        }
        catch (Exception ex)
        {
            // An async void from a XAML event: nothing may escape.
            Log.Error(Scope, "a properties search failed", ex);
        }
    }

    /// <summary>[S-3725] Show all: every answer of the search, until the next question; the keyboard goes on to the sixth.</summary>
    private void ShowAllAnswers(Level level)
    {
        var at = _levels.IndexOf(level);
        if (level.Anchor is not { } anchor || at < 0)
        {
            return;
        }

        var focused = level.Views.Any(v => v is not null && v.Node.Key.EndsWith("/all", StringComparison.Ordinal) && v.Button.FocusState == FocusState.Keyboard);
        level.ShowAll = true;
        FillLevel(level, LevelNodes(level, anchor.Node));
        Place(level, at);

        if (focused && level.Views.Where(v => v is { Button.IsEnabled: true }).Skip(SearchAnswers).FirstOrDefault() is { } next)
        {
            next.Button.Focus(FocusState.Keyboard);
        }
    }

    /// <summary>
    /// Put a submenu beside the level before it, its first line level with the line that opened it:
    /// on the end side, else the start side where the end has no room in the window, slid up to fit.
    /// </summary>
    private void Place(Level level, int at)
    {
        if (at < 1 || level.Anchor is not { } anchorView)
        {
            return;
        }

        var parent = _levels[at - 1].Root;
        UpdateLayout();

        var size = level.Root.DesiredSize;
        var origin = parent.TransformToVisual(_subs).TransformPoint(new Point(0, 0));
        var line = anchorView.Root.TransformToVisual(_subs).TransformPoint(new Point(0, 0));
        var top = line.Y - Fluent.Space1 - Fluent.Hairline;

        Canvas.SetLeft(level.Root, origin.X + parent.ActualWidth - Fluent.Space1);
        Canvas.SetTop(level.Root, top);
        UpdateLayout();

        if (XamlRoot is not { } root)
        {
            return;
        }

        var window = root.Size;
        var bounds = level.Root.TransformToVisual(null).TransformBounds(new Rect(0, 0, size.Width, size.Height));
        if (bounds.Right > window.Width - Fluent.Space2 || bounds.Left < Fluent.Space2)
        {
            Canvas.SetLeft(level.Root, origin.X - size.Width + Fluent.Space1);
        }

        var under = bounds.Bottom - (window.Height - Fluent.Space2);
        if (under > 0)
        {
            var above = bounds.Top - Fluent.Space2;
            Canvas.SetTop(level.Root, top - Math.Min(under, Math.Max(0, above)));
        }
    }

    /// <summary>Close the submenus from level <paramref name="at"/> down; the panel's own list (level 0) stays.</summary>
    private void CloseFrom(int at)
    {
        for (var i = _levels.Count - 1; i >= Math.Max(1, at); i--)
        {
            var level = _levels[i];
            level.Pending?.Cancel();
            level.Settle?.Stop();
            _subs.Children.Remove(level.Root);
            if (level.Anchor is { } anchor)
            {
                Lit(anchor, false);
            }

            _levels.RemoveAt(i);
        }
    }

    /// <summary>While its submenu is open a line stays lit, as Windows keeps it.</summary>
    private static void Lit(LineView view, bool on)
    {
        if (on)
        {
            view.Button.Background = ThemeBrush.Of("SubtleFillColorSecondaryBrush", Colors.Gray);
        }
        else
        {
            view.Button.ClearValue(Control.BackgroundProperty);
        }
    }

    /// <summary>
    /// [S-3717] The icon row: Cut and Copy at its start, Delete at its far end, as icons with their
    /// words and keys as the tooltip (Explorer's row; Delete kept apart, where a slip does not reach
    /// it). No Paste: a paste lands on the desktop where it is clicked, never on the selection.
    /// </summary>
    private Grid QuickRow(PropertiesCommands commands)
    {
        var row = new Grid
        {
            Padding = new Thickness(Fluent.Space1),
            ColumnSpacing = Fluent.Space1,
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto },
            },
        };

        string Verb(Str one, Str many) => _offer.Count > 1 ? many.Plural(_offer.Count) : one.Text();

        Add(0, Glyphs.Cut, Verb(Str.CtxMenu_Cut, Str.Shell_Card_CutMany), KeyChord.Ctrl(ChordKey.X), commands.Cut);
        Add(1, Glyphs.Copy, Verb(Str.CtxMenu_Copy, Str.Shell_Card_CopyMany), KeyChord.Ctrl(ChordKey.C), commands.Copy);
        Add(3, Glyphs.Delete, Verb(Str.Common_Delete, Str.Shell_Card_DeleteMany), KeyChord.Plain(ChordKey.Delete), commands.Delete);
        return row;

        void Add(int column, string glyph, string words, KeyChord chord, Action? run)
        {
            var on = run is not null;
            var button = new Button
            {
                Content = on ? MenuArtIcons.IconOrGlyph(glyph) : UiLanguage.Mirrored(new FontIcon { Glyph = glyph, FontSize = Fluent.IconSize }),
                Width = RowButton,
                Height = RowButton,
                IsEnabled = on,
            };
            EditBar.Subtle(button);
            button.Height = RowButton;
            button.Padding = new Thickness(0);

            var keys = KeyChordText.Format(chord);
            ToolTipService.SetToolTip(button, Str.Shell_Menu_LabelWithShortcut.Format(("label", words), ("shortcut", keys)));
            AutomationProperties.SetName(button, words);
            AutomationProperties.SetAcceleratorKey(button, keys);
            button.Click += (_, _) =>
            {
                CloseFrom(1);
                Run(run);
            };

            Grid.SetColumn(button, column);
            row.Children.Add(button);
        }
    }

    /// <summary>
    /// [S-3728] A note: words only in the disabled colour, where a line's words start, wrapped to a
    /// few lines within the menu's width and trimmed past them, the whole as its tooltip.
    /// </summary>
    private static FrameworkElement NoteLine(string text, bool icons)
    {
        var words = Fluent.Text(FluentType.Caption, "TextFillColorTertiaryBrush");
        words.Text = text;
        words.TextWrapping = TextWrapping.WrapWholeWords;
        words.TextTrimming = TextTrimming.CharacterEllipsis;
        words.MaxLines = NoteLines;
        words.MaxWidth = MinMenuWidth - (2 * (Fluent.Space1 + Fluent.Space3)) - (icons ? LeadColumn : 0);
        words.HorizontalAlignment = HorizontalAlignment.Left;
        ToolTipService.SetToolTip(words, text);
        AutomationProperties.SetName(words, text);

        return new Border
        {
            Child = words,
            Background = new SolidColorBrush(Colors.Transparent),
            Padding = new Thickness(Fluent.Space1 + Fluent.Space3 + (icons ? LeadColumn : 0), Fluent.Space2, Fluent.Space1 + Fluent.Space3, Fluent.Space2),
        };
    }

    /// <summary>A divider between groups: across the menu under the icon row, inset in the list.</summary>
    private static Rectangle Divider(bool full) => new()
    {
        Height = Fluent.Hairline,
        Fill = ThemeBrush.Of("DividerStrokeColorDefaultBrush", Colors.Gray),
        Margin = full ? new Thickness(0) : new Thickness(Fluent.Space1, Fluent.Space1, Fluent.Space1, Fluent.Space1),
        IsHitTestVisible = false,
    };

    private static IconElement Glyph(string glyph) => UiLanguage.Mirrored(new FontIcon { Glyph = glyph, FontSize = Fluent.IconSize });

    /// <summary>Run a command from inside a XAML event: a throw is logged, never left to escape.</summary>
    private static void Run(Action? action)
    {
        try
        {
            action?.Invoke();
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "a properties command failed", ex);
        }
    }

    /// <summary>
    /// [S-1862] The card's Set up button: open the menu at the line the widget's module names for what
    /// it waits for (<see cref="WidgetMenuLines.SetsUp"/>), through the submenus on the way, as clicks
    /// would: a submenu or a search opens (its first line or its box focused), any other line runs as
    /// its click does (a question asked beside the menu). A widget that names none keeps the menu as it
    /// stands. False when no line was opened.
    /// </summary>
    public bool OpenSetup()
    {
        if (_levels.Count == 0 || SetupPath(_levels[0].Nodes) is not { } path)
        {
            return false;
        }

        CloseFrom(1);
        UpdateLayout();
        for (var i = 0; i < path.Count; i++)
        {
            var key = path[i];
            if (_levels[^1].Views.FirstOrDefault(v => v is not null && v.Node.Key == key) is not { } line || !line.Button.IsEnabled)
            {
                return false;
            }

            if (Opens(line.Node))
            {
                if (!OpenSubmenu(line, focus: i == path.Count - 1))
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

    /// <summary>[S-1862] The keys of the lines from the first level down to the one that sets the widget up, or null.</summary>
    private static List<string>? SetupPath(IReadOnlyList<Node> nodes)
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

#if DEBUG
    /// <summary>
    /// Debug renders only: open the submenus along <paramref name="path"/> (the lines' words, "/"
    /// between levels), as clicks would. False when a line on the way is not there.
    /// </summary>
    internal bool DebugOpen(string path)
    {
        foreach (var words in path.Split('/'))
        {
            if (_levels.Count == 0
                || _levels[^1].Views.FirstOrDefault(v => v is not null && Opens(v.Node) && v.Node.Text == words) is not { } line
                || !OpenSubmenu(line, focus: false))
            {
                return false;
            }
        }

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

    /// <summary>
    /// [S-3721] Ask for a line of text beside the menu: a heading, the box (holding what is there now,
    /// for a name being changed), the reason it cannot be used yet under it, and the button that gives
    /// it (Enter does too; Escape or a click away drops it).
    /// </summary>
    private void Ask(WidgetTextAsk ask)
    {
        var heading = Fluent.Text(FluentType.BodyStrong);
        heading.Text = ask.Title;

        var box = new TextBox
        {
            Text = ask.Initial ?? string.Empty,
            PlaceholderText = ask.Placeholder,
            MinWidth = 280,
            IsSpellCheckEnabled = false,
            IsTextPredictionEnabled = false,
        };
        AutomationProperties.SetName(box, ask.Title);

        var reason = Fluent.Text(FluentType.Caption, "SystemFillColorCriticalBrush");
        reason.TextWrapping = TextWrapping.Wrap;
        reason.MaxWidth = 280;
        reason.Visibility = Visibility.Collapsed;

        var accept = new Button { Content = ask.Accept, HorizontalAlignment = HorizontalAlignment.Right };
        FluentLayout.ApplyStyle(accept, "AccentButtonStyle");

        var flyout = new Flyout
        {
            Content = new StackPanel { Spacing = Fluent.Space2, Children = { heading, box, reason, accept } },
            Placement = FlyoutPlacementMode.RightEdgeAlignedTop,
            ShouldConstrainToRootBounds = true,
        };

        string? Check()
        {
            var typed = box.Text.Trim();
            return typed.Length == 0 ? (ask.AllowEmpty ? null : string.Empty) : ask.Refuse(typed);
        }

        void Judge()
        {
            var refused = Check();
            accept.IsEnabled = refused is null;
            reason.Text = refused ?? string.Empty;
            reason.Visibility = string.IsNullOrEmpty(refused) ? Visibility.Collapsed : Visibility.Visible;
        }

        void Give()
        {
            if (Check() is not null)
            {
                return;
            }

            var typed = box.Text.Trim();
            flyout.Hide();
            Run(() => ask.Done(typed));
        }

        Judge();
        box.TextChanged += (_, _) => Judge();
        box.KeyDown += (_, e) =>
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                Give();
                e.Handled = true;
            }
        };
        accept.Click += (_, _) => Give();
        flyout.Opened += (_, _) =>
        {
            box.Focus(FocusState.Programmatic);
            box.SelectAll();
        };
        flyout.Closed += (_, _) => _commands.ReleaseFocus();
        flyout.ShowAt(_levels.Count > 1 ? _levels[^1].Root : _frame);
    }

    /// <summary>
    /// [S-3727] Ask beside the menu before a line destroys something: the app's one "are you sure?"
    /// (<see cref="ConfirmFlyout"/>: the question, the action and Cancel). Only the action runs it;
    /// Cancel, Escape or a click away leave everything as it was.
    /// </summary>
    private void Confirm(WidgetConfirm confirm)
    {
        var flyout = ConfirmFlyout.Create(confirm.Question, confirm.Accept, () => Run(confirm.Done), FlyoutPlacementMode.RightEdgeAlignedTop, confirm.Detail);
        flyout.Closed += (_, _) => _commands.ReleaseFocus();
        flyout.ShowAt(_levels.Count > 1 ? _levels[^1].Root : _frame);
    }

    private static (ContentAlign Value, string Glyph, Str Name)[] AlignOptions() =>
    [
        (ContentAlign.Left, Glyphs.AlignLeft, Str.Widget_Align_Left),
        (ContentAlign.Center, Glyphs.AlignCenter, Str.Widget_Align_Center),
        (ContentAlign.Right, Glyphs.AlignRight, Str.Widget_Align_Right),
    ];

    /// <summary>What a mixed selection's choice reads: a dash, as Explorer's properties show differing values.</summary>
    private const string Mixed = "—";

    private static (HeaderPlacement Value, (string Glyph, bool Turned)? Icon, string Text)[] HeaderOptions() =>
    [
        (HeaderPlacement.Top, (Glyphs.HeaderBottom, true), Str.Group_HeaderSide_Top.Text()),
        (HeaderPlacement.Bottom, (Glyphs.HeaderBottom, false), Str.Group_HeaderSide_Bottom.Text()),
        (HeaderPlacement.Left, (Glyphs.HeaderLeft, false), Str.Group_HeaderSide_Left.Text()),
        (HeaderPlacement.Right, (Glyphs.HeaderRight, false), Str.Group_HeaderSide_Right.Text()),
        (HeaderPlacement.Hidden, (Glyphs.Hide, false), Str.Toggle_Hidden.Text()),
    ];

    /// <summary>What the text "Default" paints its swatch with: the app-wide item text colour, or nothing (the theme's).</summary>
    private static Windows.UI.Color? AppItemTextColour()
        => DesignApplier.Current is { } design && HexColor.TryParse(design.ItemTextColor, out var r, out var g, out var b)
            ? Windows.UI.Color.FromArgb(255, r, g, b)
            : null;

    /// <summary>What the background's "Default" paints its swatch with: the app-wide colour, or nothing (the Windows card).</summary>
    private static Windows.UI.Color? AppBackgroundColour() => AppColour(DesignApplier.Current?.Background);

    /// <summary>What the item background's "Default" paints its swatch with: the app-wide colour, or nothing (the Fluent fills).</summary>
    private static Windows.UI.Color? AppItemColour() => AppColour(DesignApplier.Current?.ItemBackground);

    private static Windows.UI.Color? AppColour(FillChoice? fill)
        => fill is { Kind: FillKind.Colour } && HexColor.TryParse(fill.Colour, out var r, out var g, out var b)
            ? Windows.UI.Color.FromArgb(FillChoice.Alpha(fill.Opacity), r, g, b)
            : null;

    private static string ScaleText(double scale) => LocaleFormat.Percent(Strings.Current, WidgetScale.Percent(scale));

    /// <summary>
    /// [S-3704] The title, edited in place under the icon row: the kind's name as the placeholder,
    /// Enter (or leaving the box) commits, Escape puts back what was there. While it has the focus
    /// the edit keys do not fire ([S-804]); Enter and Escape hand the keyboard back to the surface.
    /// </summary>
    private TextBox TitleBox(PropertiesCommands commands, string widgetId, string defaultTitle, string? label)
    {
        _titleId = widgetId;
        _titleCommitted = label ?? string.Empty;

        var box = new TextBox
        {
            Text = _titleCommitted,
            PlaceholderText = defaultTitle,
            MinHeight = LineHeight,
            Height = LineHeight,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(Fluent.Space2, Fluent.Space2, Fluent.Space2, 0),
        };
        AutomationProperties.SetName(box, Str.Group_Title_Label.Text());

        box.GotFocus += (_, _) => CloseFrom(1);
        box.KeyDown += (_, e) =>
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                CommitTitle(box, widgetId, commands);
                commands.ReleaseFocus();
                e.Handled = true;
            }
            else if (e.Key == Windows.System.VirtualKey.Escape)
            {
                box.Text = _titleCommitted;
                commands.ReleaseFocus();
                e.Handled = true;
            }
        };

        box.LostFocus += (_, _) => CommitTitle(box, widgetId, commands);

        _title = box;
        return box;
    }

    private void CommitTitle(TextBox box, string widgetId, PropertiesCommands commands)
    {
        // A box already replaced handed its text over in CommitTitleLater.
        if (!ReferenceEquals(box, _title) || string.Equals(box.Text, _titleCommitted, StringComparison.Ordinal))
        {
            return;
        }

        _titleCommitted = box.Text;
        commands.SetTitle(widgetId, box.Text);
    }

    /// <summary>
    /// The menu is rebuilt (another widget picked) while the title box holds typing: keep it,
    /// after this pass — committing now would run a mutation inside the selection change.
    /// </summary>
    private void CommitTitleLater()
    {
        if (_title is not { } box || _titleId is not { } id
            || string.Equals(box.Text, _titleCommitted, StringComparison.Ordinal))
        {
            return;
        }

        var text = box.Text;
        var commands = _commands;
        _ = DispatcherQueue.TryEnqueue(() => commands.SetTitle(id, text));
    }
}
