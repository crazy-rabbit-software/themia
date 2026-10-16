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
/// <param name="SetSurface">[S-3703] The selected widgets' background.</param>
/// <param name="SetTitle">[S-3704] One widget's title, by id, as typed.</param>
/// <param name="ReleaseFocus">[S-3704] Hands the keyboard back to the surface after the title box, so the edit keys work again.</param>
/// <param name="SetStyle">[S-6008] The selected widgets' style, by the style enum's value.</param>
/// <param name="OpenSettings">[S-6009] Opens one widget's settings page, by id.</param>
/// <param name="SetItemColor">[S-6521] The selected widgets' own item colour, or empty for the app-wide item layer.</param>
/// <param name="SetItemOpacity">[S-6521] The selected widgets' own item opacity, 0..1.</param>
internal sealed record PropertiesCommands(
    Action<HeaderPlacement> SetHeader,
    Action<WidgetSurface> SetSurface,
    Action<string, string?> SetTitle,
    Action ReleaseFocus,
    Action<int> SetStyle,
    Action<string> OpenSettings,
    Action<string> SetItemColor,
    Action<double> SetItemOpacity);

/// <summary>
/// [S-3707] The selected widgets' properties panel: a small panel in the edit bar's look standing
/// beside the selection ([S-3708]), one row per property, its name before its control — Title (one
/// widget) or "N selected", Style (one kind with styles, [S-6007]), Header (where every selected
/// widget can have one, [S-3711]), Background, Item background and, for a widget with a layer of its
/// own, Item opacity ([S-6521]), then Settings (one widget with a page, [S-6009]). After Karpo's
/// properties panel (docs/widgets.md §145): the selection's own properties, where the selection is.
/// </summary>
internal sealed partial class PropertiesStrip : Grid
{
    /// <summary>[S-3707] The controls' column: a long widget name or a choice's words without crowding the panel.</summary>
    private const double ControlWidth = 176;

    /// <summary>[S-6521] The opacities the Item opacity choice offers.</summary>
    private static readonly double[] ItemOpacities = [1, 0.8, 0.6, 0.4, 0.2];

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

    /// <summary>The title box while one widget is selected, the widget it names and what it last committed.</summary>
    private TextBox? _title;

    private string? _titleId;

    private string _titleCommitted = string.Empty;

    /// <summary>The choices' current-value contents, rewritten in place when the selection's values change.</summary>
    private StackPanel? _styleValue;

    private StackPanel? _headerValue;

    private StackPanel? _surfaceValue;

    public PropertiesStrip(PropertiesCommands commands)
    {
        _commands = commands;
        EditBar.Frame(_frame);
        _frame.Child = _rows;
        Children.Add(_frame);

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
        _offer = offer;

        // [S-6521] The item rows are rebuilt whenever the item layer moved: the colour control keeps
        // its own value, and the opacity row comes and goes with a layer of the widget's own.
        var sameShape = previous.Shows == offer.Shows
            && previous.HasHeader == offer.HasHeader
            && previous.StyleKind == offer.StyleKind
            && string.Equals(previous.TitleWidgetId, offer.TitleWidgetId, StringComparison.Ordinal)
            && (offer.HasTitle || previous.Count == offer.Count)
            && previous.ItemsMixed == offer.ItemsMixed
            && (previous.Items is null ? offer.Items is null : previous.Items.SameAs(offer.Items));

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
        _styleValue = _headerValue = _surfaceValue = null;
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

        AddRow(Str.Settings_Style_BgLabel.Text(), Choice(
            Str.Settings_Style_BgLabel.Text(),
            SurfaceOptions(),
            () => _offer.Surface,
            commands.SetSurface,
            out var surfaceValue));
        _surfaceValue = surfaceValue;

        // [S-6521] The widgets' own item background, "Default" for the app-wide item layer.
        AddRow(Str.Shell_Items_Background.Text(), ItemColorChoice(commands));
        if (_offer.Items is { } own)
        {
            AddRow(Str.Shell_Items_Opacity.Text(), ItemOpacityChoice(own.Opacity, commands));
        }

        // [S-6009] The widget's settings, one widget only and only when its page has rows ([S-1750]).
        if (_offer is { TitleWidgetId: { } settingsId, TitleKind: { } settingsKind } && Config.ConfigPages.HasWidgetSettings(settingsKind))
        {
            var gear = new Button
            {
                Content = EditBar.Labelled(Glyphs.Settings, Str.Common_Settings.Text(), accent: false),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
            };
            EditBar.Subtle(gear);
            gear.Padding = new Thickness(Fluent.Space2, 0, Fluent.Space2, 0);
            AutomationProperties.SetName(gear, Str.Common_Settings.Text());
            gear.Click += (_, _) => commands.OpenSettings(settingsId);
            AddRow(null, gear);
        }

        ShowValues();
    }

    /// <summary>One row: the property's name in the secondary colour, then its control; no name spans both columns.</summary>
    private void AddRow(string? label, FrameworkElement control)
    {
        var row = _rows.RowDefinitions.Count;
        _rows.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        if (label is not null)
        {
            var name = Fluent.Text(FluentType.Body, "TextFillColorSecondaryBrush");
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

        if (_surfaceValue is not null)
        {
            var surface = Array.Find(SurfaceOptions(), o => o.Value == _offer.Surface);
            Show(_surfaceValue, Glyphs.Surface, false, _offer.Surface is null ? Mixed : surface.Text);
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
        (HeaderPlacement.Hidden, (Glyphs.Hide, false), Str.Toggle_Hidden.Text()),
    ];

    private static (WidgetSurface Value, (string Glyph, bool Turned)? Icon, string Text)[] SurfaceOptions() =>
    [
        (WidgetSurface.Card, null, Str.Shell_Widget_Card.Text()),
        (WidgetSurface.Translucent, null, Str.Shell_Widget_Translucent.Text()),
        (WidgetSurface.Transparent, null, Str.Settings_Style_None.Text()),
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
    /// [S-6521] The item colour: the one colour control ([S-1549]), "Default" (the app-wide item
    /// layer) or a colour of the widgets' own, which keeps the opacity they had.
    /// </summary>
    private FrameworkElement ItemColorChoice(PropertiesCommands commands)
    {
        var options = new ColorChoiceOptions
        {
            DefaultLabel = Str.Common_Default.Text(),
            Presets = ColorPresets.Surfaces,
            DefaultColor = AppItemColour,
        };

        var choice = ColorChoice.Create(_offer.Items?.Color ?? string.Empty, commands.SetItemColor, options, Str.Shell_Items_Background.Text());
        choice.MinWidth = 0;
        choice.Height = EditBar.ButtonSize;
        choice.HorizontalAlignment = HorizontalAlignment.Stretch;
        choice.Padding = new Thickness(Fluent.Space2, 0, Fluent.Space2, 0);
        return choice;
    }

    /// <summary>What "Default" paints its swatch with: the app-wide item colour, or nothing (the theme's fills).</summary>
    private static Windows.UI.Color? AppItemColour()
        => DesignApplier.Current?.ItemBackground is { } layer && HexColor.TryParse(layer.Color, out var r, out var g, out var b)
            ? Windows.UI.Color.FromArgb(255, r, g, b)
            : null;

    /// <summary>[S-6521] The widgets' own item opacity, in steps: one pick is one undo step, as a slider's drag would not be.</summary>
    private static DropDownButton ItemOpacityChoice(double current, PropertiesCommands commands)
    {
        var label = Str.Shell_Items_Opacity.Text();
        var menu = new MenuFlyout();
        foreach (var step in ItemOpacities)
        {
            var item = new RadioMenuFlyoutItem
            {
                Text = LocaleFormat.Percent(Strings.Current, Math.Round(step * 100)),
                GroupName = label,
                IsChecked = Math.Abs(step - current) < 0.001,
            };

            var chosen = step;
            item.Click += (_, _) => commands.SetItemOpacity(chosen);
            menu.Items.Add(item);
        }

        var button = ValueButton(label, menu, out var value);
        Show(value, Glyphs.Opacity, false, LocaleFormat.Percent(Strings.Current, Math.Round(current * 100)));
        return button;
    }

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
