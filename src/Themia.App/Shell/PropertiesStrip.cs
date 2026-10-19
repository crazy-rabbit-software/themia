using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
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
/// <param name="SetAlign">[S-6584] Where the selected widgets that can align draw their content. A render's stand-in may leave it out.</param>
/// <param name="Cut">[S-3717] Cut the selection, as Ctrl+X does.</param>
/// <param name="Copy">[S-3717] Copy the selection, as Ctrl+C does.</param>
/// <param name="Delete">[S-3717] Delete the selection, as the Delete key does.</param>
/// <param name="WidgetLines">[S-3719] One widget's own lines, by id, built over its live config; null for none.</param>
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
    Func<string, bool> HasSettings,
    Action<ContentAlign>? SetAlign = null,
    Action? Cut = null,
    Action? Copy = null,
    Action? Delete = null,
    Func<string, Action<WidgetTextAsk>, WidgetMenuLines?>? WidgetLines = null);

/// <summary>
/// [S-3716] The selected widgets' properties, as a menu that stands beside the selection: drawn as
/// Windows 11 draws a context menu (the flyout's surface, the icon row on top, 32-DIP lines with an
/// icon column, a value column and a chevron), but it stays open while it is used, as a panel does.
/// The icon row is Cut and Copy, Delete at its far end ([S-3717]); then the title (one widget) or
/// "N selected", Style, the widget's own lines ([S-3719]), and the card's own: Header, Alignment,
/// Background, Item background, Item text and Scale; More settings last where the widget still has a
/// page ([S-6009]). A line with a chevron opens its submenu beside it ([S-3718]). After Karpo's
/// properties panel (docs/widgets.md §145): the selection's own properties, where the selection is.
/// </summary>
internal sealed partial class PropertiesStrip : Grid
{
    private const string Scope = "properties";

    /// <summary>[S-3716] A line's height and its parts, as the Windows 11 menu draws them.</summary>
    private const double LineHeight = 32;

    /// <summary>The icon row's buttons: the size of Explorer's.</summary>
    private const double RowButton = 40;

    /// <summary>The words start after the icon column (16) and its gap (12).</summary>
    private const double IconColumn = Fluent.IconSize + Fluent.Space3;

    /// <summary>The narrowest the menu is: Explorer's menus are never a sliver.</summary>
    private const double MinMenuWidth = 256;

    /// <summary>The chevron of a line with a submenu: Windows' "ChevronRightMed", at the menu's size.</summary>
    private static readonly string Chevron = char.ConvertFromUtf32(0xE974);

    private readonly Border _frame = new() { MinWidth = MinMenuWidth };

    private readonly StackPanel _menu = new();

    private PropertiesCommands _commands;

    private QuickSetOffer _offer = QuickSetOffer.None;

    /// <summary>[S-6009] Whether the one selected widget has a settings page now ([S-1750]).</summary>
    private bool _hasSettings;

    /// <summary>[S-3719] The one selected widget's own lines as last built, and the shape they were drawn for.</summary>
    private WidgetMenuLines? _widget;

    private string _widgetShape = string.Empty;

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

        // [S-3707] The keyboard stays in the menu: Tab goes round its lines and stays in it, Up and
        // Down move between them, and Escape hands the keyboard back to the surface.
        _frame.TabFocusNavigation = KeyboardNavigationMode.Cycle;
        _menu.XYFocusKeyboardNavigation = XYFocusKeyboardNavigationMode.Enabled;
        _frame.KeyDown += OnKeyDown;

        // A press on the menu's padding or words is the menu's: it must not reach the surface,
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
    /// half-typed title); the submenus read the offer as they open.
    /// </summary>
    public void ShowSelection(QuickSetOffer offer)
    {
        ArgumentNullException.ThrowIfNull(offer);

        var previous = _offer;
        var hadSettings = _hasSettings;
        var previousShape = _widgetShape;
        _offer = offer;

        // [S-1750] A style picked here can give the widget a page or take it away (a meter's graph).
        _hasSettings = offer.TitleWidgetId is { } id && _commands.HasSettings(id);

        // [S-3719] The widget's own lines are built again on every change: their closures hold the
        // config as it is now (an undo replaces it), and their ticks say what it holds now.
        _widget = WidgetLines();
        _widgetShape = _widget is null ? string.Empty : Shape(_widget);

        var sameShape = previous.Shows == offer.Shows
            && previous.HasHeader == offer.HasHeader
            && previous.StyleKind == offer.StyleKind
            && previous.Style == offer.Style
            && string.Equals(previous.TitleWidgetId, offer.TitleWidgetId, StringComparison.Ordinal)
            && (offer.HasTitle || previous.Count == offer.Count)
            && previous.Header == offer.Header
            && previous.HasItems == offer.HasItems
            && previous.CanAlign == offer.CanAlign
            && previous.Align == offer.Align
            && previous.AlignKind == offer.AlignKind
            && Nullable.Equals(previous.Scale, offer.Scale)
            && SameFill(previous.Background, offer.Background)
            && SameFill(previous.Items, offer.Items)
            && string.Equals(previous.ItemText, offer.ItemText, StringComparison.Ordinal)
            && string.Equals(previousShape, _widgetShape, StringComparison.Ordinal)
            && hadSettings == _hasSettings;

        if (!sameShape)
        {
            Build();
            return;
        }

        // The same widget: its title may have changed (an undo, the box's own commit).
        if (_title is { } title && title.FocusState == FocusState.Unfocused)
        {
            _titleCommitted = offer.Label ?? string.Empty;
            title.Text = _titleCommitted;
        }
    }

    private WidgetMenuLines? WidgetLines()
    {
        if (_offer.TitleWidgetId is not { } id || _commands.WidgetLines is not { } lines)
        {
            return null;
        }

        try
        {
            return lines(id, Ask);
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "a widget's menu lines could not be built", ex);
            return null;
        }
    }

    /// <summary>What the widget's lines show, as one string: a change to any of it draws the menu again.</summary>
    private static string Shape(WidgetMenuLines lines)
    {
        var shape = new System.Text.StringBuilder();

        void Add(IReadOnlyList<ItemMenuEntry> entries)
        {
            foreach (var entry in entries)
            {
                shape.Append(entry.IsSeparator ? "-" : entry.Text).Append('|').Append(entry.Glyph)
                    .Append('|').Append(entry.Checked?.ToString() ?? "n").Append('|').Append(entry.Disabled ? 'd' : 'e');
                if (entry.Children is { } children)
                {
                    shape.Append('[');
                    Add(children);
                    shape.Append(']');
                }

                shape.Append(';');
            }
        }

        Add(lines.Lines);
        shape.Append("||");
        Add(lines.StyleLines);
        return shape.ToString();
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

        _menu.Children.Clear();
        _title = null;
        _titleId = null;

        if (!_offer.Shows)
        {
            return;
        }

        var commands = _commands;
        _menu.Children.Add(QuickRow(commands));
        _menu.Children.Add(Divider(full: true));

        var list = new StackPanel { Padding = new Thickness(0, Fluent.Space1, 0, Fluent.Space1), XYFocusKeyboardNavigation = XYFocusKeyboardNavigationMode.Enabled };
        _menu.Children.Add(list);

        if (_offer is { TitleWidgetId: { } id, TitleKind: { } kind })
        {
            list.Children.Add(TitleBox(commands, id, ModuleCatalog.Of(kind).Name, _offer.Label));
        }
        else
        {
            // [S-3702] Several: no title, but how many the choices reach, over the lines.
            var count = Fluent.Text(FluentType.BodyStrong);
            count.Text = Str.Shell_EditBar_Selected.Plural(_offer.Count);
            count.Margin = new Thickness(Fluent.Space4, Fluent.Space2, Fluent.Space4, Fluent.Space2);
            list.Children.Add(count);
        }

        // [S-6007] The look first: it changes the widget most. Only for one kind with styles; the
        // options only that look draws with are under it ([S-3719]).
        if (_offer.StyleKind is { } styled && WidgetStyles.For(styled) is { Count: > 0 } styles)
        {
            var current = styles.FirstOrDefault(s => s.Value == _offer.Style);
            var shown = _offer.Style is null || current.Glyph == 0 ? styles[0] : current;
            list.Children.Add(SubmenuLine(Glyph(char.ConvertFromUtf32(shown.Glyph)), Str.Styles_Label.Text(),
                _offer.Style is null ? Mixed : shown.Label.Text(), () => StyleMenu(styled, commands)));
        }

        // [S-3719] The widget's own lines, in its order.
        if (_widget is { } widget)
        {
            foreach (var entry in widget.Lines)
            {
                list.Children.Add(entry.IsSeparator ? Divider(full: false) : EntryLine(entry));
            }
        }

        list.Children.Add(Divider(full: false));

        // [S-3711] Only where every selected widget can have a header: never for a toggle.
        if (_offer.HasHeader)
        {
            var header = Array.Find(HeaderOptions(), o => o.Value == _offer.Header);
            list.Children.Add(SubmenuLine(
                EditBar.Icon(header.Icon?.Glyph ?? Glyphs.HeaderBottom, header.Icon?.Turned ?? true, Fluent.IconSize),
                Str.App_ConfigHeaderLabel.Text(),
                _offer.Header is null ? Mixed : header.Text,
                () => Choice(Str.App_ConfigHeaderLabel.Text(), HeaderOptions(), () => _offer.Header, commands.SetHeader)));
        }

        // [S-6584] Where the content sits across the card. Greyed when nothing selected can align
        // in its style ([S-6581]), as the item lines are without items.
        list.Children.Add(Unavailable(AlignLine(commands), _offer.CanAlign,
            _offer.AlignKind ? Str.Widget_Align_NoStyle.Text() : Str.Widget_Align_None.Text()));

        // [S-6566] The widgets' own background and item background: one fill control each, Default
        // for the app-wide one. [S-6571] The item lines are greyed for widgets that have no items.
        list.Children.Add(FillLine(Str.Settings_Style_BgLabel.Text(), _offer.Background, commands.SetBackground, AppBackgroundColour));
        list.Children.Add(Unavailable(FillLine(Str.Shell_Items_Background.Text(), _offer.Items, commands.SetItemBackground, AppItemColour),
            _offer.HasItems, Str.Shell_Fill_NoItems.Text()));

        // [S-6544] The colour of the text on their items, "Default" for the app-wide one.
        list.Children.Add(Unavailable(ItemTextLine(commands), _offer.HasItems, Str.Shell_Fill_NoItems.Text()));

        // [S-6531] The widgets' own content scale, after the look of the card it magnifies.
        list.Children.Add(SubmenuLine(Glyph(Glyphs.Scale), Str.Widget_Scale_Label.Text(),
            _offer.Scale is { } scale ? ScaleText(scale) : Mixed, () => ScaleMenu(commands)));

        // [S-6009] More settings: the widget's options still on its page (the menu holds the rest,
        // [S-6536]), one widget only and only when its page has lines ([S-1750]).
        if (_offer is { TitleWidgetId: { } settingsId } && _hasSettings)
        {
            list.Children.Add(Divider(full: false));
            var more = Line(Glyph(Glyphs.Settings), Str.Shell_MoreSettings.Text(), null, submenu: false);
            ToolTipService.SetToolTip(more, Str.Shell_MoreSettings_Tip.Text());
            AutomationProperties.SetHelpText(more, Str.Shell_MoreSettings_Tip.Text());
            more.Click += (_, _) => commands.OpenSettings(settingsId);
            list.Children.Add(more);
        }
    }

    /// <summary>
    /// [S-3717] The icon row: Cut and Copy at its start, Delete at its far end, as icons with their
    /// words and keys as the tooltip (Explorer's row; Delete kept apart, where a slip does not reach it).
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
            var button = new Button
            {
                Content = MenuArtIcons.IconOrGlyph(glyph),
                Width = RowButton,
                Height = RowButton,
                IsEnabled = run is not null,
            };
            EditBar.Subtle(button);
            button.Height = RowButton;
            button.Padding = new Thickness(0);

            var keys = KeyChordText.Format(chord);
            ToolTipService.SetToolTip(button, Str.Shell_Menu_LabelWithShortcut.Format(("label", words), ("shortcut", keys)));
            AutomationProperties.SetName(button, words);
            AutomationProperties.SetAcceleratorKey(button, keys);
            button.Click += (_, _) => Run(run);

            Grid.SetColumn(button, column);
            row.Children.Add(button);
        }
    }

    /// <summary>
    /// [S-3716] One line of the menu: its icon in the icon column, its words, a value (or nothing)
    /// in the secondary colour where a menu writes a shortcut, and the chevron when it opens a submenu.
    /// </summary>
    private static Button Line(UIElement? icon, string text, string? value, bool submenu, TextBlock? valueText = null)
    {
        var parts = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(IconColumn) },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto },
            },
        };

        if (icon is not null)
        {
            if (icon is FrameworkElement element)
            {
                element.HorizontalAlignment = HorizontalAlignment.Left;
                element.VerticalAlignment = VerticalAlignment.Center;
            }

            parts.Children.Add(icon);
        }

        // The words take the button's colour, so a greyed line greys them with it.
        var words = new TextBlock
        {
            Text = text,
            FontSize = FluentType.Body.FontSize,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        Grid.SetColumn(words, 1);
        parts.Children.Add(words);

        var shown = valueText ?? Fluent.Text(FluentType.Caption, "TextFillColorSecondaryBrush");
        if (valueText is null)
        {
            shown.Text = value ?? string.Empty;
        }

        shown.VerticalAlignment = VerticalAlignment.Center;
        shown.Margin = new Thickness(Fluent.Space6, 0, submenu ? Fluent.Space2 : 0, 0);
        shown.MaxWidth = 120;
        shown.TextTrimming = TextTrimming.CharacterEllipsis;
        Grid.SetColumn(shown, 2);
        parts.Children.Add(shown);

        if (submenu)
        {
            var chevron = new FontIcon
            {
                Glyph = Chevron,
                FontSize = 12,
                MirroredWhenRightToLeft = true,
                Foreground = ThemeBrush.Of("TextFillColorSecondaryBrush", Colors.Gray),
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(chevron, 3);
            parts.Children.Add(chevron);
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
        AutomationProperties.SetName(line, value is { Length: > 0 } ? text + ", " + value : text);
        return line;
    }

    /// <summary>
    /// [S-3718] A line that opens a submenu beside it, built as it opens (so it ticks what holds
    /// then): on a click, Enter, Space, or the arrow towards it. While it is open the line stays lit,
    /// as Windows keeps the line of an open submenu lit.
    /// </summary>
    private static Button SubmenuLine(UIElement? icon, string text, string? value, Func<FlyoutBase?> menu)
    {
        var line = Line(icon, text, value, submenu: true);
        Opens(line, menu);
        return line;
    }

    private static void Opens(Button line, Func<FlyoutBase?> menu)
    {
        void Open()
        {
            FlyoutBase? flyout;
            try
            {
                flyout = menu();
            }
            catch (Exception ex)
            {
                Log.Error(Scope, "a properties submenu could not be built", ex);
                return;
            }

            if (flyout is null)
            {
                return;
            }

            flyout.Placement = FlyoutPlacementMode.RightEdgeAlignedTop;
            flyout.Opened += (_, _) => line.Background = ThemeBrush.Of("SubtleFillColorSecondaryBrush", Colors.Gray);
            flyout.Closed += (_, _) => line.ClearValue(Control.BackgroundProperty);
            flyout.ShowAt(line, new FlyoutShowOptions { Placement = FlyoutPlacementMode.RightEdgeAlignedTop, ShowMode = FlyoutShowMode.Standard });
        }

#if DEBUG
        DebugMenus.AddOrUpdate(line, menu);
#endif

        line.Click += (_, _) => Open();
        line.KeyDown += (_, e) =>
        {
            var towards = UiLanguage.Flow == FlowDirection.RightToLeft ? Windows.System.VirtualKey.Left : Windows.System.VirtualKey.Right;
            if (e.Key == towards)
            {
                Open();
                e.Handled = true;
            }
        };
    }

#if DEBUG
    /// <summary>Debug renders only: what each line's submenu is built from, keyed by the line.</summary>
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Button, Func<FlyoutBase?>> DebugMenus = [];

    /// <summary>
    /// Debug renders only: the submenu of the line a screen reader calls <paramref name="label"/>
    /// (or "label, value"), built as a click would build it; null when there is no such line.
    /// </summary>
    internal FlyoutBase? DebugSubmenu(string label) => FindLine(this, label) is { } line && DebugMenus.TryGetValue(line, out var menu) ? menu() : null;

    /// <summary>Debug renders only: the line a screen reader calls <paramref name="label"/>, its value aside.</summary>
    internal static Button? FindLine(DependencyObject root, string label)
    {
        if (root is Button button && AutomationProperties.GetName(button) is { } name
            && (name == label || name.StartsWith(label + ", ", StringComparison.Ordinal)))
        {
            return button;
        }

        for (var i = 0; i < Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(root); i++)
        {
            if (FindLine(Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(root, i), label) is { } found)
            {
                return found;
            }
        }

        return null;
    }
#endif

    /// <summary>[S-3719] One of the widget's own lines: a submenu, a switch (ticked in the icon column while on) or a command.</summary>
    private static FrameworkElement EntryLine(ItemMenuEntry entry)
    {
        UIElement? icon = entry.Checked is { } on
            ? (on ? Glyph(Glyphs.Done) : null)
            : entry.Glyph is { } glyph ? (entry.Disabled ? Glyph(glyph) : MenuArtIcons.IconOrGlyph(glyph)) : null;

        if (entry.Children is { } children)
        {
            // [S-3718] A choice of one shows the option that holds, as the card's own choices do.
            var chosen = children.FirstOrDefault(c => c is { Checked: true, Group: not null });
            var sub = SubmenuLine(icon, entry.Text, chosen.Group is null ? null : chosen.Text, () =>
            {
                var menu = new MenuFlyout { ShouldConstrainToRootBounds = false };
                ItemMenu.AddTo(menu, children);
                return menu.Items.Count > 0 ? menu : null;
            });
            sub.IsEnabled = !entry.Disabled;
            return sub;
        }

        var line = Line(icon, entry.Text, entry.Shortcut is { } chord ? KeyChordText.Format(chord) : null, submenu: false);
        line.IsEnabled = !entry.Disabled;

        var action = entry.Action;
        line.Click += (_, _) => Run(action);
        return line;
    }

    /// <summary>
    /// [S-6571] [S-6584] Grey a line out unless <paramref name="available"/>, with <paramref name="why"/>
    /// as its tooltip, held by a frame around it: a disabled line takes no pointer, the frame does.
    /// </summary>
    private static FrameworkElement Unavailable(Button line, bool available, string why)
    {
        if (available)
        {
            return line;
        }

        line.IsEnabled = false;
        AutomationProperties.SetHelpText(line, why);
        var frame = new Grid { Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Colors.Transparent), Children = { line } };
        ToolTipService.SetToolTip(frame, why);
        return frame;
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
    /// [S-3721] Ask for a line of text beside the menu: a heading, the box, the reason it cannot be
    /// used yet under it, and the button that gives it (Enter does too; Escape or a click away drops it).
    /// </summary>
    private void Ask(WidgetTextAsk ask)
    {
        var heading = Fluent.Text(FluentType.BodyStrong);
        heading.Text = ask.Title;

        var box = new TextBox
        {
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

        var accept = new Button { Content = ask.Accept, IsEnabled = false, HorizontalAlignment = HorizontalAlignment.Right };
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
            return typed.Length == 0 ? string.Empty : ask.Refuse(typed);
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
        flyout.Opened += (_, _) => box.Focus(FocusState.Programmatic);
        flyout.Closed += (_, _) => _commands.ReleaseFocus();
        flyout.ShowAt(_frame);
    }

    private static (ContentAlign Value, string Glyph, Str Name)[] AlignOptions() =>
    [
        (ContentAlign.Left, Glyphs.AlignLeft, Str.Widget_Align_Left),
        (ContentAlign.Center, Glyphs.AlignCenter, Str.Widget_Align_Center),
        (ContentAlign.Right, Glyphs.AlignRight, Str.Widget_Align_Right),
    ];

    /// <summary>
    /// [S-6584] The Alignment line: its submenu is Left, Center and Right with Segoe's align glyphs,
    /// the current one with the dot (none for a mixed selection). A pick sets it on every selected
    /// widget that can align, one undo step.
    /// </summary>
    private Button AlignLine(PropertiesCommands commands)
    {
        var options = AlignOptions();
        var now = _offer.CanAlign ? _offer.Align : null;
        var current = Array.FindIndex(options, o => o.Value == now);
        var glyph = current >= 0 ? options[current].Glyph : Glyphs.AlignLeft;
        var value = current >= 0 ? options[current].Name.Text() : (_offer.CanAlign ? Mixed : string.Empty);

        return SubmenuLine(Glyph(glyph), Str.Widget_Align_Label.Text(), value, () =>
        {
            var label = Str.Widget_Align_Label.Text();
            var menu = new MenuFlyout { ShouldConstrainToRootBounds = false };
            foreach (var (option, icon, name) in AlignOptions())
            {
                var item = new RadioMenuFlyoutItem
                {
                    Text = name.Text(),
                    GroupName = label,
                    IsChecked = _offer.CanAlign && _offer.Align == option,
                    Icon = Glyph(icon),
                };

                var chosen = option;
                item.Click += (_, _) => commands.SetAlign?.Invoke(chosen);
                menu.Items.Add(item);
            }

            return menu;
        });
    }

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

    /// <summary>
    /// [S-6007]/[S-6008] The Style submenu: the kind's styles, each with its icon, the current one
    /// with the dot (none for a mixed selection); then, for one widget, the options only its current
    /// look draws with ([S-3719]). Picking a style sets it on every selected widget, one undo step.
    /// </summary>
    private MenuFlyout StyleMenu(WidgetId kind, PropertiesCommands commands)
    {
        var label = Str.Styles_Label.Text();
        var menu = new MenuFlyout { ShouldConstrainToRootBounds = false };
        var now = _offer.Style;
        foreach (var style in WidgetStyles.For(kind))
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

        if (_widget is { StyleLines.Count: > 0 } widget)
        {
            menu.Items.Add(new MenuFlyoutSeparator());
            ItemMenu.AddTo(menu, widget.StyleLines);
        }

        return menu;
    }

    /// <summary>
    /// [S-6566] A fill line: the fill's swatch in the icon column and its words as the value; the
    /// one fill control's panel ([S-6567]) opens beside it — Default (the app-wide fill), Transparent,
    /// or a colour with its opacity. A selection that does not share one reads as a dash until a pick.
    /// </summary>
    private static Button FillLine(string name, FillChoice? current, Action<FillChoice> set, Func<Windows.UI.Color?> appWide)
    {
        var options = new FillChoiceOptions
        {
            DefaultLabel = Str.Common_Default.Text(),
            DefaultColor = appWide,
            MixedLabel = Mixed,
        };

        var swatch = Swatch();
        var words = Fluent.Text(FluentType.Caption, "TextFillColorSecondaryBrush");
        var line = Line(swatch, name, null, submenu: true, words);
        var flyout = new Flyout { ShouldConstrainToRootBounds = true };
        ColorChoice.AttachFill(line, swatch, words, flyout, current, set, options);
        OpensPanel(line, flyout);
        return line;
    }

    /// <summary>
    /// [S-6544] The item text colour: the one colour control's panel beside its line, "Default" (the
    /// app-wide item text colour) or a colour of the widgets' own. A mixed selection reads as Default until one is picked.
    /// </summary>
    private Button ItemTextLine(PropertiesCommands commands)
    {
        var options = new ColorChoiceOptions
        {
            DefaultLabel = Str.Common_Default.Text(),
            Presets = ColorPresets.Surfaces,
            DefaultColor = AppItemTextColour,
        };

        var swatch = Swatch();
        var words = Fluent.Text(FluentType.Caption, "TextFillColorSecondaryBrush");
        var line = Line(swatch, Str.Shell_Items_TextShort.Text(), null, submenu: true, words);
        var flyout = new Flyout { ShouldConstrainToRootBounds = true };
        ColorChoice.Attach(line, swatch, words, flyout, _offer.ItemText ?? string.Empty, commands.SetItemTextColor, options);
        OpensPanel(line, flyout);
        return line;
    }

    /// <summary>A colour's swatch the size of a menu icon, framed as Settings frames its swatches.</summary>
    private static Border Swatch() => new()
    {
        Width = Fluent.IconSize,
        Height = Fluent.IconSize,
        CornerRadius = new CornerRadius(Fluent.ControlRadius),
        BorderThickness = new Thickness(Fluent.Hairline),
        BorderBrush = ThemeBrush.Of("ControlStrokeColorDefaultBrush", Colors.Gray),
    };

    private static void OpensPanel(Button line, Flyout flyout) => Opens(line, () => flyout);

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

    /// <summary>[S-6566] Whether two offered fills are the same for the panel: both mixed, or the same choice.</summary>
    private static bool SameFill(FillChoice? a, FillChoice? b) => a is null ? b is null : a.SameAs(b);

    /// <summary>
    /// [S-6531] The widgets' own scale, in steps (<see cref="WidgetScale.Offered"/>), the current one
    /// with the dot (none for a mixed selection), built as the submenu opens. One pick is one undo step.
    /// </summary>
    private MenuFlyout ScaleMenu(PropertiesCommands commands)
    {
        var label = Str.Widget_Scale_Label.Text();
        var menu = new MenuFlyout { ShouldConstrainToRootBounds = false };
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

        return menu;
    }

    private static string ScaleText(double scale) => LocaleFormat.Percent(Strings.Current, WidgetScale.Percent(scale));

    /// <summary>
    /// [S-3704] The title, edited in place at the top of the list: the kind's name as the
    /// placeholder, Enter (or leaving the box) commits, Escape puts back what was there. While it has
    /// the focus the edit keys do not fire ([S-804]); Enter and Escape hand the keyboard back to the surface.
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
            Margin = new Thickness(Fluent.Space2, Fluent.Space1, Fluent.Space2, Fluent.Space1),
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
        _ = DispatcherQueue.TryEnqueue(() => commands.SetTitle(id, text));
    }

    /// <summary>
    /// [S-3702]/[S-3703] A choice's submenu: its options with their icons, the selection's value with
    /// the dot, built as it opens. A mixed selection marks nothing; picking one sets it on them all.
    /// </summary>
    private static MenuFlyout Choice<T>(string label, (T Value, (string Glyph, bool Turned)? Icon, string Text)[] options, Func<T?> current, Action<T> set)
        where T : struct, Enum
    {
        var menu = new MenuFlyout { ShouldConstrainToRootBounds = false };
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

        return menu;
    }
}
