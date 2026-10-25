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
using Themia.Core.Diagnostics;
using Themia.Core.Geometry;
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
/// <param name="Preview">[S-3731] Shows the selected widgets at a slider's value while it is dragged (a scale, a fill's opacity), without storing it; null ends it. A render's stand-in may leave it out.</param>
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
    Func<string, Action<WidgetTextAsk>, Action<WidgetConfirm>, WidgetMenuLines?>? WidgetLines = null,
    Action<CardPreview?>? Preview = null);

/// <summary>[S-3731] What the selected cards show while a slider of the menu is dragged: a scale or a fill not yet stored.</summary>
internal sealed record CardPreview(double? Scale = null, FillChoice? Background = null, FillChoice? Items = null);


/// <summary>
/// [S-3716] The selected widgets' properties, as a menu that stands beside the selection: the menu
/// engine (<see cref="MenuPanel"/>) with the selection's frame. The icon row is Cut and Copy, Delete
/// at its far end ([S-3717]); then the title (one widget) or "N selected", Style, the widget's own
/// lines ([S-3719]), and the card's own (Header, Alignment, Background, Item background, Item text,
/// Scale) as one Appearance submenu: where the widget places it, else right after Style ([S-3723]).
/// It is where a widget's options are: there is no settings page for a widget ([S-6536]). After
/// Karpo's properties panel (docs/widgets.md §145).
/// <para>
/// The menu is built again from the offer and the widget's lines on every change; the engine writes
/// a change of value into the lines in place, so an open submenu, its search box and the focus stay
/// where they are.
/// </para>
/// </summary>
internal sealed partial class PropertiesStrip : MenuPanel
{
    private const string Scope = "properties";

    /// <summary>The icon row's buttons: the size of Explorer's.</summary>
    private const double RowButton = 40;

    private PropertiesCommands _commands;

    private QuickSetOffer _offer = QuickSetOffer.None;

    /// <summary>[S-3719] The one selected widget's own lines as last built.</summary>
    private WidgetMenuLines? _widget;

    /// <summary>The title box while one widget is selected, the widget it names and what it last committed.</summary>
    private TextBox? _title;

    private string? _titleId;

    private string _titleCommitted = string.Empty;

    public PropertiesStrip(PropertiesCommands commands)
    {
        _commands = commands;
    }

    /// <summary>What the panel offers now; <see cref="QuickSetOffer.Shows"/> false when it has nothing to show.</summary>
    public QuickSetOffer Offer => _offer;

    /// <summary>Rebuild the choices in the current language. Called again on a language switch.</summary>
    public void Fill(PropertiesCommands commands)
    {
        ArgumentNullException.ThrowIfNull(commands);
        _commands = commands;
        Present(FrameKey(), _offer.Shows ? Nodes() : null, Head, rebuild: true);
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

        Present(FrameKey(), _offer.Shows ? Nodes() : null, Head);

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

    /// <summary>[S-3717] [S-3702] Over the lines: the icon row, a hairline, and for several the words "N selected".</summary>
    private IEnumerable<UIElement> Head()
    {
        yield return QuickRow(_commands);
        yield return Divider(full: true);

        if (!_offer.HasTitle)
        {
            // [S-3702] Several: how many the choices reach, over the lines.
            var count = Fluent.Text(FluentType.BodyStrong);
            count.Text = Str.Shell_EditBar_Selected.Plural(_offer.Count);
            count.Margin = new Thickness(Fluent.Space4, Fluent.Space2, Fluent.Space4, 0);
            yield return count;
        }
    }

    /// <inheritdoc />
    protected override void OnRebuilding()
    {
        CommitTitleLater();
        _title = null;
        _titleId = null;
    }

    /// <summary>[S-3704] The title box goes with the Header submenu: what it held still lands.</summary>
    protected override void OnLevelClosing(Panel lines)
    {
        if (_title is { } title && lines.Children.Contains(title))
        {
            CommitTitleLater();
            _title = null;
        }
    }

    /// <summary>[S-3707] Escape with no submenu open hands the keyboard back to the surface.</summary>
    protected override void OnEscape() => _commands.ReleaseFocus();

    /// <summary>[S-3721] Closing what was asked beside the menu hands the keyboard back to the surface.</summary>
    protected override void OnAskClosed() => _commands.ReleaseFocus();


    /// <summary>
    /// [S-3702] [S-3735] The menu's first level, from the offer, the widget's lines and the commands,
    /// in sections with a divider between them: the Header line; Style and the options only that look
    /// draws with ([S-3719]); the widget's own lines in the sections it marks, with the card's
    /// Appearance as a section of its own where the widget places it, else right after Style ([S-3723]).
    /// </summary>
    private List<MenuNode> Nodes()
    {
        var nodes = new List<MenuNode>();
        if (!_offer.Shows)
        {
            return nodes;
        }

        var commands = _commands;

        if (HeaderNode(commands) is { } header)
        {
            nodes.Add(header);
            nodes.Add(MenuNode.Separator("-header"));
        }

        // [S-6007] The look first: it changes the widget most. Only for one kind with styles; the
        // options only that look draws with stand right under it, on this level ([S-3719]).
        if (_offer.StyleKind is { } styled && WidgetStyles.For(styled) is { Count: > 0 } styles)
        {
            var now = _offer.Style;
            var current = styles.FirstOrDefault(s => s.Value == now);
            var shown = now is null || current.Glyph == 0 ? styles[0] : current;
            var options = new List<MenuNode>();
            foreach (var style in styles)
            {
                var value = style.Value;
                var glyph = char.ConvertFromUtf32(style.Glyph);
                options.Add(Option("style/" + value, style.Label.Text(), glyph, () => EditBar.Icon(glyph, false, Fluent.IconSize), now == value, () => commands.SetStyle(value)));
            }

            var icon = char.ConvertFromUtf32(shown.Glyph);
            nodes.Add(new MenuNode("style")
            {
                Text = Str.Styles_Label.Text(),
                Value = now is null ? Mixed : shown.Label.Text(),
                IconKey = icon,
                Icon = () => Glyph(icon),
                Children = options,
            });

            if (_widget is { StyleLines.Count: > 0 } widget)
            {
                nodes.AddRange(FromEntries(widget.StyleLines, "style/w"));
            }

            nodes.Add(MenuNode.Separator("-style"));
        }
        else if (_widget is { StyleLines.Count: > 0 } unstyled)
        {
            // [S-6075] No Style line while every look draws the same, but what the look draws with
            // and still applies (a single picture's size) keeps its place and its section.
            nodes.AddRange(FromEntries(unstyled.StyleLines, "style/w"));
            nodes.Add(MenuNode.Separator("-style"));
        }

        // [S-3719] The widget's own lines, in its order; [S-3723] the card's as one Appearance
        // submenu among them where the widget places it, else first, right after Style: for every
        // widget and for several. [S-3735] A section of its own either way.
        var lines = _widget is { } own ? FromEntries(own.Lines, "w") : [];
        lines.InsertRange(Math.Clamp(_widget?.CardAt ?? 0, 0, lines.Count),
        [
            MenuNode.Separator("-appearance"),
            new MenuNode("appearance")
            {
                Text = Str.Settings_Section_Appearance.Text(),
                IconKey = "appearance",
                Icon = () => Glyph(Glyphs.Palette),
                Children = CardNodes(commands),
            },
            MenuNode.Separator("-appearance/"),
        ]);
        nodes.AddRange(lines);
        return nodes;
    }

    /// <summary>
    /// [S-3735] The Header line, first under the icon row: its value the title as it reads (one
    /// widget) or where the header stands (several), its submenu the title box ([S-3704]: one widget
    /// only, a title names one widget) and the header's positions with the dot. None for a selection
    /// holding a kind that never has a header ([S-3711]): it would have no positions, and such a
    /// widget's title is shown nowhere.
    /// </summary>
    private MenuNode? HeaderNode(PropertiesCommands commands)
    {
        if (!_offer.HasHeader)
        {
            return null;
        }

        var options = HeaderOptions();
        var header = Array.Find(options, o => o.Value == _offer.Header);
        var (glyph, turned) = header.Icon ?? (Glyphs.HeaderBottom, true);
        var children = new List<MenuNode>();
        string? value;

        if (_offer is { TitleWidgetId: { } id, TitleKind: { } kind })
        {
            var name = ModuleCatalog.Of(kind).Name;
            var label = _offer.Label;
            value = string.IsNullOrWhiteSpace(label) ? name : label;
            children.Add(new MenuNode("header/title", MenuNodeKind.Custom)
            {
                ValueKey = id,
                Build = () => TitleBox(commands, id, name, _offer.Label),
            });
            children.Add(MenuNode.Separator("header/-"));
        }
        else
        {
            value = _offer.Header is null ? Mixed : header.Text;
        }

        children.AddRange(options.Select(o => Option("header/" + o.Value, o.Text, o.Icon!.Value.Glyph + o.Icon.Value.Turned,
            () => EditBar.Icon(o.Icon!.Value.Glyph, o.Icon.Value.Turned, Fluent.IconSize), _offer.Header == o.Value, () => commands.SetHeader(o.Value))));

        return new MenuNode("header")
        {
            Text = Str.App_ConfigHeaderLabel.Text(),
            Value = value,
            IconKey = glyph + turned,
            Icon = () => EditBar.Icon(glyph, turned, Fluent.IconSize),
            Children = children,
        };
    }

    /// <summary>
    /// [S-3723] The card's own lines, submenus throughout but Scale: Alignment; then the colours
    /// (Background, Item background, Item text) as a section of their own ([S-3735]); then Scale, an
    /// inline slider ([S-3731]). A line that cannot apply is not there ([S-3718]): Alignment where
    /// nothing selected aligns ([S-6584]), the item lines where nothing selected has items ([S-6571]).
    /// The colours are the menu's one colour vocabulary ([S-3724]), the same lines a widget's own
    /// colour gets; a drag of a fill's Opacity or of Scale shows on the widgets as it goes.
    /// </summary>
    private List<MenuNode> CardNodes(PropertiesCommands commands)
    {
        var nodes = new List<MenuNode>();

        // [S-6584] Where the content sits across the card, for the widgets that align in their style ([S-6581]).
        if (_offer.CanAlign)
        {
            var align = AlignOptions();
            var now = _offer.Align;
            var at = Array.FindIndex(align, o => o.Value == now);
            var glyph = at >= 0 ? align[at].Glyph : Glyphs.AlignLeft;
            nodes.Add(new MenuNode("align")
            {
                Text = Str.Widget_Align_Label.Text(),
                Value = at >= 0 ? align[at].Name.Text() : Mixed,
                IconKey = glyph,
                Icon = () => Glyph(glyph),
                Children = [.. align.Select(o => Option("align/" + o.Value, o.Name.Text(), o.Glyph, () => Glyph(o.Glyph), now == o.Value, () => commands.SetAlign?.Invoke(o.Value)))],
            });
            nodes.Add(MenuNode.Separator("-align"));
        }

        // [S-6566] The widgets' own background and item background: one fill each, Default for the
        // app-wide one; [S-6544] the colour of the text on their items, Default for the app-wide one.
        // A mixed selection's value is null: the line reads as a dash and nothing holds.
        void Preview(CardPreview? preview) => commands.Preview?.Invoke(preview);
        var colours = new List<ItemMenuEntry>
        {
            new(null, Str.Settings_Style_BgLabel.Text(), null, Colour: MenuColour.OfFill(_offer.Background, commands.SetBackground,
                new FillChoiceOptions { DefaultLabel = Str.Common_Default.Text(), DefaultColor = AppBackgroundColour },
                fill => Preview(fill is null ? null : new CardPreview(Background: fill)))),
        };

        if (_offer.HasItems)
        {
            colours.Add(new(null, Str.Shell_Items_Background.Text(), null, Colour: MenuColour.OfFill(_offer.Items, commands.SetItemBackground,
                new FillChoiceOptions { DefaultLabel = Str.Common_Default.Text(), DefaultColor = AppItemColour },
                fill => Preview(fill is null ? null : new CardPreview(Items: fill)))));
        }

        // [S-6574] The text on items: not where every item drawn is a track, which it never reaches.
        if (_offer is { HasItems: true, HasItemText: true })
        {
            colours.Add(new(null, Str.Shell_Items_TextShort.Text(), null, Colour: MenuColour.Of(_offer.ItemText, commands.SetItemTextColor,
                new ColorChoiceOptions { DefaultLabel = Str.Common_Default.Text(), Presets = ColorPresets.Surfaces, DefaultColor = AppItemTextColour })));
        }

        nodes.AddRange(FromEntries(colours, "card"));
        nodes.Add(MenuNode.Separator("-colours"));

        // [S-6531] The widgets' own content scale, a slider on the stored grain, after the look of the card it magnifies.
        var scale = _offer.Scale;
        nodes.Add(new MenuNode("scale", MenuNodeKind.Slider)
        {
            Text = Str.Widget_Scale_Label.Text(),
            Value = scale is { } s ? ScaleText(s) : Mixed,
            IconKey = "scale",
            Icon = () => Glyph(Glyphs.Scale),
            Slider = new MenuSlider(scale, WidgetScale.Min, WidgetScale.Max, WidgetScale.Step, ScaleText, commands.SetScale,
                v => Preview(v is { } at ? new CardPreview(Scale: at) : null)),
        });

        return nodes;
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
                CloseSubmenus();
                Run(run);
            };

            Grid.SetColumn(button, column);
            row.Children.Add(button);
        }
    }

    private static (ContentAlign Value, string Glyph, Str Name)[] AlignOptions() =>
    [
        (ContentAlign.Left, Glyphs.AlignLeft, Str.Widget_Align_Left),
        (ContentAlign.Center, Glyphs.AlignCenter, Str.Widget_Align_Center),
        (ContentAlign.Right, Glyphs.AlignRight, Str.Widget_Align_Right),
    ];


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
    /// [S-3704] [S-3735] The title, edited in place at the top of the Header submenu, one line of the
    /// menu (32 DIPs, as every line): the kind's name as the placeholder, Enter (or leaving the box)
    /// commits, Escape puts back what was there. While it has the focus the edit keys do not fire
    /// ([S-804]); Enter and Escape hand the keyboard back to the surface.
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
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(Fluent.Space1, 0, Fluent.Space1, 0),
            IsSpellCheckEnabled = false,
            IsTextPredictionEnabled = false,
        };
        AutomationProperties.SetName(box, Str.Group_Title_Label.Text());

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
        _titleCommitted = text;
        _ = DispatcherQueue.TryEnqueue(() => commands.SetTitle(id, text));
    }
}