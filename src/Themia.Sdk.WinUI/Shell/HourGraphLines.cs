using Themia.App.Shell;
using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Sdk;

namespace Themia.Sdk.WinUI;

/// <summary>
/// [S-12707] The lines every weather graph's page shares, so they read alike: the place (the weather family's,
/// [S-2840]) then, behind a divider, the time span (its <see cref="HourGraphConfig.HourChoices"/>, in hours or,
/// whole days past two, in days); the kind adds its own after them and ends with the source's credit.
/// </summary>
public static class HourGraphLines
{
    /// <summary>The place, a divider and the time span; [S-6752] the chart look's two lines go under the Style picker.</summary>
    public static IReadOnlyList<ItemMenuEntry> Lines(WidgetMenuLines menu, HourGraphConfig config)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);
        ChartMenuLines.Add(menu, config);

        (int, string)[] spans = [.. config.HourChoices.Select(h => (h, Span(h)))];
        return
        [
            ItemMenuEntry.Separator,
            .. WeatherPlaceRows.Lines(menu, config),
            ItemMenuEntry.Separator,
            menu.Choice(Glyphs.Calendar, Str.WeatherGraph_Window.Text(), spans, config.EffectiveHours, value => config.Hours = value),
        ];
    }

    /// <summary>The source's credit, the page's last line.</summary>
    public static ItemMenuEntry Source(string name) => WidgetMenuLines.Note(Str.Online_Source.Format(("name", name)));

    /// <summary>"24 hours", or "4 days" for a whole number of days past two.</summary>
    public static string Span(int hours)
        => hours > 48 && hours % 24 == 0
            ? Str.WeatherGraph_WindowDays.Format(("count", LocaleFormat.Integer(Strings.Current, hours / 24)))
            : Str.WeatherGraph_WindowHours.Format(("count", LocaleFormat.Integer(Strings.Current, hours)));
}
