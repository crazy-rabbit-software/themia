using Themia.Sdk;
using Xunit;

namespace Themia.Widget.Brightness.Tests;

/// <summary>[S-17184] The brightness as one row: its one height, its widths, the level's column.</summary>
public sealed class BrightnessRowTests
{
    [Fact]
    public void S17184_the_brightness_is_one_short_wide_row()
    {
        // Owner, 2026-11-23: "brightness slider should ... have a row design and not columnar design".
        var sizing = ModuleCatalog.Of(new WidgetId("brightness")).Sizing(new Themia.Core.Model.BrightnessConfig());

        // One height: the inset, the slider's thumb, the inset; no room for a readout over it at any size.
        Assert.Equal(12 + 22 + 12, BrightnessRow.Height);
        Assert.Equal((BrightnessRow.Height, BrightnessRow.Height, BrightnessRow.Height), (sizing.Min.Height, sizing.Natural.Height, sizing.Max.Height));

        // Wide and short: the sun, the level held to "100%", the slider's least travel; a new one 300.
        Assert.Equal(300, sizing.Natural.Width);
        Assert.True(sizing.Natural.Width > 6 * sizing.Natural.Height);
        Assert.InRange(BrightnessRow.LevelWidth, 24, 40);
        Assert.Equal(Math.Ceiling(12 + 16 + 8 + BrightnessRow.LevelWidth + 12 + 80 + 12), sizing.Min.Width);
        Assert.Empty(sizing.Problems());
    }
}
