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
    public void S1140_the_number_is_one_height_the_inset_the_reading_at_28_and_the_inset()
    {
        var sizing = Kind.Sizing(new CpuConfig());
        Assert.Empty(sizing.Problems());
        Assert.Equal(12 + 36 + 12, sizing.Natural.Height);
        Assert.InRange(sizing.Min.Height, 33, 45); // [S-12833] [S-15752] scales down as one, glyph, figures and unit together, to its unit at 9
        Assert.Equal(Themia.Sdk.SizeStretch.Ceiling, sizing.Max.Height); // relaxed, [S-12411]: no practical Max
        Assert.Equal((160d, Themia.Sdk.SizeStretch.Ceiling), (sizing.Natural.Width, sizing.Max.Width)); // [S-12411] no practical Max
        Assert.InRange(sizing.Min.Width, 40, 160); // [S-15752] its least: glyph, "100" and unit at the unit's 9 in the inset
    }

    [Fact]
    public void S6311_the_gauge_holds_the_reading_in_its_bowl_at_its_least()
    {
        var sizing = Kind.Sizing(new CpuConfig { Style = CpuStyle.Gauge });
        Assert.Empty(sizing.Problems());
        var dial = sizing.Min.Width - 24;
        Assert.True(dial - 48 >= MeterSizing.ReadingWidth(Themia.Sdk.Kit.ReadoutRules.Split(Formatting.Percent(1))));
        Assert.Equal(dial, MeterSizing.GaugeIn(dial, sizing.Min.Height - 12 - 12));
        Assert.Equal(Themia.Sdk.SizeStretch.Ceiling, sizing.Max.Width); // relaxed, [S-12411]: no practical Max
    }
}
