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
    public void S9700_S14500_its_rows_of_two_lines_are_its_natural_height_every_cell_from_one_row_up()
    {
        var sizing = Kind.Sizing(new TopProcessesConfig());
        Assert.Empty(sizing.Problems());
        Assert.Equal(5 * 40, sizing.Natural.Height);
        Assert.Equal(40, sizing.Min.Height);
        Assert.Equal(SizeStretch.Ceiling, sizing.Max.Height);
        Assert.Equal(3 * 40, Kind.Sizing(new TopProcessesConfig { Rows = 3 }).Natural.Height);
    }
}
