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

    /// <summary>
    /// [S-11407] The block's widths: from 260, a title of a dozen letters beside the end column; a new
    /// one 320; to 520, past which the title is a sentence across a room.
    /// </summary>
    public static KitWidths Widths { get; } = new(260, 320, 520);

    /// <summary>
    /// [S-14261] The lines' least width, unscaled, beside an end column <paramref name="endWidth"/> wide: what the block
    /// keeps for its title and lines however it scales, the room they have in its least card beside that column; past it
    /// they are cut.
    /// </summary>
    public static double TextLeast(double endWidth) => TextWidth(Widths.Min - (2 * FamilySpace.Inset), endWidth);

    /// <summary>The countdown's one line (BodyStrong).</summary>
    public static double CountdownLine => FamilyType.BodyStrong.LineHeight;

    /// <summary>
    /// [S-5710] Without Join the countdown sits on the title's line: this far down, so the two lines'
    /// middles meet (Title's 28 line, BodyStrong's 20).
    /// </summary>
    public static double TitleLineOffset => (FamilyType.Title.LineHeight - CountdownLine) / 2;

    /// <summary>
    /// [S-5710] [S-14793] The end column's width: the wider of the countdown's widest wording and Join (none without a link),
    /// each by its ink (a text's side bearings are no part of it; Join's box is its ink), so its first ink is its start.
    /// </summary>
    public static double EndWidth(double countdownWidth, double joinWidth, bool hasJoin)
        => Math.Max(Finite(countdownWidth), hasJoin ? Finite(joinWidth) : 0);

    /// <summary>
    /// [S-2591] [S-14793] The lines' column, where the title is cut: the content box less the end column's ink and the gap
    /// (owner, 2026-11-20: "the next meeting clips the time into the title"). The title's cut stands the family's column gap
    /// before the end column's first ink at every width and state, never in or under it; a column with no room left is none.
    /// </summary>
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

    /// <summary>[S-12833] The end column's width as the parts estimate it: "in 59 min." over Join.</summary>
    public const double EndEstimate = 80;

    /// <summary>
    /// [S-14792] The order the block gives way in, last kept first (owner, 2026-11-20: "the next meeting clips the time into
    /// the title"): the title, then its time (when it is, the most important fact after what), the end column (how long until
    /// it, over Join) beside them while there is room, then the place, then the organiser. Each is the size it gives way at
    /// (<see cref="FaceFloor.Before"/>: one at a time in this order, never all at once leaving the title alone). A line leads
    /// with its 16 glyph, which reads like 12 text (<see cref="ScaleFloor.AsText"/>: under 0.75 the glyph would be under its
    /// floor), so the lines go at 12; the end column goes with them, before the time: the block's least scale is the lines'
    /// glyphs', and a column kept under it would run past the room's end.
    /// </summary>
    public static class GivesWay
    {
        /// <summary>The time range, the last line to go: a Body line led by its glyph.</summary>
        public static double Time => ScaleFloor.AsText(FamilyShape.Icon);

        /// <summary>The end column (the countdown over Join): before the time, after the place.</summary>
        public static double End => FaceFloor.Before(Time, 1);

        /// <summary>The place.</summary>
        public static double Place => FaceFloor.Before(Time, 2);

        /// <summary>The organiser, the first to go.</summary>
        public static double Organizer => FaceFloor.Before(Time, 3);
    }

    /// <summary>
    /// [S-12833] [S-14792] The block's parts (<see cref="FaceParts"/>): its title the main reading (Title), at 9 in its least
    /// card; the end column (how long until it, over Join) and the meeting's lines under it give way on the way down, one at
    /// a time in <see cref="GivesWay"/>'s order.
    /// </summary>
    public static FaceParts Face(NextMeetingConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var text = Widths.Natural - (2 * FamilySpace.Inset) - EndEstimate - Gap;
        var rows = new List<FaceRow>
        {
            new([FacePiece.Text(FamilyType.Title, text), FacePiece.Box(EndEstimate + Gap, FamilyType.Title.LineHeight, GivesWay.End)]),
            new([FacePiece.Text(FamilyType.Body, text, GivesWay.Time)], GapOver: LineGap),
        };

        if (config.ShowPlace)
        {
            rows.Add(new([FacePiece.Text(FamilyType.Body, text, GivesWay.Place)], GapOver: LineGap));
        }

        if (config.ShowOrganizer)
        {
            rows.Add(new([FacePiece.Text(FamilyType.Body, text, GivesWay.Organizer)], GapOver: LineGap));
        }

        return new FaceParts(FamilyType.Title.FontSize, rows);
    }

    /// <summary>
    /// [S-11407] The block's sizing: the inset over and under the taller of its lines and its end column
    /// (Join included: the size never depends on the meeting), one height; 260..520 wide, a new one 320.
    /// </summary>
    public static WidgetSizing Sizing(NextMeetingConfig config)
    {
        var height = (2 * FamilySpace.Inset) + Math.Max(LinesHeight(config), EndHeight(hasJoin: true));
        return WidgetSizing.Range(new(Widths.Min, height), new(Widths.Natural, height), new(Widths.Max, height));
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
