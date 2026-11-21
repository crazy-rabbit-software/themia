using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-12816] The pointer never changes a widget's layout (owner, 2026-11-19: "some widgets like the now playing scrubber
/// changes text sizes when hovering over it with the mouse … this should not be"): the seek bar's thumb is drawn over the
/// bar under the pointer, never laid out; [S-15104] it shows on a thin rail only.
/// </summary>
public class SeekBarFaceTests
{
    [Theory]
    [InlineData(true, false, false, false)]
    [InlineData(true, true, false, true)]
    [InlineData(true, false, true, true)]
    [InlineData(false, true, true, false)]
    public void S12816_the_thumb_shows_under_the_pointer_or_a_scrub_of_a_seekable_track(bool canSeek, bool pointed, bool scrubbing, bool shown)
        => Assert.Equal(shown, SeekBarFace.ThumbShown(canSeek, pointed, scrubbing, AtomRules.BarInk));

    [Fact]
    public void S15104_a_rail_as_thick_as_the_thumb_or_thicker_is_its_own_handle()
    {
        Assert.True(SeekBarFace.ThumbShown(true, true, false, SeekBarFace.Thumb - 1));
        Assert.False(SeekBarFace.ThumbShown(true, true, false, SeekBarFace.Thumb));
        Assert.False(SeekBarFace.ThumbShown(true, false, true, 24));
    }

    [Fact]
    public void S15104_a_thin_rails_thumb_stays_inside_the_spacing_round_it()
        => Assert.True((SeekBarFace.Thumb - AtomRules.BarInk) / 2 <= AtomSpace.Pad);
}
