using System.Numerics;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;

namespace Themia.App.Shell;

/// <summary>What the edit bar's commands do, given by the surface (or a render's stand-in).</summary>
/// <param name="AddFlyout">[S-3501] The add menu, built anew each time the bar is built.</param>
/// <param name="FillViews">Refills the View menu each time it opens, with the views as they are then.</param>
/// <param name="SnapOn">[S-905] Whether grid snap is on now.</param>
/// <param name="SetSnap">Turns grid snap on or off.</param>
/// <param name="Settings">[S-3520] ⚙: opens the settings menu under it, or closes it again.</param>
/// <param name="MenuOpening">[S-3521] Another of the bar's menus (Add widget, View) is opening: the settings menu closes.</param>
/// <param name="Done">Leaves edit mode.</param>
internal sealed record EditBarCommands(
    Func<Flyout> AddFlyout,
    Action<MenuFlyout> FillViews,
    bool SnapOn,
    Action<bool> SetSnap,
    Action Settings,
    Action MenuOpening,
    Action Done);

/// <summary>
/// [S-3500] The edit bar: a compact Windows 11 command bar at the top centre of each display's work
/// area in edit mode, after Karpo's edit bar. Labelled commands for arranging first — Add widget (its
/// "+" in the accent), View, Snap — a hairline, then the icon-only ones whose words are their
/// tooltips: Settings (⚙, a drop-down: the settings menu under it, [S-3520]; Quit Themia is its last
/// line, [S-67]) and Done. The selected widgets' header, background and
/// title are not here but on the strip by the selection ([S-3707]). 40 DIPs tall: 32-DIP buttons in a 4-DIP padding
/// with 4 between them, no fill at rest and the subtle fills under the pointer and pressed, on the
/// flyouts' acrylic with their stroke, overlay corners and shadow.
/// <para>
/// Its own control so the debug render draws the real bar, and so a language switch rebuilds its
/// contents in place (<see cref="Fill"/>) without moving it in the surface's tree.
/// </para>
/// </summary>
internal sealed partial class EditBar : Grid
{
    /// <summary>The bar's frame; the grid around it carries the surface's margins.</summary>
    private readonly Border _frame = new() { Padding = new Thickness(Fluent.Space1) };

    /// <summary>[S-3500] A button's height and an icon-only button's side.</summary>
    public const double ButtonSize = 32;

    /// <summary>[S-3500] The hairline between the arranging commands and the rest.</summary>
    private const double DividerHeight = 20;

    private ToggleButton? _snap;

    /// <summary>[S-3520] ⚙, the settings menu's button.</summary>
    private Button? _settings;

    /// <summary>[S-3520] The settings menu is open under ⚙: the button stays lit, as a drop-down's does while its menu is open.</summary>
    private bool _settingsOpen;

    /// <summary>[S-918] The View menu, and the most it may be tall (the work area below the bar).</summary>
    private MenuFlyout? _viewMenu;

    private double _viewMenuHeight = double.PositiveInfinity;

    public EditBar(EditBarCommands commands)
    {
        ChromeFrame.Apply(_frame);
        Children.Add(_frame);

        // A press on the bar's padding is the bar's: it must not reach the
        // surface, whose press on empty space starts a rubber band and clears the selection.
        _frame.PointerPressed += (_, e) => e.Handled = true;
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Top;
        Fill(commands);
    }

    /// <summary>
    /// [S-918] Cap the View menu at <paramref name="maxHeight"/>, so a long list of views scrolls
    /// inside it. Set ahead of the menu opening (on a refresh of the surface), never while it is open.
    /// </summary>
    public void CapViewMenu(double maxHeight)
    {
        if (Math.Abs(maxHeight - _viewMenuHeight) < 1 || _viewMenu is { IsOpen: true })
        {
            return;
        }

        _viewMenuHeight = maxHeight;
        if (_viewMenu is { } menu)
        {
            menu.MenuFlyoutPresenterStyle = ViewBar.MenuStyle(maxHeight);
        }
    }

    /// <summary>Build the buttons, in the current language. Called again on a language switch.</summary>
    public void Fill(EditBarCommands commands)
    {
        ArgumentNullException.ThrowIfNull(commands);

        var addFlyout = commands.AddFlyout();
        addFlyout.Opening += (_, _) => commands.MenuOpening();
        var add = new DropDownButton { Content = Labelled(Glyphs.Add, Str.Shell_Toolbar_AddWidget.Text(), accent: true), Flyout = addFlyout };
        Subtle(add);
        AutomationProperties.SetName(add, Str.Shell_Toolbar_AddWidget.Text());

        // Refilled every time it opens. Built once, it kept listing the views that existed when the
        // surface was created and ticking the one that was active then.
        var viewMenu = new MenuFlyout();
        viewMenu.Opening += (_, _) =>
        {
            commands.MenuOpening();
            commands.FillViews(viewMenu);
        };
        _viewMenu = viewMenu;
        if (double.IsFinite(_viewMenuHeight))
        {
            viewMenu.MenuFlyoutPresenterStyle = ViewBar.MenuStyle(_viewMenuHeight);
        }
        var views = new DropDownButton { Content = Labelled(Glyphs.View, Str.CtxMenu_View.Text(), accent: false), Flyout = viewMenu };
        Subtle(views);
        AutomationProperties.SetName(views, Str.CtxMenu_View.Text());

        _snap = SnapToggle(commands);

        // [S-3520] ⚙ drops the settings menu down, icon-only as Windows' own "More" is; [S-67] its
        // last line is Quit Themia, behind a second click and never the button beside Done.
        var settings = new Button { Content = Fluent.Icon(Glyphs.Settings, "TextFillColorPrimaryBrush") };
        IconOnly(settings, Str.Common_Settings.Text());
        settings.Click += (_, _) => commands.Settings();
        _settings = settings;
        SettingsOpen(_settingsOpen);

        var done = new Button { Content = Fluent.Icon(Glyphs.Done, "TextFillColorPrimaryBrush") };
        IconOnly(done, Str.Common_Done.Text());
        done.Click += (_, _) => commands.Done();

        _frame.Child = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = Fluent.Space1,
            Children = { add, views, _snap, Divider(), settings, done },
        };
    }

    /// <summary>The Add widget button's width, for the render to hang the menu under it.</summary>
    internal double AddWidth => _frame.Child is StackPanel { Children.Count: > 0 } row ? row.Children[0].DesiredSize.Width : 0;

    /// <summary>[S-905] The snap switch follows a change made elsewhere (Settings, the surface menu, an import).</summary>
    public void SyncSnap(bool on)
    {
        if (_snap is not null && _snap.IsChecked != on)
        {
            _snap.IsChecked = on;
        }
    }

    /// <summary>
    /// [S-905] Grid snap, on the bar as in the original: the one setting reached for in the middle of
    /// arranging widgets. Windows lights a toggle that is on with the accent.
    /// </summary>
    private static ToggleButton SnapToggle(EditBarCommands commands)
    {
        var toggle = new ToggleButton { IsChecked = commands.SnapOn, Content = Labelled(Glyphs.Grid, Str.Shell_Toolbar_Snap.Text(), accent: false) };
        Subtle(toggle);
        Lit(toggle);

        ToolTipService.SetToolTip(toggle, Str.Shell_Toolbar_SnapTip.Text());
        AutomationProperties.SetName(toggle, Str.Shell_Toolbar_SnapName.Text());
        toggle.Click += (_, _) => commands.SetSnap(toggle.IsChecked == true);
        return toggle;
    }

    /// <summary>
    /// [S-905] [S-6584] A toggle that is on lit with the accent, as Windows lights one: the snap
    /// toggle, the properties panel's alignment. Through the template's resource keys, as <see cref="Subtle"/>.
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

    /// <summary>[S-3520] ⚙ lit while the settings menu is open under it, as a drop-down's button stays while its menu is.</summary>
    public void SettingsOpen(bool open)
    {
        _settingsOpen = open;
        if (_settings is not { } button)
        {
            return;
        }

        if (open)
        {
            button.Background = ThemeBrush.Of("SubtleFillColorSecondaryBrush", Colors.Gray);
        }
        else
        {
            button.ClearValue(Control.BackgroundProperty);
        }
    }

    /// <summary>[S-3521] The keyboard back on ⚙ (the settings menu closed from the keyboard).</summary>
    public void FocusSettings() => _settings?.Focus(FocusState.Keyboard);

    /// <summary>[S-3521] Whether <paramref name="node"/> is ⚙: a press on it toggles the menu rather than closing it.</summary>
    public bool IsSettingsButton(DependencyObject node) => _settings is not null && ReferenceEquals(node, _settings);

    /// <summary>[S-3522] Where ⚙ is, in <paramref name="layer"/>'s coordinates; null before it has been laid out.</summary>
    public Themia.Core.Geometry.PixelRect? SettingsBox(UIElement layer)
    {
        if (_settings is not { ActualWidth: > 0 } button)
        {
            return null;
        }

        var box = button.TransformToVisual(layer).TransformBounds(new Windows.Foundation.Rect(0, 0, button.ActualWidth, button.ActualHeight));
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
            icon.RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5);
            icon.RenderTransform = new RotateTransform { Angle = 180 };
        }

        return icon;
    }

    /// <summary>A labelled command's content: a 16-DIP glyph, 8 DIPs, then its words in Body.</summary>
    internal static StackPanel Labelled(string glyph, string text, bool accent)
    {
        var words = Fluent.Text(FluentType.Body);
        words.Text = text;
        words.ClearValue(TextBlock.ForegroundProperty); // the button's own, which a checked toggle turns to on-accent
        words.VerticalAlignment = VerticalAlignment.Center;

        var icon = UiLanguage.Mirrored(new FontIcon { Glyph = glyph, FontSize = Fluent.IconSize, VerticalAlignment = VerticalAlignment.Center });
        if (accent)
        {
            icon.Foreground = ThemeBrush.Of("AccentTextFillColorPrimaryBrush", Colors.DodgerBlue);
        }

        return new StackPanel { Orientation = Orientation.Horizontal, Spacing = Fluent.Space2, Children = { icon, words } };
    }

    /// <summary>An icon-only command: square, its words the tooltip and the name a screen reader reads.</summary>
    internal static void IconOnly(Button button, string words)
    {
        Subtle(button);
        button.Width = ButtonSize;
        button.Padding = new Thickness(0);
        button.HorizontalContentAlignment = HorizontalAlignment.Center;
        ToolTipService.SetToolTip(button, words);
        AutomationProperties.SetName(button, words);
    }

    /// <summary>
    /// [S-3500] No fill and no stroke at rest, the subtle fills under the pointer and pressed. Through
    /// the template's own resource keys: the visual states set those fills on the inner border and
    /// would outrank a local Background.
    /// </summary>
    internal static void Subtle(ButtonBase button)
    {
        button.Height = ButtonSize;
        button.MinWidth = 0;
        button.Padding = new Thickness(Fluent.Space3, 0, Fluent.Space3, 0);
        button.BorderThickness = new Thickness(0);
        button.CornerRadius = new CornerRadius(Fluent.ControlRadius);
        button.VerticalAlignment = VerticalAlignment.Center;

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

    private static Rectangle Divider() => new()
    {
        Width = Fluent.Hairline,
        Height = DividerHeight,
        Margin = new Thickness(Fluent.Space1, 0, Fluent.Space1, 0),
        VerticalAlignment = VerticalAlignment.Center,
        Fill = ThemeBrush.Of("DividerStrokeColorDefaultBrush", Colors.Gray),
    };
}
