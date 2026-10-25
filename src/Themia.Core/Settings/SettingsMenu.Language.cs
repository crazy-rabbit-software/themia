using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Model;

namespace Themia.Core.Settings;

/// <summary>
/// [S-3536] Language &amp; region ›: every localisation choice in one place, as the settings window's
/// Language &amp; region page held them ([S-3797]): the language Themia speaks ([S-1109]), a choice with a
/// box that finds a language as it is typed, then the regional formats every widget shares
/// ([S-3770]): the clock, the first day of the week and the temperature scale, each offering what
/// Windows says first ([S-3771]).
/// </summary>
public static partial class SettingsMenu
{
    /// <summary>
    /// [S-1109] The thirty languages in the order the page listed them, each by its own name ("Deutsch",
    /// "日本語", the same in every language, so someone landed in a language they cannot read still
    /// finds theirs), with its English name and code, which a search finds it by too.
    /// </summary>
    internal static readonly (AppLanguage Language, Str Name, string Also)[] Languages =
    [
        (AppLanguage.English, Str.Settings_Language_English, "English en"),
        (AppLanguage.German, Str.Settings_Language_German, "German de"),
        (AppLanguage.Spanish, Str.Settings_Language_Spanish, "Spanish es"),
        (AppLanguage.French, Str.Settings_Language_French, "French fr"),
        (AppLanguage.Portuguese, Str.Settings_Language_Portuguese, "Portuguese pt"),
        (AppLanguage.Italian, Str.Settings_Language_Italian, "Italian it"),
        (AppLanguage.Polish, Str.Settings_Language_Polish, "Polish pl"),
        (AppLanguage.Japanese, Str.Settings_Language_Japanese, "Japanese ja"),
        (AppLanguage.Chinese, Str.Settings_Language_Chinese, "Chinese Simplified zh zh-Hans"),
        (AppLanguage.ChineseTraditional, Str.Settings_Language_ChineseTraditional, "Chinese Traditional zh-Hant"),
        (AppLanguage.Hindi, Str.Settings_Language_Hindi, "Hindi hi"),
        (AppLanguage.Arabic, Str.Settings_Language_Arabic, "Arabic ar"),
        (AppLanguage.Bengali, Str.Settings_Language_Bengali, "Bengali Bangla bn"),
        (AppLanguage.Russian, Str.Settings_Language_Russian, "Russian ru"),
        (AppLanguage.Indonesian, Str.Settings_Language_Indonesian, "Indonesian id"),
        (AppLanguage.Urdu, Str.Settings_Language_Urdu, "Urdu ur"),
        (AppLanguage.Marathi, Str.Settings_Language_Marathi, "Marathi mr"),
        (AppLanguage.Vietnamese, Str.Settings_Language_Vietnamese, "Vietnamese vi"),
        (AppLanguage.Korean, Str.Settings_Language_Korean, "Korean ko"),
        (AppLanguage.Dutch, Str.Settings_Language_Dutch, "Dutch nl"),
        (AppLanguage.Turkish, Str.Settings_Language_Turkish, "Turkish tr"),
        (AppLanguage.Telugu, Str.Settings_Language_Telugu, "Telugu te"),
        (AppLanguage.Hausa, Str.Settings_Language_Hausa, "Hausa ha"),
        (AppLanguage.Punjabi, Str.Settings_Language_Punjabi, "Punjabi pa"),
        (AppLanguage.Swahili, Str.Settings_Language_Swahili, "Swahili sw"),
        (AppLanguage.Filipino, Str.Settings_Language_Filipino, "Filipino Tagalog fil"),
        (AppLanguage.Tamil, Str.Settings_Language_Tamil, "Tamil ta"),
        (AppLanguage.Persian, Str.Settings_Language_Persian, "Persian Farsi fa"),
        (AppLanguage.Thai, Str.Settings_Language_Thai, "Thai th"),
        (AppLanguage.Gujarati, Str.Settings_Language_Gujarati, "Gujarati gu"),
    ];

    /// <summary>[S-3536] The Language &amp; region line and its submenu, its value the language that holds.</summary>
    public static SettingsSubmenu Language(ISettingsMenuHost host)
    {
        ArgumentNullException.ThrowIfNull(host);

        var preferences = host.State.Preferences;
        var region = preferences.Region;
        var windows = RegionFormat.Windows ?? RegionFormat.Language(Strings.Current);
        var display = DisplayLanguage(host);

        // [S-3771] Each format offers "Windows setting (…)" first, naming what Windows says.
        string Hours(bool use24) => use24 ? Str.LanguageRegion_Clock24.Format(("hours", 24)) : Str.LanguageRegion_Clock12.Format(("hours", 12));
        string Day(WeekStartDay day) => day switch
        {
            WeekStartDay.Sunday => Str.Calendar_WeekStart_Sunday.Text(),
            WeekStartDay.Saturday => Str.Calendar_WeekStart_Saturday.Text(),
            _ => Str.Calendar_WeekStart_Monday.Text(),
        };
        string Scale(TemperatureUnit unit) => unit == TemperatureUnit.Fahrenheit
            ? $"{Str.WidgetsC_Weather_Settings_Fahrenheit.Text()} (°F)"
            : $"{Str.WidgetsC_Weather_Settings_Celsius.Text()} (°C)";

        // [S-3536] The line says what is in force ("12-hour"), short enough to stand beside its words;
        // its submenu says where it comes from ("Windows setting (12-hour)").
        SettingsChoice Format<T>(string key, Str text, string glyph, T current, string inForce, IReadOnlyList<(T Value, string Text)> options, Action<RegionPreferences, T> set)
            where T : struct, Enum
            => new(Keys.Language + "/" + key, text.Text(), glyph, [.. options.Select(o =>
            {
                var holds = EqualityComparer<T>.Default.Equals(o.Value, current);
                return new SettingsRadio(Keys.Language + "/" + key + "/" + o.Value, o.Text, holds, () =>
                {
                    if (!holds)
                    {
                        host.Mutate(state => set(state.Preferences.Region, o.Value), relayout: false);
                    }
                });
            })])
            {
                Value = inForce,
            };

        var use24 = region.Clock switch { ClockHours.Twelve => false, ClockHours.TwentyFour => true, _ => windows.Use24Hour };
        var weekStart = region.WeekStart switch
        {
            FirstWeekday.Monday => WeekStartDay.Monday,
            FirstWeekday.Sunday => WeekStartDay.Sunday,
            FirstWeekday.Saturday => WeekStartDay.Saturday,
            _ => windows.WeekStart,
        };
        var fahrenheit = region.Temperature switch
        {
            TemperatureScale.Celsius => false,
            TemperatureScale.Fahrenheit => true,
            _ => windows.Temperature == TemperatureUnit.Fahrenheit,
        };

        var lines = new List<SettingsLine>
        {
            display,
            new SettingsSeparator(Keys.Language + "/-region"),
            Format(
                "clock",
                Str.LanguageRegion_Clock,
                Glyphs.Clock,
                region.Clock,
                Hours(use24),
                [(ClockHours.System, FollowWindows(Hours(windows.Use24Hour))), (ClockHours.Twelve, Hours(false)), (ClockHours.TwentyFour, Hours(true))],
                (r, v) => r.Clock = v),
            Format(
                "weekStart",
                Str.Calendar_WeekStart_Label,
                Glyphs.Calendar,
                region.WeekStart,
                Day(weekStart),
                [(FirstWeekday.System, FollowWindows(Day(windows.WeekStart))), (FirstWeekday.Monday, Day(WeekStartDay.Monday)), (FirstWeekday.Sunday, Day(WeekStartDay.Sunday)), (FirstWeekday.Saturday, Day(WeekStartDay.Saturday))],
                (r, v) => r.WeekStart = v),
            Format(
                "temperature",
                Str.Weather_UnitLabel,
                Glyphs.Weather,
                region.Temperature,
                fahrenheit ? "°F" : "°C",
                [(TemperatureScale.System, FollowWindows(windows.Temperature == TemperatureUnit.Fahrenheit ? "°F" : "°C")), (TemperatureScale.Celsius, Scale(TemperatureUnit.Celsius)), (TemperatureScale.Fahrenheit, Scale(TemperatureUnit.Fahrenheit))],
                (r, v) => r.Temperature = v),
        };

        return new SettingsSubmenu(Keys.Language, Str.LanguageRegion_Nav.Text(), Glyphs.TimeLanguage, lines) { Value = display.Value };
    }

    /// <summary>
    /// [S-1109] [S-3536] The app's display language: Windows' own first ("Windows setting (Deutsch)",
    /// naming the language Windows gives Themia), then the thirty in their own words; its submenu has
    /// a box on top that keeps the languages whose name, English name or code holds what is typed. A
    /// pick switches the whole app at once ([S-1110]).
    /// </summary>
    private static SettingsChoice DisplayLanguage(ISettingsMenuHost host)
    {
        var current = host.State.Preferences.Language;
        var key = Keys.Language + "/display";

        SettingsRadio Option(AppLanguage language, string text, string also)
        {
            var holds = language == current;
            return new SettingsRadio(key + "/" + language, text, holds, () =>
            {
                if (!holds)
                {
                    host.SetLanguage(language);
                }
            })
            {
                Keywords = also,
            };
        }

        var windows = LanguageName(LanguageResolver.Setting(host.WindowsLanguage));
        var spoken = current == AppLanguage.System ? windows : LanguageName(current);
        var options = new List<SettingsRadio>(Languages.Length + 1)
        {
            Option(AppLanguage.System, FollowWindows(windows), "Windows system default"),
        };

        foreach (var (language, name, also) in Languages)
        {
            options.Add(Option(language, name.Text(), also));
        }

        // [S-3536] The line says the language spoken, in its own words; its submenu, where it comes from.
        return new SettingsChoice(key, Str.Settings_Language_Label.Text(), Glyphs.Language, options)
        {
            Value = spoken,
            Filter = Str.SettingsMenu_FindLanguage.Text(),
        };
    }

    /// <summary>A language by its own name; Windows' own setting by the language it gives.</summary>
    internal static string LanguageName(AppLanguage language)
    {
        foreach (var (known, name, _) in Languages)
        {
            if (known == language)
            {
                return name.Text();
            }
        }

        // Windows' own setting resolves to one of the thirty, English where it is none of them.
        return Str.Settings_Language_English.Text();
    }

    /// <summary>[S-3771] "Windows setting (…)", naming what Windows says.</summary>
    private static string FollowWindows(string value) => Str.LanguageRegion_FollowWindows.Format(("value", value));

    /// <summary>
    /// [S-3536] Whether a choice's option is kept by what is typed in its submenu's box: its words or
    /// its keywords hold the typed text, whatever the case; every option for an empty box.
    /// </summary>
    public static bool Matches(string text, string? keywords, string typed)
    {
        ArgumentNullException.ThrowIfNull(text);
        var wanted = (typed ?? string.Empty).Trim();
        return wanted.Length == 0
            || text.Contains(wanted, StringComparison.OrdinalIgnoreCase)
            || (keywords?.Contains(wanted, StringComparison.OrdinalIgnoreCase) ?? false);
    }
}
