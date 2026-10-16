using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.Core.Localization;
using Themia.Core.Sync;

namespace Themia.App.Shell.Config;

/// <summary>
/// [S-3780] Settings › Widgets: how often each kind of online source is asked, for every widget
/// instead of one setting each. The regional formats are on Language &amp; region ([S-3797]).
/// </summary>
internal static partial class ConfigPages
{
    private static FrameworkElement BuildWidgets(AppHost host)
    {
        var rows = new List<FrameworkElement>();
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

        return Page(Str.WidgetPrefs_Nav.Text(), rows);
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
