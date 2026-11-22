using Themia.Core.Model;
using Xunit;

namespace Themia.Widget.Music.Tests;

/// <summary>[S-11311] Now playing's track atom: one height, 200 to 420 wide, its one option repaired and copied.</summary>
public sealed class MusicTests
{
    [Fact]
    public void S11311_the_two_lines_and_the_transport_in_the_inset_one_height_200_to_420_wide()
    {
        var sizing = MusicFace.Sizing();

        // The inset, the title and the artist line, 4, the transport, the inset: a Small cover's side.
        Assert.Equal(12 + 20 + 20 + 4 + 28 + 12, sizing.Natural.Height);
        Assert.Equal(96, sizing.Natural.Height);
        Assert.True(sizing.FixedHeight && !sizing.FixedWidth);
        Assert.Equal((200d, 244d, 420d), (sizing.Min.Width, sizing.Natural.Width, sizing.Max.Width));
    }

    [Fact]
    public void S11311_an_unknown_hiding_mode_is_repaired_and_a_copy_keeps_it()
    {
        var config = new MusicConfig { HideWhenIdle = (MusicHideWhenIdle)9 };
        Assert.Single(config.Repair());
        Assert.Equal(MusicHideWhenIdle.NotPlaying, ((MusicConfig)new MusicConfig { HideWhenIdle = MusicHideWhenIdle.NotPlaying }.Clone()).HideWhenIdle);
    }
}
