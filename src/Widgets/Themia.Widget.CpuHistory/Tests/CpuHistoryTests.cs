using System.Text.Json;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.CpuHistory.Tests;

/// <summary>[S-11010] [S-11002] The CPU history atom: headerless, its sizes, its span.</summary>
public sealed class CpuHistoryTests
{
    private static WidgetDescriptor Kind => ModuleCatalog.Of(new WidgetId("cpuHistory"));

    [Fact]
    public void S11010_it_starts_headerless_and_is_160_to_640_wide_and_48_to_200_tall()
    {
        Assert.True(Kind.Traits.HasFlag(WidgetTraits.StartsHeaderless));
        Assert.True(Kind.Traits.HasFlag(WidgetTraits.CanHaveHeader));
        var sizing = Kind.SizingOf();
        Assert.Empty(sizing.Problems());
        Assert.Equal((new SizeDip(160, 48), new SizeDip(640, 200)), (sizing.Min, sizing.Max));
    }

    [Fact]
    public void S11010_its_span_is_a_minute_survives_a_save_and_a_clone_and_is_its_one_menu_line()
    {
        var config = new CpuHistoryConfig { GraphSpanSeconds = 900 };
        var json = JsonSerializer.Serialize<WidgetConfig>(config, StateJson.WidgetConfig);
        var back = Assert.IsType<CpuHistoryConfig>(JsonSerializer.Deserialize(json, StateJson.WidgetConfig));
        Assert.Equal(900, ((CpuHistoryConfig)back.Clone()).GraphSpanSeconds);
        Assert.Equal(60, new CpuHistoryConfig().GraphSpanSeconds);

        var menu = new WidgetMenuLines(() => { }, _ => { });
        ((IWidgetModule)new CpuHistoryModule()).MenuLines(menu, config);
        Assert.Single(menu.Lines);
        Assert.Equal(MeterTiming.SpanChoices.Count, menu.Lines[0].Children!.Count);
    }
}
