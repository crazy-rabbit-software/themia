using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Widget.RotationLockToggle.Tests;

/// <summary>[S-8700] The Rotation lock toggle stands for its one quick setting, from its own config.</summary>
public sealed class RotationLockToggleTests
{
    [Fact]
    public void S8700_its_kind_stands_for_its_toggle_and_keeps_its_style()
    {
        var kind = new WidgetId("rotationLockToggle");
        var config = Assert.IsType<RotationLockToggleConfig>(WidgetConfig.CreateDefault(kind));

        Assert.Equal(QuickToggle.RotationLock, config.Toggle);
        Assert.Equal(QuickToggle.RotationLock, QuickToggleRules.ForKind(kind));
        Assert.Equal(kind, QuickToggleRules.KindOf(QuickToggle.RotationLock));
        Assert.Equal(QuickToggleStyle.Tile, config.Style);

        config.Style = QuickToggleStyle.Switch;
        Assert.Equal(QuickToggleStyle.Switch, Assert.IsType<RotationLockToggleConfig>(config.Clone()).Style);
    }
}
