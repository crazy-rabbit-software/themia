using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Shell;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Windows.Foundation;

namespace Themia.App.Shell;

/// <summary>What the edit bar's commands do, given by the surface (or a render's stand-in).</summary>
/// <param name="AddFlyout">[S-3501] The add menu, built anew each time the bar is built.</param>
/// <param name="Menu">[S-3520] A crumb's or ⚙'s menu: opens it under its button, or closes it again.</param>
/// <param name="Widget">[S-3508] The widget crumb: the selection's properties menu put away or brought back.</param>
/// <param name="MenuOpening">[S-3521] The add menu is opening: the bar's open menu closes.</param>
/// <param name="Undo">[S-3509] One step back, as Ctrl+Z.</param>
/// <param name="Redo">[S-3509] One step forward again, as Ctrl+Y.</param>
/// <param name="SetSnap">[S-3516] Turns grid snap on or off.</param>
/// <param name="Done">Leaves edit mode.</param>
internal sealed record EditBarCommands(
    Func<Flyout> AddFlyout,
    Action<BarMenu> Menu,
    Action Widget,
    Action MenuOpening,
    Action Undo,
    Action Redo,
    Action<bool> SetSnap,
    Action Done);

/// <summary>[S-3507] One crumb of the trail: what it names, its icon (a view's in its colour) and its long form, the tooltip.</summary>
internal sealed record BarCrumb(string Name, string Glyph, string? GlyphColour = null, string? Detail = null);

/// <summary>
/// [S-3500] What the bar shows now: the trail's crumbs (no widget crumb with nothing selected), whether
/// the properties menu stands beside the selection, what Undo and Redo can step, and snap.
/// </summary>
internal sealed record EditBarState(BarCrumb Display, BarCrumb View, BarCrumb? Widget, bool WidgetOpen, bool CanUndo, bool CanRedo, bool SnapOn);

/// <summary>[S-3520] The edit bar's drop-down menus, each drawn by the menu engine under its button: one open at a time.</summary>
internal enum BarMenu
{
    /// <summary>[S-3543] Display ▾: what this display shows and how, and what every display shares.</summary>
    Display,

    /// <summary>[S-3541] View ▾: the view this display shows, and every view's order, names and deletion.</summary>
    View,

    /// <summary>[S-3520] ⚙: Themia's settings as a whole.</summary>
    Settings,
}

/// <summary>
/// [S-3500] The edit bar: a Windows 11 command bar at the top centre of each display's work area in
/// edit mode, after Karpo's edit bar (karpo-app, docs/widgets.md §352, Dock.EditDeck.cs). It reads
/// where → add → tools → global → finish: the trail of where one is ([S-3507]: Display ▾ › View ▾ ›
/// Widget ▾), Add widget, a hairline, Undo and Redo ([S-3509]) and Snap ([S-3516]), a hairline, ⚙
/// ([S-3520]) and Done. 48 DIPs tall: 36-DIP buttons with radius 4 in a 6-DIP padding, 6 between two,
/// 12 either side of a 20-DIP hairline; inside a button 12 DIPs in, a 16-DIP glyph, 8, then its words
/// in 14. One button, one menu, toggled. On the flyouts' acrylic with their stroke, overlay corners
/// and shadow.
/// <para>
/// Its own control so the debug render draws the real bar, and so a language switch rebuilds its
/// contents in place (<see cref="Fill"/>) without moving it in the surface's tree.
/// </para>
/// </summary>
internal sealed partial class EditBar : Grid
{
    /// <summary>[S-3500] A button's height and an icon-only button's side.</summary>
    public const double ButtonSize = 36;

    /// <summary>[S-3500] The padding round the buttons and the room between two of them.</summary>
    public const double Gap = 6;

    /// <summary>[S-3500] The bar's height: a button in its padding.</summary>
    public const double BarHeight = ButtonSize + (2 * Gap);

    /// <summary>[S-3500] The hairline between the groups, 12 DIPs clear of either neighbour.</summary>
    private const double DividerHeight = 20;

    /// <summary>[S-3500] Done is at least this wide, as Karpo's.</summary>
    private const double DoneWidth = 80;

    /// <summary>[S-3507] The room between the trail and Add widget: the trail is where, the rest what to do.</summary>
    private const double TrailGap = Fluent.Space6;

    /// <summary>The bar's frame; the grid around it carries the surface's margins.</summary>
    private readonly Border _frame = new() { Padding = new Thickness(Gap), Margin = new Thickness(Fluent.Space2, 0, Fluent.Space2, 0) };

    /// <summary>[S-3520] The buttons whose menu opens under them (Display, View, ⚙), by the menu.</summary>
    private readonly Dictionary<BarMenu, ButtonBase> _menuButtons = [];

    private EditBarCommands _commands;
    private EditBarState? _state;

    private Crumb? _display;
    private Crumb? _view;
    private Crumb? _widget;
    private Button? _undo;
    private Button? _redo;
    private ToggleButton? _snap;

    /// <summary>[S-3520] The menu open under its button: the button stays lit, as a drop-down's does while its menu is open.</summary>
    private BarMenu? _openMenu;

    public EditBar(EditBarCommands commands)
    {
        _commands = commands ?? throw new ArgumentNullException(nameof(commands));
        ChromeFrame.Apply(_frame);
        Children.Add(_frame);

        // A press on the bar's padding is the bar's: it must not reach the
        // surface, whose press on empty space starts a rubber band and clears the selection.
        _frame.PointerPressed += (_, e) => e.Handled = true;
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Top;
        Fill(commands);
    }

    /// <summary>Whether its buttons are built.</summary>
    public bool Built => _frame.Child is not null;

    /// <summary>
    /// Build the buttons in the current language, as the state was last shown. Called again on a
    /// language switch.
    /// </summary>
    public void Fill(EditBarCommands commands)
    {
        _commands = commands ?? throw new ArgumentNullException(nameof(commands));
        _menuButtons.Clear();

        _display = new Crumb(Str.BarMenus_Display.Text());
        _display.Click += (_, _) => commands.Menu(BarMenu.Display);
        _menuButtons[BarMenu.Display] = _display;

        _view = new Crumb(Str.CtxMenu_View.Text());
        _view.Click += (_, _) => commands.Menu(BarMenu.View);
        _menuButtons[BarMenu.View] = _view;

        _widget = new Crumb(Str.EditBar_Widget.Text());
        _widget.Click += (_, _) => commands.Widget();

        // [S-3501] Windows' standard button: the one command that adds, its "+" in the accent.
        var addFlyout = commands.AddFlyout();
        addFlyout.Opening += (_, _) => commands.MenuOpening();
        var add = new Button { Content = Labelled(Glyphs.Add, Str.Shell_Toolbar_AddWidget.Text(), accent: true), Flyout = addFlyout };
        Standard(add);
        AutomationProperties.SetName(add, Str.Shell_Toolbar_AddWidget.Text());

        // [S-3509] Undo and Redo, icon-only, greyed while there is nothing to step.
        _undo = IconButton(Glyphs.Undo, Str.Shell_Undo_Undo.Text(), KeyChord.Ctrl(ChordKey.Z));
        _undo.Click += (_, _) => commands.Undo();
        _redo = IconButton(Glyphs.Redo, Str.Shell_Undo_Redo.Text(), KeyChord.Ctrl(ChordKey.Y));
        _redo.Click += (_, _) => commands.Redo();

        // [S-3516] Snap, the one setting reached for in the middle of arranging: lit while on.
        _snap = new ToggleButton { Content = new FontIcon { Glyph = Glyphs.Grid, FontSize = Fluent.IconSize } };
        Square(_snap);
        Lit(_snap);
        ToolTipService.SetToolTip(_snap, Str.Shell_Toolbar_SnapTip.Text());
        AutomationProperties.SetName(_snap, Str.Shell_Toolbar_SnapName.Text());
        _snap.Click += (_, _) => commands.SetSnap(_snap.IsChecked == true);

        // [S-3520] ⚙ drops the settings menu down, icon-only as Windows' own "More" is; [S-67] its
        // last line is Quit Themia, behind a second click and never the button beside Done.
        var settings = IconButton(Glyphs.Settings, Str.Common_Settings.Text(), null);
        settings.Click += (_, _) => commands.Menu(BarMenu.Settings);
        _menuButtons[BarMenu.Settings] = settings;

        // Done: the accent button, with its word.
        var done = new Button { Content = Words(Str.Common_Done.Text()), MinWidth = DoneWidth };
        FluentLayout.ApplyStyle(done, "AccentButtonStyle");
        Size(done);
        AutomationProperties.SetName(done, Str.Common_Done.Text());
        done.Click += (_, _) => commands.Done();

        var trail = new CrumbTrail(_display, Separator(), _view, Separator(), _widget);
        _frame.Child = new BarRow(trail, add, Divider(), _undo, _redo, _snap, Divider(), settings, done);

        if (_state is { } state)
        {
            Show(state);
        }

        MenuOpen(_openMenu);
    }

    /// <summary>
    /// [S-3507] [S-3508] [S-3509] [S-3516] Show the trail, Undo, Redo and Snap as they are now (a
    /// selection, a view shown, a step, a change made elsewhere); written in place.
    /// </summary>
    public void Show(EditBarState state)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
        if (!Built)
        {
            return;
        }

        _display!.Set(state.Display);
        _view!.Set(state.View);
        if (state.Widget is { } widget)
        {
            _widget!.Set(widget);
            _widget.Visibility = Visibility.Visible;
        }
        else
        {
            _widget!.Visibility = Visibility.Collapsed;
        }

        Light(_widget, state.Widget is not null && state.WidgetOpen);
        _undo!.IsEnabled = state.CanUndo;
        _redo!.IsEnabled = state.CanRedo;
        if (_snap!.IsChecked != state.SnapOn)
        {
            _snap.IsChecked = state.SnapOn;
        }
    }

    /// <summary>The Add widget button's width, for the render to hang the menu under it.</summary>
    internal double AddWidth => _frame.Child is BarRow { Children.Count: > 1 } row ? row.Children[1].DesiredSize.Width : 0;

    /// <summary>Where Add widget's start edge is from the bar's, for the render to hang the add menu under it.</summary>
    internal double AddOffset => _frame.Child is BarRow { Children.Count: > 1 } row && row.Children[1] is FrameworkElement add && add.ActualWidth > 0
        ? add.TransformToVisual(this).TransformPoint(new Point(0, 0)).X
        : 0;

    /// <summary>
    /// [S-3516] [S-6584] A toggle that is on lit with the accent, as Windows lights one: Snap, the
    /// properties panel's alignment. Through the template's resource keys, as <see cref="Subtle"/>.
    /// </summary>
    internal static void Lit(ToggleButton toggle)
    {
        ArgumentNullException.ThrowIfNull(toggle);

        var accent = ThemeBrush.Of("AccentFillColorDefaultBrush", Colors.DodgerBlue);
        var accentHover = ThemeBrush.Of("AccentFillColorSecondaryBrush", Colors.DodgerBlue);
        var accentPressed = ThemeBrush.Of("AccentFillColorTertiaryBrush", Colors.DodgerBlue);
        var onAccent = ThemeBrush.Of("TextOnAccentFillColorPrimaryBrush", Colors.White);
        toggle.Resources["ToggleButtonBackgroundChecked"] = accent;
        toggle.Resources["ToggleButtonBackgroundCheckedPointerOver"] = accentHover;
        toggle.Resources["ToggleButtonBackgroundCheckedPressed"] = accentPressed;
        toggle.Resources["ToggleButtonBorderBrushChecked"] = accent;
        toggle.Resources["ToggleButtonBorderBrushCheckedPointerOver"] = accentHover;
        toggle.Resources["ToggleButtonBorderBrushCheckedPressed"] = accentPressed;
        toggle.Resources["ToggleButtonForegroundChecked"] = onAccent;
        toggle.Resources["ToggleButtonForegroundCheckedPointerOver"] = onAccent;
        toggle.Resources["ToggleButtonForegroundCheckedPressed"] = onAccent;
    }

    /// <summary>[S-3520] The button of the menu open under it lit, as a drop-down's button stays while its menu is; the others not.</summary>
    public void MenuOpen(BarMenu? open)
    {
        _openMenu = open;
        foreach (var (menu, button) in _menuButtons)
        {
            Light(button, menu == open);
        }
    }

    /// <summary>[S-3521] The keyboard back on a menu's button (its menu closed from the keyboard).</summary>
    public void FocusMenu(BarMenu menu)
    {
        if (_menuButtons.TryGetValue(menu, out var button))
        {
            button.Focus(FocusState.Keyboard);
        }
    }

    /// <summary>
    /// [S-3521] [S-3508] Whether <paramref name="node"/> is one of the buttons a menu belongs to (a
    /// crumb, ⚙): a press on it toggles or swaps the menu rather than closing it.
    /// </summary>
    public bool IsMenuButton(DependencyObject node)
        => _menuButtons.Values.Any(b => ReferenceEquals(node, b)) || (_widget is not null && ReferenceEquals(node, _widget));

    /// <summary>[S-3522] Where a menu's button is, in <paramref name="layer"/>'s coordinates; null before it has been laid out.</summary>
    public Themia.Core.Geometry.PixelRect? MenuButtonBox(BarMenu menu, UIElement layer)
    {
        if (!_menuButtons.TryGetValue(menu, out var found) || found is not { ActualWidth: > 0 } button)
        {
            return null;
        }

        var box = button.TransformToVisual(layer).TransformBounds(new Rect(0, 0, button.ActualWidth, button.ActualHeight));
        return new Themia.Core.Geometry.PixelRect(box.X, box.Y, box.Width, box.Height);
    }

    /// <summary>
    /// A glyph, or the same glyph turned over (Segoe has "DockBottom" but no "DockTop", [S-3702]); the strip's too.
    /// </summary>
    internal static FontIcon Icon(string glyph, bool turned, double? size = null)
    {
        var icon = UiLanguage.Mirrored(new FontIcon { Glyph = glyph, VerticalAlignment = VerticalAlignment.Center });
        if (size is { } s)
        {
            icon.FontSize = s;
        }

        if (turned)
        {
            icon.RenderTransformOrigin = new Point(0.5, 0.5);
            icon.RenderTransform = new RotateTransform { Angle = 180 };
        }

        return icon;
    }

    /// <summary>A labelled command's content: a 16-DIP glyph, 8 DIPs, then its words in Body.</summary>
    internal static StackPanel Labelled(string glyph, string text, bool accent)
    {
        var icon = UiLanguage.Mirrored(new FontIcon { Glyph = glyph, FontSize = Fluent.IconSize, VerticalAlignment = VerticalAlignment.Center });
        if (accent)
        {
            icon.Foreground = ThemeBrush.Of("AccentTextFillColorPrimaryBrush", Colors.DodgerBlue);
        }

        return new StackPanel { Orientation = Orientation.Horizontal, Spacing = Fluent.Space2, Children = { icon, Words(text) } };
    }

    /// <summary>A button's words in Body, in the button's own colour (which a checked toggle or an accent button turns).</summary>
    private static TextBlock Words(string text)
    {
        var words = Fluent.Text(FluentType.Body);
        words.Text = text;
        words.ClearValue(TextBlock.ForegroundProperty);
        words.VerticalAlignment = VerticalAlignment.Center;
        words.TextTrimming = TextTrimming.CharacterEllipsis;
        words.TextWrapping = TextWrapping.NoWrap;
        return words;
    }

    /// <summary>An icon-only command: square, its words (and its keys) the tooltip, its words what a screen reader reads.</summary>
    private static Button IconButton(string glyph, string words, KeyChord? chord)
    {
        var button = new Button { Content = UiLanguage.Mirrored(new FontIcon { Glyph = glyph, FontSize = Fluent.IconSize }) };
        Square(button);
        var tip = chord is { } keys ? Str.Shell_Menu_LabelWithShortcut.Format(("label", words), ("shortcut", KeyChordText.Format(keys))) : words;
        ToolTipService.SetToolTip(button, tip);
        AutomationProperties.SetName(button, words);
        if (chord is { } shortcut)
        {
            AutomationProperties.SetAcceleratorKey(button, KeyChordText.Format(shortcut));
        }

        return button;
    }

    /// <summary>A subtle square: an icon-only button's 36 × 36.</summary>
    private static void Square(ButtonBase button)
    {
        Subtle(button);
        button.Width = ButtonSize;
        button.Padding = new Thickness(0);
        button.HorizontalContentAlignment = HorizontalAlignment.Center;
    }

    /// <summary>[S-3500] A button's height, radius and padding (12 DIPs in), as every button of the bar has them.</summary>
    private static void Size(ButtonBase button)
    {
        button.Height = ButtonSize;
        button.MinWidth = Math.Max(button.MinWidth, 0);
        button.Padding = new Thickness(Fluent.Space3, 0, Fluent.Space3, 0);
        button.CornerRadius = new CornerRadius(Fluent.ControlRadius);
        button.VerticalAlignment = VerticalAlignment.Center;
    }

    /// <summary>[S-3501] Windows' standard button (its fill and stroke at rest), at the bar's size.</summary>
    private static void Standard(Button button) => Size(button);

    /// <summary>
    /// [S-3500] No fill and no stroke at rest, the subtle fills under the pointer and pressed. Through
    /// the template's own resource keys: the visual states set those fills on the inner border and
    /// would outrank a local Background.
    /// </summary>
    internal static void Subtle(ButtonBase button)
    {
        ArgumentNullException.ThrowIfNull(button);
        Size(button);
        button.MinWidth = 0;
        button.BorderThickness = new Thickness(0);

        var clear = ThemeBrush.Of("SubtleFillColorTransparentBrush", Colors.Transparent);
        var hover = ThemeBrush.Of("SubtleFillColorSecondaryBrush", Colors.Gray);
        var pressed = ThemeBrush.Of("SubtleFillColorTertiaryBrush", Colors.DimGray);
        var prefix = button is ToggleButton ? "ToggleButton" : "Button";

        button.Resources[prefix + "Background"] = clear;
        button.Resources[prefix + "BackgroundPointerOver"] = hover;
        button.Resources[prefix + "BackgroundPressed"] = pressed;
        button.Resources[prefix + "BackgroundDisabled"] = clear;
        button.Resources[prefix + "BorderBrush"] = clear;
        button.Resources[prefix + "BorderBrushPointerOver"] = clear;
        button.Resources[prefix + "BorderBrushPressed"] = clear;
        button.Resources[prefix + "BorderBrushDisabled"] = clear;
    }

    /// <summary>[S-3520] [S-3508] A button whose menu is open stays lit with the subtle fill; the others clear.</summary>
    private static void Light(ButtonBase? button, bool on)
    {
        if (button is null)
        {
            return;
        }

        if (on)
        {
            button.Background = ThemeBrush.Of("SubtleFillColorSecondaryBrush", Colors.Gray);
        }
        else
        {
            button.ClearValue(Control.BackgroundProperty);
        }
    }

    /// <summary>[S-3500] The 20-DIP hairline between two groups, 12 DIPs clear of either (6 of its own and the bar's 6).</summary>
    private static Rectangle Divider() => new()
    {
        Width = Fluent.Hairline,
        Height = DividerHeight,
        Margin = new Thickness(Gap, 0, Gap, 0),
        VerticalAlignment = VerticalAlignment.Center,
        Fill = ThemeBrush.Of("DividerStrokeColorDefaultBrush", Colors.Gray),
        IsHitTestVisible = false,
    };

    /// <summary>[S-3507] The › between two crumbs: 12 DIPs, centred in the 20 between them, mirrored right to left.</summary>
    private static FontIcon Separator() => new()
    {
        Glyph = Glyphs.ChevronRight,
        FontSize = Fluent.Space3,
        Width = EditBarTrail.Between,
        MirroredWhenRightToLeft = true,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        Foreground = ThemeBrush.Of("TextFillColorSecondaryBrush", Colors.Gray),
        IsHitTestVisible = false,
    };

    /// <summary>
    /// [S-3507] A crumb: a subtle drop-down naming where one is — its icon, its name (trimmed to the
    /// width the trail gives it, in full as the tooltip with what it is: "Display: DELL U2723QE —
    /// 3840 × 2160"), a 12-DIP chevron down.
    /// </summary>
    private sealed partial class Crumb : Button
    {
        private readonly string _kind;
        private readonly Border _icon = new() { Width = Fluent.IconSize, VerticalAlignment = VerticalAlignment.Center };
        private readonly TextBlock _words = Words(string.Empty);
        private string _iconKey = string.Empty;

        public Crumb(string kind)
        {
            _kind = kind;
            Subtle(this);
            HorizontalContentAlignment = HorizontalAlignment.Stretch;
            var chevron = new FontIcon
            {
                Glyph = Glyphs.ChevronDown,
                FontSize = Fluent.Space3,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(Fluent.Space2, 0, 0, 0),
            };
            _words.Margin = new Thickness(Fluent.Space2, 0, 0, 0);

            var parts = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = GridLength.Auto },
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                    new ColumnDefinition { Width = GridLength.Auto },
                },
                Children = { _icon, _words, chevron },
            };
            Grid.SetColumn(_words, 1);
            Grid.SetColumn(chevron, 2);
            Content = parts;
        }

        public void Set(BarCrumb crumb)
        {
            _words.Text = crumb.Name;
            var key = crumb.Glyph + "|" + crumb.GlyphColour;
            if (!string.Equals(key, _iconKey, StringComparison.Ordinal))
            {
                _iconKey = key;
                _icon.Child = crumb.GlyphColour is { } colour
                    ? ViewMark.Icon(crumb.Glyph, colour)
                    : UiLanguage.Mirrored(new FontIcon { Glyph = crumb.Glyph, FontSize = Fluent.IconSize });
            }

            var said = Str.EditBar_CrumbName.Format(("kind", _kind), ("name", crumb.Detail ?? crumb.Name));
            ToolTipService.SetToolTip(this, said);
            AutomationProperties.SetName(this, said);
        }
    }

    /// <summary>
    /// [S-3507] The trail: Display, ›, View, ›, Widget, each crumb as wide as its words need up to the
    /// width <see cref="EditBarTrail.Widths"/> gives it, the trail always the width of all three at
    /// those widths, so nothing after it moves as the widget crumb comes and goes.
    /// </summary>
    private sealed partial class CrumbTrail : Panel
    {
        private double[] _widths = [.. EditBarTrail.Widest];

        public CrumbTrail(params UIElement[] parts)
        {
            foreach (var part in parts)
            {
                Children.Add(part);
            }
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            _widths = EditBarTrail.Widths(availableSize.Width);
            var crumb = 0;
            for (var i = 0; i < Children.Count; i++)
            {
                var child = Children[i];
                child.Measure(i % 2 == 0 ? new Size(_widths[crumb++], ButtonSize) : new Size(EditBarTrail.Between, ButtonSize));
            }

            return new Size(_widths.Sum() + ((_widths.Length - 1) * EditBarTrail.Between), ButtonSize);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var x = 0.0;
            for (var i = 0; i < Children.Count; i++)
            {
                var child = Children[i];

                // A › stands only between two crumbs shown: none before a widget crumb that is away.
                var shown = child.Visibility == Visibility.Visible && (i % 2 == 0 || (i + 1 < Children.Count && Children[i + 1].Visibility == Visibility.Visible));
                var width = !shown ? 0 : i % 2 == 0 ? child.DesiredSize.Width : EditBarTrail.Between;
                child.Arrange(new Rect(x, 0, width, finalSize.Height));
                x += width;
            }

            return finalSize;
        }
    }

    /// <summary>
    /// [S-3500] The bar's row: the trail, then the commands 6 DIPs apart (the hairlines carry their own
    /// 6 more). The commands take what they need; the trail has what is left of the room, at most its
    /// reserve ([S-3507]).
    /// </summary>
    private sealed partial class BarRow : Panel
    {
        public BarRow(params UIElement[] parts)
        {
            foreach (var part in parts)
            {
                Children.Add(part);
            }
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var commands = 0.0;
            for (var i = 1; i < Children.Count; i++)
            {
                Children[i].Measure(new Size(double.PositiveInfinity, ButtonSize));
                commands += Children[i].DesiredSize.Width + (i > 1 ? Gap : 0);
            }

            var room = double.IsFinite(availableSize.Width) ? availableSize.Width - commands - TrailGap : EditBarTrail.Reserve;
            Children[0].Measure(new Size(Math.Min(EditBarTrail.Reserve, Math.Max(EditBarTrail.Least, room)), ButtonSize));
            return new Size(Children[0].DesiredSize.Width + TrailGap + commands, ButtonSize);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var x = 0.0;
            for (var i = 0; i < Children.Count; i++)
            {
                var child = Children[i];
                child.Arrange(new Rect(x, 0, child.DesiredSize.Width, finalSize.Height));
                x += child.DesiredSize.Width + (i == 0 ? TrailGap : Gap);
            }

            return finalSize;
        }
    }
}
