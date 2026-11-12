using System.Text.Json;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.SystemData;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.PingHistory.Tests;

/// <summary>[S-11022] The ping history atom: headerless, its host's prober, its lines.</summary>
public sealed class PingHistoryTests
{
    private static WidgetDescriptor Kind => ModuleCatalog.Of(new WidgetId("pingHistory"));

    [Fact]
    public void S11022_it_starts_headerless_and_charts_the_probes_the_reading_reads()
    {
        Assert.True(Kind.Traits.HasFlag(WidgetTraits.StartsHeaderless));
        Assert.Equal(MeterSizing.History, Kind.SizingOf());
        Assert.Equal(900, new PingHistoryConfig().GraphSpanSeconds);
        Assert.Equal(
            SystemInterests.For(new PingConfig { Host = " 9.9.9.9 " }, @"C:\").Series,
            SystemInterests.For(new PingHistoryConfig { Host = "9.9.9.9" }, @"C:\").Series);
    }

    [Fact]
    public void S11022_its_host_and_span_survive_a_save_and_a_clone_and_its_lines_are_the_host_and_the_span()
    {
        var json = JsonSerializer.Serialize<WidgetConfig>(new PingHistoryConfig { Host = "example.com", GraphSpanSeconds = 300 }, StateJson.WidgetConfig);
        var back = (PingHistoryConfig)((PingHistoryConfig)JsonSerializer.Deserialize(json, StateJson.WidgetConfig)!).Clone();
        Assert.Equal(("example.com", 300), (back.Host, back.GraphSpanSeconds));

        var menu = new WidgetMenuLines(() => { }, _ => { });
        ((IWidgetModule)new PingHistoryModule()).MenuLines(menu, back);
        Assert.Equal(2, menu.Lines.Count);
    }
}
