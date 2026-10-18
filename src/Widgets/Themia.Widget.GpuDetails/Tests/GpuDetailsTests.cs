using Themia.Core.Model;
using Themia.Core.SystemData;
using Themia.Sdk;
using Xunit;

namespace Themia.Widget.GpuDetails.Tests;

/// <summary>[S-2431] The GPU details widget: its adapter, as the GPU meter's.</summary>
public sealed class GpuDetailsTests
{
    [Fact]
    public void S2431_a_new_widget_shows_the_first_adapter_and_a_position_that_is_none_is_repaired_to_it()
    {
        // Moved from the GPU meter's S433 test, which asserted both widgets' default together.
        Assert.Equal(0, ((GpuDetailsConfig)WidgetConfig.CreateDefault(new WidgetId("gpuDetails"))).Adapter);

        var config = new GpuDetailsConfig { Adapter = -3 };
        Assert.Single(config.Repair());
        Assert.Equal(0, config.Adapter);
        Assert.Equal(2, ((GpuDetailsConfig)new GpuDetailsConfig { Adapter = 2 }.Clone()).Adapter);
    }

    [Fact]
    public void S2431_it_reads_the_gpu_feed_and_charts_nothing()
    {
        var wants = SystemInterests.For(new GpuDetailsConfig(), @"C:\");

        Assert.Equal([FeedKey.Create(SystemSource.Gpu)], wants.Feeds);
        Assert.Empty(wants.Series);
    }
}
