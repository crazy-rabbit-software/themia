using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Sync;

namespace Themia.App.Shell.Config;

/// <summary>
/// [S-3770] [S-3780] Settings › Widgets: what every widget shares instead of carrying its own — the
/// regional formats (the clock, the first day of the week, the temperature scale) and how often each
/// kind of online source is asked.
/// </summary>
internal static partial class ConfigPages
{
    private static FrameworkElement BuildWidgets(AppHost host)
    {
        var rows = new List<FrameworkElement>();
        var region = host.State.Preferences.Region;
        var windows = RegionFormat.Windows ?? RegionFormat.Language(Strings.Current);

        // [S-3771] Each format offers "Windows setting (…)" first, naming what Windows says, so the
        // default is never a value the reader has to look up.
        string FollowWindows(string value) => Str.WidgetPrefs_FollowWindows.Format(("value", value));
        string Hours(bool use24) => use24 ? Str.WidgetPrefs_Region_Clock24.Format(("hours", 24)) : Str.WidgetPrefs_Region_Clock12.Format(("hours", 12));
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

        rows.Add(FluentLayout.Header(Str.WidgetPrefs_Region_Header.Text()));

        rows.Add(FluentLayout.Row(Glyphs.Clock, Str.WidgetPrefs_Region_Clock.Text(), null,
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

        rows.Add(FluentLayout.Header(Str.WidgetPrefs_Updates_Header.Text()));
        rows.Add(Note(Str.WidgetPrefs_Updates_Note.Text()));

        var refresh = host.State.Preferences.Refresh;

        Border Cadence(string glyph, Str title, UpdateSource source) => FluentLayout.Row(glyph, title.Text(), null,
            FluentLayout.Choice(
                [.. UpdateCadence.Choices(source).Select(minutes => (minutes, Every(minutes)))],
                UpdateCadence.Minutes(refresh, source),
                value => host.Mutate(state => UpdateCadence.Set(state.Preferences.Refresh, source, value), relayout: false)),
            "refresh", "interval", "sync");

        rows.Add(Cadence(Glyphs.Weather, Str.WidgetPrefs_Updates_Weather, UpdateSource.Weather));
        rows.Add(Cadence(Glyphs.Stocks, Str.WidgetPrefs_Updates_Markets, UpdateSource.Markets));
        rows.Add(Cadence(Glyphs.Rss, Str.WidgetPrefs_Updates_Feeds, UpdateSource.Feeds));
        rows.Add(Cadence(Glyphs.Mail, Str.WidgetPrefs_Updates_Accounts, UpdateSource.Accounts));

        return Page(Str.WidgetPrefs_Nav.Text(), rows, Str.WidgetPrefs_Subtitle.Text());
    }

    /// <summary>"Every minute", "Every 5 minutes", "Every hour", "Every 2 hours".</summary>
    private static string Every(int minutes) => minutes switch
    {
        1 => Str.Shell_Refresh_EveryMinute.Text(),
        60 => Str.Shell_Refresh_EveryHour.Text(),
        _ when minutes % 60 == 0 => Str.WidgetPrefs_Updates_EveryHours.Plural(minutes / 60),
        _ => Str.Shell_Refresh_EveryMinutes.Plural(minutes),
    };
}
