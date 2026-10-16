using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Shell;

/// <summary>What the properties strip's choices do, given by the surface (or a render's stand-in).</summary>
/// <param name="SetHeader">[S-3703] The selected widgets' header placement.</param>
/// <param name="SetSurface">[S-3703] The selected widgets' background.</param>
/// <param name="SetTitle">[S-3704] One widget's title, by id, as typed.</param>
/// <param name="ReleaseFocus">[S-3704] Hands the keyboard back to the surface after the title box, so the edit keys work again.</param>
/// <param name="SetStyle">[S-6008] The selected widgets' style, by the style enum's value.</param>
/// <param name="OpenSettings">[S-6009] Opens one widget's settings page, by id.</param>
internal sealed record PropertiesCommands(
    Action<HeaderPlacement> SetHeader,
    Action<WidgetSurface> SetSurface,
    Action<string, string?> SetTitle,
    Action ReleaseFocus,
    Action<int> SetStyle,
    Action<string> OpenSettings);

/// <summary>
/// [S-3707] The selected widgets' properties strip: a compact bar in the edit bar's look, standing
/// against the selection ([S-3708]) — the title box (one widget) or "N selected", then Style (one
/// kind with styles, [S-6007]), Header (where every selected widget can have one, [S-3711]),
/// Background, and the gear that opens the widget's settings (one widget with a page, [S-6009]). After Karpo's properties
/// panel (docs/widgets.md §145): the selection's own properties, where the selection is.
/// </summary>
internal sealed partial class PropertiesStrip : Grid
{
    /// <summary>[S-3702] The title box's width: a long widget name without crowding the strip.</summary>
    private const double TitleWidth = 168;

    private readonly Border _frame = new() { Padding = new Thickness(Fluent.Space1) };

    private readonly StackPanel _row = new() { Orientation = Orientation.Horizontal, Spacing = Fluent.Space1 };

    private PropertiesCommands _commands;

    private QuickSetOffer _offer = QuickSetOffer.None;

    /// <summary>The title box while one widget is selected, the widget it names and what it last committed.</summary>
    private TextBox? _title;

    private string? _titleId;

    private string _titleCommitted = string.Empty;

    /// <summary>[S-6007] The Style button's icon: the current style's, followed on an undo or a pick.</summary>
    private FontIcon? _styleIcon;

    public PropertiesStrip(PropertiesCommands commands)
    {
        _commands = commands;
        EditBar.Frame(_frame);
        _frame.Child = _row;
        Children.Add(_frame);

        // A press on the strip's padding or words is the strip's: it must not reach the surface,
        // whose press on empty space starts a rubber band and clears the selection.
        _frame.PointerPressed += (_, e) => e.Handled = true;
        HorizontalAlignment = HorizontalAlignment.Left;
        VerticalAlignment = VerticalAlignment.Top;
        Visibility = Visibility.Collapsed;
    }

    /// <summary>What the strip offers now; <see cref="QuickSetOffer.Shows"/> false when it has nothing to show.</summary>
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
    /// half-typed title); Header and Background read the offer as their menus open.
    /// </summary>
    public void ShowSelection(QuickSetOffer offer)
    {
        ArgumentNullException.ThrowIfNull(offer);

        var previous = _offer;
        _offer = offer;

        var sameShape = previous.Shows == offer.Shows
            && previous.HasHeader == offer.HasHeader
            && previous.StyleKind == offer.StyleKind
            && string.Equals(previous.TitleWidgetId, offer.TitleWidgetId, StringComparison.Ordinal)
            && (offer.HasTitle || previous.Count == offer.Count);

        if (!sameShape)
        {
            Build();
            return;
        }

        if (_styleIcon is { } styleIcon)
        {
            styleIcon.Glyph = StyleGlyph(offer);
        }

        // The same widget: its title may have changed (an undo, the box's own commit).
        if (_title is { } title && title.FocusState == FocusState.Unfocused)
        {
            _titleCommitted = offer.Label ?? string.Empty;
            title.Text = _titleCommitted;
        }
    }

    private void Build()
    {
        CommitTitleLater();

        _row.Children.Clear();
        _styleIcon = null;
        _title = null;
        _titleId = null;

        if (!_offer.Shows)
        {
            return;
        }

        var commands = _commands;
        if (_offer is { TitleWidgetId: { } id, TitleKind: { } kind })
        {
            _row.Children.Add(TitleBox(commands, id, WidgetCatalog.Definition(kind).Label, _offer.Label));
        }
        else
        {
            // [S-3702] Several: no title, but how many the choices reach.
            var count = Fluent.Text(FluentType.Body);
            count.Text = Str.Shell_EditBar_Selected.Plural(_offer.Count);
            count.Foreground = FluentLayout.Themed("TextFillColorSecondaryBrush", Colors.Gray);
            count.VerticalAlignment = VerticalAlignment.Center;
            count.Margin = new Thickness(Fluent.Space2, 0, Fluent.Space2, 0);
            _row.Children.Add(count);
        }

        // [S-6007] The look first: it changes the widget most. Only for one kind with styles.
        if (_offer.StyleKind is { } styled)
        {
            _row.Children.Add(StyleChoice(styled, commands));
        }

        // [S-3711] Only where every selected widget can have a header: never for a toggle.
        if (_offer.HasHeader)
        {
            _row.Children.Add(Choice(
                (Glyphs.HeaderBottom, true),
                Str.App_ConfigHeaderLabel.Text(),
                [
                    (HeaderPlacement.Top, (Glyphs.HeaderBottom, true), Str.Group_HeaderSide_Top.Text()),
                    (HeaderPlacement.Bottom, (Glyphs.HeaderBottom, false), Str.Group_HeaderSide_Bottom.Text()),
                    (HeaderPlacement.Hidden, (Glyphs.Hide, false), Str.Toggle_Hidden.Text()),
                ],
                () => _offer.Header,
                commands.SetHeader));
        }

        _row.Children.Add(Choice(
            (Glyphs.Surface, false),
            Str.Settings_Style_BgLabel.Text(),
            [
                (WidgetSurface.Card, null, Str.Shell_Widget_Card.Text()),
                (WidgetSurface.Translucent, null, Str.Shell_Widget_Translucent.Text()),
                (WidgetSurface.Transparent, null, Str.Settings_Style_None.Text()),
            ],
            () => _offer.Surface,
            commands.SetSurface));

        // [S-6009] The widget's settings, one widget only and only when its page has rows ([S-1750]).
        if (_offer is { TitleWidgetId: { } settingsId, TitleKind: { } settingsKind } && Config.ConfigPages.HasWidgetSettings(settingsKind))
        {
            var gear = new Button { Content = EditBar.Icon(Glyphs.Settings, false, Fluent.IconSize) };
            EditBar.IconOnly(gear, Str.Common_Settings.Text());
            gear.Click += (_, _) => commands.OpenSettings(settingsId);
            _row.Children.Add(gear);
        }
    }

    /// <summary>The current style's icon; a mixed selection shows the kind's first.</summary>
    private static string StyleGlyph(QuickSetOffer offer)
    {
        if (offer.StyleKind is not { } kind || WidgetStyles.For(kind) is not { Count: > 0 } styles)
        {
            return Glyphs.Settings;
        }

        var current = styles.FirstOrDefault(s => s.Value == offer.Style);
        return char.ConvertFromUtf32(current.Glyph != 0 ? current.Glyph : styles[0].Glyph);
    }

    /// <summary>
    /// [S-6007]/[S-6008] The Style choice: the kind's styles, each with its icon, the current one
    /// ticked (none for a mixed selection), built as the menu opens. Picking one sets it on every
    /// selected widget, one undo step. The button shows the current style's icon.
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

        var content = EditBar.Labelled(StyleGlyph(_offer), label, accent: false);
        _styleIcon = content.Children[0] as FontIcon;
        var button = new DropDownButton { Content = content, Flyout = menu };
        EditBar.Subtle(button);
        AutomationProperties.SetName(button, label);
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
            Width = TitleWidth,
            MinHeight = EditBar.ButtonSize,
            Height = EditBar.ButtonSize,
            VerticalAlignment = VerticalAlignment.Center,
        };
        ToolTipService.SetToolTip(box, Str.Group_Title_Label.Text());
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
    /// The strip is rebuilt (another widget picked) while the title box holds typing: keep it,
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
    /// [S-3702]/[S-3703] A choice on the strip: a labelled drop-down whose menu ticks the selection's
    /// value, built as it opens. A mixed selection ticks nothing; picking one sets it on them all.
    /// </summary>
    private static DropDownButton Choice<T>((string Glyph, bool Turned) glyph, string label, (T Value, (string Glyph, bool Turned)? Icon, string Text)[] options, Func<T?> current, Action<T> set)
        where T : struct, Enum
    {
        var menu = new MenuFlyout();
        menu.Opening += (_, _) =>
        {
            menu.Items.Clear();
            var now = current();
            foreach (var (value, icon, text) in options)
            {
                var item = new RadioMenuFlyoutItem
                {
                    Text = text,
                    GroupName = label,
                    IsChecked = now is { } v && EqualityComparer<T>.Default.Equals(v, value),
                    Icon = icon is { } i ? EditBar.Icon(i.Glyph, i.Turned) : null,
                };

                var chosen = value;
                item.Click += (_, _) => set(chosen);
                menu.Items.Add(item);
            }
        };

        var content = EditBar.Labelled(glyph.Glyph, label, accent: false);
        content.Children[0] = EditBar.Icon(glyph.Glyph, glyph.Turned, Fluent.IconSize);
        var button = new DropDownButton { Content = content, Flyout = menu };
        EditBar.Subtle(button);
        AutomationProperties.SetName(button, label);
        return button;
    }
}
