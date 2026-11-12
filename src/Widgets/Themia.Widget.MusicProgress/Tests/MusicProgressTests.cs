using Themia.Core.Model;
using Xunit;

namespace Themia.Widget.MusicProgress.Tests;

/// <summary>[S-11330] The progress atom: one row in the inset, 200 to 520 wide, its time columns on their widest case.</summary>
public sealed class MusicProgressTests
{
    [Fact]
    public void S11330_one_caption_row_in_the_inset_one_height_200_to_520_wide()
    {
        var sizing = MusicProgressFace.Sizing();
        Assert.Equal(12 + 16 + 12, sizing.Natural.Height);
        Assert.True(sizing.FixedHeight && !sizing.FixedWidth);
        Assert.Equal((200d, 340d, 520d), (sizing.Min.Width, sizing.Natural.Width, sizing.Max.Width));
    }

    [Fact]
    public void S11330_a_time_column_is_as_wide_as_its_widest_case()
    {
        Assert.Equal(MusicProgressFace.TimeWidth("3:42"), MusicProgressFace.TimeWidth("1:11"));
        Assert.True(MusicProgressFace.TimeWidth("1:03:42") > MusicProgressFace.TimeWidth("3:42"));
        Assert.Equal(MusicProgressFace.TimeWidth("8:88"), MusicProgressFace.TimeWidth(string.Empty));
    }

    [Fact]
    public void S11330_an_unknown_hiding_mode_is_repaired_and_a_copy_keeps_it()
    {
        var config = new MusicProgressConfig { HideWhenIdle = (MusicHideWhenIdle)9 };
        Assert.Single(config.Repair());
        Assert.Equal(MusicHideWhenIdle.Never, config.HideWhenIdle);
        Assert.Equal(MusicHideWhenIdle.NotPlaying, ((MusicProgressConfig)new MusicProgressConfig { HideWhenIdle = MusicHideWhenIdle.NotPlaying }.Clone()).HideWhenIdle);
    }
}
