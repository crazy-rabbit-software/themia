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
    public void S3624_the_keys_move_the_selection_when_it_is_on_screen_else_from_the_anchor()
    {
        Assert.Equal(Today.AddDays(9), CalendarShare.Step(Today.AddDays(2), selectedInView: true, Today, 7));
        Assert.Equal(Today.AddDays(-7), CalendarShare.Step(Today.AddDays(40), selectedInView: false, Today, -7));
        Assert.Equal(Today.AddDays(7), CalendarShare.Step(null, selectedInView: false, Today, 7));
    }
}
