using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Settings;
using Themia.Core.Widgets;

namespace Themia.App.Shell;

/// <summary>What the properties panel's choices do, given by the surface (or a render's stand-in).</summary>
/// <param name="SetHeader">[S-3703] The selected widgets' header placement.</param>
/// <param name="SetBackground">[S-3703] [S-6566] The selected widgets' background.</param>
/// <param name="SetTitle">[S-3704] One widget's title, by id, as typed.</param>
/// <param name="ReleaseFocus">[S-3704] Hands the keyboard back to the surface after the title box, so the edit keys work again.</param>
/// <param name="SetStyle">[S-6008] The selected widgets' style, by the style enum's value.</param>
/// <param name="OpenSettings">[S-6009] Opens one widget's settings page, by id.</param>
/// <param name="SetItemBackground">[S-6521] The selected widgets' own item background (those with items, [S-6571]).</param>
/// <param name="SetItemTextColor">[S-6544] The selected widgets' own item text colour, or empty for the app-wide one.</param>
/// <param name="SetScale">[S-6531] The selected widgets' own content scale, as a factor.</param>
/// <param name="HasSettings">[S-1750] Whether one widget, by id, has a settings page in its current style and state.</param>
internal sealed record PropertiesCommands(
    Action<HeaderPlacement> SetHeader,
    Action<FillChoice> SetBackground,
    Action<string, string?> SetTitle,
    Action ReleaseFocus,
    Action<int> SetStyle,
    Action<string> OpenSettings,
    Action<FillChoice> SetItemBackground,
    Action<string> SetItemTextColor,
    Action<double> SetScale,
    Func<string, bool> HasSettings);

/// <summary>
/// [S-3707] The selected widgets' properties panel: a small panel on a flyout's surface ([S-3715])
/// standing beside the selection, centred on it ([S-3708], [S-3714]), one row per property, its name before its control — Title (one
/// widget) or "N selected", Style (one kind with styles, [S-6007]), Header (where every selected
/// widget can have one, [S-3711]), Background and Item background (one fill control each, [S-6566];
/// the item row greyed for widgets without items, [S-6571]), Item text ([S-6544]), Scale ([S-6531]),
/// then More settings (one widget with a page, [S-6009]). After Karpo's
/// properties panel (docs/widgets.md §145): the selection's own properties, where the selection is.
/// </summary>
internal sealed partial class PropertiesStrip : Grid
{
    /// <summary>[S-3707] The controls' column: a long widget name or a choice's words without crowding the panel.</summary>
    private const double ControlWidth = 176;

    private readonly Border _frame = new() { Padding = new Thickness(Fluent.Space3, Fluent.Space2, Fluent.Space2, Fluent.Space2) };

    private readonly Grid _rows = new()
    {
        ColumnSpacing = Fluent.Space3,
        RowSpacing = Fluent.Space1,
        ColumnDefinitions =
        {
            new ColumnDefinition { Width = GridLength.Auto },
            new ColumnDefinition { Width = new GridLength(ControlWidth) },
        },
    };

    private PropertiesCommands _commands;

    private QuickSetOffer _offer = QuickSetOffer.None;

    /// <summary>[S-6009] Whether the one selected widget has a settings page now ([S-1750]).</summary>
    private bool _hasSettings;

    /// <summary>The title box while one widget is selected, the widget it names and what it last committed.</summary>
    private TextBox? _title;

    private string? _titleId;

    private string _titleCommitted = string.Empty;

    /// <summary>The choices' current-value contents, rewritten in place when the selection's values change.</summary>
    private StackPanel? _styleValue;

    private StackPanel? _headerValue;

    private StackPanel? _scaleValue;

    public PropertiesStrip(PropertiesCommands commands)
    {
        _commands = commands;
        _frame.Child = _rows;
        Children.Add(_frame);

        // [S-3715] A flyout's surface: it stands apart from the cards and the wallpaper beside it.
        FlyoutSurface.Apply(this, _frame);

        // [S-3707] The keyboard stays in the panel: Tab goes round its controls, Up and Down move
        // between its rows, and Escape hands the keyboard back to the surface.
        _frame.TabFocusNavigation = KeyboardNavigationMode.Cycle;
        _rows.XYFocusKeyboardNavigation = XYFocusKeyboardNavigationMode.Enabled;
        _frame.KeyDown += OnKeyDown;

        // A press on the panel's padding or words is the panel's: it must not reach the surface,
        // whose press on empty space starts a rubber band and clears the selection.
        _frame.PointerPressed += (_, e) => e.Handled = true;
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
        Build();
    }

    /// <summary>
    /// [S-3701]..[S-3702] Show the choices for what is selected on this display. The title box is
    /// left alone while it is typed in for the same widget (a change elsewhere must not wipe a
    /// half-typed title); the choices show their current values and read the offer as their menus open.
    /// </summary>
    public void ShowSelection(QuickSetOffer offer)
    {
        ArgumentNullException.ThrowIfNull(offer);

        var previous = _offer;
        var hadSettings = _hasSettings;
        _offer = offer;

        // [S-1750] A style picked here can give the widget a page or take it away (a meter's graph).
        _hasSettings = offer.TitleWidgetId is { } id && _commands.HasSettings(id);

        // [S-6566] The fill rows are rebuilt whenever a fill moved: each fill control keeps its own value.
        var sameShape = previous.Shows == offer.Shows
            && previous.HasHeader == offer.HasHeader
            && previous.StyleKind == offer.StyleKind
            && string.Equals(previous.TitleWidgetId, offer.TitleWidgetId, StringComparison.Ordinal)
            && (offer.HasTitle || previous.Count == offer.Count)
            && previous.HasItems == offer.HasItems
            && SameFill(previous.Background, offer.Background)
            && SameFill(previous.Items, offer.Items)
            && string.Equals(previous.ItemText, offer.ItemText, StringComparison.Ordinal)
            && hadSettings == _hasSettings;

        if (!sameShape)
        {
            Build();
            return;
        }

        ShowValues();

        // The same widget: its title may have changed (an undo, the box's own commit).
        if (_title is { } title && title.FocusState == FocusState.Unfocused)
        {
            _titleCommitted = offer.Label ?? string.Empty;
            title.Text = _titleCommitted;
        }
    }

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Escape && !e.Handled)
        {
            _commands.ReleaseFocus();
            e.Handled = true;
        }
    }

    private void Build()
    {
        CommitTitleLater();

        _rows.Children.Clear();
        _rows.RowDefinitions.Clear();
        _styleValue = _headerValue = _scaleValue = null;
        _title = null;
        _titleId = null;

        if (!_offer.Shows)
        {
            return;
        }

        var commands = _commands;
        if (_offer is { TitleWidgetId: { } id, TitleKind: { } kind })
        {
            AddRow(Str.Group_Title_Label.Text(), TitleBox(commands, id, WidgetCatalog.Definition(kind).Label, _offer.Label));
        }
        else
        {
            // [S-3702] Several: no title, but how many the choices reach, over the rows.
            var count = Fluent.Text(FluentType.BodyStrong);
            count.Text = Str.Shell_EditBar_Selected.Plural(_offer.Count);
            count.Margin = new Thickness(0, Fluent.Space1, 0, Fluent.Space1);
            AddRow(null, count);
        }

        // [S-6007] The look first: it changes the widget most. Only for one kind with styles.
        if (_offer.StyleKind is { } styled)
        {
            AddRow(Str.Styles_Label.Text(), StyleChoice(styled, commands));
        }

        // [S-3711] Only where every selected widget can have a header: never for a toggle.
        if (_offer.HasHeader)
        {
            AddRow(Str.App_ConfigHeaderLabel.Text(), Choice(
                Str.App_ConfigHeaderLabel.Text(),
                HeaderOptions(),
                () => _offer.Header,
                commands.SetHeader,
                out var headerValue));
            _headerValue = headerValue;
        }

        // [S-6566] The widgets' own background and item background: one fill control each, Default
        // for the app-wide one. [S-6571] The item row is greyed for widgets that have no items.
        AddRow(Str.Settings_Style_BgLabel.Text(), FillRow(Str.Settings_Style_BgLabel.Text(), _offer.Background, commands.SetBackground, AppBackgroundColour));
        var items = FillRow(Str.Shell_Items_Background.Text(), _offer.Items, commands.SetItemBackground, AppItemColour);
        NoItems(AddRow(Str.Shell_Items_Background.Text(), items), items);

        // [S-6544] The colour of the text on their items, "Default" for the app-wide one.
        var itemText = ItemTextChoice(commands);
        NoItems(AddRow(Str.Shell_Items_TextShort.Text(), itemText), itemText);

        // [S-6531] The widgets' own content scale, after the look of the card it magnifies.
        AddRow(Str.Widget_Scale_Label.Text(), ScaleChoice(commands));

        // [S-6009] More settings: the widget's own options on its page (the panel holds the rest,
        // [S-6536]), one widget only and only when its page has rows ([S-1750]).
        if (_offer is { TitleWidgetId: { } settingsId } && _hasSettings)
        {
            var gear = new Button
            {
                Content = EditBar.Labelled(Glyphs.Settings, Str.Shell_MoreSettings.Text(), accent: false),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
            };
            EditBar.Subtle(gear);
            gear.Padding = new Thickness(Fluent.Space2, 0, Fluent.Space2, 0);
            AutomationProperties.SetName(gear, Str.Shell_MoreSettings.Text());
            ToolTipService.SetToolTip(gear, Str.Shell_MoreSettings_Tip.Text());
            AutomationProperties.SetHelpText(gear, Str.Shell_MoreSettings_Tip.Text());
            gear.Click += (_, _) => commands.OpenSettings(settingsId);
            AddRow(null, gear);
        }

        ShowValues();
    }

    /// <summary>
    /// [S-6571] Grey an item row out when no selected widget has items: its control disabled, its
    /// name in the disabled text colour, and "This widget has no items" as the tooltip of both (a
    /// disabled control takes no pointer, so the name carries it where the pointer can find it).
    /// </summary>
    private void NoItems(TextBlock? label, Control control)
    {
        if (_offer.HasItems)
        {
            return;
        }

        var why = Str.Shell_Fill_NoItems.Text();
        control.IsEnabled = false;
        ToolTipService.SetToolTip(control, why);
        AutomationProperties.SetHelpText(control, why);
        if (label is not null)
        {
            ToolTipService.SetToolTip(label, why);
            label.Foreground = FluentLayout.Themed("TextFillColorDisabledBrush", Microsoft.UI.Colors.Gray);
        }
    }

    /// <summary>One row: the property's name in the secondary colour, then its control; no name spans both columns. Returns the name.</summary>
    private TextBlock? AddRow(string? label, FrameworkElement control)
    {
        TextBlock? name = null;
        var row = _rows.RowDefinitions.Count;
        _rows.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        if (label is not null)
        {
            name = Fluent.Text(FluentType.Body, "TextFillColorSecondaryBrush");
            name.Text = label;
            name.VerticalAlignment = VerticalAlignment.Center;
            SetRow(name, row);
            _rows.Children.Add(name);
        }

        control.VerticalAlignment = VerticalAlignment.Center;
        SetRow(control, row);
        SetColumn(control, label is null ? 0 : 1);
        SetColumnSpan(control, label is null ? 2 : 1);

        if (label is null && control is Button)
        {
            // The settings command reaches the panel's start edge, as a menu's last item does.
            control.Margin = new Thickness(-Fluent.Space2, Fluent.Space1, 0, 0);
        }

        _rows.Children.Add(control);
        return name;
    }

    /// <summary>The choices' words for the values the selection has now; a mixed one says so with a dash.</summary>
    private void ShowValues()
    {
        if (_styleValue is not null && _offer.StyleKind is { } kind && WidgetStyles.For(kind) is { Count: > 0 } styles)
        {
            var current = styles.FirstOrDefault(s => s.Value == _offer.Style);
            var shown = current.Glyph != 0 ? current : styles[0];
            Show(_styleValue, char.ConvertFromUtf32(shown.Glyph), false, _offer.Style is null ? Mixed : shown.Label.Text());
        }

        if (_headerValue is not null)
        {
            var header = Array.Find(HeaderOptions(), o => o.Value == _offer.Header);
            Show(_headerValue, header.Icon?.Glyph ?? Glyphs.HeaderBottom, header.Icon?.Turned ?? true, _offer.Header is null ? Mixed : header.Text);
        }

        if (_scaleValue is not null)
        {
            Show(_scaleValue, Glyphs.Scale, false, _offer.Scale is { } scale ? ScaleText(scale) : Mixed);
        }
    }

    /// <summary>What a mixed selection's choice reads: a dash, as Explorer's properties show differing values.</summary>
    private const string Mixed = "—";

    private static void Show(StackPanel value, string glyph, bool turned, string text)
    {
        value.Children[0] = EditBar.Icon(glyph, turned, Fluent.IconSize);
        if (value.Children[1] is TextBlock words)
        {
            words.Text = text;
        }
    }

    private static (HeaderPlacement Value, (string Glyph, bool Turned)? Icon, string Text)[] HeaderOptions() =>
    [
        (HeaderPlacement.Top, (Glyphs.HeaderBottom, true), Str.Group_HeaderSide_Top.Text()),
        (HeaderPlacement.Bottom, (Glyphs.HeaderBottom, false), Str.Group_HeaderSide_Bottom.Text()),
        (HeaderPlacement.Left, (Glyphs.HeaderLeft, false), Str.Group_HeaderSide_Left.Text()),
        (HeaderPlacement.Right, (Glyphs.HeaderRight, false), Str.Group_HeaderSide_Right.Text()),
        (HeaderPlacement.Hidden, (Glyphs.Hide, false), Str.Toggle_Hidden.Text()),
    ];

    /// <summary>
    /// [S-6007]/[S-6008] The Style choice: the kind's styles, each with its icon, the current one
    /// ticked (none for a mixed selection), built as the menu opens. Picking one sets it on every
    /// selected widget, one undo step. The button shows the current style's icon and name.
    /// </summary>
    private DropDownButton StyleChoice(WidgetKind kind, PropertiesCommands commands)
    {
        var styles = WidgetStyles.For(kind);
        var label = Str.Styles_Label.Text();
        var menu = new MenuFlyout();
        menu.Opening += (_, _) =>
        {
            menu.Items.Clear();
            var now = _offer.Style;
            foreach (var style in styles)
            {
                var item = new RadioMenuFlyoutItem
                {
                    Text = style.Label.Text(),
                    GroupName = label,
                    IsChecked = now == style.Value,
                    Icon = EditBar.Icon(char.ConvertFromUtf32(style.Glyph), false),
                };

                var chosen = style.Value;
                item.Click += (_, _) => commands.SetStyle(chosen);
                menu.Items.Add(item);
            }
        };

        var button = ValueButton(label, menu, out var value);
        _styleValue = value;
        return button;
    }

    /// <summary>A drop-down the width of the controls' column, showing a value's icon and words.</summary>
    private static DropDownButton ValueButton(string name, FlyoutBase menu, out StackPanel value)
    {
        value = EditBar.Labelled(Glyphs.Surface, string.Empty, accent: false);
        if (value.Children[1] is TextBlock words)
        {
            words.TextTrimming = TextTrimming.CharacterEllipsis;
        }

        var button = new DropDownButton
        {
            Content = value,
            Flyout = menu,
            Height = EditBar.ButtonSize,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(Fluent.Space2, 0, Fluent.Space2, 0),
        };
        AutomationProperties.SetName(button, name);
        return button;
    }

    /// <summary>
    /// [S-6566] A fill row's control: the one fill control ([S-6567]) — Default (the app-wide fill,
    /// its swatch the app-wide colour), Transparent, or a colour with its opacity — the width of the
    /// panel's column. A selection that does not share one reads as a dash until a pick.
    /// </summary>
    private static DropDownButton FillRow(string name, FillChoice? current, Action<FillChoice> set, Func<Windows.UI.Color?> appWide)
    {
        var options = new FillChoiceOptions
        {
            DefaultLabel = Str.Common_Default.Text(),
            DefaultColor = appWide,
            MixedLabel = Mixed,
        };

        var choice = ColorChoice.CreateFill(current, set, options, name);
        choice.MinWidth = 0;
        choice.Height = EditBar.ButtonSize;
        choice.HorizontalAlignment = HorizontalAlignment.Stretch;
        choice.Padding = new Thickness(Fluent.Space2, 0, Fluent.Space2, 0);
        return choice;
    }

    /// <summary>[S-6566] Whether two offered fills are the same for the panel: both mixed, or the same choice.</summary>
    private static bool SameFill(FillChoice? a, FillChoice? b) => a is null ? b is null : a.SameAs(b);

    /// <summary>
    /// [S-6544] The item text colour: the one colour control, "Default" (the app-wide item text
    /// colour) or a colour of the widgets' own. A mixed selection reads as Default until one is picked.
    /// </summary>
    private DropDownButton ItemTextChoice(PropertiesCommands commands)
    {
        var options = new ColorChoiceOptions
        {
            DefaultLabel = Str.Common_Default.Text(),
            Presets = ColorPresets.Surfaces,
            DefaultColor = AppItemTextColour,
        };

        var choice = ColorChoice.Create(_offer.ItemText ?? string.Empty, commands.SetItemTextColor, options, Str.Shell_Items_TextShort.Text());
        choice.MinWidth = 0;
        choice.Height = EditBar.ButtonSize;
        choice.HorizontalAlignment = HorizontalAlignment.Stretch;
        choice.Padding = new Thickness(Fluent.Space2, 0, Fluent.Space2, 0);
        return choice;
    }

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
            ? Windows.UI.Color.FromArgb(DesignLook.Alpha(fill.Opacity), r, g, b)
            : null;

    /// <summary>
    /// [S-6531] The widgets' own scale, in steps (<see cref="WidgetScale.Offered"/>), the current one
    /// ticked (none for a mixed selection), built as the menu opens. One pick is one undo step.
    /// </summary>
    private DropDownButton ScaleChoice(PropertiesCommands commands)
    {
        var label = Str.Widget_Scale_Label.Text();
        var menu = new MenuFlyout();
        menu.Opening += (_, _) =>
        {
            menu.Items.Clear();
            var now = _offer.Scale;
            foreach (var step in WidgetScale.Offered(now))
            {
                var item = new RadioMenuFlyoutItem
                {
                    Text = ScaleText(step),
                    GroupName = label,
                    IsChecked = now is { } v && v.Equals(step),
                };

                var chosen = step;
                item.Click += (_, _) => commands.SetScale(chosen);
                menu.Items.Add(item);
            }
        };

        var button = ValueButton(label, menu, out var value);
        _scaleValue = value;
        return button;
    }

    private static string ScaleText(double scale) => LocaleFormat.Percent(Strings.Current, WidgetScale.Percent(scale));

    /// <summary>
    /// [S-3704] The title, edited in place: the kind's name as the placeholder, Enter (or leaving
    /// the box) commits, Escape puts back what was there. While it has the focus the edit keys do
    /// not fire ([S-804]); Enter and Escape hand the keyboard back to the surface.
    /// </summary>
    private TextBox TitleBox(PropertiesCommands commands, string widgetId, string defaultTitle, string? label)
    {
        _titleId = widgetId;
        _titleCommitted = label ?? string.Empty;

        var box = new TextBox
        {
            Text = _titleCommitted,
            PlaceholderText = defaultTitle,
            MinHeight = EditBar.ButtonSize,
            Height = EditBar.ButtonSize,
            HorizontalAlignment = HorizontalAlignment.Stretch,
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
    /// The panel is rebuilt (another widget picked) while the title box holds typing: keep it,
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

    /// <summary>
    /// [S-3702]/[S-3703] A choice on the panel: a drop-down showing the selection's value, whose
    /// menu ticks it, built as it opens. A mixed selection ticks nothing; picking one sets it on them all.
    /// </summary>
    private static DropDownButton Choice<T>(string label, (T Value, (string Glyph, bool Turned)? Icon, string Text)[] options, Func<T?> current, Action<T> set, out StackPanel value)
        where T : struct, Enum
    {
        var menu = new MenuFlyout();
        menu.Opening += (_, _) =>
        {
            menu.Items.Clear();
            var now = current();
            foreach (var (option, icon, text) in options)
            {
                var item = new RadioMenuFlyoutItem
                {
                    Text = text,
                    GroupName = label,
                    IsChecked = now is { } v && EqualityComparer<T>.Default.Equals(v, option),
                    Icon = icon is { } i ? EditBar.Icon(i.Glyph, i.Turned) : null,
                };

                var chosen = option;
                item.Click += (_, _) => set(chosen);
                menu.Items.Add(item);
            }
        };

        return ValueButton(label, menu, out value);
    }
}
