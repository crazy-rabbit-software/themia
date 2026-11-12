using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Widget.FocusSession.Tests;

/// <summary>[S-4426] The focus widget's ladder on the timers' face.</summary>
public sealed class FocusSessionTests
{
    [Fact]
    public void S4426_the_time_alone_on_a_tile_too_short_for_the_controls()
    {
        // 8 shorter than XS's view: the floor step's line, 8 and the row do not fit together.
        var xs = FocusSessionLayout.Plan(new WidgetArea(120, 76), "00:00");
        Assert.False(xs.Controls);
        Assert.Equal(0, xs.Ring);
        Assert.Equal(FamilyType.Big28, xs.Step);
    }

    [Fact]
    public void S4426_the_controls_and_the_line_as_they_fit_the_ring_from_expanded_both_ways()
    {
        var s = FocusSessionLayout.Plan(new WidgetArea(200, 114), "00:00");
        Assert.True(s.Controls);
        Assert.True(s.Status);

        var m = FocusSessionLayout.Plan(new WidgetArea(320, 204), "00:00");
        Assert.True(m.Controls);
        Assert.True(m.Status);
        Assert.Equal(0, m.Ring);

        // L: a ring the box's short side over the row, the time and its line in its hole.
        var l = FocusSessionLayout.Plan(new WidgetArea(560, 384), "00:00");
        Assert.Equal(384 - FamilySpace.Inset - FamilyShape.Button - FamilySpace.S2, l.Ring);
        Assert.True(l.Status);
        Assert.Equal(FamilyType.Hero96, l.Step);
    }

    [Fact]
    public void S4426_a_session_of_an_hour_or_more_is_sized_for_its_hours()
    {
        var minutes = FocusSessionLayout.Plan(new WidgetArea(320, 204), FocusSessionRules.WidestClock(30));
        var hours = FocusSessionLayout.Plan(new WidgetArea(320, 204), FocusSessionRules.WidestClock(90));
        Assert.True(FamilyType.BaseOf(hours.Step) <= FamilyType.BaseOf(minutes.Step));
    }
}
