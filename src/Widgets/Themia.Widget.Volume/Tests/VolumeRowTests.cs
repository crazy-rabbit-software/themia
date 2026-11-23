using Themia.Sdk;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Widget.Volume.Tests;

/// <summary>[S-17225] The volume as one row, Brightness's: one height kept, the same widths, the two a pair.</summary>
public sealed class VolumeRowTests
{
    [Fact]
    public void S17225_the_volume_is_the_brightness_row()
    {
        // Owner, 2026-11-24: "volume slider should go into the quick settings too", beside Brightness and drawn as its row.
        var volume = ModuleCatalog.Of(new WidgetId("volume")).Sizing(new Themia.Core.Model.VolumeConfig());
        var brightness = ModuleCatalog.Of(new WidgetId("brightness")).Sizing(new Themia.Core.Model.BrightnessConfig());

        Assert.Equal((brightness.Min, brightness.Natural, brightness.Max, brightness.KeepsHeight), (volume.Min, volume.Natural, volume.Max, volume.KeepsHeight));
        Assert.True(volume.KeepsHeight);
        Assert.Equal((LevelRow.Height, LevelRow.Height, LevelRow.Height), (volume.Min.Height, volume.Natural.Height, volume.Max.Height));
        Assert.Equal(LevelRow.NaturalWidth, volume.Natural.Width);
        Assert.Empty(volume.Problems());
    }
}
