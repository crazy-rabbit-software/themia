using Themia.Core.Links;
using Themia.Core.Model;
using Themia.Core.Office;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-3620]..[S-3625] What the calendar widgets on one view share: the selected day, the paged range, the hovered event.</summary>
public sealed class CalendarShareTests
{
    private static readonly DateOnly Today = new(2026, 10, 11);

    private static CalendarEvent Event(string id, string account = "a", string? calendar = null) => new()
    {
        Id = id,
        AccountId = account,
        CalendarId = calendar,
        Start = new DateTimeOffset(2026, 10, 11, 9, 0, 0, TimeSpan.Zero),
        End = new DateTimeOffset(2026, 10, 11, 10, 0, 0, TimeSpan.Zero),
    };

    [Fact]
    public void S3620_a_click_selects_a_day_and_a_second_click_lets_it_go()
    {
        Assert.Equal(Today, CalendarShare.Toggle(null, Today));
        Assert.Equal(Today.AddDays(1), CalendarShare.Toggle(Today, Today.AddDays(1)));
        Assert.Null(CalendarShare.Toggle(Today, Today));
    }

    [Theory]
    [InlineData(CalendarStyle.Month)]
    [InlineData(CalendarStyle.Week)]
    [InlineData(CalendarStyle.Day)]
    [InlineData(CalendarStyle.Year)]
    public void S3621_the_dated_layouts_bring_the_selected_day_into_view(CalendarStyle layout)
    {
        var selected = new DateOnly(2027, 3, 3);

        var anchor = CalendarShare.AnchorFor(layout, Today, selected);

        Assert.True(CalendarRules.InView(layout, anchor, Today, WeekStartDay.Monday, selected));
    }

    [Fact]
    public void S3621_a_day_in_the_period_on_screen_does_not_page_it()
    {
        var anchor = CalendarShare.AnchorFor(CalendarStyle.Month, Today, Today.AddDays(5));

        Assert.Equal(
            CalendarRules.ViewRange(CalendarStyle.Month, Today, Today, WeekStartDay.Monday),
            CalendarRules.ViewRange(CalendarStyle.Month, anchor, Today, WeekStartDay.Monday));
    }

    [Fact]
    public void S3622_the_agenda_keeps_its_range_and_turns_to_the_page_holding_the_day()
    {
        Assert.Equal(Today, CalendarShare.AnchorFor(CalendarStyle.Agenda, Today, Today.AddDays(9)));

        // Rows: two today, two tomorrow, one in three days, three in a week; pages start at rows 0, 3, 6.
        DateOnly[] days = [Today, Today, Today.AddDays(1), Today.AddDays(1), Today.AddDays(3), Today.AddDays(7), Today.AddDays(7), Today.AddDays(7)];
        int[] pages = [0, 3, 6];

        Assert.Equal(0, CalendarShare.AgendaPageOf(days, pages, Today));
        Assert.Equal(0, CalendarShare.AgendaPageOf(days, pages, Today.AddDays(1)));
        Assert.Equal(1, CalendarShare.AgendaPageOf(days, pages, Today.AddDays(2)));
        Assert.Equal(1, CalendarShare.AgendaPageOf(days, pages, Today.AddDays(3)));
        Assert.Equal(1, CalendarShare.AgendaPageOf(days, pages, Today.AddDays(6)));
        Assert.Equal(0, CalendarShare.AgendaPageOf(days, pages, Today.AddDays(-4)));
        Assert.Null(CalendarShare.AgendaPageOf(days, pages, Today.AddDays(8)));
        Assert.Null(CalendarShare.AgendaPageOf([], [0], Today));
    }

    [Fact]
    public void S3623_the_hovered_event_is_the_same_event_in_every_widget_and_the_rest_dim()
    {
        var standup = Event("e1");
        var sameElsewhere = Event("e1") with { Subject = "copy", SourceIndex = 3 };

        Assert.Equal(CalendarShare.Key(standup), CalendarShare.Key(sameElsewhere));
        Assert.False(CalendarShare.Dimmed(null, standup));
        Assert.False(CalendarShare.Dimmed(CalendarShare.Key(standup), sameElsewhere));
        Assert.True(CalendarShare.Dimmed(CalendarShare.Key(standup), Event("e2")));

        // The same provider id in another account or calendar is another event.
        Assert.True(CalendarShare.Dimmed(CalendarShare.Key(standup), Event("e1", account: "b")));
        Assert.True(CalendarShare.Dimmed(CalendarShare.Key(standup), Event("e1", calendar: "work")));
    }

    [Fact]
    public void S3623_the_hovered_event_is_a_hover_that_leaves_with_its_widget()
    {
        Assert.Equal(LinkLifetime.Hover, LinkTopics.HoveredEvent.Lifetime);

        var board = new LinkBoard();
        var month = board.Join();
        var agenda = board.Join();
        EventLink? heard = new("x", null, "x");
        agenda.Watch(LinkTopics.HoveredEvent, value => heard = value);

        month.Set(LinkTopics.HoveredEvent, CalendarShare.Key(Event("e1")));
        Assert.Equal(CalendarShare.Key(Event("e1")), heard);

        month.Dispose();
        Assert.Null(heard);
    }

    [Fact]
    public void S3627_a_month_says_its_own_days_and_the_other_styles_what_they_draw()
    {
        // October 2026 fills six weeks from 28 September; its period is still 1..31 October.
        Assert.Equal(new RangeLink(new(2026, 10, 1), new(2026, 10, 31)), CalendarShare.Period(CalendarStyle.Month, Today, Today, WeekStartDay.Monday));
        Assert.Equal(new RangeLink(new(2026, 2, 1), new(2026, 2, 28)), CalendarShare.Period(CalendarStyle.Month, new(2026, 2, 14), Today, WeekStartDay.Monday));
        Assert.Equal(new RangeLink(new(2026, 10, 5), new(2026, 10, 11)), CalendarShare.Period(CalendarStyle.Week, Today, Today, WeekStartDay.Monday));
        Assert.Equal(new RangeLink(Today, Today), CalendarShare.Period(CalendarStyle.Day, Today, Today, WeekStartDay.Monday));
        Assert.Equal(new RangeLink(new(2026, 1, 1), new(2026, 12, 31)), CalendarShare.Period(CalendarStyle.Year, Today, Today, WeekStartDay.Monday));
    }

    [Fact]
    public void S3627_the_agendas_period_is_the_days_of_its_pages_rows()
    {
        DateOnly[] days = [Today, Today, Today.AddDays(1), Today.AddDays(1), Today.AddDays(3), Today.AddDays(7), Today.AddDays(7), Today.AddDays(9)];
        int[] pages = [0, 3, 6];

        Assert.Equal(new RangeLink(Today, Today.AddDays(1)), CalendarShare.AgendaPeriod(days, pages, 0));
        Assert.Equal(new RangeLink(Today.AddDays(1), Today.AddDays(7)), CalendarShare.AgendaPeriod(days, pages, 1));
        Assert.Equal(new RangeLink(Today.AddDays(7), Today.AddDays(9)), CalendarShare.AgendaPeriod(days, pages, 2));
        Assert.Equal(new RangeLink(Today.AddDays(7), Today.AddDays(9)), CalendarShare.AgendaPeriod(days, pages, 9));
        Assert.Null(CalendarShare.AgendaPeriod([], [0], 0));
    }

    [Fact]
    public void S3628_a_follower_lands_on_the_selected_day_then_today_then_the_periods_first_day()
    {
        var november = new RangeLink(new(2026, 11, 1), new(2026, 11, 30));
        var october = new RangeLink(new(2026, 10, 1), new(2026, 10, 31));

        Assert.Equal(new DateOnly(2026, 11, 1), CalendarShare.AnchorIn(november, Today, selected: null));
        Assert.Equal(new DateOnly(2026, 11, 20), CalendarShare.AnchorIn(november, Today, selected: new(2026, 11, 20)));
        Assert.Equal(Today, CalendarShare.AnchorIn(october, Today, selected: null));
        Assert.Equal(Today, CalendarShare.AnchorIn(october, Today, selected: new(2026, 12, 3)));
        Assert.Equal(new DateOnly(2026, 10, 2), CalendarShare.AnchorIn(october, Today, selected: new(2026, 10, 2)));
    }

    [Theory]
    [InlineData(CalendarStyle.Month)]
    [InlineData(CalendarStyle.Week)]
    [InlineData(CalendarStyle.Day)]
    [InlineData(CalendarStyle.Year)]
    public void S3626_a_header_paging_a_month_brings_every_calendar_of_the_view_to_it(CalendarStyle body)
    {
        // The header (a month) pages forward and says its period; a body of any dated style hears it and shows the period
        // of its own that holds the day it lands on, which lies in the header's month.
        var board = new LinkBoard();
        var header = board.Join();
        var follower = board.Join();
        var anchor = Today;
        follower.Watch(LinkTopics.VisibleRange, period =>
        {
            if (period is { } p)
            {
                anchor = CalendarShare.AnchorIn(p, Today, selected: null);
            }
        });

        var next = CalendarRules.Step(CalendarStyle.Month, Today, 1, WeekStartDay.Monday);
        header.Set(LinkTopics.VisibleRange, CalendarShare.Period(CalendarStyle.Month, next, Today, WeekStartDay.Monday));

        Assert.Equal(new DateOnly(2026, 11, 1), anchor);
        Assert.True(CalendarRules.InView(body, anchor, Today, WeekStartDay.Monday, new DateOnly(2026, 11, 1)));

        // A body that pages back says its own period, and the header hears it as any member does.
        RangeLink? heard = null;
        header.Watch(LinkTopics.VisibleRange, period => heard = period);
        follower.Set(LinkTopics.VisibleRange, CalendarShare.Period(body, Today, Today, WeekStartDay.Monday));
        Assert.True(heard!.Contains(Today));
        Assert.Equal(LinkLifetime.Sticky, LinkTopics.VisibleRange.Lifetime);
    }

    [Fact]
    public void S3629_a_wheel_notch_pages_one_period_and_parts_of_a_notch_are_carried()
    {
        Assert.Equal((-1, 0), CalendarShare.Wheel(0, 120));
        Assert.Equal((1, 0), CalendarShare.Wheel(0, -120));
        Assert.Equal((-2, 0), CalendarShare.Wheel(0, 240));

        var (steps, carry) = CalendarShare.Wheel(0, 60);
        Assert.Equal((0, 60), (steps, carry));
        Assert.Equal((-1, 0), CalendarShare.Wheel(carry, 60));

        // Turning back drops what was carried the other way.
        Assert.Equal((0, -40), CalendarShare.Wheel(60, -40));
    }

    [Fact]
    public void S3624_the_keys_move_the_selection_when_it_is_on_screen_else_from_the_anchor()
    {
        Assert.Equal(Today.AddDays(9), CalendarShare.Step(Today.AddDays(2), selectedInView: true, Today, 7));
        Assert.Equal(Today.AddDays(-7), CalendarShare.Step(Today.AddDays(40), selectedInView: false, Today, -7));
        Assert.Equal(Today.AddDays(7), CalendarShare.Step(null, selectedInView: false, Today, 7));
    }
}
