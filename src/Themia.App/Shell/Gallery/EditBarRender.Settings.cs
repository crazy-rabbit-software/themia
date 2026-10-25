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

    private static Task SettingsShot(Grid screen, string path, RenderSettingsHost host, string[] open)
        => MenuShot(screen, path, BarMenu.Settings, () => host.Menu = new BarMenuView(nameof(BarMenu.Settings), () => SettingsMenu.Root(host), () => { }), open, null);

    /// <summary>A stand-in for the settings menu's host: a state of its own, Windows' side made up, every change drawn at once.</summary>
    private sealed class RenderSettingsHost : ISettingsMenuHost
    {
        public BarMenuView? Menu { get; set; }

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
