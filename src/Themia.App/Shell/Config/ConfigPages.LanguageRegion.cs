using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.Core.Localization;
using Themia.Core.Model;

namespace Themia.App.Shell.Config;

/// <summary>
/// [S-3797] Settings › Language &amp; region, as Windows Settings' Time &amp; language › Language &amp;
/// region: the language Themia's own text is in ([S-1109]) and the regional formats every widget
/// shares — the clock, the first day of the week, the temperature scale ([S-3770]). One page for
/// every localisation choice, so none of them is on General or Widgets as well.
/// </summary>
internal static partial class ConfigPages
{
    private static FrameworkElement BuildLanguageRegion(AppHost host)
    {
        var rows = new List<FrameworkElement>
        {
            FluentLayout.Header(Str.Settings_Language_Label.Text()),
            LanguageRow(host),
        };

        var region = host.State.Preferences.Region;
        var windows = RegionFormat.Windows ?? RegionFormat.Language(Strings.Current);

        // [S-3771] Each format offers "Windows setting (…)" first, naming what Windows says, so the
        // default is never a value the reader has to look up.
        string FollowWindows(string value) => Str.LanguageRegion_FollowWindows.Format(("value", value));
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

        void SetRegion(Action<RegionPreferences> change) => host.Mutate(state => change(state.Preferences.Region), relayout: false);

        rows.Add(FluentLayout.Header(Str.LanguageRegion_Region_Header.Text()));

        rows.Add(FluentLayout.Row(Glyphs.Clock, Str.LanguageRegion_Clock.Text(), null,
            FluentLayout.Choice(
                [(ClockHours.System, FollowWindows(Hours(windows.Use24Hour))), (ClockHours.Twelve, Hours(false)), (ClockHours.TwentyFour, Hours(true))],
                region.Clock,
                value => SetRegion(r => r.Clock = value)),
            "24-hour", "12-hour", "am pm", "time format"));

        rows.Add(FluentLayout.Row(Glyphs.Calendar, Str.Calendar_WeekStart_Label.Text(), null,
            FluentLayout.Choice(
                [(FirstWeekday.System, FollowWindows(Day(windows.WeekStart))), (FirstWeekday.Monday, Day(WeekStartDay.Monday)), (FirstWeekday.Sunday, Day(WeekStartDay.Sunday)), (FirstWeekday.Saturday, Day(WeekStartDay.Saturday))],
                region.WeekStart,
                value => SetRegion(r => r.WeekStart = value)),
            "week", "calendar", "first day"));

        rows.Add(FluentLayout.Row(Glyphs.Weather, Str.Weather_UnitLabel.Text(), null,
            FluentLayout.Choice(
                [(TemperatureScale.System, FollowWindows(windows.Temperature == TemperatureUnit.Fahrenheit ? "°F" : "°C")), (TemperatureScale.Celsius, Scale(TemperatureUnit.Celsius)), (TemperatureScale.Fahrenheit, Scale(TemperatureUnit.Fahrenheit))],
                region.Temperature,
                value => SetRegion(r => r.Temperature = value)),
            "celsius", "fahrenheit", "units"));

        return Page(Str.LanguageRegion_Nav.Text(), rows);
    }

    /// <summary>
    /// [S-1109] The app's display language: Windows' own by default, or one of the thirty, switching live.
    /// Each language is listed by its own name ("Deutsch", "日本語"), the same in every language, so
    /// someone who landed in a language they cannot read still finds theirs.
    /// </summary>
    private static Border LanguageRow(AppHost host)
    {
        var options = new List<(AppLanguage Value, string Label)>
        {
            (AppLanguage.System, Str.Settings_Language_System.Text()),
            (AppLanguage.English, Str.Settings_Language_English.Text()),
            (AppLanguage.German, Str.Settings_Language_German.Text()),
            (AppLanguage.Spanish, Str.Settings_Language_Spanish.Text()),
            (AppLanguage.French, Str.Settings_Language_French.Text()),
            (AppLanguage.Portuguese, Str.Settings_Language_Portuguese.Text()),
            (AppLanguage.Italian, Str.Settings_Language_Italian.Text()),
            (AppLanguage.Polish, Str.Settings_Language_Polish.Text()),
            (AppLanguage.Japanese, Str.Settings_Language_Japanese.Text()),
            (AppLanguage.Chinese, Str.Settings_Language_Chinese.Text()),
            (AppLanguage.ChineseTraditional, Str.Settings_Language_ChineseTraditional.Text()),
            (AppLanguage.Hindi, Str.Settings_Language_Hindi.Text()),
            (AppLanguage.Arabic, Str.Settings_Language_Arabic.Text()),
            (AppLanguage.Bengali, Str.Settings_Language_Bengali.Text()),
            (AppLanguage.Russian, Str.Settings_Language_Russian.Text()),
            (AppLanguage.Indonesian, Str.Settings_Language_Indonesian.Text()),
            (AppLanguage.Urdu, Str.Settings_Language_Urdu.Text()),
            (AppLanguage.Marathi, Str.Settings_Language_Marathi.Text()),
            (AppLanguage.Vietnamese, Str.Settings_Language_Vietnamese.Text()),
            (AppLanguage.Korean, Str.Settings_Language_Korean.Text()),
            (AppLanguage.Dutch, Str.Settings_Language_Dutch.Text()),
            (AppLanguage.Turkish, Str.Settings_Language_Turkish.Text()),
            (AppLanguage.Telugu, Str.Settings_Language_Telugu.Text()),
            (AppLanguage.Hausa, Str.Settings_Language_Hausa.Text()),
            (AppLanguage.Punjabi, Str.Settings_Language_Punjabi.Text()),
            (AppLanguage.Swahili, Str.Settings_Language_Swahili.Text()),
            (AppLanguage.Filipino, Str.Settings_Language_Filipino.Text()),
            (AppLanguage.Tamil, Str.Settings_Language_Tamil.Text()),
            (AppLanguage.Persian, Str.Settings_Language_Persian.Text()),
            (AppLanguage.Thai, Str.Settings_Language_Thai.Text()),
            (AppLanguage.Gujarati, Str.Settings_Language_Gujarati.Text()),
        };

        return FluentLayout.Row(Glyphs.Language, Str.LanguageRegion_DisplayLanguage.Text(), Str.LanguageRegion_DisplayLanguageDesc.Text(),
            FluentLayout.Choice(options, host.State.Preferences.Language, host.SetLanguage),
            "language", "locale", "translation");
    }
}
