using System.Reflection;
using Themia.App.Services;
using Themia.App.Services.SystemData;
using Themia.Core.SystemData;
using Themia.Core.Widgets;
using Themia.Tests;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-1931]..[S-1933] The sample machine a render shows instead of this PC.</summary>
public sealed class SamplePcTests
{
    /// <summary>Every <c>SourceId&lt;T&gt;</c> field of <see cref="Sources"/>: what a widget can watch.</summary>
    private static IEnumerable<(string Name, SystemSource Source, Type Snapshot)> AllSources()
        => typeof(Sources).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType.IsGenericType && f.FieldType.GetGenericTypeDefinition() == typeof(SourceId<>))
            .Select(f => (f.Name, (SystemSource)f.FieldType.GetProperty("Source")!.GetValue(f.GetValue(null))!, f.FieldType.GetGenericArguments()[0]));

    [Fact]
    public void S1931_every_source_has_a_sample_answer_of_its_own_snapshot_type()
    {
        var sources = AllSources().ToList();

        // A source added to the enum without a SourceId, or the other way round, is caught here too.
        Assert.Equal(Enum.GetValues<SystemSource>().Order(), sources.Select(s => s.Source).Order());

        foreach (var (name, source, snapshot) in sources)
        {
            var readings = SamplePc.Readings(source, "1.1.1.1");
            Assert.True(readings.Count > 0, $"{name} has no sample answer");

            foreach (var reading in readings)
            {
                Assert.True(reading is null ? Nullable.GetUnderlyingType(snapshot) is not null || !snapshot.IsValueType : snapshot.IsInstanceOfType(reading), $"{name} answers {reading?.GetType().Name}, not {snapshot.Name}");
            }

            Assert.NotNull(readings[^1]);
        }
    }

    [Theory]
    [InlineData(SystemSource.Cpu)]
    [InlineData(SystemSource.Memory)]
    [InlineData(SystemSource.DiskActivity)]
    [InlineData(SystemSource.Network)]
    [InlineData(SystemSource.Battery)]
    [InlineData(SystemSource.Gpu)]
    [InlineData(SystemSource.Ping)]
    public void S1932_a_charted_source_answers_the_whole_history_the_hub_keeps(SystemSource source)
        => Assert.Equal(SeriesHistory.Capacity(source), SamplePc.Readings(source).Count);

    [Fact]
    public void S1932_the_core_graphs_have_a_full_history_of_every_core()
    {
        var cores = SamplePc.Readings(Sources.CpuCores)[^1];

        Assert.Equal(SamplePc.Logical, cores.Loads.Count);
        Assert.Equal(cores.History.Capacity, cores.History.Count);
        Assert.Equal(SamplePc.CpuNow, cores.Loads.Average(l => l ?? 0), 6);
        Assert.Equal(SamplePc.CpuNow, SamplePc.Readings(Sources.Cpu)[^1].Fraction!.Value, 6);
    }

    [Fact]
    public void S1931_the_newest_readings_are_the_sample_laptops_figures()
    {
        Assert.Equal(SamplePc.CpuNow, SamplePc.Readings(Sources.Cpu)[^1].Fraction!.Value, 6);
        Assert.Equal(SamplePc.MemoryNow, MemoryFigures.UsedFraction(SamplePc.Readings(Sources.Memory)[^1]!.Value)!.Value, 3);
        Assert.Equal(SamplePc.DownNow, SamplePc.Readings(Sources.Network)[^1]!.Down);
        Assert.Equal(SamplePc.PingNow, SamplePc.Readings(Sources.Ping, "1.1.1.1")[^1].Reading.Value);
        Assert.Equal(SamplePc.GpuNow, SamplePc.Readings(Sources.Gpu)[^1].Readings[0].Utilization);

        var battery = SamplePc.Readings(Sources.Battery)[^1]!.Value;
        Assert.Equal((true, SamplePc.BatteryNow, false, false), (battery.HasBattery, battery.Percent, battery.OnMains, battery.Charging));
        Assert.Equal(TimeSpan.FromMinutes(221), battery.Remaining);

        // A private address and a documentation one: never anybody's real address.
        Assert.StartsWith("192.168.", SamplePc.Readings(Sources.LocalAddresses)[^1].Values.Single(), StringComparison.Ordinal);
        Assert.StartsWith("203.0.113.", SamplePc.Readings(Sources.PublicAddress)[^1].Address, StringComparison.Ordinal);
    }

    [Fact]
    public void S1932_two_reads_answer_the_same()
    {
        foreach (var source in Enum.GetValues<SystemSource>())
        {
            Assert.Equal(Numbers(source), Numbers(source));
        }
    }

    [Fact]
    public void S1932_each_meter_has_a_shape_of_its_own()
    {
        var shapes = new Dictionary<string, double[]>
        {
            ["cpu"] = Numbers(SystemSource.Cpu),
            ["memory"] = Numbers(SystemSource.Memory),
            ["network"] = Numbers(SystemSource.Network),
            ["disk"] = Numbers(SystemSource.DiskActivity),
            ["gpu"] = Numbers(SystemSource.Gpu),
            ["ping"] = Numbers(SystemSource.Ping),
            ["battery"] = Numbers(SystemSource.Battery),
        };

        // No flat line, and no two the same curve (scaled): the last ten minutes of each, compared.
        var tails = shapes.ToDictionary(s => s.Key, s => Normalise(s.Value[^Math.Min(600, s.Value.Length)..]));

        foreach (var (name, tail) in tails)
        {
            Assert.True(tail.Max() - tail.Min() > 0, $"{name} is flat");
        }

        foreach (var a in tails)
        {
            foreach (var b in tails.Where(b => string.CompareOrdinal(a.Key, b.Key) < 0))
            {
                var n = Math.Min(a.Value.Length, b.Value.Length);
                Assert.True(Math.Abs(Correlation(a.Value[^n..], b.Value[^n..])) < 0.8, $"{a.Key} and {b.Key} draw the same curve");
            }
        }

        // The characters: memory holds its level and steps; the network idles and bursts; the ping jitters about its figure.
        var memory = shapes["memory"];
        Assert.True(Steps(memory, 0.005) < memory.Length / 20, "memory moves every second");
        Assert.True(Steps(memory, 0.005) > memory.Length / 200, "memory never steps");

        var network = shapes["network"];
        Assert.True(Percentile(network, 0.99) > 20 * Percentile(network, 0.5), "the network does not burst");

        var ping = shapes["ping"];
        Assert.InRange(Percentile(ping, 0.5), 10, 14);
        Assert.InRange(Percentile(ping, 0.9) - Percentile(ping, 0.1), 1, 5);

        var cpu = shapes["cpu"];
        Assert.True(Steps(cpu[^60..], 0.08) >= 3, "the CPU's last minute has no spikes");
    }

    [Fact]
    public void S1933_every_listed_program_is_an_ordinary_windows_one_with_its_image_where_windows_keeps_it()
    {
        var processes = SamplePc.Readings(Sources.Processes)[^1]!;

        Assert.Contains(processes, p => p.Name == "explorer");
        Assert.All(processes, p => Assert.Equal(default, p.Process));
        Assert.All(processes.Where(p => p.Image is not null), p => Assert.StartsWith(@"C:\Windows\", p.Image, StringComparison.OrdinalIgnoreCase));

        // Grouping keeps the image, so a grouped row draws its icon too.
        var grouped = ProcessTable.Rank(processes, Model.ProcessSort.Cpu, group: true);
        Assert.Equal(processes.Single(p => p.Name == "svchost").Image, grouped.Single(p => p.Name == "svchost").Image);
    }

    [Fact]
    public void S1931_the_sample_machine_reads_nothing_of_this_pc_the_clock_or_a_random_source()
    {
        var source = File.ReadAllText(RepoFiles.At("src", "Themia.Core", "SystemData", "SamplePc.cs"));

        foreach (var reader in new[] { "Environment.", "DateTime", "Stopwatch", "new Random", "Random.Shared", "File.", "Directory.", "Process.", "Registry", "DriveInfo", "Dns.", "NetworkInterface", "Guid.NewGuid" })
        {
            Assert.DoesNotContain(reader, source, StringComparison.Ordinal);
        }
    }

    /// <summary>A source's history as plain numbers: what its chart would draw.</summary>
    private static double[] Numbers(SystemSource source) => source switch
    {
        SystemSource.Cpu => [.. SamplePc.Readings(Sources.Cpu).Select(r => r.Fraction ?? double.NaN)],
        SystemSource.Memory => [.. SamplePc.Readings(Sources.Memory).Select(m => MemoryFigures.UsedFraction(m!.Value) ?? double.NaN)],
        SystemSource.Network => [.. SamplePc.Readings(Sources.Network).Select(n => n!.Down)],
        SystemSource.DiskActivity => [.. SamplePc.Readings(Sources.DiskActivity).Select(d => DiskRules.Total(d.Activity!.Value.Rates).Read + DiskRules.Total(d.Activity!.Value.Rates).Write)],
        SystemSource.Gpu => [.. SamplePc.Readings(Sources.Gpu).Select(g => g.Readings[0].Utilization)],
        SystemSource.Ping => [.. SamplePc.Readings(Sources.Ping).Select(p => p.Reading.Value ?? double.NaN)],
        SystemSource.Battery => [.. SamplePc.Readings(Sources.Battery).Select(b => (double)(b!.Value.Percent ?? -1))],
        SystemSource.CpuCores => [.. Enumerable.Range(0, SamplePc.Logical).SelectMany(core => SamplePc.Readings(Sources.CpuCores)[^1].History.Newest(core, 4000).Select(p => p.First))],
        SystemSource.Processes => [.. SamplePc.Readings(Sources.Processes)[^1]!.SelectMany(p => new[] { p.Cpu, p.Memory })],
        SystemSource.Performance => [(double)SamplePc.Readings(Sources.Performance)[^1].Figures!.Value.CommitTotal],
        SystemSource.Processor => [SamplePc.Readings(Sources.Processor)[^1].Mhz!.Value],
        SystemSource.BatteryPack => [(double)SamplePc.Readings(Sources.BatteryPack)[^1]!.Value.RemainingCapacity!.Value],
        SystemSource.LocalAddresses => [SamplePc.Readings(Sources.LocalAddresses)[^1].Count],
        SystemSource.PublicAddress => [(double)SamplePc.Readings(Sources.PublicAddress)[^1].State],
        _ => throw new ArgumentOutOfRangeException(nameof(source)),
    };

    private static double[] Normalise(double[] values)
    {
        var low = values.Min();
        var span = values.Max() - low;
        return [.. values.Select(v => span > 0 ? (v - low) / span : 0)];
    }

    private static double Correlation(double[] a, double[] b)
    {
        var (ma, mb) = (a.Average(), b.Average());
        var cov = a.Zip(b, (x, y) => (x - ma) * (y - mb)).Sum();
        var sa = Math.Sqrt(a.Sum(x => (x - ma) * (x - ma)));
        var sb = Math.Sqrt(b.Sum(y => (y - mb) * (y - mb)));
        return sa == 0 || sb == 0 ? 0 : cov / (sa * sb);
    }

    private static int Steps(double[] values, double size) => values.Zip(values.Skip(1), (a, b) => Math.Abs(b - a) > size).Count(s => s);

    private static double Percentile(double[] values, double share)
    {
        var sorted = values.Order().ToArray();
        return sorted[(int)Math.Clamp(share * (sorted.Length - 1), 0, sorted.Length - 1)];
    }
}
