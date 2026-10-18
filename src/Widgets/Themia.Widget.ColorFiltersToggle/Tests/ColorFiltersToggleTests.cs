using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Widget.ColorFiltersToggle.Tests;

/// <summary>[S-9300] The Color filters toggle stands for its one quick setting, from its own config.</summary>
public sealed class ColorFiltersToggleTests
{
    [Fact]
    public void S9300_its_kind_stands_for_its_toggle_and_keeps_its_style()
    {
        var kind = new WidgetId("colorFiltersToggle");
        var config = Assert.IsType<ColorFiltersToggleConfig>(WidgetConfig.CreateDefault(kind));

        Assert.Equal(QuickToggle.ColorFilters, config.Toggle);
        Assert.Equal(QuickToggle.ColorFilters, QuickToggleRules.ForKind(kind));
        Assert.Equal(kind, QuickToggleRules.KindOf(QuickToggle.ColorFilters));
        Assert.Equal(QuickToggleStyle.Tile, config.Style);

        config.Style = QuickToggleStyle.Switch;
        Assert.Equal(QuickToggleStyle.Switch, Assert.IsType<ColorFiltersToggleConfig>(config.Clone()).Style);
    }
}
