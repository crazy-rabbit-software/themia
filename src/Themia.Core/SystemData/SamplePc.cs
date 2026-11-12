using System.Collections;
using Themia.App.Services;
using Themia.App.Services.SystemData;
using Themia.Core.Widgets;

namespace Themia.Core.SystemData;

/// <summary>
/// [S-1931] The sample machine: one fixed, ordinary good laptop that a render shows instead of this
/// PC (<c>--machine sample</c>). A 12-core processor (16 logical) at about 30 %, 32 GB of memory about
/// half used, a discrete GPU, a 1 TB system drive and a data drive, Wi-Fi at a few MB/s down, a ping of
/// about 12 ms, a battery at 78 % and discharging, and the programs every Windows PC runs. Pure data:
/// nothing here reads the machine, the clock or a random source, so every call answers the same and
/// two renders draw the same picture. The readings are in each source's own snapshot type, so the
/// hub's rules (its projections, the series' history) draw them as they draw this PC's.
/// <para>
/// [S-1932] A charted source answers its whole history, oldest first, the newest last, as many points
/// as the hub keeps (<see cref="SeriesHistory.Capacity"/>); every other source one snapshot. Each
/// history has its own shape out of a fixed seed (<see cref="Noise"/>): the processor's spiky load
/// (each core its own, the total their mean), memory's slow steps, the network's and the disks'
/// bursts, the ping's jitter, the GPU's long plateaus, the battery's slow fall.
/// </para>
/// </summary>
public static class SamplePc
{
    /// <summary>The processor's name, as the CPU details' header shows it.</summary>
    public const string ProcessorName = "Intel Core i7-1360P";

    /// <summary>The discrete GPU's name.</summary>
    public const string GpuName = "NVIDIA GeForce RTX 4070 Laptop GPU";

    /// <summary>The Wi-Fi adapter's id: its key in the local addresses.</summary>
    public const string WifiId = "{5A3F2C1E-0B7D-4E8A-9C61-2D4F8B0E7A13}";

    /// <summary>A private sample address (RFC 1918), never this PC's.</summary>
    public const string LocalAddress = "192.168.1.42";

    /// <summary>A documentation address (RFC 5737, TEST-NET-3), never this PC's.</summary>
    public const string PublicAddress = "203.0.113.24";

    public const int Cores = 12;
    public const int Logical = 16;
    public const double CpuNow = 0.31;
    public const double MemoryNow = 0.52;
    public const double DownNow = 3.4 * Mb;
    public const double UpNow = 286 * Kb;
    public const double PingNow = 12;
    public const double GpuNow = 14;
    public const int BatteryNow = 78;

    public const ulong MemoryTotal = 32 * Gb;

    /// <summary>The system drive's root and the data drive's.</summary>
    public const string SystemRoot = @"C:\";
    public const string DataRoot = @"D:\";

    private const double Kb = 1024;
    private const double Mb = 1024 * 1024;
    private const ulong Gb = 1UL << 30;

    /// <summary>Minutes the battery lasts per percent at today's draw: 78 % is 3 h 41 min.</summary>
    private const double MinutesPerPercent = 221.0 / BatteryNow;

    /// <summary>[S-1932] What <paramref name="source"/> reads on the sample machine, oldest first; the last is now.</summary>
    public static IReadOnlyList<T> Readings<T>(SourceId<T> source, string? target = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        return (IReadOnlyList<T>)Readings(source.Source, target);
    }

    /// <summary>[S-1932] The readings of a source as its snapshot type (<see cref="Sources"/>), untyped: for the check that every source has some.</summary>
    public static IList Readings(SystemSource source, string? target = null) => source switch
    {
        SystemSource.Cpu => Cpu(),
        SystemSource.CpuCores => new List<CoreLoads> { CoresNow() },
        SystemSource.Memory => Memory(),
        SystemSource.Performance => new List<PerformanceSnapshot> { Performance() },
        SystemSource.Processor => new List<CpuFigures> { Processor() },
        SystemSource.Processes => new List<IReadOnlyList<ProcessSample>?> { Processes() },
        SystemSource.DiskActivity => Disk(),
        SystemSource.LocalAddresses => new List<IReadOnlyDictionary<string, string>> { new Dictionary<string, string> { [WifiId] = LocalAddress } },
        SystemSource.PublicAddress => new List<PublicAddressReading> { new(PublicAddressState.Known, PublicAddress) },
        SystemSource.Network => Network(),
        SystemSource.Battery => Battery(),
        SystemSource.BatteryPack => new List<BatteryPack?> { Pack() },
        SystemSource.Gpu => Gpu(),
        SystemSource.Ping => Ping(),
        _ => throw new ArgumentOutOfRangeException(nameof(source), source, "not a hub source"),
    };

    // ---- the processor --------------------------------------------------------------------------

    private static List<MeterReading> Cpu()
    {
        var cores = CoreSeries(SeriesHistory.Capacity(SystemSource.Cpu));
        var list = new List<MeterReading>(cores.Length);

        foreach (var sample in cores)
        {
            var load = sample.Average(l => l ?? 0);
            list.Add(new MeterReading(load, Formatting.Percent(load), true));
        }

        return list;
    }

    /// <summary>Every core's load now, and the core graphs' history of the longest span.</summary>
    private static CoreLoads CoresNow()
    {
        var history = new CoreHistory();
        history.Resize(CoreHistory.CapacityFor(MeterTiming.MaxSpanSeconds));
        var series = CoreSeries(history.Capacity);

        foreach (var sample in series)
        {
            history.Add(sample);
        }

        return new CoreLoads(series[^1], history);
    }

    /// <summary>
    /// Each logical processor's load over <paramref name="count"/> seconds: a quiet level of its own
    /// with short spikes of its own, and now and then a burst on every core (an app opening). Scaled
    /// so the newest total is <see cref="CpuNow"/>; the newest point is the same whatever the length.
    /// </summary>
    private static double?[][] CoreSeries(int count)
    {
        var noise = new Noise(0x5EED_C0DE);
        var levels = new double[Logical];
        var spikes = new (int Left, double Height)[Logical];
        var burst = (Left: 0, Height: 0.0);

        for (var core = 0; core < Logical; core++)
        {
            // Performance cores (the first eight logical) carry more than the efficiency ones.
            levels[core] = core < 8 ? 0.10 + (0.22 * noise.Next()) : 0.04 + (0.10 * noise.Next());
        }

        // Two fixed hours, of which the newest count seconds are kept: a shorter history is the same tail.
        var hour = Math.Max(7200, count);
        var all = new double[hour][];

        for (var t = 0; t < hour; t++)
        {
            if (burst.Left == 0 && noise.Next() < 0.035)
            {
                burst = (noise.Between(1, 6), noise.Between(0.08, 0.32));
            }

            var row = new double[Logical];

            for (var core = 0; core < Logical; core++)
            {
                if (spikes[core].Left == 0 && noise.Next() < 0.04)
                {
                    spikes[core] = (noise.Between(1, 4), noise.Between(0.3, 0.9));
                }

                var load = levels[core] + (0.05 * (noise.Next() - 0.5));

                if (spikes[core].Left > 0)
                {
                    load += spikes[core].Height * (0.7 + (0.3 * noise.Next()));
                    spikes[core].Left--;
                }

                if (burst.Left > 0)
                {
                    load += burst.Height * (0.4 + (0.8 * noise.Next()));
                }

                row[core] = load;
            }

            if (burst.Left > 0)
            {
                burst.Left--;
            }

            all[t] = row;
        }

        var scale = CpuNow / all[^1].Average();
        var start = hour - Math.Min(count, hour);
        var result = new double?[hour - start][];

        for (var t = start; t < hour; t++)
        {
            result[t - start] = [.. all[t].Select(load => (double?)Math.Clamp(load * scale, 0, 1))];
        }

        // Clamping a core can move the newest total a little: the newest row is set to the figure exactly.
        // A core held at 0 or 1 takes none of the shift, so the rest are shifted again until it adds up.
        for (var pass = 0; pass < 8; pass++)
        {
            var newest = result[^1];
            var off = CpuNow - newest.Average(l => l ?? 0);
            result[^1] = [.. newest.Select(l => (double?)Math.Clamp((l ?? 0) + off, 0, 1))];
        }

        return result;
    }

    private static CpuFigures Processor() => new(
        Load: CpuNow,
        Mhz: 3412,
        BaseMhz: 2200,
        Cores: Cores,
        Logical: Logical,
        Processes: Counts.Processes,
        Threads: Counts.Threads,
        Handles: Counts.Handles,
        Uptime: new TimeSpan(2, 5, 17, 0),
        Temperature: new CpuTemperature(52, CpuTemperatureSource.ThermalZone),
        Name: ProcessorName);

    private static (uint Processes, uint Threads, uint Handles) Counts => (214, 3046, 98_412);

    // ---- memory ---------------------------------------------------------------------------------

    /// <summary>Memory in use as slow steps: a level held for minutes, a few seconds' climb to the next.</summary>
    private static List<MemorySnapshot?> Memory()
    {
        var count = SeriesHistory.Capacity(SystemSource.Memory);
        var noise = new Noise(0x3E3_0A7);
        var used = new double[count];
        var level = 0.46;
        var from = level;
        var hold = 0;
        var climb = 1;
        var at = 0;

        for (var t = 0; t < count; t++)
        {
            if (hold == 0)
            {
                from = level;
                // An app opened or closed: a step of one to four percent, drawn back towards half.
                level = Math.Clamp(level + noise.Between(-0.04, 0.04) + (0.2 * (0.5 - level)), 0.40, 0.60);
                hold = noise.Between(10, 70);
                climb = noise.Between(2, 6);
                at = 0;
            }

            var share = Math.Min(1, (at + 1) / (double)climb);
            used[t] = from + ((level - from) * share) + (0.0015 * (noise.Next() - 0.5));
            at++;
            hold--;
        }

        var shift = MemoryNow - used[^1];
        var list = new List<MemorySnapshot?>(count);

        foreach (var share in used)
        {
            list.Add(MemoryOf(Math.Clamp(share + shift, 0.05, 0.95)));
        }

        return list;
    }

    private static MemorySnapshot MemoryOf(double used) => new(MemoryTotal, (ulong)(MemoryTotal * (1 - used)));

    private static PerformanceSnapshot Performance()
    {
        var physical = MemoryOf(MemoryNow);
        var figures = new PerformanceFigures(
            CommitTotal: 21 * Gb + (412UL << 20),
            CommitLimit: 36 * Gb + (512UL << 20),
            PhysicalTotal: physical.Total,
            PhysicalAvailable: physical.Available,
            SystemCache: 9 * Gb + (820UL << 20),
            KernelPaged: 912UL << 20,
            KernelNonpaged: 448UL << 20,
            Handles: Counts.Handles,
            Processes: Counts.Processes,
            Threads: Counts.Threads);
        return new PerformanceSnapshot(figures, new PagefileUsage(4 * Gb + (512UL << 20), 318UL << 20));
    }

    // ---- processes ------------------------------------------------------------------------------

    private const string Windows = @"C:\Windows\";
    private const string System32 = Windows + @"System32\";

    /// <summary>
    /// [S-2204] The apps pinned to the sample PC's taskbar, in their order: File Explorer, Edge,
    /// Terminal, Notepad, Settings, Calculator and the Microsoft Store, each where every Windows 11 PC
    /// keeps it (a file as the process list's are, a packaged app as its Apps-folder name), so a
    /// render's stand-in taskbar draws the real icons. Fixed, not read from this PC; one that is
    /// missing there is skipped by whoever draws it.
    /// </summary>
    public static IReadOnlyList<string> TaskbarApps { get; } =
    [
        Windows + "explorer.exe",
        @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
        @"shell:AppsFolder\Microsoft.WindowsTerminal_8wekyb3d8bbwe!App",
        @"shell:AppsFolder\Microsoft.WindowsNotepad_8wekyb3d8bbwe!App",
        Windows + @"ImmersiveControlPanel\SystemSettings.exe",
        @"shell:AppsFolder\Microsoft.WindowsCalculator_8wekyb3d8bbwe!App",
        @"shell:AppsFolder\Microsoft.WindowsStore_8wekyb3d8bbwe!App",
    ];

    /// <summary>
    /// [S-1933] The programs every Windows PC runs, each with its executable where Windows keeps it,
    /// so a row draws the real icon. The paths are the same on every Windows PC, not read from this one.
    /// </summary>
    private static IReadOnlyList<ProcessSample> Processes()
    {
        const string Cbs = Windows + @"SystemApps\MicrosoftWindows.Client.CBS_cw5n1h2txyewy\";
        const long M = 1L << 20;

        return
        [
            new("dwm", 0.061, 212 * M, Image: System32 + "dwm.exe"),
            new("explorer", 0.043, 186 * M, Image: Windows + "explorer.exe"),
            new("svchost", 0.032, 410 * M, 74, Image: System32 + "svchost.exe"),
            new("SearchHost", 0.018, 142 * M, Image: Cbs + "SearchHost.exe"),
            new("Taskmgr", 0.024, 61 * M, Image: System32 + "Taskmgr.exe"),
            new("System", 0.009, 148 * 1024),
            new("notepad", 0.006, 38 * M, Image: System32 + "notepad.exe"),
            new("SystemSettings", 0.005, 54 * M, Image: Windows + @"ImmersiveControlPanel\SystemSettings.exe"),
            new("ShellExperienceHost", 0.003, 71 * M, Image: Windows + @"SystemApps\ShellExperienceHost_cw5n1h2txyewy\ShellExperienceHost.exe"),
            new("RuntimeBroker", 0.002, 44 * M, 6, Image: System32 + "RuntimeBroker.exe"),
            new("ctfmon", 0.001, 17 * M, Image: System32 + "ctfmon.exe"),
            new("sihost", 0.0005, 29 * M, Image: System32 + "sihost.exe"),
            new("Memory Compression", 0, 620 * M),
        ];
    }

    // ---- disks ----------------------------------------------------------------------------------

    /// <summary>The system drive's reads and writes come in bursts (an app loading, a save), the data drive's rarely.</summary>
    private static List<DiskSnapshot> Disk()
    {
        var count = SeriesHistory.Capacity(SystemSource.DiskActivity);
        var noise = new Noise(0xD15C_0001);
        var systemRead = Bursts(noise, count, 0.035, 1, 6, 2 * Mb, 85 * Mb, 40 * Kb);
        var systemWrite = Bursts(noise, count, 0.05, 1, 4, 0.5 * Mb, 24 * Mb, 120 * Kb);
        var dataRead = Bursts(noise, count, 0.006, 2, 12, 8 * Mb, 140 * Mb, 0);
        var dataWrite = Bursts(noise, count, 0.004, 3, 20, 4 * Mb, 60 * Mb, 0);

        // Now: the system drive busy with a save, the data drive idle.
        systemRead[^1] = 12.6 * Mb;
        systemWrite[^1] = 3.1 * Mb;
        dataRead[^1] = 0;
        dataWrite[^1] = 0;

        IReadOnlyList<DiskVolume> volumes = [new(SystemRoot, "Windows", false), new(DataRoot, "Data", false)];
        var list = new List<DiskSnapshot>(count);

        for (var t = 0; t < count; t++)
        {
            var rates = new Dictionary<string, DiskRates>(StringComparer.OrdinalIgnoreCase)
            {
                [DiskRules.Instance(SystemRoot)] = new(systemRead[t], systemWrite[t]),
                [DiskRules.Instance(DataRoot)] = new(dataRead[t], dataWrite[t]),
            };
            list.Add(new DiskSnapshot(volumes, new DiskActivity(rates)));
        }

        return list;
    }

    // ---- network --------------------------------------------------------------------------------

    /// <summary>Wi-Fi: idle chatter with downloads in bursts, uploads a small share of them; now in the middle of one.</summary>
    private static List<ThroughputSample?> Network()
    {
        var count = SeriesHistory.Capacity(SystemSource.Network);
        var noise = new Noise(0x0E7_F10E);
        var down = Bursts(noise, count, 0.018, 4, 45, 0.6 * Mb, 9 * Mb, 30 * Kb);
        var chatter = Bursts(noise, count, 0.07, 1, 3, 60 * Kb, 900 * Kb, 0);
        var up = new double[count];

        for (var t = 0; t < count; t++)
        {
            // Pages and messages between the downloads: short blips of a few hundred KB.
            down[t] += chatter[t];
            up[t] =(down[t] * noise.Between(0.04, 0.09)) + noise.Between(4 * Kb, 22 * Kb);
        }

        // The last quarter minute: a download running at a few MB/s.
        for (var t = Math.Max(0, count - 14); t < count; t++)
        {
            down[t] = DownNow * noise.Between(0.82, 1.12);
            up[t] = UpNow * noise.Between(0.7, 1.2);
        }

        down[^1] = DownNow;
        up[^1] = UpNow;

        var list = new List<ThroughputSample?>(count);
        long sessionDown = 2_861L << 20;
        long sessionUp = 318L << 20;

        for (var t = 0; t < count; t++)
        {
            sessionDown += (long)down[t];
            sessionUp += (long)up[t];
            var wifi = new InterfaceRate(WifiId, "Wi-Fi", down[t], up[t], sessionDown, sessionUp, AdapterMedium.Wireless, LinkBitsPerSecond: 1_201_000_000);
            list.Add(new ThroughputSample(down[t], up[t], [wifi], sessionDown, sessionUp));
        }

        return list;
    }

    // ---- ping -----------------------------------------------------------------------------------

    /// <summary>About 12 ms with a millisecond or two of jitter and a slow answer now and then; no probe lost.</summary>
    private static List<PingOutcome> Ping()
    {
        var count = SeriesHistory.Capacity(SystemSource.Ping);
        var noise = new Noise(0x9146_0012);
        var list = new List<PingOutcome>(count);

        for (var t = 0; t < count; t++)
        {
            // The mean of three draws: a bell of jitter, not a flat band.
            var ms = 11.4 + (3.2 * (((noise.Next() + noise.Next() + noise.Next()) / 3) - 0.5));

            if (noise.Next() < 0.025)
            {
                ms += noise.Between(6, 28);
            }

            list.Add(Probe(t == count - 1 ? PingNow : Math.Round(ms, 1)));
        }

        return list;
    }

    private static PingOutcome Probe(double ms)
        => new(new MeterReading(Math.Clamp(ms / PingStats.LatencyCeilingMs, 0, 1), Formatting.Latency(ms), true, Value: ms), null);

    // ---- battery --------------------------------------------------------------------------------

    /// <summary>Discharging the whole hour: 91 % an hour ago, 78 % now, Windows' estimate following the charge.</summary>
    private static List<BatteryPower?> Battery()
    {
        var count = SeriesHistory.Capacity(SystemSource.Battery);
        var list = new List<BatteryPower?>(count);

        for (var t = 0; t < count; t++)
        {
            var percent = (int)Math.Round(BatteryNow + (13.0 * (count - 1 - t) / Math.Max(1, count - 1)));
            list.Add(new BatteryPower(true, percent, OnMains: false, Charging: false, TimeSpan.FromMinutes(Math.Round(percent * MinutesPerPercent))));
        }

        return list;
    }

    /// <summary>The pack: 70 Wh new, 66.5 Wh now, drawing 14.1 W (3 h 41 min at 78 %).</summary>
    private static BatteryPack Pack() => new(
        Relative: false,
        DesignCapacity: 70_000,
        FullCapacity: 66_500,
        RemainingCapacity: 51_870,
        Rate: -14_080,
        VoltageMillivolts: 15_620,
        Cycles: 142,
        TemperatureCelsius: 33.5,
        Chemistry: "Li-ion",
        Manufacturer: "SMP",
        Count: 1);

    // ---- GPU ------------------------------------------------------------------------------------

    /// <summary>The discrete GPU: a few percent drawing the desktop, a video call's long plateau, short spikes.</summary>
    private static List<GpuSnapshot> Gpu()
    {
        var count = SeriesHistory.Capacity(SystemSource.Gpu);
        var noise = new Noise(0x6B0_4070);
        var load = new double[count];
        var plateau = (Left: 0, Level: 0.0);

        for (var t = 0; t < count; t++)
        {
            if (plateau.Left == 0 && noise.Next() < 0.003)
            {
                plateau = (noise.Between(240, 900), noise.Between(24, 42));
            }

            var value = 4 + (3 * noise.Next());

            if (plateau.Left > 0)
            {
                value = plateau.Level + (8 * (noise.Next() - 0.5));
                plateau.Left--;
            }
            else if (noise.Next() < 0.02)
            {
                value += noise.Between(10, 55);
            }

            load[t] = Math.Clamp(value, 0, 100);
        }

        // The last minute and a half: a video playing, about GpuNow with the decoder's rhythm.
        for (var t = Math.Max(0, count - 90); t < count; t++)
        {
            load[t] = GpuNow + (6 * (noise.Next() - 0.5)) + (t % 7 == 0 ? noise.Between(3, 12) : 0);
        }

        load[^1] = GpuNow;

        var adapter = new GpuAdapter(0, GpuName, "luid_0x00000000_0x0000c3a1", 8 * Gb, 16 * Gb, 0xc3a1, 0);
        var list = new List<GpuSnapshot>(count);

        for (var t = 0; t < count; t++)
        {
            var percent = load[t];
            var reading = new GpuReading(GpuName, percent)
            {
                Engines =
                [
                    new(GpuEngineKind.ThreeD, percent),
                    new(GpuEngineKind.Copy, percent * 0.12),
                    new(GpuEngineKind.VideoEncode, 0),
                    new(GpuEngineKind.VideoDecode, percent * 0.3),
                ],
            };

            var detail = new GpuDetail(
                GpuName,
                percent,
                DedicatedUsed: (ulong)((1.9 + (percent / 40)) * Gb),
                DedicatedTotal: 8 * Gb,
                SharedUsed: 214UL << 20,
                SharedTotal: 16 * Gb,
                new GpuSensors(Celsius: 51 + Math.Round(percent / 6), FanRpm: 2150, CoreMhz: 1605, MemoryMhz: 8001));

            list.Add(new GpuSnapshot([adapter], [reading], HasLoad: true) { Details = [detail] });
        }

        return list;
    }

    // ---- shapes ---------------------------------------------------------------------------------

    /// <summary>
    /// A rate that idles about <paramref name="idle"/> and now and then (<paramref name="chance"/> a
    /// second) bursts for a few seconds up to a peak, falling off at its end.
    /// </summary>
    private static double[] Bursts(Noise noise, int count, double chance, int shortest, int longest, double low, double high, double idle)
    {
        var values = new double[count];
        var left = 0;
        var peak = 0.0;

        for (var t = 0; t < count; t++)
        {
            if (left == 0 && noise.Next() < chance)
            {
                left = noise.Between(shortest, longest);
                peak = noise.Between(low, high);
            }

            var value = idle * noise.Next() * 2;

            if (left > 0)
            {
                // Up at once, held with a jitter, the last seconds falling away.
                var tail = Math.Min(1, left / 3.0);
                value += peak * tail * noise.Between(0.75, 1.0);
                left--;
            }

            values[t] = value;
        }

        return values;
    }

    /// <summary>
    /// [S-1932] A fixed sequence of numbers out of a seed (SplitMix64): the same on every machine and
    /// every .NET, unlike <see cref="Random"/>, whose algorithm is not promised.
    /// </summary>
    internal sealed class Noise(ulong seed)
    {
        private ulong _state = seed;

        /// <summary>The next number in [0, 1).</summary>
        public double Next()
        {
            _state += 0x9E3779B97F4A7C15UL;
            var z = _state;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            z ^= z >> 31;
            return (z >> 11) * (1.0 / (1UL << 53));
        }

        public double Between(double low, double high) => low + ((high - low) * Next());

        public int Between(int low, int high) => low + (int)Math.Floor((high - low + 1) * Next());
    }
}
