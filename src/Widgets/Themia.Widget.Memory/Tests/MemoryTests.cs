using System.Text.Json;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.SystemData;
using Themia.Core.Tests;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Widget.Memory.Tests;

/// <summary>[S-6315] The memory meter's styles. Moved with the widget from Core's MeterStylesTests, unchanged in what it asserts.</summary>
public sealed class MemoryTests
{
    [Fact]
    public void S6315_memory_offers_graph_bar_and_gauge()
        => Assert.Equal(["graph", "bar", "gauge"], WidgetStyles.For(new WidgetId("memory")).Select(s => s.Name));
}
