#if DEBUG
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Themia.App.Widgets;
using Themia.Core.Geometry;
using Themia.Core.Localization;
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
/// <c>-online</c>, <c>-online-weather</c>, <c>-language</c>, <c>-language-display</c>, <c>-language-clock</c>),
/// <c>-language-find</c> (the languages' box holding "deu"), <c>-advice</c> (Start with Windows turned off in
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
            ("-language", [SettingsMenu.Keys.Language]),
            ("-language-display", [SettingsMenu.Keys.Language, SettingsMenu.Keys.Language + "/display"]),
            ("-language-clock", [SettingsMenu.Keys.Language, SettingsMenu.Keys.Language + "/clock"]),
        ];

        foreach (var (name, open) in cases)
        {
            await SettingsShot(screen, System.IO.Path.Combine(outDir, $"settings-{theme}{name}.png"), new RenderSettingsHost(), open).ConfigureAwait(true);
        }

        // [S-3537] The languages' box finds a language as it is typed.
        await SettingsShot(screen, System.IO.Path.Combine(outDir, $"settings-{theme}-language-find.png"), new RenderSettingsHost(),
            [SettingsMenu.Keys.Language, SettingsMenu.Keys.Language + "/display"], menu => menu.DebugType("deu")).ConfigureAwait(true);

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

        // [S-3560] ⚙ › Accounts over stand-in accounts.
        await AccountsShots(screen, outDir, theme).ConfigureAwait(true);

        // [S-3583] ⚙ › Themia Pro in each licence state, Back up & reset with its questions, About Themia.
        await ProShots(screen, outDir, theme).ConfigureAwait(true);
        await BackupShots(screen, outDir, theme).ConfigureAwait(true);
        await AboutShots(screen, outDir, theme).ConfigureAwait(true);
    }

    private static Task SettingsShot(Grid screen, string path, RenderSettingsHost host, string[] open, Action<BarMenuView>? then = null)
        => MenuShot(screen, path, BarMenu.Settings, () => host.Menu = new BarMenuView(nameof(BarMenu.Settings), () => SettingsMenu.Root(host), () => { }), open, then);

    /// <summary>
    /// A stand-in for the settings menu's host: a state of its own, Windows' side made up, every change
    /// drawn at once; [S-3583] a licence, a settings file and an install of its own, so no Store, file
    /// dialog or uninstall is ever reached.
    /// </summary>
    private sealed class RenderSettingsHost : ISettingsMenuHost, ILicenceMenuHost, IBackupMenuHost, IAboutMenuHost
    {
        private readonly IAccountsMenuHost? _accounts;

        public RenderSettingsHost()
        {
            Accounts = new RenderAccountsHost();
            Outcomes.Changed += () => Menu?.Show();
        }

        public BarMenuView? Menu { get; set; }

        /// <summary>[S-3560] Stand-in accounts; what is typed or tried is drawn at once.</summary>
        public IAccountsMenuHost? Accounts
        {
            get => _accounts;
            init
            {
                _accounts = value;
                if (value is { } accounts)
                {
                    accounts.Menu.Changed += () => Menu?.Show();
                }
            }
        }

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

        public Core.Localization.Locale WindowsLanguage => Core.Localization.Locale.En;

        public void SetLanguage(AppLanguage language) => Mutate(s => s.Preferences.Language = language);

        public void Confirm(SettingsConfirm confirm) => Menu?.Confirm(confirm);

        public void Quit()
        {
            // ignore: a render quits when it has drawn.
        }

        public ILicenceMenuHost Licence => this;

        public IBackupMenuHost Backup => this;

        public IAboutMenuHost About => this;

        public SettingsOutcomes Outcomes { get; } = new();

        // ---- [S-3583] a made-up licence: a developer build unless a state is given ----------------

        public Core.Licensing.LicenseStatus Status { get; init; } = new(Core.Licensing.LicenseTier.Unrestricted, Core.Licensing.LicenseBasis.DeveloperBuild);

        public bool CanPurchase { get; init; }

        public string? Price { get; init; }

        public string? Refusal { get; init; }

        /// <summary>The Store's dialog never opens in a render: the line says it is waiting, as it would.</summary>
        public void Purchase() => Outcomes.Say(SettingsMenu.Keys.ProUpgrade, new MenuOutcome(Str.SettingsMenu_Purchasing.Text(), Busy: true));

        public void Restore() => Outcomes.Say(SettingsMenu.Keys.ProRestore, new MenuOutcome(Core.Licensing.LicenseText.Title(Status)));

        // ---- [S-3583] a made-up settings file ------------------------------------------------------

        public string StatePath => @"C:\Users\Alex\AppData\Roaming\Themia\state.json";

        public void Export()
        {
            // ignore: a render shows no Save dialog.
        }

        public void Import()
        {
            // ignore: a render shows no Open dialog; its question is drawn from a made-up file (BackupShots).
        }

        public void OpenFile()
        {
            // ignore: a render opens no file.
        }

        public void Reset()
        {
            // ignore: a render resets nothing.
        }

        // ---- [S-3583] a made-up install --------------------------------------------------------------

        public string Version => Services.AppInfo.Version;

        public string Runtime => Services.AppInfo.RuntimeLine;

        public void Open(Uri page)
        {
            // ignore: a render opens no page.
        }

        /// <summary>A Store install, or a loose one (a folder's copy).</summary>
        public bool Packaged { get; init; }

        public UninstallPlan Uninstall => UninstallPlan.Create(Packaged, deleteData: true, @"C:\Users\Alex\AppData\Roaming", @"C:\Users\Alex\AppData\Local", "Themia", Version);

        public bool DeleteData { get; set; }

        public void SetDeleteData(bool on)
        {
            DeleteData = on;
            Menu?.Show();
        }

        public void UninstallNow(bool deleteData)
        {
            // ignore: a render never uninstalls ([S-1555]).
        }
    }
}
#endif
