using Themia.Core.Model;
using Themia.Core.SystemData;
using Themia.Sdk;
using Xunit;

namespace Themia.Widget.GpuFacts.Tests;

/// <summary>[S-2431] The GPU details widget: its adapter, as the GPU meter's.</summary>
public sealed class GpuFactsTests
{
    [Fact]
    public void S2431_a_new_widget_shows_the_first_adapter_and_a_position_that_is_none_is_repaired_to_it()
    {
        // Moved from the GPU meter's S433 test, which asserted both widgets' default together.
        Assert.Equal(0, ((GpuFactsConfig)WidgetConfig.CreateDefault(new WidgetId("gpuFacts"))).Adapter);

        var config = new GpuFactsConfig { Adapter = -3 };
        Assert.Single(config.Repair());
        Assert.Equal(0, config.Adapter);
        Assert.Equal(2, ((GpuFactsConfig)new GpuFactsConfig { Adapter = 2 }.Clone()).Adapter);
    }

    [Fact]
    public void S2431_it_reads_the_gpu_feed_and_charts_nothing()
    {
        var wants = SystemInterests.For(new GpuFactsConfig(), @"C:\");

        Assert.Equal([FeedKey.Create(SystemSource.Gpu)], wants.Feeds);
        Assert.Empty(wants.Series);
    }

    [Fact]
    public void S11016_a_new_one_shows_dedicated_and_shared_memory_the_temperature_and_the_fan()
        => Assert.Equal(["dedicated", "shared", "temperature", "fan"], new GpuFactsConfig().Facts);
}
