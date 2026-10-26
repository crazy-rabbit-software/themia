using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Shapes;
using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Settings;
using Themia.Core.Shell;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Themia.Sdk;

namespace Themia.App.Shell;

/// <summary>What the properties panel's choices do, given by the surface (or a render's stand-in).</summary>
/// <param name="SetHeader">[S-3703] The selected widgets' header placement.</param>
/// <param name="SetBackground">[S-3703] [S-6566] The selected widgets' background.</param>
/// <param name="SetTitle">[S-3704] One widget's title, by id, as typed.</param>
/// <param name="ReleaseFocus">[S-3707] Hands the keyboard back to the surface (Escape on the menu's own rows), so the edit keys work again.</param>
/// <param name="SetStyle">[S-6008] The selected widgets' style, by the style enum's value.</param>
/// <param name="SetItemBackground">[S-6521] The selected widgets' own item background (those with items, [S-6571]).</param>
/// <param name="SetItemTextColor">[S-6544] The selected widgets' own item text colour, or empty for the app-wide one.</param>
/// <param name="SetScale">[S-6531] The selected widgets' own content scale, as a factor.</param>
/// <param name="SetAlign">[S-6584] Where the selected widgets that can align draw their content. A render's stand-in may leave it out.</param>
/// <param name="Cut">[S-3717] Cut the selection, as Ctrl+X does.</param>
/// <param name="Copy">[S-3717] Copy the selection, as Ctrl+C does.</param>
/// <param name="Delete">[S-3717] Delete the selection, as the Delete key does.</param>
/// <param name="WidgetLines">[S-3719] One widget's own lines, by id, built over its live config (asking for text and confirmations on levels of the menu's own); null for none.</param>
/// <param name="Preview">[S-3731] Shows the selected widgets at a slider's value while it is dragged (a scale, a fill's opacity), without storing it; null ends it. A render's stand-in may leave it out.</param>
/// <param name="SetBackgroundImage">[S-6593] The selected widgets' background image, a picture file's full path or empty to remove it. A render's stand-in may leave it out.</param>
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
    Action<CardPreview?>? Preview = null,
    Action<string>? SetBackgroundImage = null);

/// <summary>[S-3731] What the selected cards show while a slider of the menu is dragged: a scale or a fill not yet stored.</summary>
internal sealed record CardPreview(double? Scale = null, FillChoice? Background = null, FillChoice? Items = null);

/// <summary>
/// [S-3716] The selected widgets' properties, as a menu that stands beside the selection: the menu
/// engine (<see cref="MenuPanel"/>) with the selection's head and foot. Its top row is the menu's
/// type (the widget kind's name, "3 widgets" for several) and Cut, Copy and Delete as icon buttons at
/// its far end ([S-3717]); then Style ([S-6007]) with the options only that look draws, the Header
/// row ([S-3735]) and the widget's own rows ([S-3719]); and pinned at the foot, under one rule, the
/// card's own look as one Appearance row ([S-3739]). It is where a widget's options are: there is no
/// settings page for a widget ([S-6536]). After Karpo's widget menus (docs/widgets.md §311, §323, §350).
/// <para>
/// The menu is built again from the offer and the widget's lines on every change; the engine writes
/// a change of value into the rows in place, so the open level, a field being typed in and the focus
/// stay where they are.
/// </para>
/// </summary>
internal sealed partial class PropertiesStrip : MenuPanel
{
    private const string Scope = "properties";

    /// <summary>[S-3717] The top row and its buttons: 40-DIP squares, the plate under the pointer 4 DIPs in from each side (Karpo's, Explorer's).</summary>
    private const double TopRow = 40;

    private const double ButtonPlate = 4;

    /// <summary>[S-3717] The hairline between two of the top row's buttons.</summary>
    private const double ButtonRule = 20;

    /// <summary>[S-3717] The least room between the type's words and the first button.</summary>
    private const double TypeGap = Fluent.Space3;

    private PropertiesCommands _commands;

    private QuickSetOffer _offer = QuickSetOffer.None;

    /// <summary>[S-3719] The one selected widget's own lines as last built.</summary>
    private WidgetMenuLines? _widget;

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
        Present(FrameKey(), _offer.Shows ? Nodes() : null, Head, Foot(), rebuild: true);
    }

    /// <summary>
    /// [S-3701]..[S-3702] Show the choices for what is selected on this display. A change of value
    /// is written into the rows in place, so the open level stays open on the pick ([S-3718]).
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

        Present(FrameKey(), _offer.Shows ? Nodes() : null, Head, Foot());
    }

    /// <summary>
    /// [S-3593] Edit mode ended: the rows, the head and the foot go, and the widget's lines with them;
    /// the next selection builds them again.
    /// </summary>
    public void Release()
    {
        _widget = null;
        _offer = QuickSetOffer.None;
        Present(string.Empty, null, null);
    }

    /// <summary>What the head shows: the selection's count or the one widget, whether there is a menu at all.</summary>
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

    /// <summary>[S-3721] [S-3736] A widget's line of text, asked on a level of the menu's own.</summary>
    private void Ask(WidgetTextAsk ask)
        => AskText(new MenuEdit(ask.Title, ask.Placeholder, ask.Initial ?? string.Empty, ask.Done) { Refuse = ask.Refuse, AllowEmpty = ask.AllowEmpty, Accept = ask.Accept });

    /// <summary>[S-3727] [S-3737] A widget's "are you sure?", asked on a level of the menu's own.</summary>
    private void Confirm(WidgetConfirm confirm)
        => AskQuestion(new MenuQuestion(confirm.Question, confirm.Accept, confirm.Done, confirm.Danger, confirm.Detail));

    /// <summary>[S-3717] Over the rows of the menu's own level: the top row and the rule the menu's full width under it.</summary>
    private IEnumerable<UIElement> Head()
    {
        yield return TopRowOf(_commands);
        yield return Divider(full: true);
    }

    /// <summary>[S-3707] Escape on the menu's own rows hands the keyboard back to the surface.</summary>
    protected override void OnEscape() => _commands.ReleaseFocus();

    /// <summary>
    /// [S-3702] [S-3735] The menu's own rows, from the offer, the widget's lines and the commands, in
    /// sections with a rule between them: Style and the options only that look draws with ([S-3719]);
    /// the Header row; the widget's own rows in the sections it marks. The card's look is the foot.
    /// </summary>
    private List<MenuNode> Nodes()
    {
        var nodes = new List<MenuNode>();
        if (!_offer.Shows)
        {
            return nodes;
        }

        var commands = _commands;

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
            // [S-6075] No Style row while every look draws the same, but what the look draws with
            // and still applies (a single picture's size) keeps its place and its section.
            nodes.AddRange(FromEntries(unstyled.StyleLines, "style/w"));
            nodes.Add(MenuNode.Separator("-style"));
        }

        if (HeaderNode(commands) is { } header)
        {
            nodes.Add(header);
            nodes.Add(MenuNode.Separator("-header"));
        }

        // [S-3719] The widget's own rows, in its order.
        if (_widget is { } own)
        {
            nodes.AddRange(FromEntries(own.Lines, "w"));
        }

        return nodes;
    }

    /// <summary>
    /// [S-3739] The foot pinned under the rows: the card's own look as one Appearance row, its value
    /// the card's own background swatch where it has one (Karpo's). For one widget and for several.
    /// </summary>
    private List<MenuNode>? Foot()
    {
        if (!_offer.Shows)
        {
            return null;
        }

        var background = _offer.Background;
        var own = background is { Kind: not FillKind.Default };
        return
        [
            new MenuNode("appearance")
            {
                Text = Str.Settings_Section_Appearance.Text(),
                IconKey = "appearance",
                Icon = () => Glyph(Glyphs.Palette),
                ValueIconKey = own ? background!.Kind + "|" + background.Colour + "|" + background.Opacity : string.Empty,
                ValueIcon = own ? () => Swatch(b => ColorChoice.PaintFill(b, background!, new ColorChoiceOptions { DefaultLabel = Str.Common_Default.Text() })) : null,
                Children = CardNodes(_commands),
            },
        ];
    }

    /// <summary>
    /// [S-3735] The Header row, under Style: its value the title as it reads (one widget) or where the
    /// header stands (several); its level the Title row ([S-3704]: one widget only, a title names one
    /// widget) and the header's positions, the tick on the one that holds. None for a selection
    /// holding a kind that never has a header ([S-3711]).
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
            var titleWords = Str.Group_Title_Label.Text();

            // [S-3704] [S-3736] The title, a line of text on a level of its own: the kind's name as its hint.
            children.Add(new MenuNode("header/title")
            {
                Text = titleWords,
                Value = value,
                IconKey = "title",
                Icon = () => Glyph(Glyphs.Rename),
                Asks = true,
                Run = () => AskText(new MenuEdit(titleWords, name, label ?? string.Empty, typed => commands.SetTitle(id, typed))),
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
    /// [S-3723] The card's own rows, the Appearance level: Alignment; then, behind a rule, the colours
    /// (Background, Item background, Item text) and the background image ([S-3735]); then Scale, a
    /// one-row slider ([S-3731]). A row that cannot apply is not there ([S-3718]): Alignment where
    /// nothing selected aligns ([S-6584]), the item rows where nothing selected has items ([S-6571]).
    /// The colours are the menu's one colour vocabulary ([S-3724]), the same rows a widget's own colour
    /// gets; a drag of a fill's Opacity or of Scale shows on the widgets as it goes.
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
        // A mixed selection's value is null: the row reads as a dash and nothing holds.
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

        // [S-6593] The widgets' own background image, under the content; their background tints it
        // ([S-6592]). Windows' picker chooses one, and Remove image goes back to the fill alone.
        var image = _offer.BackgroundImage;
        nodes.Add(new MenuNode("image")
        {
            Text = Str.Shell_Fill_Image.Text(),
            Value = image is null ? Mixed : BackgroundImage.Name(image),
            IconKey = "image",
            Icon = () => Glyph(Glyphs.Photo),
            Children =
            [
                new MenuNode("image/choose")
                {
                    Text = Str.Shell_Fill_ImageChoose.Text(),
                    Run = () => _ = FolderBrowse.PickFileAsync(BackgroundImage.Extensions, path => commands.SetBackgroundImage?.Invoke(path)),
                },
                new MenuNode("image/remove")
                {
                    Text = Str.Shell_Fill_ImageRemove.Text(),
                    Disabled = image is { Length: 0 },
                    Run = () => commands.SetBackgroundImage?.Invoke(string.Empty),
                },
            ],
        });
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
    /// [S-3717] The top row (Karpo's, §323 and §350): the menu's type at its start in the 14-DIP
    /// semibold, 16 DIPs in and trimmed short of the buttons (the widget kind's name; "3 widgets" for
    /// several), and Cut, Copy and Delete packed against its far end as 40-DIP icon buttons, a 20-DIP
    /// hairline between each two, their words and keys the tooltip ("Cut (Ctrl+X)"). No Paste: a paste
    /// lands on the desktop where it is clicked, never on the selection.
    /// </summary>
    private Grid TopRowOf(PropertiesCommands commands)
    {
        var type = new TextBlock
        {
            Text = _offer is { HasTitle: true, TitleKind: { } kind } ? ModuleCatalog.Of(kind).Name : Str.Shell_Views_WidgetCount.Plural(_offer.Count),
            FontSize = FluentType.Body.FontSize,
            FontWeight = FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.NoWrap,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(Fluent.Space4 - Fluent.Hairline, 0, TypeGap, 0),
        };
        AutomationProperties.SetHeadingLevel(type, Microsoft.UI.Xaml.Automation.Peers.AutomationHeadingLevel.Level1);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var row = new Grid
        {
            Height = TopRow,
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto },
            },
            Children = { type, buttons },
        };
        Grid.SetColumn(buttons, 1);

        string Verb(Str one, Str many) => _offer.Count > 1 ? many.Plural(_offer.Count) : one.Text();

        Add(Glyphs.Cut, Verb(Str.CtxMenu_Cut, Str.Shell_Card_CutMany), KeyChord.Ctrl(ChordKey.X), commands.Cut);
        Add(Glyphs.Copy, Verb(Str.CtxMenu_Copy, Str.Shell_Card_CopyMany), KeyChord.Ctrl(ChordKey.C), commands.Copy);
        Add(Glyphs.Delete, Verb(Str.Common_Delete, Str.Shell_Card_DeleteMany), KeyChord.Plain(ChordKey.Delete), commands.Delete);
        return row;

        void Add(string glyph, string words, KeyChord chord, Action? run)
        {
            if (buttons.Children.Count > 0)
            {
                buttons.Children.Add(new Rectangle
                {
                    Width = Fluent.Hairline,
                    Height = ButtonRule,
                    Fill = ThemeBrush.Of("DividerStrokeColorDefaultBrush", Colors.Gray),
                    VerticalAlignment = VerticalAlignment.Center,
                    IsHitTestVisible = false,
                });
            }

            var on = run is not null;
            var button = new Button
            {
                Content = on ? MenuArtIcons.IconOrGlyph(glyph) : UiLanguage.Mirrored(new FontIcon { Glyph = glyph, FontSize = Fluent.IconSize }),
                IsEnabled = on,
            };
            EditBar.Subtle(button);
            button.Width = TopRow - (2 * ButtonPlate);
            button.Height = TopRow - (2 * ButtonPlate);
            button.Margin = new Thickness(ButtonPlate);
            button.Padding = new Thickness(0);

            var keys = KeyChordText.Format(chord);
            ToolTipService.SetToolTip(button, Str.Shell_Menu_LabelWithShortcut.Format(("label", words), ("shortcut", keys)));
            AutomationProperties.SetName(button, words);
            AutomationProperties.SetAcceleratorKey(button, keys);
            button.Click += (_, _) => Run(run);
            buttons.Children.Add(button);
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
}
