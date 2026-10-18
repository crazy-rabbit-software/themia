using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Widget.EnergySaverToggle.Tests;

/// <summary>[S-8500] The Energy saver toggle stands for its one quick setting, from its own config.</summary>
public sealed class EnergySaverToggleTests
{
    [Fact]
    public void S8500_its_kind_stands_for_its_toggle_and_keeps_its_style()
    {
        var kind = new WidgetId("energySaverToggle");
        var config = Assert.IsType<EnergySaverToggleConfig>(WidgetConfig.CreateDefault(kind));

        Assert.Equal(QuickToggle.EnergySaver, config.Toggle);
        Assert.Equal(QuickToggle.EnergySaver, QuickToggleRules.ForKind(kind));
        Assert.Equal(kind, QuickToggleRules.KindOf(QuickToggle.EnergySaver));
        Assert.Equal(QuickToggleStyle.Tile, config.Style);

        config.Style = QuickToggleStyle.Switch;
        Assert.Equal(QuickToggleStyle.Switch, Assert.IsType<EnergySaverToggleConfig>(config.Clone()).Style);
    }
}
