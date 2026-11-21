using Themia.Core.Links;
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
    public void S11331_a_scrub_says_its_track_and_the_position_under_the_pointer_and_nothing_when_abandoned()
    {
        var length = TimeSpan.FromSeconds(200);
        Assert.Equal(new TrackLink("k", TimeSpan.FromSeconds(50)), MusicProgressFace.Scrub("k", 0.25, length));
        Assert.Equal(new TrackLink("k", length), MusicProgressFace.Scrub("k", 1, length));

        // Abandoned (the bar's -1), or no track: nothing is said.
        Assert.Null(MusicProgressFace.Scrub("k", -1, length));
        Assert.Null(MusicProgressFace.Scrub("k", double.NaN, length));
        Assert.Null(MusicProgressFace.Scrub(null, 0.5, length));
        Assert.Null(MusicProgressFace.Scrub(string.Empty, 0.5, length));
    }

    [Fact]
    public void S11331_the_bar_publishes_its_scrub_and_follows_nothing()
    {
        var links = new MusicProgressModule().Descriptor.Links;
        Assert.Equal([LinkTopicId.ScrubbedTrack], links.Publishes);
        Assert.Empty(links.Follows);
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
