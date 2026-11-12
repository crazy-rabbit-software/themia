using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Office;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;

namespace Themia.Widget.NextMeeting;

/// <summary>
/// [S-2591] [S-2592] [S-5710] [S-11407] The meeting block stands without a header: its title is the
/// first line, its other lines under it, and how long until it (the countdown) stands at the end side
/// over Join, one unit (how long, then the action), the family's column gap between the lines and it,
/// at every width of its range: the title is cut with its ellipsis, the end column never moves. Its
/// width is the wider of the countdown's widest wording and Join, so a ticking countdown never moves it.
/// </summary>
public static class NextMeetingJoin
{
    /// <summary>Between the lines' column and the end column.</summary>
    public const double Gap = FamilySpace.ColumnGap;

    /// <summary>Between the countdown and Join under it: one unit.</summary>
    public const double Stack = FamilySpace.S1;

    /// <summary>Between two of the meeting's lines.</summary>
    public const double LineGap = FamilySpace.S1;

    /// <summary>[S-11407] The narrowest the block reads at: a title of a dozen letters beside the end column.</summary>
    public const double MinWidth = 260;

    /// <summary>[S-11407] A new block's width.</summary>
    public const double NaturalWidth = 320;

    /// <summary>[S-11407] The widest: past it the title is a sentence across a room.</summary>
    public const double MaxWidth = 520;

    /// <summary>The countdown's one line (BodyStrong).</summary>
    public static double CountdownLine => FamilyType.BodyStrong.LineHeight;

    /// <summary>
    /// [S-5710] Without Join the countdown sits on the title's line: this far down, so the two lines'
    /// middles meet (Title's 28 line, BodyStrong's 20).
    /// </summary>
    public static double TitleLineOffset => (FamilyType.Title.LineHeight - CountdownLine) / 2;

    /// <summary>[S-5710] The end column's width: the wider of the countdown's widest wording and Join (none without a link).</summary>
    public static double EndWidth(double countdownWidth, double joinWidth, bool hasJoin)
        => Math.Max(Finite(countdownWidth), hasJoin ? Finite(joinWidth) : 0);

    /// <summary>[S-2591] The lines' column: the content box less the end column and the gap.</summary>
    public static double TextWidth(double contentWidth, double endWidth)
        => Math.Max(0, Finite(contentWidth) - Finite(endWidth) - Gap);

    /// <summary>[S-5710] The end column's height: the countdown over Join, or the countdown alone on the title's line.</summary>
    public static double EndHeight(bool hasJoin)
        => hasJoin ? CountdownLine + Stack + FamilyShape.Button : TitleLineOffset + CountdownLine;

    /// <summary>[S-11407] The lines' height: the title, the time range, and the place and the organiser where the config has them, 4 apart.</summary>
    public static double LinesHeight(NextMeetingConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var more = 1 + (config.ShowPlace ? 1 : 0) + (config.ShowOrganizer ? 1 : 0);
        return FamilyType.Title.LineHeight + (more * (LineGap + FamilyType.Body.LineHeight));
    }

    /// <summary>
    /// [S-11407] The block's sizing: the inset over and under the taller of its lines and its end column
    /// (Join included: the size never depends on the meeting), one height; 260..520 wide, a new one 320.
    /// </summary>
    public static WidgetSizing Sizing(NextMeetingConfig config)
    {
        var height = (2 * FamilySpace.Inset) + Math.Max(LinesHeight(config), EndHeight(hasJoin: true));
        return WidgetSizing.Range(new(MinWidth, height), new(NaturalWidth, height), new(MaxWidth, height));
    }

    /// <summary>
    /// [S-5710] Every wording the countdown can take before it next changes form: within the hour each
    /// minute's ("in 1 min." to "in 59 min." and "now"), so its column is as wide as the widest; otherwise
    /// the one it says, which does not tick.
    /// </summary>
    public static IReadOnlyList<string> Wordings(Locale locale, CalendarEvent item, DateTimeOffset now, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(item);
        var said = NextMeetingRules.When(locale, item, now, zone);
        if (NextMeetingRules.Phase(item, now) == MeetingPhase.Running || item.Start - now >= TimeSpan.FromHours(1))
        {
            return [said];
        }

        var all = new List<string> { said, LocaleFormat.Relative(locale, TimeSpan.Zero, RelativeStyle.Short) };
        for (var minutes = 1; minutes < 60; minutes++)
        {
            all.Add(LocaleFormat.Relative(locale, TimeSpan.FromMinutes(minutes), RelativeStyle.Short));
        }

        return [.. all.Distinct(StringComparer.Ordinal)];
    }

    private static double Finite(double value) => double.IsFinite(value) && value > 0 ? value : 0;
}
