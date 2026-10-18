using System.Buffers.Binary;
using System.Net;
using System.Text;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Tests;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Modules.Tests;

/// <summary>
/// [S-2400] [S-2412] [S-2503] The detail widgets' configs under the root's rules (each round-trips
/// through the state, a detail kind has no cadence of its own, the rows and the sort are repaired):
/// the configs are their modules', the rules' tags the root's, so they run here. Moved from Core's
/// SystemDetailsTests, unchanged in what they assert.
/// </summary>
public sealed class DetailHostTests
{

    [Fact]
    public void S2503_a_detail_kind_has_no_cadence_of_its_own_and_a_stored_one_is_not_read()
    {
        // A hand-edited cadence is not a setting: the hub's one cadence reads every kind ([S-2503]).
        var processes = (TopProcessesConfig)ConfigJson.Read("TopProcesses", """{ "kind": "topProcesses", "intervalSeconds": 0 }""");
        Assert.Empty(processes.Repair());
        Assert.Null(typeof(DetailConfig).GetProperty("IntervalSeconds"));
    }

    [Fact]
    public void S2412_rows_and_sort_are_repaired()
    {
        var config = new TopProcessesConfig { MaxRows = 400, SortBy = (ProcessSort)7 };

        Assert.Equal(2, config.Repair().Count);
        Assert.Equal(TopProcessesConfig.MaxRowsLimit, config.MaxRows);
        Assert.Equal(ProcessSort.Cpu, config.SortBy);
    }

    [Fact]
    public void S2400_every_detail_config_round_trips_through_the_state_file()
    {
        var processes = (TopProcessesConfig)ConfigJson.Read("TopProcesses", """{ "kind": "topProcesses", "sortBy": "Memory", "maxRows": 10, "groupByName": false }""");
        Assert.Equal((ProcessSort.Memory, 10, false), (processes.SortBy, processes.MaxRows, processes.GroupByName));

        var network = (NetworkDetailsConfig)ConfigJson.Read("NetworkDetails", """{ "kind": "networkDetails", "showPublicAddress": true }""");
        Assert.True(network.ShowPublicAddress);

        var gpu = (GpuDetailsConfig)ConfigJson.Read("GpuDetails", """{ "kind": "gpuDetails", "adapter": 1 }""");
        Assert.Equal(1, gpu.Adapter);
    }
}
