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

        var edited = new NextMeetingConfig { ShowBirthdays = true };
        Assert.NotEmpty(edited.Repair());
        Assert.False(edited.ShowBirthdays);
    }
}
