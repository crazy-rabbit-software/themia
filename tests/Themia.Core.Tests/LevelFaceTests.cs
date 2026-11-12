using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-2150] [S-11308] A level widget (volume, brightness): its Readout over the Controls row, always both.</summary>
public sealed class LevelFaceTests
{
    [Fact]
    public void S11308_the_readout_is_the_data_widgets_28_sized_on_100_percent()
    {
        Assert.Equal(FamilyType.Big28, LevelFace.Step);
        Assert.Equal(new ReadoutText("100", "%"), LevelFace.Worst);
    }

    [Fact]
    public void S11308_the_card_is_the_readout_its_line_under_and_the_controls_one_height_160_to_360_wide()
    {
        var volume = LevelFace.Sizing(under: true);
        var brightness = LevelFace.Sizing(under: false);

        // The header's content top, the 28 line (36) and its Caption line under (16), 8, the row (28), the inset.
        Assert.Equal(36 + 36 + 16 + 8 + 28 + 12, volume.Natural.Height);
        Assert.Equal(volume.Natural.Height - 16, brightness.Natural.Height);
        Assert.True(volume.FixedHeight && !volume.FixedWidth);
        Assert.Equal((160d, 200d, 360d), (volume.Min.Width, volume.Natural.Width, volume.Max.Width));
    }
}
