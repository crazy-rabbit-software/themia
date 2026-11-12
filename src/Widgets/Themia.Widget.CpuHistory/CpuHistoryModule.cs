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
using Themia.Widget.CpuHistory;

[assembly: ThemiaWidget("cpuHistory", typeof(CpuHistoryModule))]

namespace Themia.Widget.CpuHistory;

/// <summary>[S-11010] The processor's load over time: its history alone, edge to edge. SPEC.md beside this file.</summary>
public sealed class CpuHistoryModule : IWidgetModule
{
    /// <summary>[S-11010] A click on the card opens Task Manager, as the reading's does.</summary>
    internal static CardOpen? Opens => CardOpen.TaskManager;

    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("cpuHistory"),
        ConfigType = typeof(CpuHistoryConfig),
        CreateDefault = () => new CpuHistoryConfig(),
        Json = CpuHistoryJsonContext.Default,
        Keywords = ["processor", "load", "graph", "chart", "history", "task manager"],
        Label = CpuHistoryStr.CpuHistory_Label.Local(),
        Description = CpuHistoryStr.CpuHistory_Desc.Local(),
        Glyph = Glyphs.Cpu,
        Sizing = _ => MeterSizing.History,
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader | WidgetTraits.StartsHeaderless,
        Add = new(AddFolderId.Processor, 0, 1),
    };

    /// <summary>[S-11010] Its one setting: how much history the chart spans ([S-1121]).</summary>
    public void MenuLines(WidgetMenuLines menu, WidgetConfig config)
        => menu.Lines.Add(AtomMenuLines.Span(menu, (CpuHistoryConfig)config));

    public FrameworkElement CreateView(IWidgetHost host) => new MeterView(host, new CpuHistoryMeter());
}

/// <summary>[S-11010] What one CPU history remembers: its span (a minute, as Task Manager shows).</summary>
public sealed class CpuHistoryConfig : HistoryConfig, IReadsSystem
{
    public CpuHistoryConfig()
        : base(graphSpanSeconds: 60)
    {
    }

    /// <summary>[S-2513] The load's series, the reading's own, so the two agree to the point.</summary>
    public SystemInterests SystemWants(string systemRoot) => SystemInterests.Chart(SystemSource.Cpu);

    public override WidgetConfig Clone()
    {
        var copy = new CpuHistoryConfig();
        CopyTo(copy);
        return copy;
    }
}

/// <summary>[S-11010] The load's history in the usage pressure's role ([S-1123]).</summary>
internal sealed class CpuHistoryMeter : MeterPart
{
    public override WidgetId Kind => new("cpuHistory");

    public override bool Sized => true;

    public override MeterAtom Atom => MeterAtom.History;

    public override MeterLook Look(WidgetConfig config) => MeterLook.History;

    public override CardOpen? Opens(WidgetConfig config) => CpuHistoryModule.Opens;

    public override MeterPressure PressureOf(MeterReading reading)
        => MeterPressures.For(PressureScale.Usage, reading.Available ? reading.Fraction : null);
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(CpuHistoryConfig))]
internal sealed partial class CpuHistoryJsonContext : JsonSerializerContext;
