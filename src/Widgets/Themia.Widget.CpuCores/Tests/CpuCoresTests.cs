using System.Text.Json;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.CpuCores.Tests;

/// <summary>[S-11011] The CPU cores atom: its drawings, their sizes, the graphs' span.</summary>
public sealed class CpuCoresTests
{
    private static WidgetDescriptor Kind => ModuleCatalog.Of(new WidgetId("cpuCores"));

    [Fact]
    public void S11011_it_offers_bars_a_grid_and_graphs_and_starts_headerless()
    {
        Assert.Equal(["bars", "grid", "graphs"], WidgetStyles.For(new WidgetId("cpuCores")).Select(s => s.Name));
        Assert.True(Kind.Traits.HasFlag(WidgetTraits.StartsHeaderless));
        foreach (var style in Enum.GetValues<CpuCoresStyle>())
        {
            Assert.Empty(Kind.Sizing(new CpuCoresConfig { Style = style }).Problems());
        }
    }

    [Fact]
    public void S11011_sixteen_cores_fit_every_drawing_at_its_least()
    {
        var grid = Kind.Sizing(new CpuCoresConfig { Style = CpuCoresStyle.Grid }).Min;
        Assert.True(MeterPlan.Rings(grid.Width - 24, grid.Height - 24, 16).Shown);

        var graphs = Kind.Sizing(new CpuCoresConfig { Style = CpuCoresStyle.Graphs }).Min;
        Assert.True(MeterPlan.Graphs(graphs.Width - 24, graphs.Height - 24, 16).Shown);
    }

    [Fact]
    public void S11011_only_the_graphs_offer_a_span_and_a_style_survives_a_save()
    {
        var menu = new WidgetMenuLines(() => { }, _ => { });
        ((IWidgetModule)new CpuCoresModule()).MenuLines(menu, new CpuCoresConfig());
        Assert.Empty(menu.StyleLines);

        menu = new WidgetMenuLines(() => { }, _ => { });
        ((IWidgetModule)new CpuCoresModule()).MenuLines(menu, new CpuCoresConfig { Style = CpuCoresStyle.Graphs });
        Assert.Single(menu.StyleLines);

        var json = JsonSerializer.Serialize<WidgetConfig>(new CpuCoresConfig { Style = CpuCoresStyle.Graphs, GraphSpanSeconds = 300 }, StateJson.WidgetConfig);
        var back = Assert.IsType<CpuCoresConfig>(JsonSerializer.Deserialize(json, StateJson.WidgetConfig));
        Assert.Equal((CpuCoresStyle.Graphs, 300), (back.Style, ((CpuCoresConfig)back.Clone()).GraphSpanSeconds));
    }
}
