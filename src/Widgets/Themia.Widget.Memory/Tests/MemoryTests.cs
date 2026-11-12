using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Widget.Memory.Tests;

/// <summary>[S-6315] [S-1130] The memory reading: its drawings and their sizes.</summary>
public sealed class MemoryTests
{
    private static WidgetDescriptor Kind => ModuleCatalog.Of(new WidgetId("memory"));

    [Fact]
    public void S6315_memory_offers_the_number_a_bar_and_a_gauge()
        => Assert.Equal(["number", "bar", "gauge"], WidgetStyles.For(new WidgetId("memory")).Select(s => s.Name));

    [Fact]
    public void S1130_the_number_and_the_bar_are_one_height_and_the_bar_needs_240()
    {
        var number = Kind.Sizing(new MemoryConfig());
        Assert.Equal((84, 84), (number.Min.Height, number.Max.Height));

        var bar = Kind.Sizing(new MemoryConfig { Style = MemoryStyle.Bar });
        Assert.Equal((240, 36 + 36 + 8 + 28 + 12), (bar.Min.Width, bar.Natural.Height));
        Assert.True(bar.FixedHeight);
        Assert.Empty(Kind.Sizing(new MemoryConfig { Style = MemoryStyle.Gauge }).Problems());
    }
}
