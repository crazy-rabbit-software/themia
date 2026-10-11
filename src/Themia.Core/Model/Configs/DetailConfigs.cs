using System.Text.Json.Serialization;
using Themia.Core.Widgets;

namespace Themia.Core.Model;

/// <summary>
/// [S-2400] What every system detail widget shares: how often it reads. A detail widget draws no
/// history (that is the meter's, [S-1762]), so it has no chart settings, only a refresh.
/// </summary>
public abstract class DetailConfig : WidgetConfig
{
    /// <param name="intervalSeconds">This kind's default: how fast its figures actually move.</param>
    protected DetailConfig(int intervalSeconds) => IntervalSeconds = intervalSeconds;

    /// <summary>[S-2401] Seconds between reads, <see cref="MinIntervalSeconds"/> to <see cref="MaxIntervalSeconds"/>.</summary>
    public int IntervalSeconds { get; set; }

    /// <summary>The fastest refresh this kind offers.</summary>
    [JsonIgnore]
    public virtual int MinIntervalSeconds => MeterTiming.MinIntervalSeconds;

    /// <summary>The slowest refresh this kind offers. A local read tops out at a minute.</summary>
    [JsonIgnore]
    public virtual int MaxIntervalSeconds => MeterTiming.MaxIntervalSeconds;

    /// <summary>The refresh actually used: clamped on read too, so a value set in code cannot spin the tick.</summary>
    [JsonIgnore]
    public int EffectiveIntervalSeconds => Math.Clamp(IntervalSeconds, MinIntervalSeconds, MaxIntervalSeconds);

    [JsonIgnore]
    public TimeSpan TickInterval => TimeSpan.FromSeconds(EffectiveIntervalSeconds);

    protected void CopyTo(DetailConfig other) => other.IntervalSeconds = IntervalSeconds;

    /// <summary>[S-2401] An out-of-range refresh is clamped, loudly. Kinds append their own repairs.</summary>
    public override IReadOnlyList<string> Repair()
    {
        var notes = new List<string>();

        if (EffectiveIntervalSeconds != IntervalSeconds)
        {
            notes.Add($"{Kind}: refresh {IntervalSeconds} s is outside {MinIntervalSeconds}..{MaxIntervalSeconds}; using {EffectiveIntervalSeconds}");
            IntervalSeconds = EffectiveIntervalSeconds;
        }

        return notes;
    }
}

/// <summary>[S-2405] The processor's figures beside its load: speed, cores, processes, uptime.</summary>
public sealed class CpuDetailsConfig : DetailConfig
{
    /// <summary>Speed moves every second; two is enough to read it.</summary>
    public CpuDetailsConfig()
        : base(intervalSeconds: 2)
    {
    }

    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.CpuDetails;

    public override WidgetConfig Clone()
    {
        var copy = new CpuDetailsConfig();
        CopyTo(copy);
        return copy;
    }
}

/// <summary>[S-2408] Memory in use, available, committed, cached, the pools and the page file.</summary>
public sealed class MemoryDetailsConfig : DetailConfig
{
    public MemoryDetailsConfig()
        : base(intervalSeconds: 2)
    {
    }

    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.MemoryDetails;

    public override WidgetConfig Clone()
    {
        var copy = new MemoryDetailsConfig();
        CopyTo(copy);
        return copy;
    }
}

/// <summary>[S-2411] What the process list is ranked by.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ProcessSort>))]
public enum ProcessSort
{
    /// <summary>Processor share since the last read, busiest first.</summary>
    Cpu = 0,

    /// <summary>Private working set (Task Manager's Memory column), largest first.</summary>
    Memory = 1,
}

/// <summary>[S-2410] The busiest processes, by processor or by memory.</summary>
public sealed class TopProcessesConfig : DetailConfig
{
    /// <summary>The most rows the list ever holds; a ceiling, not a target ([S-2412]).</summary>
    public const int MaxRowsLimit = 50;

    public TopProcessesConfig()
        : base(intervalSeconds: 2)
    {
    }

    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.TopProcesses;

    /// <summary>[S-2411] The ranking.</summary>
    public ProcessSort SortBy { get; set; } = ProcessSort.Cpu;

    /// <summary>[S-2412] At most this many rows; 0 lists as many as the tile has whole rows for.</summary>
    public int MaxRows { get; set; }

    /// <summary>[S-2413] Every process of one executable is one row (Chrome's twenty are one Chrome).</summary>
    public bool GroupByName { get; set; } = true;

    public override WidgetConfig Clone()
    {
        var copy = new TopProcessesConfig
        {
            SortBy = SortBy,
            MaxRows = MaxRows,
            GroupByName = GroupByName,
        };

        CopyTo(copy);
        return copy;
    }

    public override IReadOnlyList<string> Repair()
    {
        var notes = new List<string>(base.Repair());

        if (!Enum.IsDefined(SortBy))
        {
            notes.Add($"{Kind}: sort {(int)SortBy} is unknown; ranking by processor");
            SortBy = ProcessSort.Cpu;
        }

        if (MaxRows is < 0 or > MaxRowsLimit)
        {
            var repaired = Math.Clamp(MaxRows, 0, MaxRowsLimit);
            notes.Add($"{Kind}: {MaxRows} rows is outside 0..{MaxRowsLimit}; using {repaired}");
            MaxRows = repaired;
        }

        return notes;
    }
}

/// <summary>[S-2415] Down and up, the session's totals, the addresses and every adapter.</summary>
public sealed class NetworkDetailsConfig : DetailConfig
{
    public NetworkDetailsConfig()
        : base(intervalSeconds: 1)
    {
    }

    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.NetworkDetails;

    /// <summary>
    /// [S-2417] Ask a public service for this connection's public address. Off by default: it is a
    /// request to somebody else's server, and the user should choose it.
    /// </summary>
    public bool ShowPublicAddress { get; set; }

    public override WidgetConfig Clone()
    {
        var copy = new NetworkDetailsConfig { ShowPublicAddress = ShowPublicAddress };
        CopyTo(copy);
        return copy;
    }
}

/// <summary>[S-2420] Free and used space, one row per drive.</summary>
public sealed class DiskSpaceConfig : DetailConfig
{
    /// <summary>Space changes slowly; half a minute is plenty, and a walk wakes no drive ([S-1162]).</summary>
    public DiskSpaceConfig()
        : base(intervalSeconds: 30)
    {
    }

    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.DiskSpace;

    /// <summary>A free-space read is cheap but space barely moves: up to five minutes.</summary>
    [JsonIgnore]
    public override int MaxIntervalSeconds => 300;

    /// <summary>[S-2421] The drives to list, as roots (<c>C:\</c>), in this order. Empty lists every drive.</summary>
    public List<string> Drives { get; set; } = [];

    public override WidgetConfig Clone()
    {
        var copy = new DiskSpaceConfig { Drives = [.. Drives ?? []] };
        CopyTo(copy);
        return copy;
    }

    /// <summary>[S-2421] Every entry kept as a root; anything that is not a drive letter, or a repeat, is dropped loudly.</summary>
    public override IReadOnlyList<string> Repair()
    {
        var notes = new List<string>(base.Repair());
        var raw = Drives ?? [];
        var kept = DiskSpaceRules.Normalise(raw);

        if (kept.Count != raw.Count || !kept.SequenceEqual(raw, StringComparer.Ordinal))
        {
            notes.Add($"{Kind}: drive list repaired from {raw.Count} to {kept.Count} entries");
        }

        Drives = kept;
        return notes;
    }
}

/// <summary>[S-2423] What the battery itself reports: status, time, health, cycles, voltage, rate.</summary>
public sealed class BatteryDetailsConfig : DetailConfig
{
    public BatteryDetailsConfig()
        : base(intervalSeconds: 15)
    {
    }

    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.BatteryDetails;

    public override WidgetConfig Clone()
    {
        var copy = new BatteryDetailsConfig();
        CopyTo(copy);
        return copy;
    }
}

/// <summary>[S-2426] Latency statistics over a window of probes: average, range, jitter and loss.</summary>
public sealed class PingDetailsConfig : DetailConfig
{
    public const int MinWindow = 5;

    public const int MaxWindow = 500;

    public PingDetailsConfig()
        : base(intervalSeconds: 10)
    {
    }

    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.PingDetails;

    /// <summary>A probe goes over the network: up to five minutes, as the ping meter allows.</summary>
    [JsonIgnore]
    public override int MaxIntervalSeconds => 300;

    /// <summary>Host or address to probe. Empty falls back to <see cref="PingConfig.DefaultHost"/>.</summary>
    public string Host { get; set; } = PingConfig.DefaultHost;

    /// <summary>[S-1173] A name for the host: the header title.</summary>
    public string HostLabel { get; set; } = string.Empty;

    /// <summary>[S-2427] How many of the latest probes the statistics cover.</summary>
    public int Window { get; set; } = 60;

    [JsonIgnore]
    public string EffectiveHost => string.IsNullOrWhiteSpace(Host) ? PingConfig.DefaultHost : Host.Trim();

    [JsonIgnore]
    public string? EffectiveLabel => string.IsNullOrWhiteSpace(HostLabel) ? null : HostLabel.Trim();

    [JsonIgnore]
    public int EffectiveWindow => Math.Clamp(Window, MinWindow, MaxWindow);

    public override WidgetConfig Clone()
    {
        var copy = new PingDetailsConfig
        {
            Host = Host,
            HostLabel = HostLabel,
            Window = Window,
        };

        CopyTo(copy);
        return copy;
    }

    public override IReadOnlyList<string> Repair()
    {
        var notes = new List<string>(base.Repair());

        Host ??= PingConfig.DefaultHost;
        HostLabel ??= string.Empty;

        if (HostLabel.Length > PingConfig.MaxLabelLength)
        {
            notes.Add($"{Kind}: host label is {HostLabel.Length} characters; cut to {PingConfig.MaxLabelLength}");
            HostLabel = HostLabel[..PingConfig.MaxLabelLength];
        }

        if (EffectiveWindow != Window)
        {
            notes.Add($"{Kind}: window {Window} is outside {MinWindow}..{MaxWindow}; using {EffectiveWindow}");
            Window = EffectiveWindow;
        }

        return notes;
    }
}

/// <summary>[S-2430] One adapter's memory and sensors.</summary>
public sealed class GpuDetailsConfig : DetailConfig
{
    public GpuDetailsConfig()
        : base(intervalSeconds: 2)
    {
    }

    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.GpuDetails;

    /// <summary>[S-2431] The adapter, as its position in DXGI's list (as <see cref="GpuConfig.Adapters"/>).</summary>
    public int Adapter { get; set; }

    public override WidgetConfig Clone()
    {
        var copy = new GpuDetailsConfig { Adapter = Adapter };
        CopyTo(copy);
        return copy;
    }

    public override IReadOnlyList<string> Repair()
    {
        var notes = new List<string>(base.Repair());

        if (Adapter < 0)
        {
            notes.Add($"{Kind}: adapter {Adapter} is not a position; using the first");
            Adapter = 0;
        }

        return notes;
    }
}
