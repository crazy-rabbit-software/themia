using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Widget.MonoAudioToggle.Tests;

/// <summary>[S-9200] The Mono audio toggle stands for its one quick setting, from its own config.</summary>
public sealed class MonoAudioToggleTests
{
    [Fact]
    public void S9200_its_kind_stands_for_its_toggle_and_keeps_its_style()
    {
        var kind = new WidgetId("monoAudioToggle");
        var config = Assert.IsType<MonoAudioToggleConfig>(WidgetConfig.CreateDefault(kind));

        Assert.Equal(QuickToggle.MonoAudio, config.Toggle);
        Assert.Equal(QuickToggle.MonoAudio, QuickToggleRules.ForKind(kind));
        Assert.Equal(kind, QuickToggleRules.KindOf(QuickToggle.MonoAudio));
        Assert.Equal(QuickToggleStyle.Tile, config.Style);

        config.Style = QuickToggleStyle.Switch;
        Assert.Equal(QuickToggleStyle.Switch, Assert.IsType<MonoAudioToggleConfig>(config.Clone()).Style);
    }
}
