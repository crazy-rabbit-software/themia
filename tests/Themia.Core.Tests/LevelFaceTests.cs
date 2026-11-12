using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-2150] A level tile (volume, brightness): its Readout over the Controls row, the controls first.</summary>
public sealed class LevelFaceTests
{
    // A tier's view area: the card less its 44 header.
    private static LevelPlan At(double w, double h, bool under = true) => LevelFace.Plan(new WidgetArea(w, h - FamilySpace.ContentTop), under);

    [Fact]
    public void S2150_the_readout_climbs_28_40_68_as_a_meters_reading()
    {
        Assert.Equal(FamilyType.Big28, At(200, 150).Step);
        Assert.Equal(FamilyType.Big40, At(320, 240).Step);
        Assert.Equal(FamilyType.Big40, At(480, 160).Step);
        Assert.Equal(FamilyType.Big40, At(200, 400).Step);
        Assert.Equal(FamilyType.Big68, At(560, 420).Step);
        Assert.Equal(FamilyType.Big68, At(800, 560).Step);
    }

    [Fact]
    public void S2150_a_card_too_short_for_both_keeps_the_controls()
    {
        // XS: 60 of content, the row takes 40, the floor step's 36 does not fit over it.
        Assert.False(At(120, 120).Readout);
        Assert.True(At(200, 150).Readout);
        Assert.False(LevelFace.Plan(default, true).Readout);
    }

    [Fact]
    public void S2150_the_line_under_stands_where_it_fits_and_never_lowers_the_step()
    {
        Assert.False(At(200, 150).Under);

        // Wide: the 40 step's line fits over the row, its Body line under does not; the step stays 40.
        var wide = At(480, 160);
        Assert.True(wide is { Readout: true, Under: false });
        Assert.Equal(FamilyType.Big40, wide.Step);

        Assert.True(At(320, 240).Under);
        Assert.True(At(200, 400).Under);
        Assert.True(At(560, 420).Under);
        Assert.True(At(800, 560).Under);

        // Nothing to say under it: no line.
        Assert.False(At(560, 420, under: false).Under);
    }
}
