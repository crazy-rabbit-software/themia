using Themia.Core.Model;
using Xunit;

namespace Themia.Widget.NextMeeting.Tests;

/// <summary>[S-4820] Next meeting's config (moved from Core's DeskInfoWidgetsTests, unchanged).</summary>
public sealed class NextMeetingTests
{
    [Fact]
    public void S4820_next_meeting_never_asks_for_birthdays_and_keeps_its_calendars_on_clone()
    {
        var config = new NextMeetingConfig { AccountIds = ["a"], Calendars = new() { ["a"] = ["c1"] } };
        Assert.False(config.ShowBirthdays);

        var clone = Assert.IsType<NextMeetingConfig>(config.Clone());
        Assert.Equal(["a"], clone.AccountIds);
        Assert.Equal(["c1"], clone.Calendars["a"]);

        Assert.False(Assert.IsType<NextMeetingConfig>(new NextMeetingConfig { ShowPlace = false }.Clone()).ShowPlace);

        var edited = new NextMeetingConfig { ShowBirthdays = true };
        Assert.NotEmpty(edited.Repair());
        Assert.False(edited.ShowBirthdays);
    }

    [Fact]
    public void S11407_the_block_is_one_height_its_lines_or_its_end_column_and_260_to_520_wide()
    {
        var all = new NextMeetingModule().Descriptor.Sizing(new NextMeetingConfig());
        var title = new NextMeetingModule().Descriptor.Sizing(new NextMeetingConfig { ShowPlace = false, ShowOrganizer = false });

        // The title (28) and three lines of 20, 4 apart, in the inset: 12 + 100 + 12.
        Assert.Equal(new Themia.Sdk.SizeDip(320, 124), all.Natural);
        Assert.True(all.FixedHeight);
        Assert.Equal((260, 520), (all.Min.Width, all.Max.Width));

        // The title and the time alone: the end column (the countdown over Join, 52) is the taller.
        Assert.Equal(12 + NextMeetingJoin.EndHeight(hasJoin: true) + 12, title.Natural.Height);
        Assert.Empty(all.Problems());
    }
}
