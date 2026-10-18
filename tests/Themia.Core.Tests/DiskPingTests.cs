using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>The disk and ping meters' rules (S-1160..S-1174).</summary>
public sealed class DiskPingTests
{
    // ---- S-1160: which drives ------------------------------------------------------------------

    [Theory]
    [InlineData("C:\\", "C:\\")]
    [InlineData("c:", "C:\\")]
    [InlineData(" d ", "D:\\")]
    [InlineData("e:/", "E:\\")]
    [InlineData("", null)]
    [InlineData("_Total", null)]
    [InlineData("HarddiskVolume3", null)]
    [InlineData("C:\\Mount", null)]
    [InlineData("\\\\server\\share", null)]
    [InlineData("1:", null)]
    public void S1160_only_a_drive_letter_is_a_root(string drive, string? expected)
        => Assert.Equal(expected, DiskRules.Root(drive));

    // ---- S-1162: per-drive rates -----------------------------------------------------------------

    [Fact]
    public void S1162_rates_pair_by_drive_and_leave_out_the_total_and_folder_mounts()
    {
        var rates = DiskRules.Pair(
            [("C:", 1000), ("_Total", 5000), ("HarddiskVolume4", 7), ("d:", 10)],
            [("C:", 200), ("E:", 30)]);

        Assert.Equal(new DiskRates(1000, 200), rates["C:"]);
        Assert.Equal(new DiskRates(10, 0), rates["D:"]);
        Assert.Equal(new DiskRates(0, 30), rates["E:"]);
        Assert.Equal(3, rates.Count);
    }

    [Fact]
    public void S1162_a_drive_the_counters_do_not_list_has_no_rate_never_the_total()
    {
        var rates = DiskRules.Pair([("_Total", 5000), ("C:", 1)], []);

        Assert.Null(DiskRules.RatesFor("D:\\", rates));
        Assert.Equal(new DiskRates(1, 0), DiskRules.RatesFor("c:\\", rates));
    }

    [Fact]
    public void S1162_negative_or_non_finite_counter_values_read_as_zero()
    {
        var rates = DiskRules.Pair([("C:", -5), ("D:", double.NaN)], [("C:", double.PositiveInfinity)]);

        Assert.Equal(new DiskRates(0, 0), rates["C:"]);
        Assert.Equal(new DiskRates(0, 0), rates["D:"]);
    }

    // ---- S-1820 / S-1822: the reading and the chart are throughput, of one drive or all ------------

    [Fact]
    public void S1820_the_throughput_is_reads_and_writes_together()
        => Assert.Equal(1300, new DiskRates(1000, 300).Total);

    [Fact]
    public void S1822_the_rates_follow_the_scope()
    {
        var rates = DiskRules.Pair([("C:", 100), ("D:", 10)], [("C:", 1), ("D:", 2)]);

        Assert.Equal(new DiskRates(100, 1), DiskRules.RatesOf(DiskScope.OneDrive, "C:\\", rates));
        Assert.Equal(new DiskRates(10, 2), DiskRules.RatesOf(DiskScope.OneDrive, "d:", rates));
        Assert.Equal(new DiskRates(110, 3), DiskRules.RatesOf(DiskScope.AllDrives, "C:\\", rates));
        Assert.Null(DiskRules.RatesOf(DiskScope.OneDrive, "E:\\", rates));
        Assert.Null(DiskRules.RatesOf(DiskScope.AllDrives, "C:\\", DiskRules.Pair([], [])));
    }

    [Fact]
    public void S1820_a_sample_is_a_rate_a_wait_a_missing_drive_or_no_counters()
    {
        var rates = DiskRules.Pair([("C:", 100)], [("C:", 5)]);

        Assert.Equal(new DiskReading(DiskReadingState.Rate, new DiskRates(100, 5)), DiskRules.Read(DiskScope.OneDrive, "C:\\", rates));
        Assert.Equal(DiskReadingState.DriveMissing, DiskRules.Read(DiskScope.OneDrive, "E:\\", rates).State);
        Assert.Equal(DiskReadingState.Waiting, DiskRules.Read(DiskScope.OneDrive, "C:\\", DiskRules.Pair([], [])).State);
        Assert.Equal(DiskReadingState.Waiting, DiskRules.Read(DiskScope.AllDrives, "C:\\", DiskRules.Pair([], [])).State);
        Assert.Equal(DiskReadingState.NoCounters, DiskRules.Read(DiskScope.OneDrive, "C:\\", null).State);
    }

    [Fact]
    public void S1163_all_drives_sums_the_letters_and_never_adds_the_total_instance()
        => Assert.Equal(new DiskRates(30, 0), DiskRules.Total(DiskRules.Pair([("C:", 10), ("D:", 20), ("_Total", 30)], [])));

    [Fact]
    public void S1821_the_chart_tops_out_at_a_doubling_of_one_megabyte_per_second()
    {
        var scale = DiskRules.Scale;

        // An idle disk's background writes stay low on the chart instead of filling it.
        Assert.Equal(DiskRules.ChartFloor, scale.Ceiling([0, 40 * 1024, 300 * 1024]));
        Assert.Equal(1024 * 1024, DiskRules.ChartFloor);

        // A burst sets the top to the next doubling above it: the trace keeps headroom, and the
        // top does not move with every sample that is a little higher than the last.
        Assert.Equal(16 * DiskRules.ChartFloor, scale.Ceiling([2 * DiskRules.ChartFloor, 12.4 * DiskRules.ChartFloor]));
        Assert.Equal(16 * DiskRules.ChartFloor, scale.Ceiling([15.9 * DiskRules.ChartFloor]));
        Assert.Equal(16 * DiskRules.ChartFloor, scale.Ceiling([16 * DiskRules.ChartFloor]));
    }

    // ---- S-1161: wording ----------------------------------------------------------------------------

    [Fact]
    public void S1161_a_drive_is_named_as_explorer_names_it()
    {
        Assert.Equal("System (C:)", DiskRules.Title(new DiskVolume("C:\\", " System ", false)));
        Assert.Equal("Local Disk (D:)", DiskRules.Title(new DiskVolume("D:\\", "", false)));
        Assert.Equal("USB Drive (E:)", DiskRules.Title(new DiskVolume("E:\\", "", true)));
    }

    [Fact]
    public void S1161_the_header_names_the_drive_or_all_drives()
    {
        DiskVolume[] volumes = [new("C:\\", "", false), new("D:\\", "Data", false)];

        Assert.Equal("Local Disk (C:)", DiskRules.HeaderTitle(Locale.En, DiskScope.OneDrive, "C:\\", volumes));
        Assert.Equal("Data (D:)", DiskRules.HeaderTitle(Locale.En, DiskScope.OneDrive, "d:\\", volumes));

        // A drive the walk did not list is still named, by its letter.
        Assert.Equal("F:", DiskRules.HeaderTitle(Locale.En, DiskScope.OneDrive, "F:\\", volumes));
        Assert.Equal("All drives", DiskRules.HeaderTitle(Locale.En, DiskScope.AllDrives, "C:\\", volumes));
    }

    // ---- S-1166: the drives list -------------------------------------------------------------------

    // ---- S-1170 / S-1172: status -------------------------------------------------------------------

    [Theory]
    [InlineData(false, false, PingStatus.Checking)]
    [InlineData(true, true, PingStatus.Online)]
    [InlineData(true, false, PingStatus.Offline)]
    public void S1170_status_follows_the_last_probe(bool any, bool answered, PingStatus expected)
        => Assert.Equal(expected, PingStats.Status(any, answered));

    [Fact]
    public void S1172_online_is_success_offline_is_critical()
    {
        Assert.Equal(MeterPressure.Healthy, PingStats.Pressure(PingStatus.Online));
        Assert.Equal(MeterPressure.Critical, PingStats.Pressure(PingStatus.Offline));
        Assert.Equal(MeterPressure.Normal, PingStats.Pressure(PingStatus.Checking));
    }

    // ---- S-1173: the host is the title -----------------------------------------------------------

}
