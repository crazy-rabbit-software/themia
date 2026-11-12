using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.App.Widgets.Meters;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.SystemData;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Themia.Widget.GpuHistory;

[assembly: ThemiaWidget("gpuHistory", typeof(GpuHistoryModule))]

namespace Themia.Widget.GpuHistory;

/// <summary>[S-11014] One graphics adapter's load over time: its history alone, edge to edge. SPEC.md beside this file.</summary>
public sealed class GpuHistoryModule : IWidgetModule
{
    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("gpuHistory"),
        ConfigType = typeof(GpuHistoryConfig),
        CreateDefault = () => new GpuHistoryConfig(),
        Json = GpuHistoryJsonContext.Default,
        Keywords = ["graphics", "gpu", "graph", "chart", "history", "nvidia", "amd"],
        Label = GpuHistoryStr.GpuHistory_Label.Local(),
        Description = GpuHistoryStr.GpuHistory_Desc.Local(),
        Glyph = Glyphs.Gpu,
        Sizing = _ => MeterSizing.History,
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader | WidgetTraits.StartsHeaderless,
        Add = new(AddFolderId.Graphics, 0, 1),
    };

    /// <summary>[S-11014] The adapter (offered with two or more, [S-433]) and the chart's span ([S-1121]).</summary>
    public void MenuLines(WidgetMenuLines menu, WidgetConfig config) => Lines(menu, (GpuHistoryConfig)config, GpuAdapters.List());

    internal static void Lines(WidgetMenuLines menu, GpuHistoryConfig config, IReadOnlyList<GpuAdapter> adapters)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);
        if (GpuAdapterLine.Line(menu, config.Adapter, value => config.Adapter = value, adapters) is { } adapter)
        {
            menu.Lines.Add(adapter);
        }

        menu.Lines.Add(AtomMenuLines.Span(menu, config));
    }

    public FrameworkElement CreateView(IWidgetHost host) => new MeterView(host, new GpuHistoryMeter());
}

/// <summary>[S-11014] What one GPU history remembers: its adapter and its span (a minute).</summary>
public sealed class GpuHistoryConfig : HistoryConfig, IReadsSystem, IGpuAdapter
{
    public GpuHistoryConfig()
        : base(graphSpanSeconds: 60)
    {
    }

    /// <summary>[S-433] The adapter, as its position in DXGI's list (as the GPU reading's).</summary>
    public int Adapter { get; set; }

    /// <summary>[S-2513] The adapter's series, the reading's own.</summary>
    public SystemInterests SystemWants(string systemRoot) => GpuInterests.Of(Adapter);

    public override WidgetConfig Clone()
    {
        var copy = new GpuHistoryConfig { Adapter = Adapter };
        CopyTo(copy);
        return copy;
    }

    public override IReadOnlyList<string> Repair()
        => [.. base.Repair(), .. GpuInterests.Repair(Kind, Adapter, a => Adapter = a)];
}

/// <summary>[S-11014] The adapter's load as a history, absent with the reading when the adapter is gone.</summary>
internal sealed class GpuHistoryMeter : GpuPart
{
    public override WidgetId Kind => new("gpuHistory");

    public override bool Sized => true;

    public override MeterAtom Atom => MeterAtom.History;

    public override MeterLook Look(WidgetConfig config) => MeterLook.History;
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(GpuHistoryConfig))]
internal sealed partial class GpuHistoryJsonContext : JsonSerializerContext;
