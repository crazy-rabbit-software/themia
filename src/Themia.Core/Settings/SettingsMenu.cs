using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Motion;
using Themia.Core.Shell;

namespace Themia.Core.Settings;

/// <summary>
/// [S-3524] What the settings menu reads and writes: the state (every write one <see cref="Mutate"/>,
/// as the settings pages wrote it), and what Windows owns, which the host reads and asks for.
/// </summary>
public interface ISettingsMenuHost
{
    /// <summary>The live state, read as it is now.</summary>
    AppState State { get; }

    /// <summary>One change of the state: saved, one undo step.</summary>
    void Mutate(Action<AppState> change, bool relayout = true);

    /// <summary>[S-935] What Windows said of start with Windows when it was last read; null until it has been.</summary>
    AutostartStatus? Startup { get; }

    /// <summary>[S-933] Ask Windows to start Themia at sign-in, or not; what it reached comes back as <see cref="Startup"/>.</summary>
    void SetStartup(bool on);

    /// <summary>[S-920] Game mode is holding the widgets now.</summary>
    bool Suspended { get; }

    /// <summary>[S-920] Turn game mode on or off.</summary>
    void SetGameModeSuspend(bool on);

    /// <summary>[S-1010] Windows' animation effects are on.</summary>
    bool AnimationsEnabled { get; }

    /// <summary>[S-1014] Play a moment's intro on the desktop.</summary>
    void ReplayIntro(IntroMode mode);

    /// <summary>[S-3536] The language Windows gives Themia: what "Windows setting" means for the display language.</summary>
    Locale WindowsLanguage { get; }

    /// <summary>[S-1109] [S-1110] Speak <paramref name="language"/>: stored, and the whole app switches at once.</summary>
    void SetLanguage(AppLanguage language);

    /// <summary>[S-3550] The accounts and what is done with them; null where there is no account service (a failed start).</summary>
    IAccountsMenuHost? Accounts { get; }

    /// <summary>[S-3570] The licence and the Store: Themia Pro ›.</summary>
    ILicenceMenuHost Licence { get; }

    /// <summary>[S-3575] The settings file: Back up &amp; reset ›.</summary>
    IBackupMenuHost Backup { get; }

    /// <summary>[S-3580] The app itself: About Themia ›.</summary>
    IAboutMenuHost About { get; }

    /// <summary>[S-3572] [S-3576] How what the lines of Themia Pro and Back up &amp; reset started went, under each.</summary>
    SettingsOutcomes Outcomes { get; }

    /// <summary>
    /// [S-2351] [S-3727] Ask, after a step of its own (a preset picked over a look of one's own), in place in the card of
    /// the line keyed <paramref name="at"/>; only the answer runs it. A line that always asks first says so itself
    /// (<see cref="SettingsCommand.Confirm"/>, <see cref="SettingsTool.Confirm"/>).
    /// </summary>
    void Confirm(string at, SettingsConfirm confirm);
}

/// <summary>
/// [S-3524] The settings' sections, one builder each in its own file (<c>SettingsMenu.&lt;Section&gt;.cs</c>);
/// the edit bar's pages group them ([S-2300], <see cref="SettingsPages"/>). A section with no lines
/// is not there: nothing stands empty or greyed for what is not built.
/// After Karpo's <c>KarpoMenu</c> (karpo-app, docs/widgets.md §335).
/// </summary>
public static partial class SettingsMenu
{
    /// <summary>
    /// The lines along <paramref name="path"/> (keys, first level first), as the menu would open
    /// them; null when a key on the way is not there. Tests and the deep links use it.
    /// </summary>
    public static SettingsLine? Find(IReadOnlyList<SettingsLine> lines, IReadOnlyList<string> path)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(path);

        SettingsLine? found = null;
        IReadOnlyList<SettingsLine> level = lines;
        foreach (var key in path)
        {
            found = level.FirstOrDefault(l => l.Key == key);
            if (found is null)
            {
                return null;
            }

            level = found switch
            {
                SettingsSubmenu submenu => submenu.Lines,
                SettingsChoice choice => choice.Options,
                _ => [],
            };
        }

        return found;
    }

    /// <summary>The menu's keys that something outside it opens (a deep link, a render).</summary>
    public static class Keys
    {
        public const string Startup = "startup";

        /// <summary>[S-5593] The desktop's icon size.</summary>
        public const string DesktopIconSize = "desktopIconSize";

        /// <summary>[S-13970] The desktop's icon layout: text below, text beside, icon only.</summary>
        public const string DesktopIconLayout = "desktopIconLayout";

        /// <summary>[S-12950] Lock widgets: outside edit mode only desktop icons move.</summary>
        public const string LockWidgets = "lockWidgets";
        public const string GameMode = "gameMode";
        public const string Appearance = "appearance";
        public const string Motion = "motion";
        public const string Language = "language";
        public const string Accounts = "accounts";

        /// <summary>[S-2312] "Your accounts", the section every account's card stands in.</summary>
        public const string YourAccounts = "accounts/yours";

        /// <summary>[S-2312] "Add an account", the section of the ways to add one.</summary>
        public const string AddAccount = "accounts/add";

        /// <summary>[S-2315] The General page's headings after its first: the desktop icons, Services, Themia, About.</summary>
        public const string DesktopHeading = "desktop";
        public const string ServicesHeading = "services";
        public const string ThemiaHeading = "themia";

        /// <summary>[S-2315] The regional formats' card under the language.</summary>
        public const string Formats = "language/formats";

        /// <summary>[S-2319] The Formats card's Windows region settings, and its Reset all.</summary>
        public const string FormatsRegion = "language/formats/region";
        public const string FormatsReset = "language/formats/reset";

        /// <summary>[S-2311] The Appearance page's live preview.</summary>
        public const string Preview = "appearance/preview";
        public const string Online = "online";
        public const string Pro = "pro";
        public const string ProUpgrade = "pro/upgrade";
        public const string ProRestore = "pro/restore";
        public const string Backup = "backup";
        public const string BackupExport = "backup/export";
        public const string BackupImport = "backup/import";
        public const string BackupOpen = "backup/open";
        public const string BackupReset = "backup/reset";
        public const string About = "about";
        public const string Uninstall = "uninstall";

        /// <summary>[S-3536] Language &amp; region's regional formats, each a line of the Formats card.</summary>
        public const string TimeFormat = Language + "/clock";
        public const string WeekStart = Language + "/weekStart";
        public const string Temperature = Language + "/temperature";
    }

    /// <summary>A whole number and its unit as the settings sliders wrote it ("12px", "500 ms"), or a percentage of a fraction.</summary>
    internal static string Unit(double value, string unit)
        => $"{LocaleFormat.Integer((long)Math.Round(value))}{unit}";

    /// <summary>A fraction as the language writes a percentage ("42 %" where it spaces it).</summary>
    internal static string Percent(double fraction) => LocaleFormat.Percent(Strings.Current, (int)Math.Round(fraction * 100));
}
