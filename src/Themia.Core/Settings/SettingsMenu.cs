using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Motion;
using Themia.Core.Shell;

namespace Themia.Core.Settings;

/// <summary>
/// [S-3524] One line of the edit bar's settings menu, as data: what it shows and what a pick does.
/// The app draws every one with the menu engine the properties menu is drawn with ([S-3716]); the
/// rules (which lines a section has, their choices, their words, what each writes) are here, so they
/// are tested without a window. <see cref="Key"/> names the line across rebuilds and languages
/// (a path: "appearance/cards/radius"), so an open submenu stays open through a change of words.
/// </summary>
public abstract record SettingsLine(string Key)
{
    /// <summary>Shown but not clickable.</summary>
    public bool Disabled { get; init; }

    /// <summary>[S-3810] The colour its glyph is drawn in (<c>#RRGGBB</c>: a view's mark); null for the theme's.</summary>
    public string? GlyphColour { get; init; }

    /// <summary>[S-3581] A picture in its icon column instead of a glyph (the app's mark, the studio's).</summary>
    public MenuPicture? Picture { get; init; }
}

/// <summary>A divider between groups; leading, trailing and doubled ones are dropped.</summary>
public sealed record SettingsSeparator(string Key) : SettingsLine(Key);

/// <summary>[S-3740] A heading over a group (Karpo's): its words in the 12-DIP semibold, never picked.</summary>
public sealed record SettingsHeading(string Key, string Text) : SettingsLine(Key);

/// <summary>[S-3728] Words only, greyed, never clickable; nothing at all while <see cref="Text"/> is empty.</summary>
public sealed record SettingsNote(string Key, string Text) : SettingsLine(Key)
{
    /// <summary>[S-3561] What went wrong (a try's refusal): in the critical colour.</summary>
    public bool Critical { get; init; }

    /// <summary>[S-3561] Something is being done (a sign-in waiting): a ring before the words.</summary>
    public bool Busy { get; init; }
}

/// <summary>
/// [S-3561] A reading: words and a value at the line's end in the text colour, never clickable (what
/// an account holds on this PC: "Mail · 1,240 messages").
/// </summary>
public sealed record SettingsReading(string Key, string Text, string? Glyph, string? Value) : SettingsLine(Key);

/// <summary>
/// [S-3561] [S-3736] A line of text typed in the menu itself (a form's field): a row of its words and
/// <see cref="Value"/>, whose level is the field with its hint, and what a change does (each letter, as it is typed).
/// A <see cref="Masked"/> field is a password's ([S-3556]): it never carries what it holds
/// (<see cref="Value"/> is null; <see cref="HasValue"/> says whether there is one), <see cref="Fill"/>
/// gives a box built again what was typed, and <see cref="Wipe"/> runs as its submenu closes.
/// </summary>
public sealed record SettingsField(string Key, string Text, string? Value, string Placeholder, Action<string> Set) : SettingsLine(Key)
{
    public bool Masked { get; init; }

    /// <summary>[S-3556] A masked field holds something (its box keeps it while it is drawn).</summary>
    public bool HasValue { get; init; }

    /// <summary>[S-3556] What a masked box built again starts with; null for nothing.</summary>
    public Func<string>? Fill { get; init; }

    /// <summary>[S-3556] [S-3557] Its form's level closed: what it held is wiped.</summary>
    public Action? Wipe { get; init; }

    /// <summary>A number (a port): the touch keyboard shows digits.</summary>
    public bool Numeric { get; init; }
}

/// <summary>A switch: ticked while on; a click turns it the other way. <see cref="Value"/> at its end (a state Windows reports).</summary>
public sealed record SettingsToggle(string Key, string Text, string? Glyph, bool On, Action<bool> Set) : SettingsLine(Key)
{
    public string? Value { get; init; }
}

/// <summary>[S-3718] One option of a choice of one: the dot while it holds. Picking the one that holds changes nothing.</summary>
public sealed record SettingsRadio(string Key, string Text, bool Holds, Action Pick) : SettingsLine(Key)
{
    /// <summary>Its glyph (a view's), or none.</summary>
    public string? Glyph { get; init; }

    /// <summary>[S-3537] Words its choice's box finds it by besides its own (a language's English name and code).</summary>
    public string? Keywords { get; init; }
}

/// <summary>[S-3718] A choice of one: a line whose submenu is its options, the one that holds at its end.</summary>
public sealed record SettingsChoice(string Key, string Text, string? Glyph, IReadOnlyList<SettingsRadio> Options) : SettingsLine(Key)
{
    private readonly string? _value;

    /// <summary>
    /// What the line says at its end: the option that holds, or what it is set to say in its place
    /// ([S-3536]: what a "Windows setting (…)" option gives, which fits beside the line's words).
    /// </summary>
    public string? Value
    {
        get => _value ?? Options.FirstOrDefault(o => o.Holds)?.Text;
        init => _value = value;
    }

    /// <summary>
    /// [S-3537] The hint of a box on top of its submenu that keeps the options whose words or
    /// keywords hold what is typed (<see cref="SettingsMenu.Matches"/>); null for a plain choice.
    /// </summary>
    public string? Filter { get; init; }
}

/// <summary>[S-3731] A number set by feel: an inline slider, stored once on the release.</summary>
public sealed record SettingsSlider(string Key, string Text, string? Glyph, double Value, double Min, double Max, double Step, Func<double, string> Format, Action<double> Set) : SettingsLine(Key);

/// <summary>
/// [S-3724] A colour (<c>#RRGGBB</c>, empty for the default): the one colour submenu. <see cref="DefaultText"/>
/// is Default's words; <see cref="DefaultIsWindowsAccent"/> paints Default's swatch in the Windows accent.
/// </summary>
public sealed record SettingsColour(string Key, string Text, string Current, Action<string> Set, string DefaultText, IReadOnlyList<ColorPreset> Presets) : SettingsLine(Key)
{
    public bool DefaultIsWindowsAccent { get; init; }
}

/// <summary>[S-3724] A fill (<see cref="FillChoice"/>): the one colour submenu with Transparent and Opacity.</summary>
public sealed record SettingsFill(string Key, string Text, FillChoice Current, Action<FillChoice> Set, string DefaultText) : SettingsLine(Key);

/// <summary>[S-3528] A font: its submenu is the font picker over every installed family, then Add fonts.</summary>
public sealed record SettingsFont(string Key, string Text, string? Glyph, string Current, Action<string> Set) : SettingsLine(Key)
{
    /// <summary>What the line says at its end: the chosen font's name ([S-6441]).</summary>
    public string Value => SettingsMenu.FontName(Current);
}

/// <summary>A submenu of <see cref="Lines"/>, <see cref="Value"/> at its end.</summary>
public sealed record SettingsSubmenu(string Key, string Text, string? Glyph, IReadOnlyList<SettingsLine> Lines) : SettingsLine(Key)
{
    public string? Value { get; init; }

    /// <summary>[S-3541] The item it stands for is the one that holds (the view this display shows): the dot before its chevron.</summary>
    public bool Holds { get; init; }

    /// <summary>[S-3541] What a click does besides opening it (show the view here); null for a submenu that only opens.</summary>
    public Action? Run { get; init; }

    /// <summary>[S-3732] An item of an ordered list: its line is dragged up and down among the list's.</summary>
    public SettingsOrder? Order { get; init; }
}

/// <summary>
/// [S-3732] A line's place in an ordered list (the views): where it is, how many there are, and what
/// a drop or Alt+Shift+Up / Down does (move the item at the first index to the second, and save once).
/// </summary>
public sealed record SettingsOrder(int Index, int Count, Action<int, int> Move);

/// <summary>
/// [S-3721] [S-3736] A line that asks for a line of text on a level of its own (Rename…): the level's
/// name, the field's hint, the words of the row that gives it, what the field starts with, why a typed
/// text cannot be used (null when it can) and what is done with it.
/// </summary>
public sealed record SettingsAsk(string Key, string Text, string? Glyph, string Title, string Placeholder, string Accept, string Initial, Func<string, string?> Refuse, Action<string> Done) : SettingsLine(Key)
{
    /// <summary>[S-3551] An empty box may be given (a nickname cleared back to the account's own name).</summary>
    public bool AllowEmpty { get; init; }

    /// <summary>What the line says at its end (a nickname).</summary>
    public string? Value { get; init; }
}

/// <summary>
/// [S-3542] An icon (one glyph, or none for the default): its submenu is the icon picker over every
/// Segoe Fluent glyph, its default cell <see cref="DefaultText"/> (a view's initials) named
/// <see cref="DefaultName"/>. <see cref="Glyph"/> is the line's own glyph (the icon that holds).
/// </summary>
public sealed record SettingsIcon(string Key, string Text, string? Glyph, string? Current, string DefaultText, string DefaultName, Action<string?> Set) : SettingsLine(Key);

/// <summary>A command: a click does it, and the menu stays.</summary>
public sealed record SettingsCommand(string Key, string Text, string? Glyph, Action Run) : SettingsLine(Key)
{
    /// <summary>[S-3716] It destroys something (Delete view): drawn in the critical colour.</summary>
    public bool Danger { get; init; }

    /// <summary>[S-3580] What it says at its end (the site a link opens: "lometics.com").</summary>
    public string? Value { get; init; }
}

/// <summary>
/// [S-3749] A link row (Karpo's): a setting another menu owns, never a copy of it. The owner's glyph
/// and words, the owner's name (<paramref name="Owner"/>: the tooltip's "Opens Accounts"), and what a
/// pick does: close this menu and open the owner's at the level that sets it. Drawn with Windows'
/// "open in new window" mark at its end, no chevron.
/// </summary>
public sealed record SettingsLink(string Key, string Text, string? Glyph, string Owner, Action Open) : SettingsLine(Key);

/// <summary>
/// [S-3727] [S-3737] An "are you sure?" asked on a level of the menu's own: the question, the words of
/// the row that does it (in the critical colour where <paramref name="Danger"/>), what it does, and
/// what goes with it.
/// </summary>
public sealed record SettingsConfirm(string Question, string Accept, Action Done, string? Detail = null, bool Danger = false);

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

    /// <summary>[S-930] Whether Windows' desktop icons show, as Windows says now (the stored cache where it cannot be read).</summary>
    bool DesktopIconsShown { get; }

    /// <summary>[S-930] Ask Windows to show or hide its desktop icons.</summary>
    void SetDesktopIcons(bool shown);

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

    /// <summary>[S-3727] Ask beside the menu; only the answer runs it.</summary>
    void Confirm(SettingsConfirm confirm);

    /// <summary>[S-3532] Quit Themia on every display.</summary>
    void Quit();
}

/// <summary>
/// [S-3524] The edit bar's settings menu (⚙): its sections, one builder each in its own file
/// (<c>SettingsMenu.&lt;Section&gt;.cs</c>), in bands with a divider between them. A section with no
/// lines is not there, nor a band with none: nothing stands empty or greyed for what is not built.
/// After Karpo's <c>KarpoMenu</c> (karpo-app, docs/widgets.md §335).
/// </summary>
public static partial class SettingsMenu
{
    /// <summary>
    /// [S-3524] The first level: General's lines; Appearance, Accounts, Language &amp; region and Online
    /// updates; Themia Pro, Back up &amp; reset and About Themia; then Quit Themia.
    /// </summary>
    public static IReadOnlyList<SettingsLine> Root(ISettingsMenuHost host)
    {
        ArgumentNullException.ThrowIfNull(host);

        IReadOnlyList<SettingsLine>[] bands =
        [
            General(host),
            [Appearance(host), Accounts(host), Language(host), OnlineUpdates(host)],
            [Pro(host), Backup(host), About(host)],
            [new SettingsCommand(Keys.Quit, Str.Settings_Quit_Label.Text(), Glyphs.Power, host.Quit)],
        ];

        var lines = new List<SettingsLine>();
        foreach (var band in bands.Where(b => b.Count > 0))
        {
            if (lines.Count > 0)
            {
                lines.Add(new SettingsSeparator("-" + band[0].Key));
            }

            lines.AddRange(band);
        }

        return lines;
    }

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
        public const string DesktopIcons = "desktopIcons";
        public const string GameMode = "gameMode";
        public const string Appearance = "appearance";
        public const string Language = "language";
        public const string Accounts = "accounts";
        public const string AddAccount = "accounts/add";
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
        public const string Uninstall = "about/uninstall";
        public const string Quit = "quit";
    }

    /// <summary>A whole number and its unit as the settings sliders wrote it ("12px", "500 ms"), or a percentage of a fraction.</summary>
    internal static string Unit(double value, string unit)
        => $"{LocaleFormat.Integer((long)Math.Round(value))}{unit}";

    /// <summary>A fraction as the language writes a percentage ("42 %" where it spaces it).</summary>
    internal static string Percent(double fraction) => LocaleFormat.Percent(Strings.Current, (int)Math.Round(fraction * 100));
}
