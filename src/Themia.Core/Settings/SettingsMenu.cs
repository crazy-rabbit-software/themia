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
}

/// <summary>A divider between groups; leading, trailing and doubled ones are dropped.</summary>
public sealed record SettingsSeparator(string Key) : SettingsLine(Key);

/// <summary>[S-3728] Words only, greyed, never clickable.</summary>
public sealed record SettingsNote(string Key, string Text) : SettingsLine(Key);

/// <summary>A switch: ticked while on; a click turns it the other way. <see cref="Value"/> at its end (a state Windows reports).</summary>
public sealed record SettingsToggle(string Key, string Text, string? Glyph, bool On, Action<bool> Set) : SettingsLine(Key)
{
    public string? Value { get; init; }
}

/// <summary>[S-3718] One option of a choice of one: the dot while it holds. Picking the one that holds changes nothing.</summary>
public sealed record SettingsRadio(string Key, string Text, bool Holds, Action Pick) : SettingsLine(Key);

/// <summary>[S-3718] A choice of one: a line whose submenu is its options, the one that holds at its end.</summary>
public sealed record SettingsChoice(string Key, string Text, string? Glyph, IReadOnlyList<SettingsRadio> Options) : SettingsLine(Key)
{
    /// <summary>What the line says at its end: the option that holds.</summary>
    public string? Value => Options.FirstOrDefault(o => o.Holds)?.Text;
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
}

/// <summary>A command: a click does it, and the menu stays.</summary>
public sealed record SettingsCommand(string Key, string Text, string? Glyph, Action Run) : SettingsLine(Key);

/// <summary>[S-3727] An "are you sure?" asked beside the menu: the question, the words of the button that does it, what it does, and what goes with it.</summary>
public sealed record SettingsConfirm(string Question, string Accept, Action Done, string? Detail = null);

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

    /// <summary>[S-3727] Ask beside the menu; only the answer runs it.</summary>
    void Confirm(SettingsConfirm confirm);

    /// <summary>[S-3533] The settings window, on its first page (temporary: the window goes in the last step of the move).</summary>
    void ShowAllSettings();

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
    /// <summary>[S-3524] The first level: General's lines; Appearance and Online updates; then the way out.</summary>
    public static IReadOnlyList<SettingsLine> Root(ISettingsMenuHost host)
    {
        ArgumentNullException.ThrowIfNull(host);

        IReadOnlyList<SettingsLine>[] bands =
        [
            General(host),
            [Appearance(host), OnlineUpdates(host)],
            Leave(host),
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
    /// [S-3532] [S-3533] The last band: All settings… (temporary, until the settings window's last
    /// pages have moved here too), then Quit Themia, the menu's last line.
    /// </summary>
    private static IReadOnlyList<SettingsLine> Leave(ISettingsMenuHost host) =>
    [
        // TEMPORARY (removed when the settings window goes): its pages not moved yet.
        new SettingsCommand(Keys.AllSettings, Str.SettingsMenu_AllSettings.Text(), Glyphs.Settings, host.ShowAllSettings),
        new SettingsCommand(Keys.Quit, Str.Settings_Quit_Label.Text(), Glyphs.Power, host.Quit),
    ];

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
        public const string Online = "online";
        public const string AllSettings = "allSettings";
        public const string Quit = "quit";
    }

    /// <summary>A whole number and its unit as the settings sliders wrote it ("12px", "500 ms"), or a percentage of a fraction.</summary>
    internal static string Unit(double value, string unit)
        => $"{LocaleFormat.Integer((long)Math.Round(value))}{unit}";

    /// <summary>A fraction as the language writes a percentage ("42 %" where it spaces it).</summary>
    internal static string Percent(double fraction) => LocaleFormat.Percent(Strings.Current, (int)Math.Round(fraction * 100));
}
