using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.SystemData;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.App.Services.SystemData;

/// <summary>
/// [S-2500] This PC: every source read from Windows (the counters, the kernel's lists, the battery,
/// the probes). The hub's machine unless a render asks for the sample one ([S-1931]). What a reading
/// means on a chart is the hub's (<see cref="SeriesProjections"/>), the same for every machine.
/// </summary>
internal sealed partial class ThisPc(Ticker ticker) : SystemMachine
{
    private const string Scope = "system-hub";

    /// <inheritdoc />
    public override bool Live => true;

    /// <inheritdoc />
    public override HubSource<T> Open<T>(SourceId<T> source, string target) => (HubSource<T>)(source.Source switch
    {
        SystemSource.Cpu => (HubSource)new CpuSource(),
        SystemSource.Memory => new MemorySource(),
        SystemSource.Performance => new PerformanceSource(),
        SystemSource.Processor => new ProcessorSource(),
        SystemSource.CpuCores => new CpuCoresSource(),
        SystemSource.Processes => new ProcessesSource(),
        SystemSource.DiskActivity => new DiskActivitySource(ticker),
        SystemSource.LocalAddresses => new LocalAddressesSource(),
        SystemSource.PublicAddress => new PublicAddressSource(),
        SystemSource.Network => new NetworkSource(),
        SystemSource.Battery => new BatterySource(),
        SystemSource.BatteryPack => new BatteryPackSource(),
        SystemSource.Gpu => new GpuSource(),
        SystemSource.Ping => new PingSource(target),
        _ => throw new ArgumentOutOfRangeException(nameof(source), source, "not a hub source"),
    });

    // ---- sources --------------------------------------------------------------------------------

    /// <summary>[S-1140] [S-81] Load across all cores: a delta between two reads, so the hub must be its only reader.</summary>
    private sealed class CpuSource : HubSource<MeterReading>
    {
        public override MeterReading Read() => SystemMetrics.Cpu();

    }

    /// <summary>[S-1130] Physical memory in use.</summary>
    private sealed class MemorySource : HubSource<MemorySnapshot?>
    {
        public override MemorySnapshot? Read() => MemoryCounters.Read();

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
    private sealed class DiskActivitySource(Ticker ticker) : HubSource<DiskSnapshot>
    {
        private IDisposable? _counters;
        private IDisposable? _drives;

        public override void Open()
        {
            _counters = DiskMetrics.Acquire();

            // [S-4655] The volumes and labels are the shared walk's; held so it walks while the meter is
            // wanted. A new walk needs no read of its own: the next tick (a second away) carries it.
            _drives = Drives.DriveService.Shared.Watch(ticker, static () => { });
        }

        public override DiskSnapshot Read() => new(DiskMetrics.Volumes(), DiskMetrics.Activity());

        public override void Close()
        {
            _counters?.Dispose();
            _counters = null;
            _drives?.Dispose();
            _drives = null;
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
    }

    /// <summary>[S-1180] [S-1187] Charge and source from <c>GetSystemPowerStatus</c>; refreshed at once by power events ([S-2509]).</summary>
    private sealed class BatterySource : HubSource<BatteryPower?>
    {
        public override BatteryPower? Read() => SystemMetrics.Battery();

    }

    /// <summary>[S-2423] What the battery device reports, asked only while Windows says there is a battery: a desktop costs one status call.</summary>
    private sealed class BatteryPackSource : HubSource<BatteryPack?>
    {
        public override BatteryPack? Read() => SystemMetrics.Battery() is { HasBattery: true } ? BatteryDevice.Read() : null;
    }

    /// <summary>
    /// [S-431] Every adapter, on the pool, one sample at a time; the instance is an adapter's DXGI
    /// position. [S-437] While a watch holds it, a GPU widget is on screen and the sampler opens the
    /// NVIDIA library for it.
    /// </summary>
    private sealed class GpuSource : HubSource<GpuSnapshot>
    {
        private bool _held;
        private bool _shown;

        public override bool IsAsync => true;

        public override void Open()
        {
            GpuSampler.Acquire();
            _held = true;
        }

        public override void Held(bool held)
        {
            if (!_held || held == _shown)
            {
                return;
            }

            _shown = held;

            if (held)
            {
                GpuSampler.Show();
            }
            else
            {
                GpuSampler.Hide();
            }
        }

        public override Task<GpuSnapshot> ReadAsync(CancellationToken token) => GpuSampler.SampleAsync();

        public override void Close()
        {
            Held(false);

            if (_held)
            {
                _held = false;
                GpuSampler.Release();
            }
        }
    }

    /// <summary>
    /// [S-2508] [S-106] [S-108] One prober per host, probing every fifth tick ([S-2512]), shared by the ping meter and the ping
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
