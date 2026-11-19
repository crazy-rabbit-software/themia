using System.Text.Json;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Modules.Tests.Modules;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Modules.Tests;

/// <summary>[S-6752] Every graph kind's chart look: its defaults are its look until now, its repair, its copy and its sizing.</summary>
public sealed class ChartLooksTests
{
    /// <summary>What each graph kind drew before there was a choice: a new widget looks as it did, unasked.</summary>
    private static readonly Dictionary<string, ChartType> Before = new(StringComparer.Ordinal)
    {
        ["airQualityGraph"] = ChartType.Area,
        ["batteryHistory"] = ChartType.Area,
        ["cpuCores"] = ChartType.Area,
        ["cpuHistory"] = ChartType.Area,
        ["diskHistory"] = ChartType.Area,
        ["exchangeHistory"] = ChartType.Area,
        ["gpuHistory"] = ChartType.Area,
        ["humidityGraph"] = ChartType.Area,
        ["memoryHistory"] = ChartType.Area,
        ["networkHistory"] = ChartType.Area,
        ["pingHistory"] = ChartType.Area,
        ["pressureGraph"] = ChartType.Line,
        ["rainGraph"] = ChartType.Bars,
        ["stockHistory"] = ChartType.Area,
        ["temperatureGraph"] = ChartType.Area,
        ["uvGraph"] = ChartType.Bars,
        ["windGraph"] = ChartType.Line,
    };

    private static IEnumerable<(string Kind, IChartLooks Looks)> Graphs()
        => ModuleRegistry.Widgets
            .Select(m => (m.Descriptor.Id.Value, m.Descriptor.CreateDefault()))
            .Where(d => d.Item2 is IChartLooks)
            .Select(d => (d.Item1, (IChartLooks)d.Item2));

    [Fact]
    public void S6752_every_graph_kind_starts_in_its_look_until_now_and_simple()
    {
        Assert.Equal(Before.Keys.Order(StringComparer.Ordinal), Graphs().Select(g => g.Kind).Order(StringComparer.Ordinal));
        foreach (var (kind, looks) in Graphs())
        {
            Assert.Equal(Before[kind], looks.Chart);
            Assert.Equal(Before[kind], looks.DefaultChart);
            Assert.Contains(looks.DefaultChart, looks.ChartTypes);
            Assert.Equal(ChartDetail.Simple, looks.Detail);
            Assert.Empty(ChartLooks.Repair(looks, kind));
        }
    }

    [Fact]
    public void S6752_candles_are_offered_for_a_price_only()
    {
        foreach (var (kind, looks) in Graphs())
        {
            Assert.Equal(kind is "stockHistory" or "exchangeHistory", looks.ChartTypes.Contains(ChartType.Candles));
            Assert.Contains(ChartType.Area, looks.ChartTypes);
            Assert.Equal(kind != "cpuCores", looks.ChartTypes.Contains(ChartType.Line)); // the cores' bars draw no line: no choice
        }
    }

    [Fact]
    public void S6752_a_type_not_offered_is_repaired_to_the_kinds_own_loudly()
    {
        var history = (IChartLooks)ModuleCatalog.Of(new WidgetId("networkHistory")).CreateDefault();
        history.Chart = ChartType.Candles;
        Assert.Single(ChartLooks.Repair(history, "networkHistory"));
        Assert.Equal(ChartType.Area, history.Chart);

        var cores = (IChartLooks)ModuleCatalog.Of(new WidgetId("cpuCores")).CreateDefault();
        Assert.False(cores.OffersDetail);
        cores.Detail = ChartDetail.Detailed;
        Assert.Equal(ChartDetail.Simple, ChartLooks.EffectiveDetail(cores));
        Assert.Single(ChartLooks.Repair(cores, "cpuCores"));
    }

    [Fact]
    public void S6752_the_look_survives_a_copy_and_the_state_file()
    {
        foreach (var (kind, _) in Graphs())
        {
            var module = ModuleCatalog.Of(new WidgetId(kind));
            var config = module.CreateDefault();
            var looks = (IChartLooks)config;
            looks.Chart = ChartType.Line;
            looks.Detail = looks.OffersDetail ? ChartDetail.Detailed : ChartDetail.Simple;

            var copy = (IChartLooks)config.Clone();
            Assert.Equal((looks.Chart, looks.Detail), (copy.Chart, copy.Detail));

            var options = new JsonSerializerOptions { TypeInfoResolver = module.Json };
            var json = JsonSerializer.Serialize(config, module.ConfigType, options);
            Assert.Contains("\"Line\"", json, StringComparison.Ordinal);
            var back = (IChartLooks)JsonSerializer.Deserialize(json, module.ConfigType, options)!;
            Assert.Equal((looks.Chart, looks.Detail), (back.Chart, back.Detail));
        }
    }

    [Fact]
    public void S6751_a_detailed_graph_is_never_shorter_and_a_simple_one_keeps_its_sizing()
    {
        foreach (var (kind, _) in Graphs())
        {
            var module = ModuleCatalog.Of(new WidgetId(kind));
            var simple = module.Sizing(module.CreateDefault());
            var config = module.CreateDefault();
            ((IChartLooks)config).Detail = ChartDetail.Detailed;
            var detailed = module.Sizing(config);
            Assert.True(detailed.Min.Height >= simple.Min.Height, kind);
            Assert.Equal(simple.Min.Width, detailed.Min.Width);
        }
    }
}
