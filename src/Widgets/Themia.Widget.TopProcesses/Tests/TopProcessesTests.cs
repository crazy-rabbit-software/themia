using Themia.Core.Model;
using Themia.Core.SystemData;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Widget.TopProcesses.Tests;

/// <summary>[S-9700] [S-2412] The top processes widget as a module: the feed it reads, its defaults, its rows.</summary>
public sealed class TopProcessesTests
{
    private static WidgetDescriptor Kind => ModuleCatalog.Of(new WidgetId("topProcesses"));

    [Fact]
    public void S9700_it_reads_the_process_snapshot_and_charts_nothing()
    {
        var wants = SystemInterests.For(new TopProcessesConfig(), @"C:\");

        Assert.Equal([FeedKey.Create(SystemSource.Processes)], wants.Feeds);
        Assert.Empty(wants.Series);
    }

    [Fact]
    public void S9700_it_ranks_by_processor_shows_five_rows_and_groups_a_programs_processes_by_default()
    {
        var config = new TopProcessesConfig();

        Assert.Equal((ProcessSort.Cpu, 5, 2, true), (config.SortBy, config.Rows, config.Lines, config.GroupByName));
        Assert.Empty(config.Repair());

        var copy = (TopProcessesConfig)new TopProcessesConfig { SortBy = ProcessSort.Memory, Rows = 10, GroupByName = false }.Clone();
        Assert.Equal((ProcessSort.Memory, 10, false), (copy.SortBy, copy.Rows, copy.GroupByName));
    }

    [Fact]
    public void S9700_its_height_follows_its_rows_of_two_lines_and_a_dragged_edge_sets_them()
    {
        var sizing = Kind.Sizing(new TopProcessesConfig());
        Assert.Empty(sizing.Problems());
        Assert.Equal(36 + (5 * 40) + 12, sizing.Natural.Height);
        Assert.Equal(36 + (20 * 40) + 12, sizing.Max.Height);

        var given = new TopProcessesConfig();
        var dragged = Assert.IsType<TopProcessesConfig>(Kind.SizeSets!(given, new SizeDip(300, 36 + (3 * 40) + 12)));
        Assert.Equal(3, dragged.Rows);
        Assert.Equal(5, given.Rows);
        Assert.Same(given, Kind.SizeSets!(given, sizing.Natural));
    }
}
