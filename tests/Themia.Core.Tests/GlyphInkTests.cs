using Themia.App.Shell;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-14240] [S-14247] A readout's lead glyph and its line stand their ink on the card's insets, not their boxes (owner,
/// 2026-11-19, WR-1: "there should not be a space at the top"; WR-3): where a glyph or a text inks in its box is what
/// the host's meter measures (<see cref="InkMeter"/>, [S-14200]); these pin the rules that read it and the model that
/// stands without one (Core's tests install no meter).
/// </summary>
public class GlyphInkTests
{
    [Fact]
    public void S14240_without_a_meter_a_glyph_inks_its_whole_box_and_a_text_its_box_across()
    {
        Assert.Null(InkMeter.Room(new InkText(Glyphs.Memory, "Segoe Fluent Icons", 28, 400, 0)));
        Assert.Null(InkMeter.Room(new InkText("47", "Segoe UI Variable", 28, 600, 36)));
        Assert.Equal((0, 0), InkRoom.Ends(null, null, rightToLeft: false, led: false));
    }

    [Fact]
    public void S14240_a_lead_glyph_inks_where_its_glyph_does_in_its_box_centred_on_the_line()
    {
        // The Number look's reading at 28 on a 36 line, led by a 28 glyph: its box stands 4 under the line's top.
        var line = LineInk.Of(new TypeStep(28, 36, true));

        // A chip measured an eighth inside its box over and under: its ink still stands over the capitals and under the baseline.
        var memory = line.With(new LineInk(28 * 0.125, 28 * 0.125), 28, 36);
        Assert.Equal(4 + (28 * 0.125), memory.Over, 6);
        Assert.Equal(Math.Min(line.Under, 4 + (28 * 0.125)), memory.Under, 6);

        // A drive's flat bar through the middle inks inside the capitals: the text's ink is the line's.
        Assert.Equal(line, line.With(new LineInk(28 * 0.3125, 28 * 0.3125), 28, 36));

        // A glyph not measured inks its whole box, as before.
        Assert.Equal(line.With(28, 36), line.With(LineInk.None, 28, 36));
        Assert.Equal(new LineInk(4, 4), line.With(28, 36));
    }

    [Fact]
    public void S14240_a_glyph_starts_its_line_by_its_ink_on_its_start_side()
    {
        // A battery measured an eighth of its 28 box from its left and none from its right.
        var battery = new InkRoom(8, 8, 3.5, 0);
        Assert.Equal(3.5, battery.Start(rightToLeft: false));
        Assert.Equal(0, battery.Start(rightToLeft: true));
    }

    [Fact]
    public void S14247_a_line_reaches_back_over_the_room_its_first_and_last_letters_leave()
    {
        // "47" then "%": left to right the value's left room starts the line and the unit's right room ends it.
        var value = new InkRoom(7, 8, 1.2, 1.1);
        var unit = new InkRoom(5, 4, 0.6, 0.5);
        Assert.Equal((1.2, 0.5), InkRoom.Ends(value, unit, rightToLeft: false, led: false));

        // Led by a glyph its start is the gap to the glyph's box, never trimmed.
        Assert.Equal((0, 0.5), InkRoom.Ends(value, unit, rightToLeft: false, led: true));

        // A value alone ends the line itself.
        Assert.Equal((1.2, 1.1), InkRoom.Ends(value, value, rightToLeft: false, led: false));

        // Right to left either run may stand at either side: each side takes the least, never past the ink.
        Assert.Equal((0.5, 0.6), InkRoom.Ends(value, unit, rightToLeft: true, led: false));

        // Room that is no room (ink past its box: an italic's overhang) trims nothing.
        Assert.Equal((0, 0), InkRoom.Ends(new InkRoom(1, 1, -0.4, -0.2), null, rightToLeft: false, led: false));
    }
}
