using System.Text.Json;
using Themia.App.Services;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.SystemData;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.GpuHistory.Tests;

/// <summary>[S-11014] The GPU history atom: headerless, its adapter's series, its lines.</summary>
public sealed class GpuHistoryTests
{
    private static WidgetDescriptor Kind => ModuleCatalog.Of(new WidgetId("gpuHistory"));

    [Fact]
    public void S11014_it_starts_headerless_and_charts_its_adapters_series_as_the_reading_does()
    {
        Assert.True(Kind.Traits.HasFlag(WidgetTraits.StartsHeaderless));
        Assert.Equal(MeterSizing.History, Kind.SizingOf());
        Assert.Equal(SystemInterests.For(new GpuConfig { Adapter = 2 }, @"C:\").Series, SystemInterests.For(new GpuHistoryConfig { Adapter = 2 }, @"C:\").Series);
    }

    [Fact]
    public void S11014_its_adapter_and_span_survive_a_save_and_a_clone_and_a_bad_adapter_is_repaired()
    {
        var json = JsonSerializer.Serialize<WidgetConfig>(new GpuHistoryConfig { Adapter = 1, GraphSpanSeconds = 300 }, StateJson.WidgetConfig);
        var back = (GpuHistoryConfig)((GpuHistoryConfig)JsonSerializer.Deserialize(json, StateJson.WidgetConfig)!).Clone();
        Assert.Equal((1, 300), (back.Adapter, back.GraphSpanSeconds));

        var bad = new GpuHistoryConfig { Adapter = -1 };
        Assert.Single(bad.Repair());
        Assert.Equal(0, bad.Adapter);
    }

    [Fact]
    public void S11014_one_adapter_offers_only_the_span_two_offer_the_adapter_first()
    {
        GpuAdapter[] two = [new(0, "Radeon RX 7800", "a", 0, 0), new(1, "Intel Arc", "b", 0, 0)];
        var one = new WidgetMenuLines(() => { }, _ => { });
        GpuHistoryModule.Lines(one, new GpuHistoryConfig(), [two[0]]);
        Assert.Single(one.Lines);

        var both = new WidgetMenuLines(() => { }, _ => { });
        GpuHistoryModule.Lines(both, new GpuHistoryConfig(), two);
        Assert.Equal(2, both.Lines.Count);
    }
}
