using Themia.Core.Model;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Widget.MusicCover.Tests;

/// <summary>[S-11320] The cover atom: a square by its Size, its settings repaired and copied.</summary>
public sealed class MusicCoverTests
{
    [Theory]
    [InlineData(ReadoutSize.Small, 96)]
    [InlineData(ReadoutSize.Medium, 160)]
    [InlineData(ReadoutSize.Large, 240)]
    public void S11320_the_cover_is_a_square_of_its_size_fixed_both_ways(ReadoutSize size, double side)
    {
        var sizing = MusicCoverFace.Sizing(new MusicCoverConfig { Size = size });
        Assert.Equal(new Themia.Sdk.SizeDip(side, side), sizing.Natural);
        Assert.True(sizing.FixedWidth && sizing.FixedHeight);
    }

    [Fact]
    public void S11320_size_and_hiding_are_repaired_and_copied()
    {
        var config = new MusicCoverConfig { Size = (ReadoutSize)9, HideWhenIdle = (MusicHideWhenIdle)9 };
        Assert.Equal(2, config.Repair().Count);
        Assert.Equal((ReadoutSize.Medium, MusicHideWhenIdle.Never), (config.Size, config.HideWhenIdle));

        var copy = Assert.IsType<MusicCoverConfig>(new MusicCoverConfig { Size = ReadoutSize.Small, HideWhenIdle = MusicHideWhenIdle.NotPlaying }.Clone());
        Assert.Equal((ReadoutSize.Small, MusicHideWhenIdle.NotPlaying), (copy.Size, copy.HideWhenIdle));
    }
}
