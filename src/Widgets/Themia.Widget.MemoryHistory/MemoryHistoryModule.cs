using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Themia.App.Services;
using Themia.App.Services.SystemData;
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
using Themia.Widget.MemoryHistory;

[assembly: ThemiaWidget("memoryHistory", typeof(MemoryHistoryModule))]

namespace Themia.Widget.MemoryHistory;

/// <summary>[S-11013] Memory in use over time: its history alone, edge to edge. SPEC.md beside this file.</summary>
public sealed class MemoryHistoryModule : IWidgetModule
{
    /// <summary>[S-11013] A click on the card opens what the reading's opens.</summary>
    internal static CardOpen? Opens => CardOpen.TaskManager;

    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("memoryHistory"),
        ConfigType = typeof(MemoryHistoryConfig),
        CreateDefault = () => new MemoryHistoryConfig(),
        Json = MemoryHistoryJsonContext.Default,
        Keywords = ["ram", "memory", "graph", "chart", "history", "task manager"],
        Label = MemoryHistoryStr.MemoryHistory_Label.Local(),
        Description = MemoryHistoryStr.MemoryHistory_Desc.Local(),
        Glyph = Glyphs.Memory,
        Sizing = _ => MeterSizing.History,
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader | WidgetTraits.StartsHeaderless,
        Add = new(AddFolderId.MemoryReading, 0, 1),
    };

    /// <summary>[S-11013] Its one setting: how much history the chart spans ([S-1121]).</summary>
    public void MenuLines(WidgetMenuLines menu, WidgetConfig config)
        => menu.Lines.Add(AtomMenuLines.Span(menu, (MemoryHistoryConfig)config));

    public FrameworkElement CreateView(IWidgetHost host) => new MeterView(host, new MemoryHistoryMeter());
}

/// <summary>[S-11013] What one memoryHistory remembers: its span.</summary>
public sealed class MemoryHistoryConfig : HistoryConfig, IReadsSystem
{
    public MemoryHistoryConfig()
        : base(graphSpanSeconds: 60)
    {
    }

    /// <summary>[S-2513] The reading's own series, so the two agree to the point.</summary>
    public SystemInterests SystemWants(string systemRoot) => SystemInterests.Chart(SystemSource.Memory);

    public override WidgetConfig Clone()
    {
        var copy = new MemoryHistoryConfig();
        CopyTo(copy);
        return copy;
    }
}

/// <summary>[S-11013] The history in its pressure's role ([S-1123]).</summary>
internal sealed class MemoryHistoryMeter : MeterPart
{
    public override WidgetId Kind => new("memoryHistory");

    public override bool Sized => true;

    public override MeterAtom Atom => MeterAtom.History;

    public override MeterLook Look(WidgetConfig config) => MeterLook.History;

    public override CardOpen? Opens(WidgetConfig config) => MemoryHistoryModule.Opens;

    public override MeterPressure PressureOf(MeterReading reading)
        => MeterPressures.For(PressureScale.Usage, reading.Available ? reading.Fraction : null);
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(MemoryHistoryConfig))]
internal sealed partial class MemoryHistoryJsonContext : JsonSerializerContext;
