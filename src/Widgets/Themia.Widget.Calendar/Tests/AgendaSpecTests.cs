using Themia.Core.Model;
using Themia.Core.Office;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Widget.Calendar.Tests;

/// <summary>[S-3650] The agenda's rows as kit items.</summary>
public sealed class AgendaSpecTests
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.Now);

    private static CalendarEvent At(int hour, int minutes, string subject = "Design review")
    {
        var start = new DateTimeOffset(Today.ToDateTime(new TimeOnly(hour, 0), DateTimeKind.Local));
        return new CalendarEvent { Id = "e1", Subject = subject, Start = start, End = start.AddMinutes(minutes), Location = "Room 4", Organizer = "Sam", AccountId = "a", CalendarId = "c" };
    }

    [Fact]
    public void S3650_a_timed_event_reads_start_over_end_with_where_and_who()
    {
        var item = AgendaSpec.Item(new DayItem(At(10, 60), null), Today, new DateTimeOffset(Today.ToDateTime(new TimeOnly(8, 0), DateTimeKind.Local)), null, _ => "#FF0000");

        Assert.Equal("Design review", item.Title);
        Assert.Equal(Today, item.Group?.Day);
        Assert.False(item.Group?.Selected);
        Assert.NotNull(item.Schedule);
        Assert.False(item.Schedule!.Strong || item.Schedule.Word);
        Assert.NotNull(item.Schedule.End);
        Assert.Equal($"{item.Schedule.Start}–{item.Schedule.End}", item.Schedule.Span);
        Assert.Equal(("Room 4", "Sam", "#FF0000"), (item.Schedule.Where, item.Schedule.Who, item.Schedule.Mark));
    }

    [Fact]
    public void S3650_an_event_under_way_says_now_in_the_strong_step_and_the_selected_day_stands_out()
    {
        var now = new DateTimeOffset(Today.ToDateTime(new TimeOnly(10, 30), DateTimeKind.Local));
        var item = AgendaSpec.Item(new DayItem(At(10, 60), null), Today, now, Today, null);

        Assert.True(item.Schedule!.Strong && item.Schedule.Word);
        Assert.Equal(item.Schedule.Start, item.Schedule.Span);
        Assert.Null(item.Schedule.Mark);
        Assert.True(item.Group?.Selected);
    }

    [Fact]
    public void S3650_a_birthday_has_its_gift_and_a_note_and_no_time()
    {
        var item = AgendaSpec.Item(new DayItem(null, new Birthday("Mia Chen", Today.Month, Today.Day, null)), Today, DateTimeOffset.Now, null, null);

        Assert.Null(item.Schedule!.Start);
        Assert.NotNull(item.Schedule.Note);
        Assert.Equal(nameof(KitIcon.Gift), item.Lead.Value);
        Assert.Equal(KitTone.Caution, item.Tone);
    }

    /// <summary>[S-3652] [S-7065] An event's row opens on a double-click or Enter, through the view (Outlook or the provider's page); a birthday opens nothing.</summary>
    [Fact]
    public void S3652_an_event_opens_and_a_birthday_offers_nothing()
    {
        var open = KitMenu.Primary(AgendaSpec.Actions(new DayItem(At(10, 60), null), openable: true));
        Assert.Equal(AgendaSpec.OpenId, open?.Id);
        Assert.Empty(AgendaSpec.Actions(new DayItem(At(10, 60), null), openable: false)); // CalDAV: no page to open
        Assert.Empty(AgendaSpec.Actions(new DayItem(null, new Birthday("Mia", 1, 1, null)), openable: true));
    }
}
