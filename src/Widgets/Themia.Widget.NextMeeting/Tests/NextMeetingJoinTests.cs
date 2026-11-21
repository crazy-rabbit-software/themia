using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Office;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Widget.NextMeeting.Tests;

/// <summary>
/// [S-2591] [S-2592] [S-5710] [S-11407] The countdown over Join at the end side at every width of the
/// block's range; without a link the countdown alone on the title's line.
/// </summary>
public sealed class NextMeetingJoinTests
{
    /// <summary>"Join" on the Body step with the button's padding and border: about 58.</summary>
    private const double Join = 58;

    /// <summary>"in 59 min." in BodyStrong: about 76.</summary>
    private const double Countdown = 76;

    private static readonly DateTimeOffset Now = new(2026, 11, 12, 10, 42, 0, TimeSpan.Zero);

    private static CalendarEvent In(double minutes, double length = 30) => new()
    {
        Id = "m",
        Subject = "Design review",
        Start = Now.AddMinutes(minutes),
        End = Now.AddMinutes(minutes + length),
        AccountId = "a",
    };

    [Fact]
    public void S5710_the_end_column_is_the_wider_of_the_countdown_and_join_and_the_lines_have_the_rest()
    {
        Assert.Equal(Join + 40, NextMeetingJoin.EndWidth(Countdown, Join + 40, hasJoin: true));
        Assert.Equal(Countdown, NextMeetingJoin.EndWidth(Countdown, Join, hasJoin: true));

        // At the narrowest block (260 less its insets) the title keeps the rest beside the column.
        Assert.Equal(236 - Countdown - 12, NextMeetingJoin.TextWidth(236, Countdown));
        Assert.Equal(0, NextMeetingJoin.TextWidth(double.NaN, Countdown));
    }

    [Fact]
    public void S5710_the_countdown_is_measured_on_every_minute_of_the_hour_so_the_column_does_not_move()
    {
        var soon = NextMeetingJoin.Wordings(Locale.En, In(18), Now, TimeZoneInfo.Utc);
        Assert.Contains("in 18 min.", soon);
        Assert.Contains("in 59 min.", soon);
        Assert.Contains("in 1 min.", soon);

        // A day and a time, or a meeting under way, does not tick: it is its one wording.
        Assert.Single(NextMeetingJoin.Wordings(Locale.En, In(180), Now, TimeZoneInfo.Utc));
        Assert.Single(NextMeetingJoin.Wordings(Locale.En, In(-5), Now, TimeZoneInfo.Utc));
    }

    [Fact]
    public void S2592_no_join_link_is_no_button_the_countdown_alone_on_the_titles_line()
    {
        Assert.Equal(Countdown, NextMeetingJoin.EndWidth(Countdown, Join + 100, hasJoin: false));

        // On the title's 28 line, its middle on the title's: 4 down.
        Assert.Equal(4, NextMeetingJoin.TitleLineOffset);
        Assert.Equal(4 + 20, NextMeetingJoin.EndHeight(hasJoin: false));
        Assert.Equal(20 + 4 + 28, NextMeetingJoin.EndHeight(hasJoin: true));
    }

    /// <summary>
    /// [S-11407] [S-14261] The lines keep their least card's room however the block scales: in its least card at scale 1 they stand
    /// beside the end column whole; a long title is cut there, never holding the block small.
    /// </summary>
    [Fact]
    public void S11407_the_lines_least_width_is_their_room_in_the_least_card()
    {
        // Beside the countdown: the least card's content box less the column and the gap, so at scale 1 the block fits it.
        var least = NextMeetingJoin.TextLeast(Countdown);
        Assert.Equal(NextMeetingJoin.Widths.Min - 24 - Countdown - NextMeetingJoin.Gap, least);
        Assert.Equal(NextMeetingJoin.Widths.Min - 24, least + NextMeetingJoin.Gap + Countdown);

        // A wider column ("Now · ends 11:45 AM" over Join) leaves the lines less, never below nothing.
        Assert.True(NextMeetingJoin.TextLeast(Countdown + 60) < least);
        Assert.Equal(0, NextMeetingJoin.TextLeast(400));
    }

    /// <summary>The desktop sizes the block is judged at, the owner's 370 x 90 among them.</summary>
    private static readonly (double W, double H)[] Cards =
        [(200, 60), (240, 90), (300, 80), (350, 70), (370, 80), (370, 90), (370, 120), (330, 130), (300, 164), (600, 200), (320, 124), (520, 600)];

    /// <summary>
    /// [S-14792] The block gives way one part at a time, the organiser first, then the place, the end column, the time last
    /// (owner, 2026-11-20: "the next meeting clips the time into the title"). Its lines were all Body and went together: at
    /// 370 x 80, 300 x 80, 350 x 70 and 200 x 60 the card showed its title alone, scaled huge and cut ("Design re…").
    /// </summary>
    [Fact]
    public void S14792_the_block_gives_way_organiser_place_end_column_then_time_never_its_title_alone_first()
    {
        var order = new[] { NextMeetingJoin.GivesWay.Organizer, NextMeetingJoin.GivesWay.Place, NextMeetingJoin.GivesWay.End, NextMeetingJoin.GivesWay.Time };
        Assert.Equal(order.Order(), order);
        Assert.Equal(order.Length, order.Distinct().Count());

        // The lines lead with their 16 glyph, read like 12 text: they go where the glyph would be under its floor (0.75).
        Assert.Equal(12, NextMeetingJoin.GivesWay.Time);

        var face = NextMeetingJoin.Face(new NextMeetingConfig());
        Assert.Equal(order.Order(), face.Sizes.Where(s => s < face.Main).Order());

        // 370 x 80: the time and the place stay beside the end column; the organiser alone gave way.
        var eighty = face.Fit(370 - 24, 80 - 24);
        Assert.True(eighty.Draws(NextMeetingJoin.GivesWay.Time) && eighty.Draws(NextMeetingJoin.GivesWay.Place) && eighty.Draws(NextMeetingJoin.GivesWay.End));
        Assert.False(eighty.Draws(NextMeetingJoin.GivesWay.Organizer));

        foreach (var (w, h) in Cards.Concat([(370.0, 50.0), (260, 56), (200, 40)]))
        {
            var fit = face.Fit(w - 24, h - 24);

            // In the order: nothing is drawn while a part after it in the order has gone, and the time goes last.
            for (var i = 1; i < order.Length; i++)
            {
                Assert.True(!fit.Draws(order[i - 1]) || fit.Draws(order[i]), $"{w}x{h}: {order[i - 1]} drawn without {order[i]}");
            }

            Assert.All(face.Sizes.Where(fit.Draws), size => Assert.True(size * fit.Scale >= ScaleFloor.Text - 1e-4, $"{w}x{h}: {size} at {size * fit.Scale}"));
        }
    }

    /// <summary>
    /// [S-14791] [S-14793] The title's cut stands the family's column gap before the end column's first ink at every size and
    /// state, and the block never grows past where the title stands whole (owner, 2026-11-20: "the next meeting clips the time
    /// into the title"). The end column is measured by its ink (none, "--", "in 59 min.", "Now · ends 11:45 AM" over Join); the
    /// title short, the sample's (280 whole at Title) or a sentence. Laid at the block's scale, the title's column and the gap
    /// and the end column never pass the content, and the title is cut only where the block is at its floor.
    /// </summary>
    [Fact]
    public void S14793_the_titles_cut_stands_the_gap_before_the_end_column_at_every_size_and_state()
    {
        var least = ScaleFloor.Least(FamilyType.Body.FontSize, FamilyShape.Icon);
        var ink = new LineInk(LineInk.Of(FamilyType.Title).Over, LineInk.Of(FamilyType.Body).Under);
        var packed = NextMeetingJoin.LinesHeight(new NextMeetingConfig());
        foreach (var (w, h) in Cards)
        {
            foreach (var end in new[] { 0.0, 15, Countdown, 140 })
            {
                foreach (var title in new[] { 60.0, 280, 600 })
                {
                    var room = w - 24;
                    var beside = end > 0 ? NextMeetingJoin.Gap + end : 0;
                    var asked = Math.Min(title, NextMeetingJoin.TextLeast(end)) + beside;
                    var scale = BlockLayout.Fill(room, h - 24, asked, packed, ink, least, BlockLayout.Uncut(room, title + beside)).Scale;
                    var content = room / scale;
                    var cut = end > 0 ? NextMeetingJoin.TextWidth(content, end) : content;
                    var at = $"{w}x{h} end {end} title {title}: scale {scale:0.###}";

                    // Never into or under the end column: the cut, the gap, the column within the content.
                    Assert.True(cut >= 0, at);
                    Assert.True(cut + beside <= content + 1e-9, at);

                    // Never grown past the whole title: cut only at the floor.
                    Assert.True(title <= cut + 1e-6 || Math.Abs(scale - least) < 1e-9, at);
                }
            }
        }
    }
}
