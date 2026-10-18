using Themia.Core.Model;
using Themia.Core.SystemData;
using Xunit;

namespace Themia.Widget.TopProcesses.Tests;

/// <summary>[S-9700] The top processes widget as a module: the feed it reads and its defaults.</summary>
public sealed class TopProcessesTests
{
    [Fact]
    public void S9700_it_reads_the_process_snapshot_and_charts_nothing()
    {
        var wants = SystemInterests.For(new TopProcessesConfig(), @"C:\");

        Assert.Equal([FeedKey.Create(SystemSource.Processes)], wants.Feeds);
        Assert.Empty(wants.Series);
    }

    [Fact]
    public void S9700_it_ranks_by_processor_lists_what_fits_and_groups_a_programs_processes_by_default()
    {
        var config = new TopProcessesConfig();

        Assert.Equal((ProcessSort.Cpu, 0, true), (config.SortBy, config.MaxRows, config.GroupByName));
        Assert.Empty(config.Repair());

        var copy = (TopProcessesConfig)new TopProcessesConfig { SortBy = ProcessSort.Memory, MaxRows = 10, GroupByName = false }.Clone();
        Assert.Equal((ProcessSort.Memory, 10, false), (copy.SortBy, copy.MaxRows, copy.GroupByName));
    }
}
