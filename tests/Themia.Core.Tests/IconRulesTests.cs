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
}
