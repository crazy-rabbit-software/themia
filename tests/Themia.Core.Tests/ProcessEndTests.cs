using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-2437] Which processes a top-processes row stands for, for End task.</summary>
public class ProcessEndTests
{
    private static ProcessSample P(string name, int pid, long created = 1) => new(name, 0.1, 100, Process: new ProcessIdentity(pid, created));

    private static readonly ProcessSample[] Read =
    [
        P("svchost", 10), P("svchost", 11), P("Chrome", 20, 5), P("chrome", 21, 6), P("explorer", 30),
    ];

    [Fact]
    public void S2437_one_row_per_process_keys_each_by_its_pid_so_two_rows_of_one_name_are_two_rows()
    {
        var rows = ProcessTable.Rows(Locale.En, ProcessTable.Rank(Read, ProcessSort.Cpu, group: false), ProcessSort.Cpu, 0, grouped: false);

        Assert.Equal(rows.Count, rows.Select(r => r.Key).Distinct().Count());
        Assert.Contains("svchost#10", rows.Select(r => r.Key));
        Assert.Contains("svchost#11", rows.Select(r => r.Key));
    }

    [Fact]
    public void S2437_a_grouped_row_is_keyed_by_its_program_and_stands_for_every_process_of_it()
    {
        var rows = ProcessTable.Rows(Locale.En, ProcessTable.Rank(Read, ProcessSort.Cpu, group: true), ProcessSort.Cpu, 0, grouped: true);
        var chrome = Assert.Single(rows, r => r.Key.Equals("chrome", StringComparison.OrdinalIgnoreCase));

        Assert.Equal(
            [new ProcessIdentity(20, 5), new ProcessIdentity(21, 6)],
            ProcessTable.Members(Read, chrome.Key, grouped: true).OrderBy(p => p.Pid));
    }

    [Fact]
    public void S2437_an_ungrouped_row_stands_for_its_one_process()
    {
        Assert.Equal([new ProcessIdentity(11, 1)], ProcessTable.Members(Read, "svchost#11", grouped: false));
        Assert.Empty(ProcessTable.Members(Read, "svchost", grouped: false));
    }

    [Fact]
    public void S2437_a_row_the_read_no_longer_has_stands_for_nothing()
    {
        Assert.Empty(ProcessTable.Members(Read, "notepad", grouped: true));
        Assert.Empty(ProcessTable.Members(Read, "svchost#99", grouped: false));
    }

    [Fact]
    public void S2437_a_process_without_a_known_pid_is_never_a_member()
    {
        ProcessSample[] read = [new("System", 0, 0), P("System", 4)];

        Assert.Equal([new ProcessIdentity(4, 1)], ProcessTable.Members(read, "System", grouped: true));
        Assert.Equal("System", ProcessTable.RowKey(read[0], grouped: false));
    }
}
