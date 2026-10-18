using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Widget.SpatialSoundToggle.Tests;

/// <summary>[S-9100] The Spatial sound toggle stands for its one quick setting, from its own config.</summary>
public sealed class SpatialSoundToggleTests
{
    [Fact]
    public void S9100_its_kind_stands_for_its_toggle_and_keeps_its_style()
    {
        var kind = new WidgetId("spatialSoundToggle");
        var config = Assert.IsType<SpatialSoundToggleConfig>(WidgetConfig.CreateDefault(kind));

        Assert.Equal(QuickToggle.SpatialSound, config.Toggle);
        Assert.Equal(QuickToggle.SpatialSound, QuickToggleRules.ForKind(kind));
        Assert.Equal(kind, QuickToggleRules.KindOf(QuickToggle.SpatialSound));
        Assert.Equal(QuickToggleStyle.Tile, config.Style);

        config.Style = QuickToggleStyle.Switch;
        Assert.Equal(QuickToggleStyle.Switch, Assert.IsType<SpatialSoundToggleConfig>(config.Clone()).Style);
    }
}
