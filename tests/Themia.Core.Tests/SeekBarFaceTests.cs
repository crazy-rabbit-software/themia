using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-12816] The pointer never changes a widget's layout (owner, 2026-11-19: "some widgets like the now playing scrubber
/// changes text sizes when hovering over it with the mouse … this should not be"): the seek bar's thumb, shown under the
/// pointer, is never ink the block it stands in is scaled by.
/// </summary>
public class SeekBarFaceTests
{
    [Fact]
    public void S12816_the_bar_inks_its_track_and_the_thumb_fits_its_height()
    {
        Assert.Equal(new LineInk((SeekBarFace.Height - SeekBarFace.Track) / 2, (SeekBarFace.Height - SeekBarFace.Track) / 2), SeekBarFace.Ink);
        Assert.True(SeekBarFace.Ink.Over > 0);
        Assert.True(SeekBarFace.Thumb <= SeekBarFace.Height);
    }

    [Theory]
    [InlineData(true, false, false, false)]
    [InlineData(true, true, false, true)]
    [InlineData(true, false, true, true)]
    [InlineData(false, true, true, false)]
    public void S12816_the_thumb_shows_under_the_pointer_or_a_scrub_of_a_seekable_track(bool canSeek, bool pointed, bool scrubbing, bool shown)
        => Assert.Equal(shown, SeekBarFace.ThumbShown(canSeek, pointed, scrubbing));

    /// <summary>
    /// Music progress's line (the times on Caption, the bar centred in the line) in a wide, short card, where the room's
    /// height holds the scale: the block keeps the scale its times give it with the bar's ink, and would have shrunk had
    /// the shown thumb counted as ink reaching the bar's edges (the bug: the times drew smaller under the pointer).
    /// </summary>
    [Fact]
    public void S12816_a_music_progress_line_keeps_its_scale_when_the_thumb_shows()
    {
        var line = FamilyType.Caption.LineHeight;
        var times = LineInk.Of(FamilyType.Caption);
        var barTop = (line - SeekBarFace.Height) / 2;

        static LineInk Beside(LineInk a, LineInk b) => new(Math.Min(a.Over, b.Over), Math.Min(a.Under, b.Under));
        var rest = Beside(times, new LineInk(barTop + SeekBarFace.Ink.Over, barTop + SeekBarFace.Ink.Under));
        var thumbAsInk = Beside(times, new LineInk(barTop, barTop));

        const double width = 876, height = 28, intrinsic = 317;
        var fill = BlockLayout.Fill(width, height, intrinsic, line, rest);
        Assert.Equal(BlockLayout.Fill(width, height, intrinsic, line, times), fill);
        Assert.True(fill.Scale > 1);
        Assert.NotEqual(fill.Scale, BlockLayout.Fill(width, height, intrinsic, line, thumbAsInk).Scale);
    }
}
