using System.Text.Json.Serialization;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Storage;
using Themia.Core.SystemData;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.Core.Model;

/// <summary>[S-2423] What the battery itself reports: status, time, health, cycles, voltage, rate.</summary>
public sealed class BatteryDetailsConfig : DetailConfig, IReadsSystem
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

    /// <summary>[S-2513] The charge and what the battery itself reports.</summary>
    public SystemInterests SystemWants(string systemRoot) => SystemInterests.Read(SystemSource.Battery, SystemSource.BatteryPack);

    public override WidgetConfig Clone()
    {
        var copy = new BatteryDetailsConfig();
        return copy;
    }
}

/// <summary>[S-2426] Latency statistics over a window of probes: average, range, jitter and loss.</summary>
public sealed class PingDetailsConfig : DetailConfig, IReadsSystem
{
    public const int MinWindow = 5;

    public const int MaxWindow = SeriesHistory.MaxPingWindow;


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

    /// <summary>Host or address to probe. Empty falls back to <see cref="HostAddress.DefaultPingHost"/>.</summary>
    public string Host { get; set; } = HostAddress.DefaultPingHost;

    /// <summary>[S-2427] How many of the latest probes the statistics cover.</summary>
    public int Window { get; set; } = 60;

    [JsonIgnore]
    public string EffectiveHost => string.IsNullOrWhiteSpace(Host) ? HostAddress.DefaultPingHost : Host.Trim();

    [JsonIgnore]
    public int EffectiveWindow => Math.Clamp(Window, MinWindow, MaxWindow);

    /// <summary>[S-2513] The statistics' window is the prober's history: a series like the meter's.</summary>
    public SystemInterests SystemWants(string systemRoot) => SystemInterests.Chart(FeedKey.Create(SystemSource.Ping, EffectiveHost));

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

        Host ??= HostAddress.DefaultPingHost;

        if (EffectiveWindow != Window)
        {
            notes.Add($"{Kind}: window {Window} is outside {MinWindow}..{MaxWindow}; using {EffectiveWindow}");
            Window = EffectiveWindow;
        }

        return notes;
    }
}

/// <summary>[S-2430] One adapter's memory and sensors.</summary>
public sealed class GpuDetailsConfig : DetailConfig, IReadsSystem
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

    /// <summary>[S-2513] Every adapter's sample, with its memory and sensors.</summary>
    public SystemInterests SystemWants(string systemRoot) => SystemInterests.Read(SystemSource.Gpu);

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
