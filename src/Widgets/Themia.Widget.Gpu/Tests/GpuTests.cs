using System.Text.Json;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.SystemData;
using Themia.Core.Tests;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Widget.Gpu.Tests;

/// <summary>[S-433] [S-6320] The GPU meter: one adapter per widget and its styles. Moved with the widget from Core's GpuRulesTests and MeterStylesTests, unchanged in what they assert.</summary>
public sealed class GpuTests
{
    [Fact]
    public void S433_a_new_meter_shows_the_first_adapter_which_dxgi_lists_for_the_primary_display()
    {
        Assert.Equal(0, new GpuConfig().Adapter);
        Assert.Equal(0, ((GpuConfig)WidgetConfig.CreateDefault(new WidgetId("gpu"))).Adapter);
    }

    [Fact]
    public void S433_the_adapter_round_trips_and_a_position_that_is_none_is_repaired_to_the_first()
    {
        var kept = (GpuConfig)ConfigJson.Read("gpu", """{ "kind": "gpu", "adapter": 2, "style": "gauge" }""");
        Assert.Equal(2, kept.Adapter);
        Assert.Equal(GpuStyle.Gauge, kept.Style);

        var bad = (GpuConfig)ConfigJson.Read("gpu", """{ "kind": "gpu", "adapter": -4 }""");
        Assert.Equal(0, bad.Adapter);
        Assert.Empty(bad.Repair());
    }

    [Fact]
    public void S433_a_clone_keeps_the_adapter()
    {
        var copy = (GpuConfig)new GpuConfig { Adapter = 1 }.Clone();
        Assert.Equal(1, copy.Adapter);
    }

    [Fact]
    public void S6320_the_gpu_offers_graph_engines_and_gauge_and_keeps_its_adapter_through_a_clone()
    {
        Assert.Equal(["graph", "engines", "gauge"], WidgetStyles.For(new WidgetId("gpu")).Select(s => s.Name));

        var gpu = new GpuConfig { Adapter = 1, Style = GpuStyle.Engines };
        var copy = (GpuConfig)gpu.Clone();
        Assert.Equal(GpuStyle.Engines, copy.Style);
        Assert.Equal(1, copy.Adapter);
    }
}
