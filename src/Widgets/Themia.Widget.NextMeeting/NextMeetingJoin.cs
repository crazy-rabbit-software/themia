using Themia.Core.Localization;
using Themia.Core.Office;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;

namespace Themia.Widget.NextMeeting;

/// <summary>[S-2591] [S-5710] Where the end column (the countdown over Join) stands: beside the meeting's lines, or under them as one row.</summary>
public enum EndPlace
{
    /// <summary>At the end side of the lines: centred on them with Join, on the title's line without.</summary>
    Beside,

    /// <summary>One row at the content's foot, Join at its start and the countdown at its end: the card too narrow for the lines beside it.</summary>
    Under,

    /// <summary>At the content's foot as a column, the countdown over Join at the start: too narrow even for the two side by side.</summary>
    Stacked,
}

/// <summary>
/// [S-2591] [S-2592] [S-5710] The meeting stands without a header (owner decision 2026-11-12): its
/// title is the first line, and how long until it (the countdown) stands at the end side over Join,
/// one unit (how long, then the action), the family's column gap between the lines and it, as long as
/// the lines keep a title a sensible width; narrower, the end column goes under the lines as one row.
/// Decided on the measured widths alone (the end column is the wider of the countdown's widest wording
/// and Join), so a language with longer words moves it sooner and a ticking countdown never moves it.
/// </summary>
public static class NextMeetingJoin
{
    /// <summary>
    /// The narrowest text column the title keeps beside the end column: about a dozen Title characters,
    /// under which a meeting's name is cut to a word or two and reads better on its own line.
    /// </summary>
    public const double MinTextColumn = 160;

    /// <summary>Between the lines' column and the end column.</summary>
    public const double Gap = FamilySpace.ColumnGap;

    /// <summary>Between the countdown and Join under it: one unit.</summary>
    public const double Stack = FamilySpace.S1;

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

    /// <summary>
    /// [S-2591] Where the end column <paramref name="endWidth"/> wide stands in a content box
    /// <paramref name="contentWidth"/> wide (the card less its side insets).
    /// </summary>
    public static EndPlace Place(double contentWidth, double endWidth)
        => double.IsFinite(contentWidth) && TextWidth(EndPlace.Beside, contentWidth, endWidth) >= MinTextColumn
            ? EndPlace.Beside
            : EndPlace.Under;

    /// <summary>
    /// [S-5710] Where the end column stands for a countdown <paramref name="countdownWidth"/> wide (its widest
    /// wording) and Join <paramref name="joinWidth"/> wide: beside the lines where they keep their width;
    /// else under them as one row, Join and the countdown the column gap apart; and where even that row
    /// does not fit, stacked at the foot as it stands beside the lines.
    /// </summary>
    public static EndPlace Place(double contentWidth, double countdownWidth, double joinWidth, bool hasJoin)
    {
        var place = Place(contentWidth, EndWidth(countdownWidth, joinWidth, hasJoin));
        var row = Finite(countdownWidth) + (hasJoin ? Finite(joinWidth) + Gap : 0);
        return place == EndPlace.Under && hasJoin && !(double.IsFinite(contentWidth) && row <= contentWidth) ? EndPlace.Stacked : place;
    }

    /// <summary>The width the meeting's lines have: the content's, less the end column and the gap where it stands beside them.</summary>
    public static double TextWidth(EndPlace place, double contentWidth, double endWidth)
        => place == EndPlace.Beside ? Math.Max(0, contentWidth - Finite(endWidth) - Gap) : Math.Max(0, contentWidth);

    /// <summary>[S-5710] The end column's height beside the lines: the countdown, and Join under it.</summary>
    public static double EndHeight(bool hasJoin)
        => hasJoin ? CountdownLine + Stack + FamilyShape.Button : TitleLineOffset + CountdownLine;

    /// <summary>
    /// [S-5710] What the end column costs the ladder's height: nothing beside the lines (it costs width),
    /// under them its row (Join's 28, or the countdown's line without it) or, stacked, its column, and the 8 over it.
    /// </summary>
    public static double FootCost(EndPlace place, bool hasJoin) => place switch
    {
        EndPlace.Under => (hasJoin ? FamilyShape.Button : CountdownLine) + FamilySpace.S2,
        EndPlace.Stacked => EndHeight(hasJoin) + FamilySpace.S2,
        _ => 0,
    };

    /// <summary>
    /// [S-5710] Every wording the countdown can take while <paramref name="item"/> stays in the form it is
    /// in at <paramref name="now"/>: within the hour every minute's ("in 1 min" .. "in 59 min", "now"),
    /// else the one it says (a day and a time, "Now · ends …"), which does not tick. The widest of them is
    /// the end column's width, so the column does not move as the minutes go.
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
