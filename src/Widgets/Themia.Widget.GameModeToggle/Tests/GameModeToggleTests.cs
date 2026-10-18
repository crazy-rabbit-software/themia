using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Widget.GameModeToggle.Tests;

/// <summary>[S-9400] The Game mode toggle stands for its one quick setting, from its own config.</summary>
public sealed class GameModeToggleTests
{
    [Fact]
    public void S9400_its_kind_stands_for_its_toggle_and_keeps_its_style()
    {
        var kind = new WidgetId("gameModeToggle");
        var config = Assert.IsType<GameModeToggleConfig>(WidgetConfig.CreateDefault(kind));

        Assert.Equal(QuickToggle.GameMode, config.Toggle);
        Assert.Equal(QuickToggle.GameMode, QuickToggleRules.ForKind(kind));
        Assert.Equal(kind, QuickToggleRules.KindOf(QuickToggle.GameMode));
        Assert.Equal(QuickToggleStyle.Tile, config.Style);

        config.Style = QuickToggleStyle.Switch;
        Assert.Equal(QuickToggleStyle.Switch, Assert.IsType<GameModeToggleConfig>(config.Clone()).Style);
    }
}
