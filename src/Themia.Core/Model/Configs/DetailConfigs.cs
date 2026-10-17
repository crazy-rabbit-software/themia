using System.Text.Json.Serialization;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.Core.Model;

/// <summary>
/// [S-2400] What every system detail widget shares. It reads on the hub's one cadence ([S-2503]) and
/// draws no history (that is the meter's, [S-1762]), so it has no timing or chart settings.
/// </summary>
public abstract class DetailConfig : WidgetConfig
{
    /// <summary>Nothing shared can be out of range. Kinds append their own repairs.</summary>
    public override IReadOnlyList<string> Repair() => [];
}

/// <summary>[S-2405] The processor's figures beside its load: speed, cores, processes, uptime.</summary>
public sealed class CpuDetailsConfig : DetailConfig
{

    /// <summary>[S-6902] This widget's descriptor: what the host knows of it without its UI.</summary>
    public static WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("cpuDetails"),
        ConfigType = typeof(CpuDetailsConfig),
        CreateDefault = () => new CpuDetailsConfig(),
        Json = WidgetConfigJsonContext.Default,
        Keywords = ["processor", "clock speed", "frequency", "ghz", "cores", "threads", "uptime", "temperature", "task manager"],
        Label = Str.Details_Catalog_Cpu_Label.Local(),
        Description = Str.Details_Catalog_Cpu_Desc.Local(),
        Glyph = Glyphs.Cpu,
        DefaultWidth = 280,
        DefaultHeight = 240,
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader,
        Add = new(AddFolderId.Processor, 0, 1),
    };

    public override WidgetConfig Clone()
    {
        var copy = new CpuDetailsConfig();
        return copy;
    }
}

/// <summary>[S-2408] Memory in use, available, committed, cached, the pools and the page file.</summary>
public sealed class MemoryDetailsConfig : DetailConfig
{

    /// <summary>[S-6902] This widget's descriptor: what the host knows of it without its UI.</summary>
    public static WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("memoryDetails"),
        ConfigType = typeof(MemoryDetailsConfig),
        CreateDefault = () => new MemoryDetailsConfig(),
        Json = WidgetConfigJsonContext.Default,
        Keywords = ["ram", "committed", "cached", "page file", "swap", "paged pool", "task manager"],
        Label = Str.Details_Catalog_Memory_Label.Local(),
        Description = Str.Details_Catalog_Memory_Desc.Local(),
        Glyph = Glyphs.Memory,
        DefaultWidth = 280,
        DefaultHeight = 240,
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader,
        Add = new(AddFolderId.MemoryReading, 0, 1),
    };

    public override WidgetConfig Clone()
    {
        var copy = new MemoryDetailsConfig();
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


    /// <summary>[S-6902] This widget's descriptor: what the host knows of it without its UI.</summary>
    public static WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("topProcesses"),
        ConfigType = typeof(TopProcessesConfig),
        CreateDefault = () => new TopProcessesConfig(),
        Json = WidgetConfigJsonContext.Default,
        Keywords = ["processes", "task manager", "apps", "cpu hog", "memory usage", "running"],
        Label = Str.Details_Catalog_Processes_Label.Local(),
        Description = Str.Details_Catalog_Processes_Desc.Local(),
        Glyph = Glyphs.ListLayout,
        DefaultWidth = 300,
        DefaultHeight = 300,
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader,
        Add = new(AddFolderId.Processor, 0, 2),
    };

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

    /// <summary>[S-6902] This widget's descriptor: what the host knows of it without its UI.</summary>
    public static WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("networkDetails"),
        ConfigType = typeof(NetworkDetailsConfig),
        CreateDefault = () => new NetworkDetailsConfig(),
        Json = WidgetConfigJsonContext.Default,
        Keywords = ["internet", "adapters", "ip address", "public ip", "wifi", "ethernet", "data used"],
        Label = Str.Details_Catalog_Network_Label.Local(),
        Description = Str.Details_Catalog_Network_Desc.Local(),
        Glyph = Glyphs.Network,
        DefaultWidth = 300,
        DefaultHeight = 280,
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader,
        Add = new(AddFolderId.NetworkReading, 0, 1),
    };

    /// <summary>
    /// [S-2417] Ask a public service for this connection's public address. Off by default: it is a
    /// request to somebody else's server, and the user should choose it.
    /// </summary>
    public bool ShowPublicAddress { get; set; }

    public override WidgetConfig Clone()
    {
        var copy = new NetworkDetailsConfig { ShowPublicAddress = ShowPublicAddress };
        return copy;
    }
}

/// <summary>[S-2423] What the battery itself reports: status, time, health, cycles, voltage, rate.</summary>
public sealed class BatteryDetailsConfig : DetailConfig
{

    /// <summary>[S-6902] This widget's descriptor: what the host knows of it without its UI.</summary>
    public static WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("batteryDetails"),
        ConfigType = typeof(BatteryDetailsConfig),
        CreateDefault = () => new BatteryDetailsConfig(),
        Json = WidgetConfigJsonContext.Default,
        Keywords = ["power", "battery health", "wear", "cycles", "voltage", "laptop", "time remaining"],
        Label = Str.Details_Catalog_Battery_Label.Local(),
        Description = Str.Details_Catalog_Battery_Desc.Local(),
        Glyph = Glyphs.Battery,
        DefaultWidth = 280,
        DefaultHeight = 240,
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader,
        Add = new(AddFolderId.BatteryReading, 0, 1),
    };

    public override WidgetConfig Clone()
    {
        var copy = new BatteryDetailsConfig();
        return copy;
    }
}

/// <summary>[S-2426] Latency statistics over a window of probes: average, range, jitter and loss.</summary>
public sealed class PingDetailsConfig : DetailConfig
{
    public const int MinWindow = 5;

    public const int MaxWindow = 500;


    /// <summary>[S-6902] This widget's descriptor: what the host knows of it without its UI.</summary>
    public static WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("pingDetails"),
        ConfigType = typeof(PingDetailsConfig),
        CreateDefault = () => new PingDetailsConfig(),
        Json = WidgetConfigJsonContext.Default,
        Keywords = ["latency", "jitter", "packet loss", "internet", "connection quality", "ms"],
        Label = Str.Details_Catalog_Ping_Label.Local(),
        Description = Str.Details_Catalog_Ping_Desc.Local(),
        Glyph = Glyphs.Ping,
        DefaultWidth = 260,
        DefaultHeight = 240,
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader,
        Add = new(AddFolderId.PingReading, 0, 1),
    };

    /// <summary>Host or address to probe. Empty falls back to <see cref="PingConfig.DefaultHost"/>.</summary>
    public string Host { get; set; } = PingConfig.DefaultHost;

    /// <summary>[S-2427] How many of the latest probes the statistics cover.</summary>
    public int Window { get; set; } = 60;

    [JsonIgnore]
    public string EffectiveHost => string.IsNullOrWhiteSpace(Host) ? PingConfig.DefaultHost : Host.Trim();

    [JsonIgnore]
    public int EffectiveWindow => Math.Clamp(Window, MinWindow, MaxWindow);

    public override WidgetConfig Clone()
    {
        var copy = new PingDetailsConfig
        {
            Host = Host,
            Window = Window,
        };

        return copy;
    }

    public override IReadOnlyList<string> Repair()
    {
        var notes = new List<string>(base.Repair());

        Host ??= PingConfig.DefaultHost;

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

    /// <summary>[S-6902] This widget's descriptor: what the host knows of it without its UI.</summary>
    public static WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("gpuDetails"),
        ConfigType = typeof(GpuDetailsConfig),
        CreateDefault = () => new GpuDetailsConfig(),
        Json = WidgetConfigJsonContext.Default,
        Keywords = ["graphics", "vram", "video memory", "temperature", "fan", "nvidia", "amd"],
        Label = Str.Details_Catalog_Gpu_Label.Local(),
        Description = Str.Details_Catalog_Gpu_Desc.Local(),
        Glyph = Glyphs.Gpu,
        DefaultWidth = 280,
        DefaultHeight = 240,
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader,
        Add = new(AddFolderId.Graphics, 0, 1),
    };

    /// <summary>[S-2431] The adapter, as its position in DXGI's list (as <see cref="GpuConfig.Adapter"/>: the primary display's first).</summary>
    public int Adapter { get; set; }

    public override WidgetConfig Clone()
    {
        var copy = new GpuDetailsConfig { Adapter = Adapter };
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
