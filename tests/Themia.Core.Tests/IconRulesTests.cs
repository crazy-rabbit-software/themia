using Themia.App.Shell;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-15000]..[S-15002] The icon widget's base rules: its size, the inset and the step its glyph has in a box, its roles.</summary>
public sealed class IconRulesTests
{
    // ---- [S-15000] the size -----------------------------------------------------------------------------

    [Fact]
    public void S15000_a_new_icon_is_the_40_steps_line_in_the_inset_a_square()
    {
        var sizing = IconRules.Sizing();

        Assert.Equal(new SizeDip(12 + 52 + 12, 12 + 52 + 12), sizing.Natural);
        Assert.Equal(new SizeDip(TileRules.MaxSide, TileRules.MaxSide), sizing.Max);
        Assert.Equal(StretchRoom.Picture, sizing.Room);
        Assert.Empty(sizing.Problems());
    }

    [Fact]
    public void S15000_it_goes_down_to_the_least_widget_never_under_its_glyph_floor()
    {
        var sizing = IconRules.Sizing();

        // WR-14: a glyph is read down to 12; the grid's least widget is 32, which holds it in the least inset (4).
        Assert.Equal(new SizeDip(WidgetSizing.LeastScaled, WidgetSizing.LeastScaled), sizing.Min);
        Assert.True(sizing.Min.Width >= ScaleFloor.Glyph + (2 * TileRules.TightInset));
    }

    [Fact]
    public void S15000_the_descriptor_relaxes_it_to_no_practical_maximum()
    {
        var relaxed = SizeStretch.Relax(IconRules.Sizing());

        Assert.Equal(SizeStretch.Ceiling, relaxed.Max.Width);
        Assert.Equal(SizeStretch.Ceiling, relaxed.Max.Height);
        Assert.Equal(IconRules.Sizing().Natural, relaxed.Natural);
    }

    // ---- [S-15002] the inset and the step in a box --------------------------------------------------------

    [Theory]
    [InlineData(64, 64, 12)]
    [InlineData(52, 300, 12)]
    [InlineData(300, 52, 12)]
    [InlineData(48, 48, 10)]
    [InlineData(40, 40, 6)]
    [InlineData(36, 36, 4)]
    [InlineData(32, 32, 4)]
    public void S15002_the_inset_is_the_familys_until_the_short_side_no_longer_holds_the_28_glyph_in_it(double width, double height, double inset)
        => Assert.Equal(inset, IconRules.Inset(width, height));

    [Fact]
    public void S15002_the_inset_never_grows_as_the_card_shrinks()
    {
        var last = double.PositiveInfinity;
        for (var side = 200.0; side >= IconRules.LeastSide; side--)
        {
            var inset = IconRules.Inset(side, side);
            Assert.True(inset <= last, $"{side}: {inset} after {last}");
            Assert.True(IconRules.Room(side, side) >= ScaleFloor.Glyph, $"{side}: room {IconRules.Room(side, side)}");
            last = inset;
        }
    }

    [Fact]
    public void S15002_an_unmeasured_card_has_the_familys_inset_and_the_natural_room()
    {
        Assert.Equal(FamilySpace.Inset, IconRules.Inset(0, 0));
        Assert.Equal(FamilySpace.Inset, IconRules.Inset(double.NaN, 40));
        Assert.Equal(52, IconRules.Room(double.PositiveInfinity, 64));
    }

    [Theory]
    [InlineData(32, 32, 28)]
    [InlineData(64, 64, 40)]
    [InlineData(76, 76, 40)]
    [InlineData(64, 400, 40)]
    [InlineData(100, 100, 68)]
    [InlineData(130, 120, 96)]
    [InlineData(480, 480, 136)]
    public void S15002_the_glyph_is_planned_on_the_largest_step_its_room_holds(double width, double height, double step)
        => Assert.Equal(step, IconRules.Step(width, height).FontSize);

    [Fact]
    public void S15002_the_glyph_room_is_the_short_side_less_the_inset_on_both_sides()
    {
        Assert.Equal(64 - 24, IconRules.Room(64, 200));
        Assert.Equal(32 - 8, IconRules.Room(32, 32));
    }

    // ---- [S-15001] what it says by its colours -----------------------------------------------------------

    [Fact]
    public void S15001_it_is_drawn_in_the_items_text_role_the_accents_on_and_disabled_unavailable()
    {
        Assert.Equal(ColourRole.TextPrimary, IconRules.Role(TileState.Off));
        Assert.Equal(ColourRole.TextOnAccent, IconRules.Role(TileState.On));
        Assert.Equal(ColourRole.TextDisabled, IconRules.Role(TileState.Unavailable));
    }

    [Fact]
    public void S15001_its_tooltip_is_its_name_and_why_while_unavailable()
    {
        Assert.Equal("Play", new IconShown(Glyphs.Play) { Name = "Play" }.Tooltip);
        Assert.Equal("Nothing playing", (IconShown.None(Glyphs.Play, "Nothing playing") with { Name = "Play" }).Tooltip);
        Assert.Equal("Play", (IconShown.None(Glyphs.Play) with { Name = "Play" }).Tooltip);
        Assert.Equal(TileState.Unavailable, IconShown.None(Glyphs.Play).State);
    }

    // ---- [S-15710] why, under the glyph --------------------------------------------------------------------

    [Theory]
    [InlineData(76, 76, false)] // a new icon: no room across or under (the tooltip)
    [InlineData(330, 140, true)]
    [InlineData(300, 164, true)]
    [InlineData(600, 200, true)]
    [InlineData(152, 152, true)] // 128 across holds a sentence's 96, 128 down the 28 glyph over the band
    [InlineData(330, 70, false)] // 46 down: the 28 glyph and its band (24) need 52
    [InlineData(110, 400, false)] // 86 across: under a sentence's room
    [InlineData(double.NaN, double.NaN, false)]
    public void S15710_a_card_says_why_under_its_glyph_where_its_inset_holds_the_line(double width, double height, bool says)
        => Assert.Equal(says, IconRules.SaysWhy(width, height));

    [Fact]
    public void S15710_over_the_why_the_glyph_is_planned_on_the_room_left_above_the_band()
    {
        // 330 by 140: 116 down, 92 over the band; the 68 step fits both ways, the 96 step no longer does.
        Assert.Equal(96, IconRules.Step(330, 140).FontSize);
        Assert.Equal(68, IconRules.Step(330, 140, why: true).FontSize);
        Assert.Equal(24, WhyLine.Band);
    }

    // ---- [S-15716] why, beside the glyph -------------------------------------------------------------------

    [Fact]
    public void S15716_a_wide_short_card_says_why_beside_its_glyph_the_two_centred_as_one()
    {
        // 350 by 70: the inset box 326 by 46, the glyph 46, a 130 sentence 12 after it.
        var beside = IconRules.Beside(350, 70, 130, marked: false)!.Value;

        Assert.Equal(46, beside.Glyph);
        Assert.Equal(130, beside.TextWidth);
        Assert.Equal(beside.GlyphLeft + 46 + FamilySpace.ColumnGap, beside.TextLeft);
        Assert.Equal(350 - (beside.TextLeft + beside.TextWidth), beside.GlyphLeft, 6);
    }

    [Fact]
    public void S15716_the_mark_takes_its_room_at_the_end_and_a_long_why_is_cut_at_what_is_left()
    {
        var beside = IconRules.Beside(350, 70, 1000, marked: true)!.Value;

        Assert.Equal(326 - IconRules.MarkRoom - 46 - FamilySpace.ColumnGap, beside.TextWidth, 6);
        Assert.Equal(12, beside.GlyphLeft, 6);
    }

    [Theory]
    [InlineData(76, 76)] // no room across: the mark and the tooltip (held for the owner)
    [InlineData(32, 32)]
    [InlineData(330, 140)] // room under the glyph: the line under it
    [InlineData(200, 20)] // under a Caption line's height
    public void S15716_no_why_beside_where_it_does_not_fit_or_fits_under(double width, double height)
        => Assert.Null(IconRules.Beside(width, height, 130, marked: false));

    // ---- [S-15717] the glyph clear of the mark in a small card -------------------------------------------------

    [Fact]
    public void S15717_at_32_the_glyph_stands_clear_of_the_mark_at_its_floor_the_mark_in_the_corner()
    {
        var box = IconRules.Box(32, 32, marked: true);

        Assert.Equal(0, box.Mark);
        Assert.Equal(new IconEdges(4, 16, 4, 4), box.Glyph);
        Assert.Equal(ScaleFloor.Glyph, 32 - box.Glyph.Top - box.Glyph.Bottom);
    }

    [Theory]
    [InlineData(76, 76)] // the family's inset: the mark on the inset round the glyph ([S-15160])
    [InlineData(330, 140)]
    public void S15717_with_the_familys_inset_the_glyph_keeps_its_box_and_the_mark_stands_4_in(double width, double height)
    {
        var inset = IconRules.Inset(width, height);
        Assert.Equal(new IconBox(new(inset, inset, inset, inset), FamilySpace.S1), IconRules.Box(width, height, marked: true));
    }

    [Fact]
    public void S15717_without_a_mark_the_glyph_keeps_its_inset()
        => Assert.Equal(new IconBox(new(4, 4, 4, 4), FamilySpace.S1), IconRules.Box(32, 32, marked: false));
}
