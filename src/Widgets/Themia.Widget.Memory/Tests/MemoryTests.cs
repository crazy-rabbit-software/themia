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
        // [S-12833] The reading alone scales down as one to its unit's floor.
        Assert.Equal((Math.Max(Themia.Sdk.WidgetSizing.LeastScaled, Math.Ceiling(60d * Themia.Core.Widgets.ScaleFloor.Least(Themia.Sdk.Kit.FamilyType.Unit(Themia.Sdk.Kit.FamilyType.Big28).FontSize, Themia.Sdk.Kit.FamilyShape.Icon))), 60d, Themia.Sdk.SizeStretch.Ceiling), (number.Min.Height, number.Natural.Height, number.Max.Height)); // relaxed, [S-12411]: no practical Max

        var bar = Kind.Sizing(new MemoryConfig { Style = MemoryStyle.Bar });
        Assert.Equal((240, 12 + 36 + 8 + 28 + 12), (bar.Min.Width, bar.Natural.Height));
        Assert.Equal(bar.Min.Height, bar.Natural.Height);
        Assert.Empty(Kind.Sizing(new MemoryConfig { Style = MemoryStyle.Gauge }).Problems());
    }
}
