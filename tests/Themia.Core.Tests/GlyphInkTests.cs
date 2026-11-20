using Themia.App.Shell;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-14240] A readout's lead glyph stands its ink on the card's insets, not its box (owner, 2026-11-19, WR-1: "there
/// should not be a space at the top"): the font's glyphs ink only part of their em box.
/// </summary>
public class GlyphInkTests
{
    [Fact]
    public void S14240_the_meters_glyphs_ink_where_the_font_draws_them()
    {
        // Read off SegoeIcons.ttf (scripts/glyph-ink.py): the memory chip an eighth inside its box over and under,
        // the drive a flat bar through the middle, the battery a third over and under and an eighth from its start.
        Assert.Equal(new GlyphBox(0, 0.125, 0.9375, 0.875), GlyphInk.Of(Glyphs.Memory));
        Assert.Equal(new GlyphBox(0, 0.3125, 1, 0.6875), GlyphInk.Of(Glyphs.Disk));
        Assert.Equal(new GlyphBox(0.125, 0.28125, 0.9375, 0.71875), GlyphInk.Of(Glyphs.Battery8));
    }

    [Fact]
    public void S14240_a_glyph_that_inks_its_whole_box_and_any_other_text_take_the_whole_box()
    {
        Assert.Equal(GlyphBox.Whole, GlyphInk.Of(Glyphs.Cpu));
        Assert.Equal(GlyphBox.Whole, GlyphInk.Of(null));
        Assert.Equal(GlyphBox.Whole, GlyphInk.Of(string.Empty));
        Assert.Equal(GlyphBox.Whole, GlyphInk.Of("47"));
        Assert.Equal(GlyphBox.Whole, GlyphInk.Of("A"));
    }

    [Fact]
    public void S14240_a_lead_glyph_inks_where_its_glyph_does_in_its_box_centred_on_the_line()
    {
        // The Number look's reading at 28 on a 36 line, led by a 28 glyph: its box stands 4 under the line's top.
        var line = LineInk.Of(new TypeStep(28, 36, true));

        // The chip's ink (an eighth inside its box) still stands over the capitals and under the baseline.
        var memory = line.With(GlyphInk.Of(Glyphs.Memory), 28, 36);
        Assert.Equal(4 + (28 * 0.125), memory.Over, 6);
        Assert.Equal(Math.Min(line.Under, 4 + (28 * 0.125)), memory.Under, 6);

        // The drive's bar inks inside the capitals: the text's ink is the line's.
        Assert.Equal(line, line.With(GlyphInk.Of(Glyphs.Disk), 28, 36));

        // A glyph inking its whole box is the box, as before.
        Assert.Equal(line.With(28, 36), line.With(GlyphBox.Whole, 28, 36));
        Assert.Equal(new LineInk(4, 4), line.With(28, 36));
    }
}
