#if DEBUG
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Themia.App.Widgets;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Motion;
using Themia.Core.Settings;
using Themia.Core.Shell;
using Windows.Foundation;

namespace Themia.App.Shell.Gallery;

/// <summary>
/// [S-3535] The edit bar's settings menu open under ⚙ on the stand-in display, over the busy
/// wallpaper: <c>settings-&lt;theme&gt;.png</c> (its first level), then its submenus opened along their
/// keys (<c>-appearance</c>, <c>-style</c>, <c>-theme</c>, <c>-accent</c> (a colour submenu),
/// <c>-background</c> (a fill's), <c>-items</c>, <c>-cards</c>, <c>-border</c>, <c>-text</c>,
/// <c>-font</c> (the font picker), <c>-layout</c>, <c>-motion</c>, <c>-motion-startup</c>,
/// <c>-online</c>, <c>-online-weather</c>), <c>-advice</c> (Start with Windows turned off in
/// Windows, game mode holding the widgets, Windows' animations off) and <c>-replace</c> (a preset
/// picked over a look of one's own: the question beside the menu, its <c>-popup0</c>). A stand-in
/// host: nothing reaches Windows or the user's state.
/// </summary>
internal static partial class EditBarRender
{
    private static async Task SettingsShots(Grid screen, string outDir, string theme)
    {
        const string a = SettingsMenu.Keys.Appearance;
        (string Name, string[] Open)[] cases =
        [
            (string.Empty, []),
            ("-appearance", [a]),
            ("-style", [a, a + "/style"]),
            ("-theme", [a, a + "/theme"]),
            ("-accent", [a, a + "/theme", a + "/theme/accent"]),
            ("-background", [a, a + "/theme", a + "/theme/background"]),
            ("-items", [a, a + "/items"]),
            ("-cards", [a, a + "/cards"]),
            ("-border", [a, a + "/cards", a + "/cards/border"]),
            ("-text", [a, a + "/text"]),
            ("-font", [a, a + "/text", a + "/text/font"]),
            ("-layout", [a, a + "/layout"]),
            ("-motion", [a, a + "/motion"]),
            ("-motion-startup", [a, a + "/motion", a + "/motion/startup"]),
            ("-online", [SettingsMenu.Keys.Online]),
            ("-online-weather", [SettingsMenu.Keys.Online, SettingsMenu.Keys.Online + "/weather"]),
        ];

        foreach (var (name, open) in cases)
        {
            await SettingsShot(screen, System.IO.Path.Combine(outDir, $"settings-{theme}{name}.png"), new RenderSettingsHost(), open).ConfigureAwait(true);
        }

        // [S-3525] [S-3529] What holds against a setting is said: start with Windows turned off in
        // Windows (the advice under it), game mode holding the widgets, Windows' animations off.
        var advice = new RenderSettingsHost
        {
            Startup = new AutostartStatus(AutostartState.TurnedOffInWindows),
            Suspended = true,
            AnimationsEnabled = false,
        };
        advice.State.Preferences.GameModeSuspend = true;
        await SettingsShot(screen, System.IO.Path.Combine(outDir, $"settings-{theme}-advice.png"), advice, [a, a + "/motion"]).ConfigureAwait(true);

        // [S-3527] A preset picked over a look of one's own asks beside the menu first.
        var custom = new RenderSettingsHost();
        custom.State.Design.CornerRadiusPx = 17;
        custom.State.Design.PrimaryColor = "#3A6EA5";
        ConfirmFlyout.RenderInWindow = true;
        await SettingsShot(screen, System.IO.Path.Combine(outDir, $"settings-{theme}-replace.png"), custom, [a, a + "/style", a + "/style/" + DesignPresets.All[1].Id]).ConfigureAwait(true);
    }

    private static async Task SettingsShot(Grid screen, string path, RenderSettingsHost host, string[] open)
    {
        screen.Children.Clear();
        screen.Height = OpenHeight;
        var flat = screen.Background;
        screen.Background = Wallpaper();

        var canvas = new Canvas { Clip = new RectangleGeometry { Rect = new Rect(0, 0, ScreenWidth, screen.Height) } };
        screen.Children.Add(canvas);
        Blobs(canvas);

        // Laid out before it is measured: the first picture's bar was measured before its words were.
        var bar = Bar(snapOn: true);
        canvas.Children.Add(bar);
        screen.UpdateLayout();
        bar.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var barRect = new PixelRect(Math.Round((ScreenWidth - bar.DesiredSize.Width) / 2), Fluent.Space2, bar.DesiredSize.Width, bar.DesiredSize.Height);
        Canvas.SetLeft(bar, barRect.X);
        Canvas.SetTop(bar, barRect.Y);

        var menu = new SettingsMenuView(host, () => { });
        host.Menu = menu;
        UiLanguage.ApplyFlow(menu);
        menu.Visibility = Visibility.Visible;
        canvas.Children.Add(menu);
        menu.Show();
        bar.SettingsOpen(true);
        screen.UpdateLayout();

        // [S-3522] Under ⚙, as the surface places it.
        var area = new PixelRect(0, 0, ScreenWidth, screen.Height);
        var gear = bar.SettingsBox(canvas) ?? barRect;
        menu.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var (x, y) = DropDownPlacement.Place(gear, barRect, menu.DesiredSize.Width, menu.DesiredSize.Height, area, [barRect], UiLanguage.Flow == FlowDirection.RightToLeft);
        Canvas.SetLeft(menu, Math.Round(x));
        Canvas.SetTop(menu, Math.Round(y));
        menu.Room = new MenuRoom(area, null, [barRect]);
        screen.UpdateLayout();

        if (open.Length > 0 && !menu.Open(open, focus: false))
        {
            Console.WriteLine($"editbar: no settings submenu {string.Join('/', open)} for {path}");
        }

        await Task.Delay(800).ConfigureAwait(true);
        await RenderPng.SaveAsync(screen, path).ConfigureAwait(true);
        Console.WriteLine($"editbar: wrote {path}");
        screen.Background = flat;
        await SavePopups(screen, path).ConfigureAwait(true);
    }

    /// <summary>A stand-in for the settings menu's host: a state of its own, Windows' side made up, every change drawn at once.</summary>
    private sealed class RenderSettingsHost : ISettingsMenuHost
    {
        public SettingsMenuView? Menu { get; set; }

        public AppState State { get; } = AppState.CreateDefault();

        public AutostartStatus? Startup { get; init; } = new(AutostartState.On);

        public bool DesktopIconsShown => true;

        public bool Suspended { get; init; }

        public bool AnimationsEnabled { get; init; } = true;

        public void Mutate(Action<AppState> change, bool relayout = true)
        {
            change(State);
            Menu?.Show();
        }

        public void SetStartup(bool on)
        {
            // ignore: a render never reaches Windows' startup apps.
        }

        public void SetDesktopIcons(bool shown)
        {
            // ignore: a render never shows or hides the user's desktop icons.
        }

        public void SetGameModeSuspend(bool on) => Mutate(s => s.Preferences.GameModeSuspend = on);

        public void ReplayIntro(IntroMode mode)
        {
            // ignore: a render plays no intro.
        }

        public void Confirm(SettingsConfirm confirm) => Menu?.Confirm(confirm);

        public void ShowAllSettings()
        {
            // ignore: a render opens no window.
        }

        public void Quit()
        {
            // ignore: a render quits when it has drawn.
        }
    }
}
#endif
