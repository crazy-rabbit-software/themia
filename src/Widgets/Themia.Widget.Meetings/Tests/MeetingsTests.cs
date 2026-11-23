using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Office;
using Themia.Core.Tests;
using Themia.Core.Widgets;
using Themia.Sdk.Items;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.Meetings.Tests;

/// <summary>
/// [S-16960]..[S-16964] The Meeting and the Meetings on the item base: which meetings a list shows and in what order, which
/// one a Meeting shows, a meeting as an item (Join at its end), and the old Next meeting and After this replaced in place.
/// </summary>
public sealed class MeetingsTests
{
    private static readonly DateTimeOffset Today = new(2026, 11, 12, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Now = Today.AddHours(10).AddMinutes(18);
    private static readonly TimeZoneInfo Utc = TimeZoneInfo.Utc;

    private static CalendarEvent At(string subject, double hour, double minutes = 30, string? join = null) => new()
    {
        Id = subject,
        Subject = subject,
        Start = Today.AddHours(hour),
        End = Today.AddHours(hour).AddMinutes(minutes),
        AccountId = "a",
        JoinUrl = join,
    };

    private static readonly CalendarEvent[] Day =
    [
        At("Standup", 9, 15),
        At("Design review", 10.5, 60, "https://teams.microsoft.com/l/meetup-join/x"),
        At("Declined", 12, 60) with { Declined = true },
        At("1:1", 14),
        At("Planning", 33.5),
        At("Next week", 24 * 8),
        CalendarEvent.AllDayEvent("holiday", "Holiday", DateOnly.FromDateTime(Today.DateTime), DateOnly.FromDateTime(Today.DateTime).AddDays(1)),
    ];

    private static string[] Subjects(IEnumerable<CalendarEvent> events) => [.. events.Select(e => e.Subject)];

    [Fact]
    public void S16962_a_list_shows_what_is_coming_up_past_or_both_within_its_reach_in_its_order()
    {
        Assert.Equal(["Design review", "1:1", "Planning"], Subjects(MeetingItems.Select(Day, new MeetingsConfig { Days = 7 }, Now, Utc)));
        Assert.Equal(["Design review", "1:1"], Subjects(MeetingItems.Select(Day, new MeetingsConfig { Days = 1 }, Now, Utc)));
        Assert.Equal(["Design review", "1:1", "Planning", "Next week"], Subjects(MeetingItems.Select(Day, new MeetingsConfig(), Now, Utc)));
        Assert.Equal(["Standup"], Subjects(MeetingItems.Select(Day, new MeetingsConfig { When = MeetingWhen.Past }, Now, Utc)));
        Assert.Equal(["Planning", "1:1", "Design review", "Standup"], Subjects(MeetingItems.Select(Day, new MeetingsConfig { When = MeetingWhen.Both, Days = 2, Order = MeetingOrder.Latest }, Now, Utc)));
    }

    [Fact]
    public void S16962_S11408_leaving_out_the_next_meeting_keeps_those_after_it()
    {
        // The design review starts in 12 minutes: the next one; after it, those still to start.
        Assert.Equal(["1:1", "Planning"], Subjects(MeetingItems.Select(Day, new MeetingsConfig { AfterNext = true, Days = 7 }, Now, Utc)));
    }

    [Fact]
    public void S16960_a_meeting_shows_the_next_one_or_the_one_picked_while_it_is_not_over()
    {
        Assert.Equal("Design review", MeetingItems.Pick(Day, new MeetingConfig(), Now)!.Subject);

        var planning = Day.Single(e => e.Subject == "Planning");
        var picked = new MeetingConfig { Pick = MeetingPick.Chosen, Chosen = MeetingItems.Id(planning) };
        Assert.Equal("Planning", MeetingItems.Pick(Day, picked, Now)!.Subject);

        // Over: the next one again.
        Assert.Equal("Next week", MeetingItems.Pick(Day, picked, planning.End.AddMinutes(1))!.Subject);
        var standup = new MeetingConfig { Pick = MeetingPick.Chosen, Chosen = MeetingItems.Id(Day[0]) };
        Assert.Equal("Design review", MeetingItems.Pick(Day, standup, Now)!.Subject);
        Assert.Equal(["Design review", "1:1", "Planning", "Next week"], Subjects(MeetingItems.Upcoming(Day, Now)));

        // A picked one without its id is the next one, loudly.
        var bad = new MeetingConfig { Pick = MeetingPick.Chosen };
        Assert.NotEmpty(bad.Repair());
        Assert.Equal(MeetingPick.Next, bad.Pick);
    }

    [Fact]
    public void S16963_a_meeting_with_a_join_link_ends_with_join_and_says_how_long_until_it()
    {
        var item = MeetingItems.Item(Day[1] with { Location = "Room 4.12", Organizer = "Ana" }, new MeetingConfig(), Now, Utc, Locale.En);

        Assert.Equal("Design review", item.Title);
        Assert.Equal((ItemEndKind.Action, "Join"), (item.End!.Kind, item.End.Value));
        Assert.StartsWith("in 12 min", item.Subtitle, StringComparison.Ordinal);
        Assert.Equal("Room 4.12 · Ana", item.Note);
        Assert.Null(item.Bar);

        // No place nor organiser where the switches are off.
        Assert.Equal(string.Empty, MeetingItems.Item(Day[1] with { Location = "x", Organizer = "y" }, new MeetingConfig { ShowPlace = false, ShowOrganizer = false }, Now, Utc, Locale.En).Note);
    }

    [Fact]
    public void S16963_without_a_link_its_end_is_its_start_under_way_its_bar_how_far_and_over_none()
    {
        var oneOnOne = MeetingItems.Item(Day[3], new MeetingConfig(), Now, Utc, Locale.En);
        Assert.Equal(ItemEndKind.Text, oneOnOne.End!.Kind);
        Assert.Equal(LocaleFormat.Time(Locale.En, Today.AddHours(14).DateTime), oneOnOne.End.Value);

        var running = MeetingItems.Item(Day[1], new MeetingConfig(), Today.AddHours(11), Utc, Locale.En);
        Assert.True(running.Strong);
        Assert.Equal(0.5, running.Bar!.To, 6);

        var over = MeetingItems.Item(Day[0], new MeetingConfig(), Now, Utc, Locale.En);
        Assert.Null(over.End);
        Assert.True(over.Muted);
    }

    [Fact]
    public void S16963_a_meeting_leaves_out_its_lead_and_bar_by_default_and_a_copy_keeps_everything()
    {
        var config = new MeetingsConfig { When = MeetingWhen.Both, Order = MeetingOrder.Latest, Days = 7, AfterNext = true, Rows = 4, Gap = 2, Style = ItemStyle.Table, ColumnTitles = false, ShowPlace = false, AccountIds = ["a"] };
        Assert.Equal([ItemPart.Lead, ItemPart.Bar], config.HiddenParts);
        var copy = Assert.IsType<MeetingsConfig>(config.Clone());
        Assert.Equal((MeetingWhen.Both, MeetingOrder.Latest, 7, true, 4, 2, ItemStyle.Table, false, false, "a"), (copy.When, copy.Order, copy.Days, copy.AfterNext, copy.Rows, copy.Gap, copy.Style, copy.ColumnTitles, copy.ShowPlace, copy.AccountIds.Single()));

        var off = new MeetingsConfig { Days = 5, ShowBirthdays = true };
        Assert.NotEmpty(off.Repair());
        Assert.Equal((14, false), (off.Days, off.ShowBirthdays));
    }

    [Fact]
    public void S16964_a_placed_next_meeting_becomes_a_meeting_showing_the_next_one()
    {
        var config = Assert.IsType<MeetingConfig>(ConfigJson.Read("NextMeeting", """{ "kind": "nextMeeting", "showPlace": false, "accountIds": ["a"] }"""));
        Assert.Equal((MeetingPick.Next, false, "a"), (config.Pick, config.ShowPlace, config.AccountIds.Single()));
    }

    [Fact]
    public void S16964_a_placed_after_this_becomes_a_list_coming_up_without_the_next_one()
    {
        var config = Assert.IsType<MeetingsConfig>(ConfigJson.Read("UpNext", """{ "kind": "upNext", "rows": 5 }"""));
        Assert.Equal((MeetingWhen.Future, true, ItemStyle.ColumnList, 5), (config.When, config.AfterNext, config.Style, config.Rows));
    }

    [Fact]
    public void S16961_a_list_is_its_rows_of_one_item_with_join()
    {
        var sizing = new MeetingsModule().Descriptor.Sizing(new MeetingsConfig());

        // No lead nor bar: the title over its subtitle beside the 28 button, the insets: 60 a meeting.
        Assert.Equal(3 * 60, sizing.Natural.Height);
        var face = new MeetingModule().Descriptor.Sizing(new MeetingConfig());
        Assert.Equal((60.0, 60.0), (face.Min.Height, face.Natural.Height));
    }
}
