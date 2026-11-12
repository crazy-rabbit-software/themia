using System.Numerics;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Settings;
using Themia.Core.Shell;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;
using Windows.Foundation;
using Windows.System;
using B = Themia.Sdk.Kit.ShellMetrics.Bar;

namespace Themia.App.Shell;

/// <summary>What the edit bar's entries and buttons do, given by the surface (or a render's stand-in).</summary>
/// <param name="AddFlyout">[S-3501] The add menu, built anew each time the bar is built.</param>
/// <param name="Page">[S-2327] A page's entry: its flyout opened, or closed again.</param>
/// <param name="Selection">[S-2343] The selection's entry: its flyout opened, or closed again.</param>
/// <param name="AddOpening">[S-2327] The add menu is opening: the open flyout closes.</param>
/// <param name="IntoFlyout">[S-2327] Down from the bar: the keyboard into the open flyout.</param>
/// <param name="Done">Leaves edit mode.</param>
internal sealed record EditBarCommands(
    Func<Flyout> AddFlyout,
    Action<SettingsPageId> Page,
    Action Selection,
    Action AddOpening,
    Action IntoFlyout,
    Action Done);

/// <summary>[S-2341] The selection's entry: its words ("Clock", "2 × Clock", "3 widgets") and its kind's glyph.</summary>
internal sealed record BarSelection(string Words, string Glyph);

/// <summary>[S-2320] What the bar shows now: the selection's entry (none with nothing selected), the problems, which entry's flyout is open, the room it has.</summary>
/// <param name="Open">The page whose flyout is open; with <paramref name="SelectionOpen"/> the selection's; neither: none.</param>
internal sealed record EditBarState(BarSelection? Selection, int Errors, SettingsPageId? Open, bool SelectionOpen, double Widest);

/// <summary>
/// [S-2320]..[S-2324] The edit bar, Karpo's command bar (karpo-app, docs/widgets.md §575, §578, §596, §608;
/// <c>Dock.EditDeck</c>): one row 48 tall at the top centre of each display's work area, 8 under its top,
/// one width whatever is selected. Left to right: the pages' entries (General, Accounts, Display, Views,
/// Appearance) 4 apart, no fill until pointed at, the open one on the subtle fill with the accent's pill at
/// its foot; 8, a hairline 20 tall while something is selected, 8; the selection's entry in its 256 room;
/// a 16 gutter; the problems' button while there are any, in its room; 4; Add widget; 8; Done (the one
/// accent button, at least 80). On a narrow display the pages' entries show their icon alone (their words
/// the tooltip). Laid out by <see cref="EditBarLayout"/>. No grip, no undo, no search, no close.
/// <para>
/// Its own control so the debug render draws the real bar; its buttons are built as edit mode starts
/// (<see cref="Fill"/>, again on a language switch) and dropped as it ends (<see cref="Release"/>,
/// [S-3593]), the add menu with them.
/// </para>
/// </summary>
internal sealed partial class EditBar : Grid
{
    /// <summary>[S-2320] The bar's height.</summary>
    public const double BarHeight = B.Height;

    /// <summary>[S-2320] A button's height (the placement line's too).</summary>
    public const double ButtonSize = B.Control;

    private readonly Border _frame = new();
    private readonly Canvas _row = new() { Height = B.Control };

    private readonly Dictionary<SettingsPageId, Entry> _pages = [];
    private Entry? _selection;
    private Rectangle? _divider;
    private Button? _errors;
    private Button? _add;
    private Button? _done;

    private EditBarCommands _commands;
    private EditBarState _state = new(null, 0, null, false, double.PositiveInfinity);

    public EditBar(EditBarCommands commands)
    {
        _commands = commands ?? throw new ArgumentNullException(nameof(commands));
        Frame(_frame);
        _frame.Padding = new Thickness(0, B.Pad, 0, B.Pad);
        Children.Add(_frame);
        Height = B.Height;
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Top;

        // A press on the bar is the bar's: it must not reach the surface, whose press on empty space
        // starts a rubber band and clears the selection.
        _frame.PointerPressed += (_, e) => e.Handled = true;
        _frame.KeyDown += OnKey;
    }

    /// <summary>[S-2325] The look the bar and the pages' flyouts share: Windows' ground, the flyout's rim, radius 8, a flyout's shadow.</summary>
    internal static void Frame(Border border)
    {
        ArgumentNullException.ThrowIfNull(border);
        border.CornerRadius = new CornerRadius(ShellMetrics.OverlayRadius);
        border.Background = ShellBrush.Of(ShellRole.CardBase);
        border.BorderBrush = ShellBrush.Of(ShellRole.FlyoutStroke);
        border.BorderThickness = new Thickness(FamilyShape.Hairline);
        border.Shadow = new ThemeShadow();
        border.Translation = new Vector3(0, 0, ShellMetrics.Shadow.Elevation);
    }

    /// <summary>Whether its buttons are built (edit mode, or a render).</summary>
    public bool Built => _frame.Child is not null;

    /// <summary>[S-2322] The bar's width as last laid out.</summary>
    public double BarWidth { get; private set; }

    /// <summary>Build the entries and buttons in the current language; again on a language switch.</summary>
    public void Fill(EditBarCommands commands)
    {
        _commands = commands ?? throw new ArgumentNullException(nameof(commands));
        _row.Children.Clear();
        _pages.Clear();

        foreach (var page in SettingsPages.Order)
        {
            var entry = new Entry(SettingsPages.Glyph(page), SettingsPages.Name(page)) { Tag = "page:" + page };
            entry.Click += (_, _) => commands.Page(page);
            _pages[page] = entry;
            _row.Children.Add(entry);
        }

        _divider = new Rectangle
        {
            Width = FamilyShape.Hairline,
            Height = B.Divider,
            Fill = ThemeBrush.Of("DividerStrokeColorDefaultBrush", Colors.Gray),
            IsHitTestVisible = false,
        };
        _row.Children.Add(_divider);

        _selection = new Entry(Glyphs.Apps, string.Empty) { Tag = "selection" };
        _selection.Click += (_, _) => commands.Selection();
        _row.Children.Add(_selection);

        _errors = new Button { Content = new FontIcon { Glyph = Glyphs.ErrorBadge, FontSize = B.Icon, Foreground = ThemeBrush.Of("SystemFillColorCriticalBrush", Colors.IndianRed) }, Width = B.IconOnly, Tag = "errors" };
        Entry.Subtle(_errors);
        _row.Children.Add(_errors);

        // [S-3501] Windows' standard button: the one command that adds, its "+" in the accent.
        var addFlyout = commands.AddFlyout();
        addFlyout.Opening += (_, _) => commands.AddOpening();
        _add = new Button { Content = Entry.Labelled(Glyphs.Add, Str.Shell_Toolbar_AddWidget.Text(), ShellBrush.Of(ShellRole.AccentText)), Flyout = addFlyout, Tag = "add" };
        Entry.Sized(_add);
        AutomationProperties.SetName(_add, Str.Shell_Toolbar_AddWidget.Text());
        _row.Children.Add(_add);

        // Done: the one accent button.
        _done = new Button { Content = Entry.Labelled(Glyphs.Done, Str.Common_Done.Text(), null), MinWidth = B.DoneLeast, Tag = "done" };
        FluentLayout.ApplyStyle(_done, "AccentButtonStyle");
        Entry.Sized(_done);
        AutomationProperties.SetName(_done, Str.Common_Done.Text());
        _done.Click += (_, _) => commands.Done();
        _row.Children.Add(_done);

        _frame.Child = _row;
        Show(_state);
    }

    /// <summary>[S-3593] Edit mode ended: the buttons and the add menu go; <see cref="Fill"/> builds them again.</summary>
    public void Release()
    {
        _frame.Child = null;
        _row.Children.Clear();
        _pages.Clear();
        _selection = null;
        _divider = null;
        _errors = _add = _done = null;
    }

    /// <summary>[S-2320] Lay the bar out for <paramref name="state"/>: the selection's entry, the problems, the open entry lit.</summary>
    public void Show(EditBarState state)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
        if (!Built)
        {
            return;
        }

        var plan = EditBarLayout.Lay(
            [.. SettingsPages.Order.Select(p => Measure(SettingsPages.Name(p)))],
            state.Selection is { } selection ? Measure(selection.Words) : null,
            state.Errors > 0,
            Measure(Str.Shell_Toolbar_AddWidget.Text()),
            Measure(Str.Common_Done.Text()),
            state.Widest);

        BarWidth = plan.Width;
        Width = plan.Width;
        _row.Width = plan.Width;

        foreach (var (page, entry) in _pages)
        {
            var part = plan.Of(BarPartKind.Page, SettingsPages.Order.ToList().IndexOf(page))!.Value;
            entry.Lay(part, state.Open == page);
        }

        if (plan.Of(BarPartKind.Divider) is { } divider)
        {
            _divider!.Visibility = Visibility.Visible;
            Canvas.SetLeft(_divider, divider.X);
            Canvas.SetTop(_divider, (B.Control - B.Divider) / 2);
        }
        else
        {
            _divider!.Visibility = Visibility.Collapsed;
        }

        if (state.Selection is { } shown && plan.Of(BarPartKind.Selection) is { } room)
        {
            _selection!.Set(shown.Glyph, shown.Words);
            _selection.Visibility = Visibility.Visible;
            _selection.Lay(room, state.SelectionOpen);
        }
        else
        {
            _selection!.Visibility = Visibility.Collapsed;
        }

        if (plan.Of(BarPartKind.Errors) is { } errors)
        {
            _errors!.Visibility = Visibility.Visible;
            Canvas.SetLeft(_errors, errors.X);
            var said = Str.EditBar_Page_Errors.Text();
            ToolTipService.SetToolTip(_errors, said);
            AutomationProperties.SetName(_errors, said);
        }
        else
        {
            _errors!.Visibility = Visibility.Collapsed;
        }

        var add = plan.Of(BarPartKind.Add)!.Value;
        Canvas.SetLeft(_add, add.X);
        _add!.Width = add.Width;
        var done = plan.Of(BarPartKind.Done)!.Value;
        Canvas.SetLeft(_done, done.X);
        _done!.Width = done.Width;
    }

    /// <summary>One line of Body words: how wide.</summary>
    private static double Measure(string words)
    {
        var text = Fluent.Text(FamilyType.Body);
        text.Text = words;
        text.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return Math.Ceiling(text.DesiredSize.Width);
    }

    /// <summary>Add widget's width and its start from the bar's, for the render to hang the add menu under it.</summary>
    internal double AddWidth => _add?.Width ?? 0;

    internal double AddOffset => _add is null ? 0 : Canvas.GetLeft(_add);

    /// <summary>[S-2325] Where an entry is, in <paramref name="layer"/>'s coordinates (null for the selection's page): null before it is laid out.</summary>
    public Themia.Core.Geometry.PixelRect? EntryBox(SettingsPageId? page, UIElement layer)
    {
        var entry = page is { } id ? _pages.GetValueOrDefault(id) : _selection;
        if (entry is not { ActualWidth: > 0 } shown)
        {
            return null;
        }

        var box = shown.TransformToVisual(layer).TransformBounds(new Rect(0, 0, shown.ActualWidth, shown.ActualHeight));
        return new Themia.Core.Geometry.PixelRect(box.X, box.Y, box.Width, box.Height);
    }

    /// <summary>[S-2327] The keyboard back on an entry (its flyout closed from the keyboard); null for the selection's.</summary>
    public void FocusEntry(SettingsPageId? page)
    {
        var entry = page is { } id ? _pages.GetValueOrDefault(id) : _selection;
        entry?.Focus(FocusState.Keyboard);
    }

    /// <summary>[S-2327] Whether <paramref name="node"/> is on the bar: a press there toggles or swaps a flyout, never closes it first.</summary>
    public bool IsOnBar(DependencyObject node)
    {
        for (var at = node; at is not null; at = VisualTreeHelper.GetParent(at))
        {
            if (ReferenceEquals(at, this))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>[S-2327] Left and Right walk the bar; Down goes into the open flyout.</summary>
    private void OnKey(object sender, KeyRoutedEventArgs e)
    {
        var order = _row.Children.OfType<Control>().Where(c => c.Visibility == Visibility.Visible && c.IsEnabled).OrderBy(Canvas.GetLeft).ToList();
        var at = order.FindIndex(c => c.FocusState != FocusState.Unfocused);
        if (at < 0)
        {
            return;
        }

        var rtl = FlowDirection == FlowDirection.RightToLeft;
        switch (e.Key)
        {
            case VirtualKey.Right or VirtualKey.Left:
                var step = (e.Key == VirtualKey.Right) != rtl ? 1 : -1;
                order[Math.Clamp(at + step, 0, order.Count - 1)].Focus(FocusState.Keyboard);
                e.Handled = true;
                break;
            case VirtualKey.Down when _state.Open is not null || _state.SelectionOpen:
                _commands.IntoFlyout();
                e.Handled = true;
                break;
        }
    }

    /// <summary>
    /// [S-2321] An entry: 12, its 16 icon, 8, its words, 12 (its icon alone on a narrow display, its words
    /// the tooltip); no fill or rim until pointed at; the open one on the subtle fill with the accent's
    /// 16 × 3 pill at its foot.
    /// </summary>
    private sealed partial class Entry : Button
    {
        private readonly Border _icon = new() { Width = B.Icon, Height = B.Icon, VerticalAlignment = VerticalAlignment.Center };
        private readonly TextBlock _words;
        private readonly Rectangle _pill = new()
        {
            Width = B.PillWidth,
            Height = B.PillHeight,
            RadiusX = B.PillRadius,
            RadiusY = B.PillRadius,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            Fill = Family.Brush(ColourRole.Accent),
            Visibility = Visibility.Collapsed,
            IsHitTestVisible = false,
        };

        public Entry(string glyph, string words)
        {
            Subtle(this);
            _words = Words(words);
            _words.Margin = new Thickness(B.IconGap, 0, 0, 0);
            Set(glyph, words);
            var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Children = { _icon, _words } };
            Content = new Grid { Children = { row, _pill } };
            HorizontalContentAlignment = HorizontalAlignment.Left;
            VerticalContentAlignment = VerticalAlignment.Stretch;
        }

        public void Set(string glyph, string words)
        {
            _icon.Child = UiLanguage.Mirrored(new FontIcon { Glyph = glyph, FontSize = B.Icon });
            _words.Text = words;
            ToolTipService.SetToolTip(this, words);
            AutomationProperties.SetName(this, words);
        }

        /// <summary>[S-2320] Where the plan puts it; [S-2321] lit while its flyout is open.</summary>
        public void Lay(BarPart part, bool open)
        {
            Canvas.SetLeft(this, part.X);
            Width = part.Width;
            _words.Visibility = part.IconOnly ? Visibility.Collapsed : Visibility.Visible;
            Padding = part.IconOnly ? new Thickness(0) : new Thickness(B.Lead, 0, B.Lead, 0);
            HorizontalContentAlignment = part.IconOnly ? HorizontalAlignment.Center : HorizontalAlignment.Left;
            _pill.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
            _pill.Margin = new Thickness(0, 0, 0, -FamilyShape.Hairline);
            if (open)
            {
                Background = ThemeBrush.Of("SubtleFillColorSecondaryBrush", Colors.Gray);
            }
            else
            {
                ClearValue(BackgroundProperty);
            }

        }

        /// <summary>A command's content: its 16 glyph, 8, its words in Body.</summary>
        internal static StackPanel Labelled(string glyph, string text, Brush? glyphBrush)
        {
            var icon = UiLanguage.Mirrored(new FontIcon { Glyph = glyph, FontSize = B.Icon, VerticalAlignment = VerticalAlignment.Center });
            if (glyphBrush is not null)
            {
                icon.Foreground = glyphBrush;
            }

            var words = Words(text);
            words.Margin = new Thickness(B.IconGap, 0, 0, 0);
            return new StackPanel { Orientation = Orientation.Horizontal, Children = { icon, words } };
        }

        private static TextBlock Words(string text)
        {
            var words = Fluent.Text(FamilyType.Body);
            words.Text = text;
            words.ClearValue(TextBlock.ForegroundProperty);
            words.VerticalAlignment = VerticalAlignment.Center;
            words.TextTrimming = TextTrimming.CharacterEllipsis;
            words.TextWrapping = TextWrapping.NoWrap;
            return words;
        }

        /// <summary>[S-2320] A button's 32 height, radius 4 and 12 padding.</summary>
        internal static void Sized(ButtonBase button)
        {
            button.Height = B.Control;
            button.Padding = new Thickness(B.Lead, 0, B.Lead, 0);
            button.CornerRadius = new CornerRadius(Fluent.ControlRadius);
            button.HorizontalContentAlignment = HorizontalAlignment.Center;
        }

        /// <summary>No fill and no rim at rest, the subtle fills under the pointer and pressed.</summary>
        internal static void Subtle(ButtonBase button)
        {
            Sized(button);
            button.MinWidth = 0;
            button.BorderThickness = new Thickness(0);
            var clear = ThemeBrush.Of("SubtleFillColorTransparentBrush", Colors.Transparent);
            var hover = ThemeBrush.Of("SubtleFillColorSecondaryBrush", Colors.Gray);
            var pressed = ThemeBrush.Of("SubtleFillColorTertiaryBrush", Colors.DimGray);
            button.Resources["ButtonBackground"] = clear;
            button.Resources["ButtonBackgroundPointerOver"] = hover;
            button.Resources["ButtonBackgroundPressed"] = pressed;
            button.Resources["ButtonBackgroundDisabled"] = clear;
            var rim = "Button" + nameof(Control.BorderBrush);
            button.Resources[rim] = clear;
            button.Resources[rim + "PointerOver"] = clear;
            button.Resources[rim + "Pressed"] = clear;
            button.Resources[rim + "Disabled"] = clear;
        }
    }
}
