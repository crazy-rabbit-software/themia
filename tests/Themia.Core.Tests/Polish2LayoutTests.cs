using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-1622] The ladder's "first size that fits"; [S-1624] the mixer's fill.
/// </summary>
public class Polish2LayoutTests
{
    // -----------------------------------------------------------------------------------------
    // [S-1622] Disclosure.Fits / TakeFirst
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void S1622_take_first_takes_the_richest_size_that_fits_and_keeps_the_ladder_open()
    {
        var room = new Disclosure(100, 100, gap: 8);
        room.Reserve(40);                                   // 60 left

        Assert.Equal(1, room.TakeFirst([80, 40, 20]));      // 40 + gap 8 fits; 80 does not
        Assert.Equal(12, room.Remaining);
        Assert.True(room.Take(4));                          // still open after a smaller size was taken
    }

    [Fact]
    public void S1622_take_first_with_no_size_that_fits_is_a_miss_that_closes_the_ladder()
    {
        var room = new Disclosure(100, 50, gap: 8);
        room.Reserve(40);

        Assert.Equal(-1, room.TakeFirst([30, 20]));
        Assert.False(room.Take(1));
        Assert.Equal(-1, room.TakeFirst([1]));
        Assert.Equal(-1, new Disclosure(100, 100).TakeFirst([]));
    }

    [Fact]
    public void S1622_fits_asks_without_taking_or_closing()
    {
        var room = new Disclosure(100, 60, gap: 8);
        room.Reserve(20);                                   // 40 left

        Assert.False(room.Fits(40));                        // 40 + gap
        Assert.True(room.Fits(32));
        Assert.Equal(40, room.Remaining);
        Assert.True(room.Take(32));
    }

    // -----------------------------------------------------------------------------------------
    // [S-1624] what a mixer row's second line says
    // -----------------------------------------------------------------------------------------

    [Theory]
    [InlineData(false, false, true, 1, "Playing")]
    [InlineData(false, false, true, 3, "Playing")]
    [InlineData(false, false, false, 1, "Silent")]
    [InlineData(true, false, true, 2, "Muted")]
    [InlineData(false, true, true, 1, "Windows sounds")]
    public void S1624_the_caption_says_what_the_app_is_doing(bool muted, bool system, bool active, int sessions, string expected)
    {
        var row = new MixerRow("app", "App", null, 0.5, muted, active, system, sessions);

        Assert.Equal(expected, MixerFill.Detail(row));
    }
}
