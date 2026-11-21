using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-15100]..[S-15104] The atoms' rules (owner, 2026-11-21: "the spacing inside the atoms should be universal and the same
/// for all the widgets (also buttons and text)"): one spacing on every side of every atom, and a content that fills the
/// room left: a text's capitals by its height alone, a glyph by its ink, a bar, a picture.
/// </summary>
public class AtomRulesTests
{
    [Fact]
    public void S15100_the_universal_spacing_is_one_step_of_the_familys_scale()
    {
        Assert.Equal(8, AtomSpace.Pad);
        Assert.True(FamilySpace.IsSpace(AtomSpace.Pad));
    }

    [Theory]
    [InlineData(240, 40, 224, 24)]
    [InlineData(100, 100, 84, 84)]
    [InlineData(16, 16, 0, 0)]
    [InlineData(10, 4, 0, 0)]
    public void S15100_an_atoms_room_is_its_box_less_the_spacing_on_every_side_never_negative(double width, double height, double roomWidth, double roomHeight)
    {
        Assert.Equal(new AtomRoom(roomWidth, roomHeight), AtomRules.Room(width, height));
        Assert.Equal(roomWidth > 0 && roomHeight > 0, AtomRules.Room(width, height).IsUsable);
    }

    [Fact]
    public void S15100_a_box_not_laid_out_yet_has_no_room()
        => Assert.False(AtomRules.Room(double.PositiveInfinity, double.NaN).IsUsable);

    [Fact]
    public void S15100_a_cards_side_is_its_content_in_the_spacing()
    {
        Assert.Equal(30, AtomRules.Side(AtomRules.TextRoom));
        Assert.Equal(AtomRules.TextRoom, AtomRules.Room(AtomRules.Side(AtomRules.TextRoom), AtomRules.Side(AtomRules.TextRoom)).Height);
    }

    [Theory]
    [InlineData(6)]
    [InlineData(14)]
    [InlineData(23)]
    public void S15101_a_texts_capitals_fill_its_room_and_it_stands_on_its_baseline_at_the_rooms_foot(double height)
    {
        var font = AtomRules.FontSize(height);
        Assert.Equal(height, font * AtomRules.Cap, 6);

        // The capitals' top is the room's top and the baseline the room's foot: the line stands its lift over the room.
        var baseline = (AtomRules.LineHeight(font) * LineInk.FaceBaseline) - AtomRules.Lift(font);
        Assert.Equal(height, baseline, 6);

        // Its descenders hang into the spacing under the room, and stay in it.
        Assert.InRange(font * AtomRules.Descent, 0, AtomSpace.Pad);
    }

    [Theory]
    [InlineData(24)]
    [InlineData(34)]
    [InlineData(64)]
    public void S15101_a_text_too_large_for_the_spacing_is_held_so_its_descenders_end_on_the_cards_edge(double height)
    {
        var font = AtomRules.FontSize(height);
        Assert.True(font * AtomRules.Cap < height);

        // Its capitals' top is still the room's top; its descenders' foot is the card's edge, the spacing under the room.
        var baseline = (AtomRules.LineHeight(font) * LineInk.FaceBaseline) - AtomRules.Lift(font);
        Assert.Equal(font * AtomRules.Cap, baseline, 6);
        Assert.Equal(height + AtomSpace.Pad, baseline + (font * AtomRules.Descent), 6);
    }

    [Fact]
    public void S15101_held_changes_nothing_while_the_descenders_fit_the_spacing()
    {
        Assert.Equal(20, AtomRules.Held(20, 14, 4.8, 14));
        Assert.Equal(20, AtomRules.Held(20, 14, AtomSpace.Pad, 14));

        // Capitals of 28 and a descent of 9.6 in a room of 28: (28 + 8) / (28 + 9.6) of the size.
        Assert.Equal(40 * 36 / 37.6, AtomRules.Held(40, 28, 9.6, 28), 6);
        Assert.Equal(40, AtomRules.Held(40, 0, 9.6, 28));
    }

    [Fact]
    public void S15101_a_taller_room_is_a_larger_text_and_no_room_no_text()
    {
        Assert.True(AtomRules.FontSize(14) > AtomRules.FontSize(6));
        Assert.Equal(2 * AtomRules.FontSize(7), AtomRules.FontSize(14), 6);
        Assert.True(AtomRules.FontSize(48) > AtomRules.FontSize(24));
        Assert.Equal(0, AtomRules.FontSize(0));
        Assert.Equal(0, AtomRules.FontSize(double.NaN));
    }

    [Fact]
    public void S15101_a_measured_face_is_refitted_so_its_letters_ink_exactly_the_room()
    {
        // Measured at 26 the letters ink 25 tall: in a room of 24 the size is 26 * 24 / 25.
        Assert.Equal(24.96, AtomRules.Refit(26, 25, 24), 6);

        // Nothing measured: the size stays.
        Assert.Equal(26, AtomRules.Refit(26, 0, 24));
        Assert.Equal(26, AtomRules.Refit(26, double.NaN, 24));
    }

    [Fact]
    public void S15102_a_text_atoms_card_is_its_capitals_in_the_spacing_down_to_the_legibility_floor()
    {
        var text = AtomRules.TextSizing(240, AtomRules.TextRoom);
        Assert.Equal(new SizeDip(240, 30), text.Natural);
        Assert.Empty(text.Problems());
        Assert.True(text.Min.Height >= AtomRules.Side(AtomRules.LeastFont * AtomRules.Cap));
        Assert.True(text.Min.Height < text.Natural.Height);
        Assert.True(text.Min.Width < text.Natural.Width);
        Assert.True(AtomRules.FontSize(AtomRules.TextRoom) >= AtomRules.LeastFont);

        // A short card (a time) is never asked to be narrower than itself.
        var time = AtomRules.TextSizing(40, AtomRules.TextRoom);
        Assert.Empty(time.Problems());
    }

    [Fact]
    public void S15102_the_bar_and_the_picture_have_their_cards()
    {
        var bar = AtomRules.BarSizing(240);
        Assert.Equal(new SizeDip(240, AtomRules.Side(FamilyShape.Track)), bar.Natural);
        Assert.Empty(bar.Problems());
        Assert.True(bar.Min.Height < bar.Natural.Height);
        Assert.False(bar.FixedHeight);

        var picture = AtomRules.PictureSizing(160);
        Assert.Equal(new SizeDip(160, 160), picture.Natural);
        Assert.Equal(new SizeDip(WidgetSizing.LeastScaled, WidgetSizing.LeastScaled), picture.Min);
        Assert.Empty(picture.Problems());
    }

    [Fact]
    public void S15103_a_glyphs_ink_fills_the_room_until_one_axis_stops_it()
    {
        // The whole em box inks: the room's short side.
        Assert.Equal(24, AtomRules.GlyphSize(new AtomRoom(64, 24), 1, 1));
        Assert.Equal(24, AtomRules.GlyphSize(new AtomRoom(24, 64), 1, 1));

        // An ink three quarters of the em box tall: the em box is larger than the room, the ink the room's height.
        var size = AtomRules.GlyphSize(new AtomRoom(64, 24), 0.75, 0.75);
        Assert.Equal(24, size * 0.75, 6);

        // No room, no glyph; a fraction that is none reads the whole box.
        Assert.Equal(0, AtomRules.GlyphSize(new AtomRoom(0, 24), 1, 1));
        Assert.Equal(24, AtomRules.GlyphSize(new AtomRoom(64, 24), 0, double.NaN));
    }

    [Theory]
    [InlineData(ContentAlign.Left, 0)]
    [InlineData(ContentAlign.Center, 20)]
    [InlineData(ContentAlign.Right, 40)]
    public void S15103_S15105_a_content_stands_at_the_start_the_middle_or_the_end_of_its_room(ContentAlign align, double start)
        => Assert.Equal(start, AtomRules.Across(align, 64, 24));

    [Fact]
    public void S15104_a_bar_has_round_ends_and_fills_from_its_start()
    {
        Assert.Equal(2, AtomRules.BarRadius(new AtomRoom(224, 4)));
        Assert.Equal(12, AtomRules.BarRadius(new AtomRoom(224, 24)));
        Assert.Equal(0, AtomRules.BarRadius(new AtomRoom(0, 0)));

        Assert.Equal(0, AtomRules.BarFill(0, 224));
        Assert.Equal(56, AtomRules.BarFill(0.25, 224));
        Assert.Equal(224, AtomRules.BarFill(1.5, 224));
        Assert.Equal(0, AtomRules.BarFill(-1, 224));
        Assert.Equal(0, AtomRules.BarFill(double.NaN, 224));
        Assert.Equal(0, AtomRules.BarFill(0.5, 0));
    }
}
