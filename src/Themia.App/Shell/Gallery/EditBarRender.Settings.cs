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
/// [S-2328] The General, Appearance and Accounts pages open from their entries on the stand-in display, over
/// the busy wallpaper: each page at its top (<c>page-&lt;theme&gt;-general.png</c>, <c>-appearance</c>), then opened at a
/// row as a deep link opens it, its picker open (<c>-appearance-style</c>, <c>-theme</c>, <c>-accent</c> (a colour),
/// <c>-background</c> (a fill), <c>-border</c>, <c>-font</c>, <c>-general-motion</c> (an expander), <c>-online-weather</c>,
/// <c>-language</c> (a lookup), <c>-language-find</c> ("deu" typed), <c>-link-temperature</c> (where a widget's link
/// lands), <c>-advice</c>, <c>-replace</c> (an "are you sure?" page), the assistants', Pro's, Back up's and About's,
/// and Accounts'. A stand-in host: nothing reaches Windows or the user's state.
/// </summary>
internal static partial class EditBarRender
{
    private static async Task SettingsShots(Grid screen, string outDir, string theme)
    {
        const string a = SettingsMenu.Keys.Appearance;
        (string Name, string? Key)[] cases =
        [
            ("general", null),
            ("general-end", null),
            ("appearance", null),
            ("appearance-style", a + "/style"),
            ("appearance-theme", a + "/theme/theme"),
            ("appearance-accent", a + "/theme/accent"),
            ("appearance-background", a + "/theme/background"),
            ("appearance-border", a + "/cards/border"),
            ("appearance-font", a + "/text/font"),
            ("general-motion", SettingsMenu.Keys.Motion + "/startup"),
            ("general-online-weather", SettingsMenu.Keys.Online + "/weather"),
            ("general-language", SettingsMenu.Keys.Language + "/display"),
            ("general-clock", SettingsMenu.Keys.Language + "/clock"),

            // [S-3749] Where a widget's link lands: the temperature unit's row, lit, as RequestSetting opens it.
            ("general-link-temperature", SettingsMenu.Keys.Format(Themia.Sdk.OwnedSetting.TemperatureUnit)),
        ];

        foreach (var (name, key) in cases)
        {
            await SettingsShot(screen, outDir, theme, name, new RenderSettingsHost(), key, after: name.EndsWith("-end", StringComparison.Ordinal) ? f => f.Scroll(Windows.System.VirtualKey.End) : null).ConfigureAwait(true);
        }

        // [S-2308] The languages' lookup finds a language as it is typed.
        await SettingsShot(screen, outDir, theme, "general-language-find", new RenderSettingsHost(), SettingsMenu.Keys.Language + "/display", view => view.DebugType("deu")).ConfigureAwait(true);

        // [S-3525] [S-3529] What holds against a setting is said: start with Windows turned off in
        // Windows (the advice under it), game mode holding the widgets, Windows' animations off.
        var advice = new RenderSettingsHost
        {
            Startup = new AutostartStatus(AutostartState.TurnedOffInWindows),
            Suspended = true,
            AnimationsEnabled = false,
        };
        advice.State.Preferences.GameModeSuspend = true;
        await SettingsShot(screen, outDir, theme, "general-advice", advice, null).ConfigureAwait(true);

        // [S-3527] A preset picked over a look of one's own asks first, on a page of its own.
        var custom = new RenderSettingsHost();
        custom.State.Design.CornerRadiusPx = 17;
        custom.State.Design.PrimaryColor = "#3A6EA5";
        await SettingsShot(screen, outDir, theme, "appearance-replace", custom, a + "/style", view => Click(view, a + "/style/" + DesignPresets.All[1].Id)).ConfigureAwait(true);

        // [S-9979] AI assistants: off (the default), listening with its setups, and a port taken.
        await SettingsShot(screen, outDir, theme, "general-assistants", new RenderSettingsHost(), SettingsMenu.Keys.McpServer).ConfigureAwait(true);
        var listening = new RenderSettingsHost { Mcp = new RenderMcpHost(new McpServerStatus(McpServerState.Listening, RenderMcpHost.Address)) };
        listening.State.Preferences.McpServer = true;
        await SettingsShot(screen, outDir, theme, "general-assistants-on", listening, SettingsMenu.Keys.McpServer).ConfigureAwait(true);
        var taken = new RenderSettingsHost { Mcp = new RenderMcpHost(new McpServerStatus(McpServerState.Failed, RenderMcpHost.Address, McpProblem.PortTaken)) };
        taken.State.Preferences.McpServer = true;
        await SettingsShot(screen, outDir, theme, "general-assistants-failed", taken, SettingsMenu.Keys.McpServer).ConfigureAwait(true);

        // [S-3560] Accounts over stand-in accounts.
        await AccountsShots(screen, outDir, theme).ConfigureAwait(true);

        // [S-3583] Themia Pro in each licence state, Back up & reset with its questions, About Themia.
        await ProShots(screen, outDir, theme).ConfigureAwait(true);
        await BackupShots(screen, outDir, theme).ConfigureAwait(true);
        await AboutShots(screen, outDir, theme).ConfigureAwait(true);
    }

    /// <summary>
    /// [S-2328] The page that holds <paramref name="key"/> (General without one) open at it over <paramref name="host"/>:
    /// <c>page-&lt;theme&gt;-&lt;name&gt;.png</c>.
    /// </summary>
    private static Task SettingsShot(Grid screen, string outDir, string theme, string name, RenderSettingsHost host, string? key, Action<PageView>? then = null, Action<PageFlyout>? after = null)
    {
        Func<SettingsPage>[] pages = [() => SettingsPages.General(host), () => SettingsPages.Accounts(host), () => SettingsPages.Appearance(host)];
        var core = key is null
            ? (name.StartsWith("appearance", StringComparison.Ordinal) ? pages[2] : name.StartsWith("account", StringComparison.Ordinal) ? pages[1] : pages[0])
            : pages.FirstOrDefault(p => SettingsPages.PathTo(p(), key) is not null) ?? pages[0];
        var page = core().Id;
        var view = new PageView(() => PageRows.Of(core(), edit => host.Page?.Ask(edit)));
        host.Page = view;
        return PageShot(screen, System.IO.Path.Combine(outDir, $"page-{theme}-{name}.png"), page, view, core, key, then, after);
    }

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
            Outcomes.Changed += () => Page?.Show();
        }

        public PageView? Page { get; set; }

        /// <summary>[S-3560] Stand-in accounts; what is typed or tried is drawn at once.</summary>
        public IAccountsMenuHost? Accounts
        {
            get => _accounts;
            init
            {
                _accounts = value;
                if (value is { } accounts)
                {
                    accounts.Menu.Changed += () => Page?.Show();
                }
            }
        }

        public AppState State { get; } = AppState.CreateDefault();

        public AutostartStatus? Startup { get; init; } = new(AutostartState.On);

        public bool Suspended { get; init; }

        public bool AnimationsEnabled { get; init; } = true;

        public void Mutate(Action<AppState> change, bool relayout = true)
        {
            change(State);
            Page?.Show();
        }

        public void SetStartup(bool on)
        {
            // ignore: a render never reaches Windows' startup apps.
        }

        // [S-5500] Stored in the render's own state only: a render never hides or shows the user's desktop icons.
        public void SetDesktop(DesktopShows shows) => Mutate(s => s.Preferences.Desktop = shows);

        public void SetGameModeSuspend(bool on) => Mutate(s => s.Preferences.GameModeSuspend = on);

        public void ReplayIntro(IntroMode mode)
        {
            // ignore: a render plays no intro.
        }

        public Core.Localization.Locale WindowsLanguage => Core.Localization.Locale.En;

        public void SetLanguage(AppLanguage language) => Mutate(s => s.Preferences.Language = language);

        public void Confirm(SettingsConfirm confirm) => Page?.Confirm(new PageQuestion(confirm.Question, confirm.Accept, confirm.Done, confirm.Danger, confirm.Detail));

        public void Quit()
        {
            // ignore: a render quits when it has drawn.
        }

        public ILicenceMenuHost Licence => this;

        public IBackupMenuHost Backup => this;

        public IAboutMenuHost About => this;

        /// <summary>[S-9979] A made-up server: nothing listens, and a copy reaches no clipboard.</summary>
        public IMcpMenuHost? Mcp { get; init; } = new RenderMcpHost(new McpServerStatus(McpServerState.Off));

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
            Page?.Show();
        }

        public void UninstallNow(bool deleteData)
        {
            // ignore: a render never uninstalls ([S-1555]).
        }
    }

    /// <summary>[S-9979] A stand-in server for ⚙ › AI assistants: its status given, its token made up and never shown.</summary>
    private sealed class RenderMcpHost(McpServerStatus status) : IMcpMenuHost
    {
        public static readonly Themia.Core.Mcp.McpAddress Address = new(48213, new string('x', 43));

        public McpServerStatus Status { get; } = status;

        public bool Copy(string text) => true;

        public void NewAddress()
        {
            // ignore: a render opens no socket.
        }
    }
}
#endif
