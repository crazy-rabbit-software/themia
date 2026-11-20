using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-14200] [S-14201] A face that stands centred (a tile, a one-line face: owner, 2026-11-19, "Centred is fine") stands
/// on its ink, the bands over and under it alike, not on its box.
/// </summary>
public class InkCentringTests
{
    [Fact]
    public void S14200_without_a_meter_a_line_stands_on_the_model()
    {
        // Core's tests install no meter (the host does, at start): the family's model of capitals on the baseline stands.
        Assert.Null(InkMeter.Measure(new InkText("Sleep", "Segoe UI Variable", 14, 400, 20)));
        Assert.Equal(LineInk.Of(FamilyType.Body), InkMeter.Line(FamilyType.Body, "Sleep", "Segoe UI Variable", 400));
    }

    [Fact]
    public void S14200_a_line_saying_nothing_is_a_planned_line_never_measured()
    {
        Assert.Null(InkMeter.Measure(new InkText(string.Empty, "Segoe UI Variable", 14, 400, 20)));
        Assert.Null(InkMeter.Measure(new InkText("On", "Segoe UI Variable", 0, 400, 20)));
        Assert.Equal(LineInk.Of(FamilyType.Body), InkMeter.Line(FamilyType.Body, null, "Segoe UI Variable", 400));
    }

    [Fact]
    public void S14201_a_tile_face_stands_centred_on_its_ink_not_its_box()
    {
        // The Wi-Fi toggle at 232: in its inset (208) the 96 glyph (its arcs 12 under its em box's top), 8, the name (28)
        // and the state line (20, its baseline 4 over its foot): 152 tall. Centred by its box it stood 28 from the inset's
        // top and foot, its ink 40 under the top and 32 over the foot (the gap check's 52 / 44 with the card's 12).
        var ink = new LineInk(12, 4);
        var lay = BlockFit.Lay(208, 208, 208, 152, ink, most: 1);

        // On its ink: the same band over and under, and never drawn larger than its plan.
        Assert.Equal(1, lay.Scale);
        var over = lay.Top + ink.Over;
        var under = 208 - (lay.Top + 152 - ink.Under);
        Assert.Equal(over, under, 6);
        Assert.Equal(36, over, 6);
    }

    [Fact]
    public void S14201_a_face_too_tall_for_its_box_keeps_its_first_ink_on_the_top()
    {
        // Never above the box: a face taller than its room keeps its first ink on the top, as every one-readout block.
        var lay = BlockFit.Lay(40, 40, 40, 60, new LineInk(5, 5), least: 1, most: 1);
        Assert.Equal(1, lay.Scale);
        Assert.Equal(-5, lay.Top, 6);
    }
}
