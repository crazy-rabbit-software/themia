using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Widget.RainNextHour.Tests;

/// <summary>[S-5126] [S-11140] The rain atom's one layout and its size, on the family's tokens.</summary>
public sealed class RainNextHourTests
{
    [Fact]
    public void S11140_the_sentence_its_caption_and_the_bars_at_one_height_220_to_420_wide()
    {
        var sizing = RainLayout.Sizing();

        Assert.Equal(36 + 28 + 20 + 8 + (48 + 4 + 16) + 12, sizing.Natural.Height);
        Assert.True(sizing.FixedHeight);
        Assert.Equal((220.0, 300.0, 420.0), (sizing.Min.Width, sizing.Natural.Width, sizing.Max.Width));
    }

    [Fact]
    public void S5126_only_rain_to_tell_of_draws_the_sentence_and_the_bars()
    {
        Assert.True(RainLayout.Warns(RainHeadline.StartsIn));
        Assert.True(RainLayout.Warns(RainHeadline.StopsIn));
        Assert.True(RainLayout.Warns(RainHeadline.Continues));
        Assert.False(RainLayout.Warns(RainHeadline.Dry));
        Assert.False(RainLayout.Warns(RainHeadline.Outdated));
    }
}
