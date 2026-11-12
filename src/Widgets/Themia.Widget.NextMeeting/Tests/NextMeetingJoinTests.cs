using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Widget.NextMeeting.Tests;

/// <summary>[S-2591] [S-2592] Join beside the meeting where the title keeps its width, under it where not, nowhere without a link.</summary>
public sealed class NextMeetingJoinTests
{
    /// <summary>"Join" on the Body step with the button's padding and border: about 58.</summary>
    private const double Join = 58;

    [Theory]
    [InlineData(496)] // 520 × 230 less the side insets
    [InlineData(296)] // 320 × 200
    [InlineData(230)]
    public void S2591_join_stands_beside_the_lines_where_the_title_keeps_its_width(double content)
    {
        Assert.Equal(JoinPlace.Beside, NextMeetingJoin.Place(hasJoin: true, content, Join));
        Assert.Equal(content - Join - FamilySpace.ColumnGap, NextMeetingJoin.TextWidth(JoinPlace.Beside, content, Join));
    }

    [Theory]
    [InlineData(96)]  // XS: 120 less the insets
    [InlineData(156)] // S
    [InlineData(229)]
    public void S2591_join_goes_under_the_lines_where_the_column_beside_it_would_be_too_narrow(double content)
    {
        Assert.Equal(JoinPlace.Under, NextMeetingJoin.Place(hasJoin: true, content, Join));
        Assert.Equal(content, NextMeetingJoin.TextWidth(JoinPlace.Under, content, Join));
    }

    [Fact]
    public void S2591_the_rule_is_the_column_beside_join_against_its_minimum()
    {
        var edge = NextMeetingJoin.MinTextColumn + Join + NextMeetingJoin.Gap;
        Assert.Equal(JoinPlace.Beside, NextMeetingJoin.Place(true, edge, Join));
        Assert.Equal(JoinPlace.Under, NextMeetingJoin.Place(true, edge - 1, Join));

        // A longer word for Join (German's "Teilnehmen") moves it under sooner.
        Assert.Equal(JoinPlace.Under, NextMeetingJoin.Place(true, edge, Join + 40));
    }

    [Fact]
    public void S2591_an_unmeasured_card_keeps_join_under()
        => Assert.Equal(JoinPlace.Under, NextMeetingJoin.Place(true, double.NaN, Join));

    [Theory]
    [InlineData(96)]
    [InlineData(496)]
    public void S2592_no_join_link_is_no_button_and_the_lines_take_the_full_width(double content)
    {
        Assert.Equal(JoinPlace.None, NextMeetingJoin.Place(hasJoin: false, content, Join));
        Assert.Equal(content, NextMeetingJoin.TextWidth(JoinPlace.None, content, Join));
    }
}
