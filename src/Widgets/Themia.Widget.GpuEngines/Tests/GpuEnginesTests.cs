using Themia.App.Widgets.Meters;
using Themia.Core.Model;
using Themia.Core.SystemData;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Widget.GpuEngines.Tests;

/// <summary>[S-11015] The GPU engines atom: four rows at one height, the engines in their order.</summary>
public sealed class GpuEnginesTests
{
    private static WidgetDescriptor Kind => ModuleCatalog.Of(new WidgetId("gpuEngines"));

    [Fact]
    public void S11015_four_rows_under_the_header_one_height()
    {
        var sizing = Kind.SizingOf();
        Assert.Equal(36 + (4 * 28) + 12, sizing.Natural.Height);
        Assert.True(sizing.FixedHeight);
        Assert.False(Kind.Traits.HasFlag(WidgetTraits.StartsHeaderless));
    }

    [Fact]
    public void S11015_every_engine_type_has_its_row_in_order_and_one_not_read_is_a_dash()
    {
        var items = GpuPart.Items([new GpuEngineLoad(GpuEngineKind.VideoDecode, 50), new GpuEngineLoad(GpuEngineKind.ThreeD, 10)]);
        Assert.Equal(4, items.Count);
        Assert.Equal([0.1, null, null, 0.5], items.Select(i => i.Fraction));
        Assert.Equal("--", items[1].Value);
        Assert.Equal(4, GpuPart.Items([]).Count);
    }

    [Fact]
    public void S11015_it_reads_its_adapter_and_a_clone_keeps_it()
    {
        Assert.Equal(["3"], SystemInterests.For(new GpuEnginesConfig { Adapter = 3 }, @"C:\").Series.Select(s => s.Instance));
        Assert.Equal(2, ((GpuEnginesConfig)new GpuEnginesConfig { Adapter = 2 }.Clone()).Adapter);
    }
}
