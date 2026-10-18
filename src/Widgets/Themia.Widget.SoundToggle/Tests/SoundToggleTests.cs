using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Widget.SoundToggle.Tests;

/// <summary>[S-8900] The Sound toggle stands for its one quick setting, from its own config.</summary>
public sealed class SoundToggleTests
{
    [Fact]
    public void S8900_its_kind_stands_for_its_toggle_and_keeps_its_style()
    {
        var kind = new WidgetId("soundToggle");
        var config = Assert.IsType<SoundToggleConfig>(WidgetConfig.CreateDefault(kind));

        Assert.Equal(QuickToggle.Sound, config.Toggle);
        Assert.Equal(QuickToggle.Sound, QuickToggleRules.ForKind(kind));
        Assert.Equal(kind, QuickToggleRules.KindOf(QuickToggle.Sound));
        Assert.Equal(QuickToggleStyle.Tile, config.Style);

        config.Style = QuickToggleStyle.Switch;
        Assert.Equal(QuickToggleStyle.Switch, Assert.IsType<SoundToggleConfig>(config.Clone()).Style);
    }
}
