using Themia.Core.Widgets;
using Themia.Core.Model;
using Xunit;

namespace Themia.Widget.MusicProgress.Tests;

/// <summary>[S-11330] [S-15104] The progress atom: the bar alone, a bar atom; its times are text atoms of the Now playing family.</summary>
public sealed class MusicProgressTests
{
    [Fact]
    public void S11330_the_progress_is_a_bar_atom_a_thin_rail_in_the_spacing_free_on_both_axes()
    {
        var sizing = new MusicProgressModule().Descriptor.Sizing(new MusicProgressConfig());
        Assert.Equal(new Themia.Sdk.SizeDip(MusicProgressFace.NaturalWidth, AtomRules.Side(AtomRules.BarInk)), sizing.Natural);
        Assert.False(sizing.FixedHeight || sizing.FixedWidth);
        Assert.True(sizing.Min.Height < sizing.Natural.Height);
        Assert.Equal(8, new MusicProgressModule().Descriptor.Add.Rank);
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
