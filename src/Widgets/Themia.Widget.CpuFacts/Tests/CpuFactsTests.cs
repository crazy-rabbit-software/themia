using System.Text.Json;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.SystemData;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.CpuFacts.Tests;

/// <summary>[S-11012] [S-11007] The CPU facts atom: the feeds it reads, its pick, its height and its menu.</summary>
public sealed class CpuFactsTests
{
    private static WidgetDescriptor Kind => ModuleCatalog.Of(new WidgetId("cpuFacts"));

    [Fact]
    public void S11012_it_reads_the_load_the_counts_and_the_processor_and_charts_nothing()
    {
        var wants = SystemInterests.For(new CpuFactsConfig(), @"C:\");

        Assert.Equal([FeedKey.Create(SystemSource.Cpu), FeedKey.Create(SystemSource.Performance), FeedKey.Create(SystemSource.Processor)], wants.Feeds);
        Assert.Empty(wants.Series);
    }

    [Fact]
    public void S11012_a_new_one_shows_speed_base_speed_cores_and_logical_processors()
        => Assert.Equal(["speed", "baseSpeed", "cores", "logical"], new CpuFactsConfig().Facts);

    [Fact]
    public void S11012_the_height_follows_the_pick_and_a_dragged_edge_adds_the_next_facts()
    {
        var config = new CpuFactsConfig();
        var sizing = Kind.Sizing(config);
        Assert.Empty(sizing.Problems());
        Assert.Equal(36 + (4 * 28) + 12, sizing.Natural.Height);
        Assert.Equal(36 + (CpuDetailRules.Facts.Count * 28) + 12, sizing.Max.Height);

        config.Facts = ["uptime", "speed"];
        var grown = Assert.IsType<CpuFactsConfig>(Kind.SizeSets!(config, new SizeDip(240, 36 + (4 * 28) + 12)));
        Assert.Equal(["uptime", "speed", "baseSpeed", "cores"], grown.Facts);
        Assert.Equal(["uptime", "speed"], config.Facts);

        var cut = Assert.IsType<CpuFactsConfig>(Kind.SizeSets!(grown, new SizeDip(240, 36 + 28 + 12)));
        Assert.Equal(["uptime"], cut.Facts);
        Assert.Same(grown, Kind.SizeSets!(grown, new SizeDip(400, 36 + (4 * 28) + 12)));
    }

    [Fact]
    public void S11012_a_stored_pick_round_trips_and_unknown_or_repeated_keys_are_repaired()
    {
        var json = JsonSerializer.Serialize<WidgetConfig>(new CpuFactsConfig { Facts = ["threads", "speed"] }, StateJson.WidgetConfig);
        var back = Assert.IsType<CpuFactsConfig>(JsonSerializer.Deserialize(json, StateJson.WidgetConfig));
        Assert.Equal(["threads", "speed"], back.Facts);

        var bad = new CpuFactsConfig { Facts = ["speed", "nope", "speed"] };
        Assert.NotEmpty(bad.Repair());
        Assert.Equal(["speed"], bad.Facts);

        var none = new CpuFactsConfig { Facts = [] };
        Assert.NotEmpty(none.Repair());
        Assert.Equal(new CpuFactsConfig().Facts, none.Facts);
    }

    [Fact]
    public void S11012_the_pick_is_the_sheets_facts_by_key_and_a_fact_not_read_is_a_dimmed_dash()
    {
        var sheet = CpuDetailRules.Sheet(Locale.En, new CpuFigures(Mhz: 3400, Cores: 8));
        var rows = FactsRules.Pick(Locale.En, sheet, ["cores", "threads", "speed"], CpuDetailRules.Facts);

        Assert.Equal(["8", "--", "3.40 GHz"], rows.Select(r => r.Value));
        Assert.Equal([true, false, true], rows.Select(r => r.Available));
        Assert.Equal("Threads", rows[1].Label);
    }

    [Fact]
    public void S11012_the_menu_lists_the_pick_then_offers_every_fact_not_shown()
    {
        var config = new CpuFactsConfig();
        var menu = new WidgetMenuLines(() => { }, _ => { });
        ((IWidgetModule)new CpuFactsModule()).MenuLines(menu, config);

        var facts = Assert.Single(menu.Lines);
        var adds = facts.Children!.Where(c => c.Text is not null && !config.Facts.Any(k => CpuDetailRules.Facts.First(o => o.Key == k).Label.Text() == c.Text) && c.Action is not null && c.Children is null).ToList();
        Assert.Equal(CpuDetailRules.Facts.Count - 4, adds.Count);

        adds[0].Action!();
        Assert.Equal(["speed", "baseSpeed", "cores", "logical", "processes"], config.Facts);
    }
}
