using System.Text.Json;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Widget.Cpu.Tests;

/// <summary>[S-1140] [S-6311] [S-11001] The CPU reading atom: its drawings and their sizes.</summary>
public sealed class CpuTests
{
    private static WidgetDescriptor Kind => ModuleCatalog.Of(new WidgetId("cpu"));

    [Fact]
    public void S6311_the_cpu_reading_offers_the_number_and_a_gauge()
    {
        Assert.Equal(["number", "gauge"], WidgetStyles.For(new WidgetId("cpu")).Select(s => s.Name));
        Assert.Equal(CpuStyle.Number, new CpuConfig().Style);
    }

    [Fact]
    public void S6311_a_style_survives_a_save_and_a_clone()
    {
        var json = JsonSerializer.Serialize<WidgetConfig>(new CpuConfig { Style = CpuStyle.Gauge }, StateJson.WidgetConfig);
        Assert.Contains("\"style\": \"gauge\"", json, StringComparison.Ordinal);

        var back = Assert.IsType<CpuConfig>(JsonSerializer.Deserialize(json, StateJson.WidgetConfig));
        Assert.Equal(CpuStyle.Gauge, ((CpuConfig)back.Clone()).Style);
    }

    [Fact]
    public void S1140_the_number_is_one_height_the_header_the_reading_at_28_and_the_inset()
    {
        var sizing = Kind.Sizing(new CpuConfig());
        Assert.Empty(sizing.Problems());
        Assert.True(sizing.FixedHeight);
        Assert.Equal(36 + 36 + 12, sizing.Natural.Height);
        Assert.Equal((120, 160, 320), (sizing.Min.Width, sizing.Natural.Width, sizing.Max.Width));
        Assert.Null(Kind.SizeSets);
    }

    [Fact]
    public void S6311_the_gauge_holds_the_reading_in_its_bowl_at_its_least()
    {
        var sizing = Kind.Sizing(new CpuConfig { Style = CpuStyle.Gauge });
        Assert.Empty(sizing.Problems());
        var dial = sizing.Min.Width - 24;
        Assert.True(dial - 48 >= MeterSizing.ReadingWidth(Themia.Sdk.Kit.ReadoutRules.Split(Formatting.Percent(1))));
        Assert.Equal(dial, MeterSizing.GaugeIn(dial, sizing.Min.Height - 36 - 12));
        Assert.True(sizing.Max.Width <= 320);
    }
}
