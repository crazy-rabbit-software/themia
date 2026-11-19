using Themia.Core.Localization;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-12813] A facts list never vanishes for want of data (owner, 2026-11-19: "even if there is no cpu data available
/// it should show the default values ... also for other widgets"): every picked fact keeps its row, its label and
/// "--", drawn unavailable.
/// </summary>
public sealed class FactsDefaultsTests
{
    [Fact]
    public void S12813_a_sheet_with_nothing_read_still_gives_every_picked_fact_its_row_with_the_default()
    {
        var empty = new DetailSheet(null, null, [], [], Note: "No data");
        var pick = CpuDetailRules.Facts.Take(4).Select(o => o.Key).ToList();

        var rows = FactsRules.Pick(Locale.En, empty, pick, CpuDetailRules.Facts);

        Assert.Equal(pick, rows.Select(r => r.Key));
        Assert.All(rows, r =>
        {
            Assert.Equal("--", r.Value);
            Assert.False(r.Available);
            Assert.False(string.IsNullOrEmpty(r.Label));
        });
    }

    [Fact]
    public void S12813_a_drive_that_cannot_be_read_keeps_its_facts_as_defaults()
    {
        var gone = new DriveEntry(@"Z:\", VolumeKind.Network, DriveState.Disconnected, Share: @"\server\share");
        var pick = DriveRules.Facts.Select(o => o.Key).ToList();

        var rows = FactsRules.Pick(Locale.En, DriveRules.Sheet(Locale.En, gone), pick, DriveRules.Facts);

        Assert.Equal(pick.Count, rows.Count);
        Assert.All(rows, r => Assert.False(string.IsNullOrEmpty(r.Label)));
    }
}
