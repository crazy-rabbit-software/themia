using Themia.Core.Model;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Widget.MusicCover.Tests;

/// <summary>[S-11320] The cover atom: a picture as large as its card, its settings repaired and copied.</summary>
public sealed class MusicCoverTests
{
    [Fact]
    public void S11320_S2449_retired_a_new_cover_is_a_square_and_the_card_goes_down_to_the_grids_least_and_up_freely()
    {
        var sizing = new MusicCoverModule().Descriptor.Sizing(new MusicCoverConfig());
        Assert.Equal(new Themia.Sdk.SizeDip(MusicCoverFace.Side, MusicCoverFace.Side), sizing.Natural);
        Assert.Equal(new Themia.Sdk.SizeDip(Themia.Sdk.WidgetSizing.LeastScaled, Themia.Sdk.WidgetSizing.LeastScaled), sizing.Min);
        Assert.Equal(new Themia.Sdk.SizeDip(Themia.Sdk.SizeStretch.Ceiling, Themia.Sdk.SizeStretch.Ceiling), sizing.Max);
        Assert.Null(typeof(MusicCoverConfig).GetProperty("Size"));
    }

    [Fact]
    public void S11320_hiding_is_repaired_and_copied()
    {
        var config = new MusicCoverConfig { HideWhenIdle = (MusicHideWhenIdle)9 };
        Assert.Single(config.Repair());
        Assert.Equal(MusicHideWhenIdle.Never, config.HideWhenIdle);

        var copy = Assert.IsType<MusicCoverConfig>(new MusicCoverConfig { HideWhenIdle = MusicHideWhenIdle.NotPlaying }.Clone());
        Assert.Equal(MusicHideWhenIdle.NotPlaying, copy.HideWhenIdle);
    }
}
