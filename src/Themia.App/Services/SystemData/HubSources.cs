using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.SystemData;
using Themia.Core.Widgets;

namespace Themia.App.Services.SystemData;

/// <summary>[S-2408] [S-2409] The machine-wide counts and memory figures, and the page files.</summary>
internal readonly record struct PerformanceSnapshot(PerformanceFigures? Figures, PagefileUsage? Pagefile);

/// <summary>[S-1161] [S-1162] The volumes as of the last walk, and the I/O counters' rates (null without counters).</summary>
internal sealed record DiskSnapshot(IReadOnlyList<DiskVolume> Volumes, DiskActivity? Activity);

/// <summary>[S-2420] [S-2422] The walked volumes and the space of each one the walk found ready, by root.</summary>
internal sealed record DiskSpaceSnapshot(IReadOnlyList<DiskVolume> Walked, IReadOnlyDictionary<string, DriveSpace> Space);

/// <summary>[S-2500] Every source the hub reads, by the snapshot it hands back.</summary>
internal static class Sources
{
    public static readonly SourceId<MeterReading> Cpu = new(SystemSource.Cpu);
    public static readonly SourceId<MemorySnapshot?> Memory = new(SystemSource.Memory);
    public static readonly SourceId<PerformanceSnapshot> Performance = new(SystemSource.Performance);
    public static readonly SourceId<CpuFigures> Processor = new(SystemSource.Processor);
    public static readonly SourceId<IReadOnlyList<ProcessSample>?> Processes = new(SystemSource.Processes);
    public static readonly SourceId<DiskSnapshot> DiskActivity = new(SystemSource.DiskActivity);
    public static readonly SourceId<DiskSpaceSnapshot> DiskSpace = new(SystemSource.DiskSpace);
    public static readonly SourceId<IReadOnlyDictionary<string, string>> LocalAddresses = new(SystemSource.LocalAddresses);
    public static readonly SourceId<PublicAddressReading> PublicAddress = new(SystemSource.PublicAddress);
    public static readonly SourceId<ThroughputSample?> Network = new(SystemSource.Network);
    public static readonly SourceId<BatteryPower?> Battery = new(SystemSource.Battery);
    public static readonly SourceId<BatteryPack?> BatteryPack = new(SystemSource.BatteryPack);
    public static readonly SourceId<GpuSnapshot> Gpu = new(SystemSource.Gpu);

    /// <summary>[S-2508] Probes of one host; the host is the feed's target.</summary>
    public static readonly SourceId<PingOutcome> Ping = new(SystemSource.Ping);

    /// <summary>The disk series of every drive together; any other disk instance is a drive root.</summary>
    public const string AllDrives = "*";

    /// <summary>[S-1124] The lowest top the network chart may have: 1 KB/s, so idle chatter stays a flat line (the original's floor).</summary>
    public const double NetworkChartFloor = 1024;
}

internal sealed partial class SystemHub
{
    /// <summary>The reader for a source; opened here, on the first feed that reads it.</summary>
    private static Runner OpenRunner(SystemSource source, string target)
    {
        Runner runner = source switch
        {
            SystemSource.Cpu => Make(source, target, new CpuSource()),
            SystemSource.Memory => Make(source, target, new MemorySource()),
            SystemSource.Performance => Make(source, target, new PerformanceSource()),
            SystemSource.Processor => Make(source, target, new ProcessorSource()),
            SystemSource.Processes => Make(source, target, new ProcessesSource()),
            SystemSource.DiskActivity => Make(source, target, new DiskActivitySource()),
            SystemSource.DiskSpace => Make(source, target, new DiskSpaceSource()),
            SystemSource.LocalAddresses => Make(source, target, new LocalAddressesSource()),
            SystemSource.PublicAddress => Make(source, target, new PublicAddressSource()),
            SystemSource.Network => Make(source, target, new NetworkSource()),
            SystemSource.Battery => Make(source, target, new BatterySource()),
            SystemSource.BatteryPack => Make(source, target, new BatteryPackSource()),
            SystemSource.Gpu => Make(source, target, new GpuSource()),
            SystemSource.Ping => Make(source, target, new PingSource(target)),
            _ => throw new ArgumentOutOfRangeException(nameof(source), source, "not a hub source"),
        };

        return runner;
    }

    private static Runner<T> Make<T>(SystemSource source, string target, HubSource<T> reader)
    {
        try
        {
            reader.Open();
        }
        catch (Exception ex)
        {
            // Reads will say what is missing; the feed still exists so its widgets are not left hanging.
            Log.Error(Scope, $"could not open {source}", ex);
        }

        return new Runner<T>(source, target, reader);
    }

    // ---- sources --------------------------------------------------------------------------------

    /// <summary>[S-1140] [S-81] Load across all cores: a delta between two reads, so the hub must be its only reader.</summary>
    private sealed class CpuSource : HubSource<MeterReading>
    {
        public override MeterReading Read() => SystemMetrics.Cpu();

        public override MeterSample Project(MeterReading snapshot, string instance) => MeterSample.Of(snapshot, ChartScale.Share);
    }

    /// <summary>[S-1130] Physical memory in use.</summary>
    private sealed class MemorySource : HubSource<MemorySnapshot?>
    {
        public override MemorySnapshot? Read() => MemoryCounters.Read();

        public override MeterSample Project(MemorySnapshot? snapshot, string instance)
            => snapshot is { } s && MemoryFigures.UsedFraction(s) is { } fraction
                ? MeterSample.Of(new MeterReading(fraction, Formatting.Percent(fraction), true), ChartScale.Share)
                : new MeterSample(MeterReading.Unavailable(Str.WidgetsB_Meters_Unavailable.Text()), null);
    }

    private sealed class PerformanceSource : HubSource<PerformanceSnapshot>
    {
        public override PerformanceSnapshot Read() => PerformanceInfo.Read();
    }

    /// <summary>[S-2405] The clock, cores, thermal zone and name. The load and the counts are the CPU and performance sources' (read first on a tick).</summary>
    private sealed class ProcessorSource : HubSource<CpuFigures>
    {
        private IDisposable? _lease;

        public override void Open() => _lease = ProcessorInfo.Acquire();

        public override CpuFigures Read() => ProcessorInfo.Read();

        public override void Close()
        {
            _lease?.Dispose();
            _lease = null;
        }
    }

    /// <summary>[S-2410] One process snapshot per due tick for every list.</summary>
    private sealed class ProcessesSource : HubSource<IReadOnlyList<ProcessSample>?>
    {
        private IDisposable? _lease;

        public override void Open() => _lease = ProcessList.Acquire();

        public override IReadOnlyList<ProcessSample>? Read() => ProcessList.Read();

        public override void Close()
        {
            _lease?.Dispose();
            _lease = null;
        }
    }

    /// <summary>[S-1162] [S-1820] Reads and writes per drive; the instance is a root or <see cref="Sources.AllDrives"/>.</summary>
    private sealed class DiskActivitySource : HubSource<DiskSnapshot>
    {
        private IDisposable? _counters;

        public override void Open() => _counters = DiskMetrics.Acquire();

        public override DiskSnapshot Read() => new(DiskMetrics.Volumes(refresh: true), DiskMetrics.Activity());

        public override MeterSample Project(DiskSnapshot snapshot, string instance)
        {
            var all = instance == Sources.AllDrives;
            var root = all ? DiskMetrics.SystemRoot : instance;
            var sample = DiskRules.Read(all ? DiskScope.AllDrives : DiskScope.OneDrive, root, snapshot.Activity?.Rates);

            var reading = sample.State switch
            {
                // The fraction is only the "this is a sample" mark: a window-scaled chart plots the value.
                DiskReadingState.Rate => new MeterReading(
                    ChartScale.Fraction(sample.Rates.Total, DiskRules.ChartFloor),
                    Formatting.Rate(sample.Rates.Total),
                    true,
                    Value: sample.Rates.Total),

                // The counters' first collection: no rate yet, so no number and no point on the chart.
                DiskReadingState.Waiting => new MeterReading(null, "--", false),
                DiskReadingState.DriveMissing => MeterReading.Unavailable(Str.WidgetsB_Disk_Unavailable.Format(("drive", DiskRules.Instance(root)))),
                _ => MeterReading.Unavailable(Str.WidgetsB_Meters_Unavailable.Text()),
            };

            return MeterSample.Of(reading, DiskRules.Scale);
        }

        public override void Close()
        {
            _counters?.Dispose();
            _counters = null;
        }
    }

    /// <summary>
    /// [S-2420] [S-2422] Free and used space of every drive the disk walk found ready, so no tick wakes a
    /// sleeping drive; a widget picks its drives from it.
    /// </summary>
    private sealed class DiskSpaceSource : HubSource<DiskSpaceSnapshot>
    {
        private string? _lastFailure;

        public override DiskSpaceSnapshot Read()
        {
            var walked = DiskMetrics.Volumes(refresh: true);
            var space = new Dictionary<string, DriveSpace>(walked.Count, StringComparer.OrdinalIgnoreCase);

            foreach (var volume in walked)
            {
                space[volume.Root] = ReadOne(volume);
            }

            return new DiskSpaceSnapshot(walked, space);
        }

        /// <summary>One drive's space, or a drive that did not answer (Total 0), logged once on a change.</summary>
        private DriveSpace ReadOne(DiskVolume volume)
        {
            try
            {
                // Free to this user (quotas included), as Explorer counts it. The walk found the drive
                // ready, so this is GetDiskFreeSpaceEx on a mounted volume, not a wake-up.
                var drive = new DriveInfo(volume.Root);
                return new DriveSpace(volume, (ulong)Math.Max(0, drive.TotalSize), (ulong)Math.Max(0, drive.AvailableFreeSpace));
            }
            catch (IOException ex)
            {
                Note($"{volume.Root} did not answer: {ex.Message}");
            }
            catch (UnauthorizedAccessException ex)
            {
                Note($"{volume.Root} refused: {ex.Message}");
            }
            catch (ArgumentException ex)
            {
                Note($"{volume.Root} is not a drive: {ex.Message}");
            }

            return new DriveSpace(volume, 0, 0);
        }

        private void Note(string failure)
        {
            if (failure != _lastFailure)
            {
                _lastFailure = failure;
                Log.Debug(Scope, failure);
            }
        }
    }

    /// <summary>[S-2419] Each adapter's local address; walked again at once after an address change ([S-2509]).</summary>
    private sealed class LocalAddressesSource : HubSource<IReadOnlyDictionary<string, string>>
    {
        private IDisposable? _lease;

        public override void Open() => _lease = Services.LocalAddresses.Acquire();

        public override IReadOnlyDictionary<string, string> Read() => Services.LocalAddresses.Current();

        public override void Close()
        {
            _lease?.Dispose();
            _lease = null;
        }
    }

    /// <summary>[S-2417] [S-2418] The public address, only while a widget that wants it holds the feed.</summary>
    private sealed class PublicAddressSource : HubSource<PublicAddressReading>
    {
        private IDisposable? _lease;

        public override void Open() => _lease = Services.PublicAddress.Acquire();

        public override PublicAddressReading Read() => Services.PublicAddress.Current();

        public override void Close()
        {
            _lease?.Dispose();
            _lease = null;
        }
    }

    /// <summary>[S-1190] [S-1191] Throughput per adapter and in total; the meter's series is download (filled) and upload.</summary>
    private sealed class NetworkSource : HubSource<ThroughputSample?>
    {
        public override ThroughputSample? Read() => NetworkThroughput.Read();

        public override MeterSample Project(ThroughputSample? snapshot, string instance)
        {
            if (snapshot is null)
            {
                return new MeterSample(MeterReading.Unavailable(Str.WidgetsB_Meters_Unavailable.Text()), null);
            }

            if (snapshot.Interfaces.Count == 0)
            {
                return new MeterSample(MeterReading.Unavailable(Str.WidgetsB_Network_NoConnection.Text()), null);
            }

            // The fraction only marks a sample: the chart is window-scaled and plots the values.
            var reading = new MeterReading(
                ChartScale.Fraction(snapshot.Down, Sources.NetworkChartFloor),
                Formatting.Rate(snapshot.Down),
                true,
                Value: snapshot.Down,
                Second: snapshot.Up);

            return MeterSample.Of(reading, ChartScale.Peak(Sources.NetworkChartFloor));
        }
    }

    /// <summary>[S-1180] [S-1187] Charge and source from <c>GetSystemPowerStatus</c>; refreshed at once by power events ([S-2509]).</summary>
    private sealed class BatterySource : HubSource<BatteryPower?>
    {
        public override BatteryPower? Read() => SystemMetrics.Battery();

        public override MeterSample Project(BatteryPower? snapshot, string instance)
        {
            if (snapshot is not { } power)
            {
                return new MeterSample(MeterReading.Unavailable(Str.WidgetsB_Meters_Unavailable.Text()), null);
            }

            if (!power.HasBattery)
            {
                return new MeterSample(MeterReading.Missing(Str.WidgetsB_Battery_None.Text()), null);
            }

            if (power.Percent is not { } percent)
            {
                return new MeterSample(MeterReading.Unavailable(Str.WidgetsB_Meters_Unavailable.Text()), null);
            }

            var reading = new MeterReading(percent / 100.0, LocaleFormat.Percent(Strings.Current, percent), true, Charging: power.Charging);
            return MeterSample.Of(reading, ChartScale.Share);
        }
    }

    /// <summary>[S-2423] What the battery device reports. A widget holds this feed only while there is a battery.</summary>
    private sealed class BatteryPackSource : HubSource<BatteryPack?>
    {
        public override BatteryPack? Read() => BatteryDevice.Read();
    }

    /// <summary>[S-431] Every adapter, on the pool, one sample at a time; the instance is an adapter's DXGI position.</summary>
    private sealed class GpuSource : HubSource<GpuSnapshot>
    {
        private bool _held;

        public override bool IsAsync => true;

        public override void Open()
        {
            GpuSampler.Acquire();
            _held = true;
        }

        public override Task<GpuSnapshot> ReadAsync(CancellationToken token) => GpuSampler.SampleAsync();

        public override MeterSample Project(GpuSnapshot snapshot, string instance)
        {
            if (!int.TryParse(instance, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var index)
                || index >= snapshot.Readings.Count)
            {
                // [S-433] An adapter that has gone (undocked, or not on this machine).
                return new MeterSample(new MeterReading(null, "--", false), null);
            }

            var reading = snapshot.Readings[index];

            // [S-430] Load is a rate: the first sample has none, which is "--", never a 0% that did not happen.
            if (!reading.HasLoad)
            {
                return new MeterSample(new MeterReading(null, "--", false), null);
            }

            var fraction = Math.Clamp(reading.Utilization / 100, 0, 1);
            return MeterSample.Of(new MeterReading(fraction, Formatting.Percent(fraction), true, Value: reading.Utilization), ChartScale.Share);
        }

        public override void Close()
        {
            if (_held)
            {
                _held = false;
                GpuSampler.Release();
            }
        }
    }

    /// <summary>
    /// [S-2508] [S-106] [S-108] One prober per host and cadence, shared by the ping meter and the ping
    /// statistics: one probe out at a time (the hub's single flight), and a transition logged once.
    /// </summary>
    private sealed class PingSource(string host) : HubSource<PingOutcome>
    {
        private string? _detail;
        private bool _any;

        public override bool IsAsync => true;

        public override async Task<PingOutcome> ReadAsync(CancellationToken token)
        {
            var outcome = await NetworkMetrics.PingAsync(host, token).ConfigureAwait(true);
            Note(outcome.Detail);
            return outcome;
        }

        /// <summary>[S-1170] [S-1172] An answer is its latency; an unanswered probe a dimmed "--" that plots on the baseline.</summary>
        public override MeterSample Project(PingOutcome snapshot, string instance)
        {
            var reading = snapshot.Reading is { Available: true, Value: { } ms }
                ? new MeterReading(Math.Clamp(ms / NetworkMetrics.LatencyCeilingMs, 0, 1), Formatting.Latency(ms), true, Value: ms)
                : new MeterReading(0, "--", false, Value: 0, Reason: snapshot.Reading.Reason ?? PingStats.StatusText(PingStatus.Offline));

            return MeterSample.Of(reading, ChartScale.Peak(1));
        }

        /// <summary>A probe is logged only when it says something the previous one did not.</summary>
        private void Note(string? detail)
        {
            if (detail == _detail && _any)
            {
                return;
            }

            var first = !_any;
            _any = true;
            _detail = detail;

            if (detail is null)
            {
                if (!first)
                {
                    Log.Info(Scope, $"ping answering again: {host}");
                }

                return;
            }

            Log.Warn(Scope, detail);
        }
    }
}
