using System.Text.Json;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.NetworkHistory.Tests;

/// <summary>[S-11019] The networkHistory atom: headerless, its sizes, its span.</summary>
public sealed class NetworkHistoryTests
{
    private static WidgetDescriptor Kind => ModuleCatalog.Of(new WidgetId("networkHistory"));

    [Fact]
    public void S11019_it_starts_headerless_and_is_a_history_strip()
    {
        Assert.True(Kind.Traits.HasFlag(WidgetTraits.StartsHeaderless));
        Assert.True(Kind.Traits.HasFlag(WidgetTraits.CanHaveHeader));
        Assert.Equal(MeterSizing.History, Kind.SizingOf());
        Assert.Empty(Kind.SizingOf().Problems());
    }

    [Fact]
    public void S11019_its_span_survives_a_save_and_a_clone_and_is_its_one_menu_line()
    {
        Assert.Equal(60, new NetworkHistoryConfig().GraphSpanSeconds);
        var json = JsonSerializer.Serialize<WidgetConfig>(new NetworkHistoryConfig { GraphSpanSeconds = 1800 }, StateJson.WidgetConfig);
        var back = Assert.IsType<NetworkHistoryConfig>(JsonSerializer.Deserialize(json, StateJson.WidgetConfig));
        Assert.Equal(1800, ((NetworkHistoryConfig)back.Clone()).GraphSpanSeconds);

        var menu = new WidgetMenuLines(() => { }, _ => { });
        ((IWidgetModule)new NetworkHistoryModule()).MenuLines(menu, back);
        Assert.Single(menu.Lines);
    }
}
