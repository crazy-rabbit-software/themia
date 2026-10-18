using System.Text.Json;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.SystemData;
using Themia.Core.Tests;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Widget.Cpu.Tests;

/// <summary>[S-6311] [S-6317] The CPU meter: its styles and the core graphs' settings. Moved with the widget from Core's MeterStylesTests and CoreGraphsTests, unchanged in what they assert.</summary>
public sealed class CpuTests
{
    [Fact]
    public void S6311_the_cpu_offers_graph_all_cores_core_grid_gauge_and_core_graphs_in_that_order()
    {
        Assert.Equal(["graph", "cores", "grid", "gauge", "coreGraphs"], WidgetStyles.For(new WidgetId("cpu")).Select(s => s.Name));
        Assert.Equal(CpuStyle.Graph, new CpuConfig().Style);
    }

    [Fact]
    public void S6311_a_style_survives_a_save_and_the_history_settings_stay_beside_it()
    {
        var cpu = new CpuConfig { Style = CpuStyle.Grid, ShowGraph = false, GraphSpanSeconds = 300 };
        var json = JsonSerializer.Serialize<WidgetConfig>(cpu, StateJson.WidgetConfig);
        Assert.Contains("\"style\": \"grid\"", json, StringComparison.Ordinal);

        var back = Assert.IsType<CpuConfig>(JsonSerializer.Deserialize(json, StateJson.WidgetConfig));
        Assert.Equal(CpuStyle.Grid, back.Style);
        Assert.False(back.ShowGraph);
        Assert.Equal(300, back.GraphSpanSeconds);
    }

    [Fact]
    public void S6317_core_graphs_survive_a_save_and_offer_the_history_span()
    {
        var cpu = new CpuConfig { Style = CpuStyle.CoreGraphs, GraphSpanSeconds = 300 };
        var json = JsonSerializer.Serialize<WidgetConfig>(cpu, StateJson.WidgetConfig);
        Assert.Contains("\"style\": \"coreGraphs\"", json, StringComparison.Ordinal);

        var back = Assert.IsType<CpuConfig>(JsonSerializer.Deserialize(json, StateJson.WidgetConfig));
        Assert.Equal(CpuStyle.CoreGraphs, back.Style);
        Assert.Equal(300, back.GraphSpanSeconds);
        Assert.Equal(CpuStyle.CoreGraphs, ((CpuConfig)back.Clone()).Style);

        // The span is the core graphs' too; the styles without a history offer none.
        Assert.Equal([MeterSetting.Span], back.ShownSettings);
        Assert.Equal([MeterSetting.Span], new CpuConfig { Style = CpuStyle.Graph }.ShownSettings);
        Assert.Empty(new CpuConfig { Style = CpuStyle.Grid }.ShownSettings);
        Assert.Empty(new CpuConfig { Style = CpuStyle.Gauge }.ShownSettings);
    }
}
