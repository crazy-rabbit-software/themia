using System.Buffers.Binary;
using System.Net;
using System.Text;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-2400]..[S-2432] The system detail widgets: the shared ladder, each kind's parsing and rules.
/// Areas are the seven tiers less the header, as the card hands them to a view.
/// </summary>
public sealed class SystemDetailsTests
{
    private const double Inset = 16;

    private static readonly (string Name, double W, double H)[] Tiers =
    [
        ("XS", 120, 76), ("S", 200, 106), ("M", 320, 196), ("Wide", 480, 116),
        ("Tall", 200, 356), ("L", 560, 376), ("XL", 800, 516),
    ];

    private static DetailShape Facts(int count, double valueChars = 8)
        => new(true, 8, true, count, ValueCharacters: valueChars, LabelCharacters: 18);

    // ---- [S-2402] the ladder -------------------------------------------------------------------

    [Fact]
    public void S2402_a_tiny_tile_is_the_hero_alone()
    {
        var plan = DetailFace.Plan(new WidgetArea(120, 76), Facts(8), Inset);

        Assert.Equal(DetailArrangement.HeroOnly, plan.Arrangement);
        Assert.Equal(0, plan.Facts);
        Assert.False(plan.Caption);
    }

    [Fact]
    public void S2402_a_wide_strip_puts_the_hero_beside_the_facts()
    {
        var plan = DetailFace.Plan(new WidgetArea(480, 116), Facts(8), Inset);

        Assert.Equal(DetailArrangement.Side, plan.Arrangement);
        Assert.True(plan.Facts > 0);
        Assert.True(plan.HeroColumn < 480);
    }

    [Fact]
    public void S2402_growing_a_tile_never_shows_fewer_facts()
    {
        var last = -1;

        foreach (var height in new double[] { 130, 160, 200, 260, 320, 400, 500 })
        {
            var plan = DetailFace.Plan(new WidgetArea(320, height), Facts(9), Inset);
            Assert.True(plan.Facts >= last, $"{height}: {plan.Facts} < {last}");
            last = plan.Facts;
        }

        Assert.Equal(9, last);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void S2402_the_facts_shown_always_fit_the_height(int tier)
    {
        var (_, w, h) = Tiers[tier];
        var plan = DetailFace.Plan(new WidgetArea(w, h), Facts(9), Inset);

        if (plan.Arrangement != DetailArrangement.Stacked)
        {
            return;
        }

        var rows = (plan.Facts + plan.Columns - 1) / plan.Columns;
        var used = plan.Hero.LineHeight + (plan.Caption ? FluentType.Body.LineHeight : 0)
            + (plan.Facts > 0 ? DetailFace.SectionGap : 0) + DetailFace.FactsHeight(plan.Style, rows);
        Assert.True(used + plan.Top <= h - HeroLayout.TopInset - Inset + Disclosure.Tolerance, $"{used} > {h - Inset}");
    }

    [Fact]
    public void S3862_large_tiles_show_every_fact_as_a_stat_cell_and_grow_the_hero()
    {
        var plan = DetailFace.Plan(new WidgetArea(800, 516), Facts(8), Inset);

        Assert.Equal(8, plan.Facts);
        Assert.Equal(FactStyle.Cell, plan.Style);
        Assert.True(plan.Columns >= 2);
        Assert.True(plan.Hero.FontSize >= FluentType.Display.FontSize);
        Assert.True(plan.Hero.FontSize <= (FluentType.Display.FontSize * HeroLayout.MaxGrowth) + 1e-6);
    }

    [Fact]
    public void S3862_facts_are_only_ever_drawn_as_the_shared_stat_parts()
    {
        Assert.Equal(PartMetrics.StatLine, DetailFace.FactHeight(FactStyle.Line));
        Assert.Equal(PartMetrics.StatCell, DetailFace.FactHeight(FactStyle.Cell));
    }

    [Fact]
    public void S2402_a_cell_style_is_never_chosen_when_it_would_cut_a_value_or_stand_alone()
    {
        // A narrow column has room for one cell across: the facts are lines there.
        var plan = DetailFace.Plan(new WidgetArea(200, 356), Facts(6, valueChars: 13), Inset);

        Assert.Equal(FactStyle.Line, plan.Style);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void S3861_the_stacked_block_and_its_balance_fill_the_tile_exactly_when_there_is_no_list(int tier)
    {
        var (name, w, h) = Tiers[tier];
        var plan = DetailFace.Plan(new WidgetArea(w, h), Facts(9), Inset);

        if (plan.Arrangement != DetailArrangement.Stacked)
        {
            return;
        }

        var rows = (plan.Facts + plan.Columns - 1) / plan.Columns;
        var block = plan.Hero.LineHeight + (plan.Caption ? FluentType.Body.LineHeight : 0)
            + (plan.Facts > 0 ? DetailFace.SectionGap + DetailFace.FactsHeight(plan.Style, rows) : 0);
        var box = h - HeroLayout.TopInset - Inset;

        // Above the block is half of what is spare, under it the other half (to a pixel of rounding).
        Assert.True(block + (2 * plan.Top) <= box + Disclosure.Tolerance, name);
        Assert.True(box - block - (2 * plan.Top) <= 1 + Disclosure.Tolerance, name);
    }

    [Theory]
    [InlineData(560, 376)]
    [InlineData(800, 516)]
    public void S3861_a_short_list_is_as_tall_as_its_rows_and_the_block_stands_in_the_middle(double w, double h)
    {
        // Network details: two adapters under the facts in a large tile.
        var shape = new DetailShape(true, 11, true, 6, ListRows: 2, ListRowHeight: 44, ValueCharacters: 11, LabelCharacters: 19, ListPadding: 14);
        var plan = DetailFace.Plan(new WidgetArea(w, h), shape, Inset);

        Assert.True(plan.List);
        Assert.Equal((2 * 44) + 14, plan.ListHeight, 6);
        Assert.True(plan.Top > 0, $"no balance at {w}x{h}");

        // The hero grew into the room, but only to the tier's step: the list is still the point.
        Assert.True(plan.Hero.FontSize <= HeroLayout.TierStep(new WidgetArea(w, h)).FontSize);
    }

    [Fact]
    public void S3861_a_list_that_overflows_fills_the_room_and_starts_at_the_top_of_its_block()
    {
        var shape = new DetailShape(true, 11, true, 6, ListRows: 40, ListRowHeight: 44, ValueCharacters: 11, LabelCharacters: 19, ListPadding: 14);
        var plan = DetailFace.Plan(new WidgetArea(560, 376), shape, Inset);

        Assert.True(plan.ListHeight < (40 * 44) + 14);
        Assert.Equal(0, plan.Top);
    }

    [Fact]
    public void S3861_rows_that_grow_take_all_the_room_and_read_from_the_top()
    {
        var shape = new DetailShape(true, 8, false, 0, ListRows: 1, ListRowHeight: 56, ListPadding: 14, RowsGrow: true);
        var plan = DetailFace.Plan(new WidgetArea(560, 376), shape, Inset);

        Assert.True(plan.ListHeight > 56 * 2);
        Assert.Equal(0, plan.Top);
    }

    [Fact]
    public void S2402_a_list_gets_at_least_one_whole_row_or_none()
    {
        var shape = new DetailShape(true, 8, true, 3, ListRows: 6, ListRowHeight: 44);

        foreach (var (name, w, h) in Tiers)
        {
            var plan = DetailFace.Plan(new WidgetArea(w, h), shape, Inset);
            Assert.True(plan.ListHeight == 0 || plan.ListHeight >= 44 - Disclosure.Tolerance, name);
        }

        Assert.True(DetailFace.Plan(new WidgetArea(560, 376), shape, Inset).List);
    }

    [Fact]
    public void S2402_a_list_without_a_hero_takes_the_whole_tile()
    {
        var plan = DetailFace.Plan(new WidgetArea(120, 76), new DetailShape(false, 0, false, 0, 10, 32), Inset);

        Assert.Equal(76, plan.ListHeight);
    }

    // ---- [S-2401] configs ----------------------------------------------------------------------

    [Fact]
    public void S2427_the_ping_window_is_clamped()
    {
        var config = new PingDetailsConfig { Window = 1 };

        Assert.Single(config.Repair());
        Assert.Equal(PingDetailsConfig.MinWindow, config.Window);
    }

    // ---- [S-2405]..[S-2407] CPU ----------------------------------------------------------------

    [Fact]
    public void S2406_the_clock_is_base_times_performance_else_the_reported_one()
    {
        Assert.Equal(4800, CpuDetailRules.EffectiveMhz(150, 3200, 3200));
        Assert.Equal(3200, CpuDetailRules.EffectiveMhz(null, 3200, 3200));
        Assert.Equal(2900, CpuDetailRules.EffectiveMhz(0, 3200, 2900));
        Assert.Null(CpuDetailRules.EffectiveMhz(null, 0, 0));
        Assert.Equal("4.80 GHz", CpuDetailRules.Gigahertz(Locale.En, 4800));
    }

    [Fact]
    public void S2407_the_thermal_zone_is_the_hottest_plausible_one()
    {
        Assert.Equal(55, CpuDetailRules.HottestZone([300.15, 328.15, 0, double.NaN, 700])!.Value, 3);
        Assert.Null(CpuDetailRules.HottestZone([0, 273.15]));
    }

    [Theory]
    [InlineData("12th Gen Intel(R) Core(TM) i9-12900K", "Core i9-12900K")]
    [InlineData("AMD Ryzen 7 5800X 8-Core Processor            ", "Ryzen 7 5800X")]
    [InlineData("Intel(R) Core(TM) i7-8650U CPU @ 1.90GHz", "Core i7-8650U")]
    [InlineData("Snapdragon(R) X Elite - X1E78100 - Qualcomm(R) Oryon(TM) CPU", "Snapdragon X Elite - X1E78100 - Qualcomm Oryon")]
    public void S2405_the_processor_name_drops_vendor_noise(string raw, string clean)
        => Assert.Equal(clean, CpuDetailRules.CleanName(raw));

    [Fact]
    public void S2405_a_figure_windows_does_not_give_is_left_out()
    {
        var sheet = CpuDetailRules.Sheet(Locale.En, new CpuFigures(Load: 0.25, Mhz: 3000, Cores: 8));

        Assert.Equal("3.00 GHz", sheet.Hero);
        Assert.Equal("25% utilization", sheet.Caption);
        Assert.Equal(["Cores"], sheet.Facts.Select(f => f.Label));
        Assert.False(CpuDetailRules.Sheet(Locale.En, new CpuFigures()).HeroAvailable);
    }

    // ---- [S-2408] [S-2409] memory --------------------------------------------------------------

    [Fact]
    public void S2409_page_files_are_summed_along_the_chain()
    {
        var buffer = new byte[64];
        Pagefile(buffer, 0, next: 32, size: 1000, used: 100);
        Pagefile(buffer, 32, next: 0, size: 500, used: 600);

        var usage = MemoryDetailRules.ParsePagefiles(buffer, 4096);

        Assert.Equal(1500UL * 4096, usage.Total);
        Assert.Equal(600UL * 4096, usage.Used); // the second file's in-use is capped at its size
    }

    [Fact]
    public void S2409_no_page_file_and_a_broken_chain_are_safe()
    {
        Assert.Equal(new PagefileUsage(0, 0), MemoryDetailRules.ParsePagefiles([], 4096));

        var buffer = new byte[40];
        Pagefile(buffer, 0, next: 4000, size: 10, used: 1);
        Assert.Equal(10UL * 4096, MemoryDetailRules.ParsePagefiles(buffer, 4096).Total);
    }

    [Fact]
    public void S2408_the_memory_sheet_leads_with_memory_in_use()
    {
        const ulong Gb = 1UL << 30;
        var sheet = MemoryDetailRules.Sheet(Locale.En, new PerformanceFigures(10 * Gb, 20 * Gb, 16 * Gb, 4 * Gb, 3 * Gb, Gb, Gb / 2, 1, 1, 1), new PagefileUsage(0, 0));

        Assert.Equal("12 GB", sheet.Hero);
        Assert.Equal("75% of 16 GB", sheet.Caption);
        Assert.Equal("None", sheet.Facts[^1].Value);
        Assert.Same(DetailSheet.Waiting, MemoryDetailRules.Sheet(Locale.En, null, null));
    }

    // ---- [S-2410]..[S-2414] processes ----------------------------------------------------------

    [Fact]
    public void S2410_the_snapshot_is_parsed_names_resolved_and_idle_left_out()
    {
        const ulong Base = 0x1000_0000;
        var buffer = new byte[(ProcessTable.EntrySize * 3) + 128];
        var names = (ProcessTable.EntrySize * 3) + 8;
        Encoding.Unicode.GetBytes("chrome.exe").CopyTo(buffer, names);

        Entry(buffer, 0, next: ProcessTable.EntrySize, pid: 0, nameAt: 0, nameLength: 0, Base, cpu: 999, memory: 0);
        Entry(buffer, ProcessTable.EntrySize, next: ProcessTable.EntrySize, pid: 42, nameAt: names, nameLength: 20, Base, cpu: 500, memory: 4096);
        Entry(buffer, ProcessTable.EntrySize * 2, next: 0, pid: 7, nameAt: 99999, nameLength: 8, Base, cpu: 1, memory: 1);

        var entries = ProcessTable.Parse(buffer, Base);

        Assert.Equal(2, entries.Count);
        Assert.Equal(new ProcessEntry(42, "chrome", 0, 500, 4096), entries[0]);
        Assert.Equal("7", entries[1].Name); // a name pointer outside the buffer is never read
    }

    [Fact]
    public void S2410_a_chain_running_off_the_buffer_stops()
    {
        var buffer = new byte[ProcessTable.EntrySize];
        Entry(buffer, 0, next: 0x4000, pid: 5, nameAt: 0, nameLength: 0, 0, cpu: 1, memory: 1);

        Assert.Single(ProcessTable.Parse(buffer, 0));
        Assert.Empty(ProcessTable.Parse(new byte[10], 0));
    }

    [Fact]
    public void S2414_a_share_is_of_the_whole_machine_and_a_reused_pid_starts_over()
    {
        Assert.Equal(0.5, ProcessTable.CpuFraction(4_000_000, 1_000_000, 8));
        Assert.Equal(1, ProcessTable.CpuFraction(99_000_000, 1_000_000, 1));
        Assert.Equal(0, ProcessTable.CpuFraction(-5, 1_000_000, 4));

        var tracker = new ProcessTracker();
        Assert.Equal(0, tracker.Update([new ProcessEntry(9, "a", 100, 1_000, 0)], 0, 1)[0].Cpu);
        Assert.Equal(0.5, tracker.Update([new ProcessEntry(9, "a", 100, 5_001_000, 0)], 10_000_000, 1)[0].Cpu, 3);

        // The same pid with another start time is another process: its whole lifetime is not one interval's work.
        Assert.Equal(0, tracker.Update([new ProcessEntry(9, "b", 200, 90_000_000, 0)], 20_000_000, 1)[0].Cpu);
    }

    [Fact]
    public void S2413_grouping_adds_a_programs_processes_up()
    {
        ProcessSample[] samples = [new("chrome", 0.1, 100), new("Chrome", 0.2, 300), new("code", 0.25, 50)];

        var grouped = ProcessTable.Rank(samples, ProcessSort.Cpu, group: true);

        Assert.Equal(["chrome", "code"], grouped.Select(s => s.Name));
        Assert.Equal(0.3, grouped[0].Cpu, 6);
        Assert.Equal(2, grouped[0].Count);
        Assert.Equal(3, ProcessTable.Rank(samples, ProcessSort.Cpu, group: false).Count);
    }

    [Fact]
    public void S2411_S2412_rows_rank_by_the_chosen_figure_and_stop_at_the_ceiling()
    {
        ProcessSample[] samples = [new("a", 0.9, 10), new("b", 0.1, 5000), new("c", 0.5, 20)];

        var byMemory = ProcessTable.Rank(samples, ProcessSort.Memory, group: false);
        var rows = ProcessTable.Rows(Locale.En, byMemory, ProcessSort.Memory, maxRows: 2);

        Assert.Equal(["b", "c"], rows.Select(r => r.Key));
        Assert.Equal("10%", rows[0].Second);
        Assert.Equal(3, ProcessTable.Rows(Locale.En, byMemory, ProcessSort.Memory, 0).Count);
    }

    // ---- [S-2433] process icons ------------------------------------------------------------------

    [Fact]
    public void S2433_a_process_path_is_asked_once_per_process()
    {
        var asked = new List<int>();
        var cache = new ProcessPathCache(pid => { asked.Add(pid); return pid == 4 ? null : $@"C:\Apps\{pid}.exe"; });
        var app = new ProcessIdentity(10, 111);

        Assert.Equal(@"C:\Apps\10.exe", cache.PathOf(app));
        Assert.Equal(@"C:\Apps\10.exe", cache.PathOf(app));

        // A process Windows will not open is remembered as such, not asked every read.
        Assert.Null(cache.PathOf(new ProcessIdentity(4, 1)));
        Assert.Null(cache.PathOf(new ProcessIdentity(4, 1)));

        Assert.Equal([10, 4], asked);
    }

    [Fact]
    public void S2433_a_reused_pid_is_another_process_and_an_exited_one_is_forgotten()
    {
        var asked = 0;
        var cache = new ProcessPathCache(pid => { asked++; return $"p{asked}"; });

        Assert.Equal("p1", cache.PathOf(new ProcessIdentity(10, 111)));
        Assert.Equal("p2", cache.PathOf(new ProcessIdentity(10, 222)));

        cache.Retain([new ProcessIdentity(10, 222)]);
        Assert.Equal(1, cache.Count);
        Assert.Equal("p2", cache.PathOf(new ProcessIdentity(10, 222)));
    }

    [Fact]
    public void S2433_no_pid_is_no_path_and_asks_nothing()
    {
        var cache = new ProcessPathCache(_ => throw new InvalidOperationException("asked"));

        Assert.Null(cache.PathOf(default));
    }

    [Fact]
    public void S2433_a_group_takes_its_oldest_members_icon_and_every_row_leads_with_one()
    {
        ProcessSample[] samples =
        [
            new("chrome", 0.1, 100, Process: new ProcessIdentity(30, 500)),
            new("chrome", 0.2, 300, Process: new ProcessIdentity(20, 100)),
            new("code", 0.25, 50, Process: new ProcessIdentity(40, 300)),
        ];

        var grouped = ProcessTable.Rank(samples, ProcessSort.Cpu, group: true);
        Assert.Equal(new ProcessIdentity(20, 100), grouped[0].Process);

        var rows = ProcessTable.Rows(Locale.En, grouped, ProcessSort.Cpu, 0, s => s.Process.Pid == 20 ? @"C:\chrome.exe" : null);
        Assert.Equal(@"C:\chrome.exe", rows[0].IconPath);
        Assert.Null(rows[1].IconPath);
        Assert.All(rows, r => Assert.Equal(RowSymbol.App, r.Symbol));
    }

    [Fact]
    public void S2433_the_tracker_keeps_each_processs_identity()
    {
        var tracker = new ProcessTracker();
        var sample = Assert.Single(tracker.Update([new ProcessEntry(9, "a", 100, 1_000, 0)], 0, 1));

        Assert.Equal(new ProcessIdentity(9, 100), sample.Process);
    }

    // ---- [S-2415]..[S-2419] network ------------------------------------------------------------

    [Theory]
    [InlineData(6, AdapterMedium.Ethernet)]
    [InlineData(117, AdapterMedium.Ethernet)]
    [InlineData(71, AdapterMedium.Wireless)]
    [InlineData(243, AdapterMedium.Cellular)]
    [InlineData(1, AdapterMedium.Other)]
    public void S2416_an_adapters_kind_comes_from_its_interface_type(int iana, AdapterMedium medium)
        => Assert.Equal(medium, NetworkRules.MediumOf(iana));

    [Theory]
    [InlineData(1_000_000_000L, "1 Gbps")]
    [InlineData(2_500_000_000L, "2.5 Gbps")]
    [InlineData(866_700_000L, "866.7 Mbps")]
    [InlineData(100_000_000L, "100 Mbps")]
    [InlineData(54_000_000L, "54 Mbps")]
    [InlineData(0L, "--")]
    public void S2415_a_link_speed_reads_as_windows_writes_it(long bits, string expected)
        => Assert.Equal(expected, NetworkDetailRules.LinkSpeed(Locale.En, bits));

    [Fact]
    public void S2416_an_idle_adapter_without_an_address_gets_no_row_but_still_counts()
    {
        var idle = new InterfaceRate("v", "Local Area Connection* 10", 0, 0, 0, 0);
        var busy = new InterfaceRate("w", "Wi-Fi", 100, 0, 100, 0, AdapterMedium.Wireless);
        var sample = new ThroughputSample(100, 0, [busy, idle], 100, 0);
        var sheet = NetworkDetailRules.Sheet(Locale.En, sample, new Dictionary<string, string>(), new PublicAddressReading(PublicAddressState.Off));

        Assert.Equal("w", Assert.Single(sheet.Rows).Key);
        Assert.True(NetworkDetailRules.Listed(idle, new Dictionary<string, string> { ["v"] = "192.168.137.1" }));
    }

    [Fact]
    public void S2416_an_adapter_line_is_its_address_and_speed_whichever_it_has()
    {
        Assert.Equal("10.0.0.2 · 1 Gbps", NetworkDetailRules.AdapterLine(Locale.En, "10.0.0.2", 1_000_000_000));
        Assert.Equal("10.0.0.2", NetworkDetailRules.AdapterLine(Locale.En, "10.0.0.2", 0));
        Assert.Equal("1 Gbps", NetworkDetailRules.AdapterLine(Locale.En, null, 1_000_000_000));
        Assert.Null(NetworkDetailRules.AdapterLine(Locale.En, null, 0));
    }

    [Theory]
    [InlineData("203.0.113.42\n", "203.0.113.42")]
    [InlineData("2001:db8::1", "2001:db8::1")]
    [InlineData("<html>login</html>", null)]
    [InlineData("", null)]
    [InlineData("999.1.1.1", null)]
    public void S2418_only_an_address_is_shown(string body, string? shown)
        => Assert.Equal(shown, NetworkDetailRules.PublicAddress(body));

    [Fact]
    public void S2419_the_local_address_skips_link_local_and_prefers_ipv4()
    {
        Assert.Equal("192.168.1.20", NetworkDetailRules.LocalAddress([IPAddress.Parse("fe80::1"), IPAddress.Parse("169.254.3.4"), IPAddress.Parse("192.168.1.20")]));
        Assert.Equal("2001:db8::5", NetworkDetailRules.LocalAddress([IPAddress.Parse("fe80::1"), IPAddress.Parse("2001:db8::5")]));
        Assert.Null(NetworkDetailRules.LocalAddress([IPAddress.Parse("169.254.0.1")]));
    }

    [Fact]
    public void S2415_S2416_the_sheet_lists_every_adapter_and_says_no_connection()
    {
        var sample = new ThroughputSample(2048, 1024, [new InterfaceRate("w", "Wi-Fi", 2048, 1024, 10, 5, AdapterMedium.Wireless, null, 866_700_000)], 10, 5);
        var sheet = NetworkDetailRules.Sheet(Locale.En, sample, new Dictionary<string, string> { ["w"] = "10.0.0.2" }, new PublicAddressReading(PublicAddressState.Off));

        Assert.Equal("2.0 KB/s", sheet.Hero);
        Assert.Equal("Download over Wi-Fi", sheet.Caption);
        Assert.Equal(("Upload", "1.0 KB/s"), (sheet.Facts[0].Label, sheet.Facts[0].Value));
        Assert.Contains(sheet.Facts, f => f.Value == "10.0.0.2");
        Assert.Contains(sheet.Facts, f => f.Label == "Link speed" && f.Value == "866.7 Mbps");
        Assert.DoesNotContain(sheet.Facts, f => f.Label == "Public address");
        var row = Assert.Single(sheet.Rows);
        Assert.Equal("10.0.0.2 · 866.7 Mbps", row.Detail);
        Assert.Equal(RowSymbol.Wireless, row.Symbol);
        Assert.Equal(("↓ 2.0 KB/s", "↑ 1.0 KB/s"), (row.Value, row.Second));

        var none = NetworkDetailRules.Sheet(Locale.En, new ThroughputSample(0, 0, [], 0, 0), new Dictionary<string, string>(), new PublicAddressReading(PublicAddressState.Checking));
        Assert.Equal("No connection", none.Caption);
        Assert.Contains(none.Facts, f => f.Label == "Public address" && !f.Available);
    }

    // ---- [S-2423]..[S-2425] battery ------------------------------------------------------------

    [Fact]
    public void S2424_the_driver_structures_are_parsed_and_unknowns_are_null()
    {
        var info = new byte[BatteryDetailRules.InformationSize];
        Encoding.ASCII.GetBytes("LION").CopyTo(info, 8);
        BinaryPrimitives.WriteUInt32LittleEndian(info.AsSpan(12), 50_000);
        BinaryPrimitives.WriteUInt32LittleEndian(info.AsSpan(16), 45_000);
        BinaryPrimitives.WriteUInt32LittleEndian(info.AsSpan(32), 0);

        Assert.Equal(new BatteryInformation(false, "Li-ion", 50_000, 45_000, null), BatteryDetailRules.ParseInformation(info));
        Assert.Null(BatteryDetailRules.ParseInformation(new byte[10]));

        var status = new byte[BatteryDetailRules.StatusSize];
        BinaryPrimitives.WriteUInt32LittleEndian(status.AsSpan(4), 0xFFFFFFFF);
        BinaryPrimitives.WriteUInt32LittleEndian(status.AsSpan(8), 12_000);
        BinaryPrimitives.WriteInt32LittleEndian(status.AsSpan(12), -8_000);

        Assert.Equal(new BatteryState(0, null, 12_000, -8_000), BatteryDetailRules.ParseStatus(status));
        Assert.Equal(25, BatteryDetailRules.Celsius(2981));
        Assert.Null(BatteryDetailRules.Celsius(0));
    }

    [Fact]
    public void S2425_health_and_times_follow_from_the_capacities()
    {
        var pack = new BatteryPack(false, 50_000, 40_000, 20_000, -10_000, 12_000, 300, null, null, null, 1);

        Assert.Equal(0.8, BatteryDetailRules.Health(pack));
        Assert.Equal(TimeSpan.FromHours(2), BatteryDetailRules.TimeToEmpty(pack));
        Assert.Null(BatteryDetailRules.TimeToFull(pack));
        Assert.Equal(TimeSpan.FromHours(2), BatteryDetailRules.TimeToFull(pack with { Rate = 10_000 }));
        Assert.Null(BatteryDetailRules.TimeToEmpty(pack with { Relative = true }));
    }

    [Fact]
    public void S2425_a_desktop_is_the_empty_state_and_a_laptop_leads_with_its_charge()
    {
        Assert.True(BatteryDetailRules.Sheet(Locale.En, new BatteryPower(false, null, true, false, null), null).Absent);

        var sheet = BatteryDetailRules.Sheet(Locale.En, new BatteryPower(true, 64, false, false, TimeSpan.FromMinutes(90)),
            new BatteryPack(false, 50_000, 40_000, 20_000, -10_000, 12_000, 300, null, null, null, 1));

        Assert.Equal("64%", sheet.Hero);
        Assert.Equal("On battery", sheet.Caption);
        Assert.Equal(("Time left", "1h 30m"), (sheet.Facts[0].Label, sheet.Facts[0].Value));
        Assert.Contains(sheet.Facts, f => f.Label == "Discharge rate" && f.Value == "10.0 W");
    }

    // ---- [S-2426]..[S-2429] ping ---------------------------------------------------------------

    [Fact]
    public void S2428_statistics_over_the_window()
    {
        var window = new PingWindow(5);
        foreach (var probe in new double?[] { 10, 20, null, 14, 30 })
        {
            window.Add(probe);
        }

        var stats = window.Stats();

        Assert.Equal((5, 4), (stats.Sent, stats.Answered));
        Assert.Equal((10.0, 18.5, 30.0), (stats.Min!.Value, stats.Average!.Value, stats.Max!.Value));
        Assert.Equal((10 + 6 + 16) / 3.0, stats.Jitter!.Value, 6);
        Assert.Equal(0.2, stats.Loss);
    }

    [Fact]
    public void S2427_the_window_drops_its_oldest_probe_and_one_answer_has_no_jitter()
    {
        var window = new PingWindow(2);
        window.Add(null);
        window.Add(5);
        window.Add(double.NaN);

        var stats = window.Stats();
        Assert.Equal((2, 1), (stats.Sent, stats.Answered));
        Assert.Null(stats.Jitter);
        Assert.Null(stats.Last);
        Assert.Equal(new PingWindowStats(0, 0, null, null, null, null, null, null), new PingWindow(3).Stats());
    }

    [Fact]
    public void S2429_a_lost_probe_dims_the_hero_and_says_why()
    {
        var window = new PingWindow(10);
        window.Add(null);

        var sheet = PingDetailRules.Sheet(Locale.En, window.Stats(), "1.1.1.1", PingStatus.Offline);

        Assert.False(sheet.HeroAvailable);
        Assert.Equal("Offline", sheet.Caption);
        Assert.Equal("1.1.1.1 · Offline", sheet.Note);
        Assert.Contains(sheet.Facts, f => f.Label == "Packet loss" && f.Value == "100%");
    }

    // ---- [S-2430]..[S-2432] GPU ----------------------------------------------------------------

    [Fact]
    public void S2430_memory_counters_are_summed_per_adapter()
    {
        (string, long)[] instances = [("luid_0x00000000_0x0000abcd_phys_0", 100), ("luid_0x00000000_0x0000abcd_phys_1", 50), ("luid_0x00000000_0x00001111_phys_0", 7)];

        Assert.Equal(150UL, GpuDetailRules.SumFor(instances, "luid_0x00000000_0x0000abcd"));
        Assert.Null(GpuDetailRules.SumFor(instances, "luid_0x00000000_0x00009999"));
    }

    [Fact]
    public void S2432_sensors_are_kept_only_when_the_driver_reports_them()
    {
        var adapter = new byte[GpuDetailRules.AdapterPerfSize];
        BinaryPrimitives.WriteUInt64LittleEndian(adapter.AsSpan(8), 9_001_000_000);
        BinaryPrimitives.WriteUInt32LittleEndian(adapter.AsSpan(48), 0);
        BinaryPrimitives.WriteUInt32LittleEndian(adapter.AsSpan(56), 425);
        var node = new byte[GpuDetailRules.NodePerfSize];
        BinaryPrimitives.WriteUInt64LittleEndian(node.AsSpan(8), 2_535_000_000);
        var caps = new byte[GpuDetailRules.AdapterCapsSize];
        BinaryPrimitives.WriteUInt32LittleEndian(caps.AsSpan(24), 3000);

        var sensors = GpuDetailRules.ParseSensors(adapter, node, caps);
        Assert.Equal(new GpuSensors(42.5, 0, 2535, 9001), sensors);

        // No fan sensor in the caps: a 0 RPM is "not reported", not a stopped fan; no temperature is 0.
        BinaryPrimitives.WriteUInt32LittleEndian(adapter.AsSpan(56), 0);
        Assert.Equal(new GpuSensors(null, null, null, 9001), GpuDetailRules.ParseSensors(adapter, [], []));
    }

    [Fact]
    public void S2430_an_integrated_adapter_leads_with_shared_memory()
    {
        const ulong Gb = 1UL << 30;
        var sheet = GpuDetailRules.Sheet(Locale.En, new GpuDetail("UHD", 12, null, 0, 2 * Gb, 16 * Gb, default));

        Assert.Equal("2.0 GB", sheet.Hero);
        Assert.Equal("of 16 GB shared memory", sheet.Caption);
        Assert.Equal("Load", Assert.Single(sheet.Facts).Label);
    }

    // ---- helpers -------------------------------------------------------------------------------

    private static void Pagefile(byte[] buffer, int at, uint next, uint size, uint used)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(at), next);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(at + 4), size);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(at + 8), used);
    }

    private static void Entry(byte[] buffer, int at, uint next, long pid, int nameAt, ushort nameLength, ulong bufferAddress, long cpu, long memory)
    {
        var e = buffer.AsSpan(at);
        BinaryPrimitives.WriteUInt32LittleEndian(e, next);
        BinaryPrimitives.WriteInt64LittleEndian(e[ProcessTable.PrivateWorkingSetOffset..], memory);
        BinaryPrimitives.WriteInt64LittleEndian(e[ProcessTable.UserTimeOffset..], cpu);
        BinaryPrimitives.WriteUInt16LittleEndian(e[ProcessTable.NameLengthOffset..], nameLength);
        BinaryPrimitives.WriteUInt64LittleEndian(e[ProcessTable.NameBufferOffset..], bufferAddress + (ulong)nameAt);
        BinaryPrimitives.WriteInt64LittleEndian(e[ProcessTable.PidOffset..], pid);
    }
}
