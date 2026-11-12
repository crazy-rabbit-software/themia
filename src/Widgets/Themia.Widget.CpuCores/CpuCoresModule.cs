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
using Themia.Widget.CpuCores;

[assembly: ThemiaWidget("cpuCores", typeof(CpuCoresModule))]

namespace Themia.Widget.CpuCores;

/// <summary>[S-11011] Each logical processor's load: a bar, a ring or a graph per core. SPEC.md beside this file.</summary>
public sealed class CpuCoresModule : IWidgetModule
{
    /// <summary>[S-11011] A click on the card opens Task Manager, as the reading's does.</summary>
    internal static CardOpen? Opens => CardOpen.TaskManager;

    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("cpuCores"),
        ConfigType = typeof(CpuCoresConfig),
        CreateDefault = () => new CpuCoresConfig(),
        Json = CpuCoresJsonContext.Default,
        Keywords = ["processor", "cores", "threads", "per core", "load", "task manager"],
        Label = CpuCoresStr.CpuCores_Label.Local(),
        Description = CpuCoresStr.CpuCores_Desc.Local(),
        Glyph = Glyphs.Cpu,
        Sizing = c => ((CpuCoresConfig)c).Style switch
        {
            CpuCoresStyle.Grid => MeterSizing.Tiles,
            CpuCoresStyle.Graphs => MeterSizing.Graphs,
            _ => MeterSizing.Columns,
        },
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader | WidgetTraits.StartsHeaderless,
        Add = new(AddFolderId.Processor, 0, 2),
    };

    /// <summary>[S-11011] The Graphs style's span ([S-1121]), under Style; the bars and the rings draw none.</summary>
    public void MenuLines(WidgetMenuLines menu, WidgetConfig config)
    {
        if (config is CpuCoresConfig { Style: CpuCoresStyle.Graphs } cores)
        {
            menu.StyleLines.Add(AtomMenuLines.Span(menu, cores));
        }
    }

    public FrameworkElement CreateView(IWidgetHost host) => new MeterView(host, new CpuCoresMeter());
}

/// <summary>[S-11011] The cores' drawings ([S-6000]).</summary>
[JsonConverter(typeof(StyleJsonConverter<CpuCoresStyle>))]
public enum CpuCoresStyle
{
    /// <summary>[S-6312] A column per logical processor.</summary>
    Bars = 0,

    /// <summary>[S-6313] A ring per logical processor.</summary>
    Grid = 1,

    /// <summary>[S-6317] A cell per logical processor, its own history and load.</summary>
    Graphs = 2,
}

/// <summary>[S-11011] What one CPU cores widget remembers: its drawing and, for the graphs, their span.</summary>
public sealed class CpuCoresConfig : HistoryConfig, IStyled, IReadsSystem
{
    public CpuCoresConfig()
        : base(graphSpanSeconds: 60)
    {
    }

    /// <summary>[S-11011] The cores' drawing.</summary>
    public CpuCoresStyle Style { get; set; } = CpuCoresStyle.Bars;

    private static readonly StyleOption[] CoreStyles =
    [
        StyleOption.Of(CpuCoresStyle.Bars, CpuCoresStr.Styles_Cpu_Cores.Local(), 0xE9E9),
        StyleOption.Of(CpuCoresStyle.Grid, CpuCoresStr.Styles_Cpu_Grid.Local(), 0xF0E2),
        StyleOption.Of(CpuCoresStyle.Graphs, CpuCoresStr.Styles_Cpu_CoreGraphs.Local(), 0xF246),
    ];

    IReadOnlyList<StyleOption> IStyled.Styles => CoreStyles;

    int IStyled.StyleValue
    {
        get => (int)Style;
        set => Style = (CpuCoresStyle)value;
    }

    /// <summary>[S-6573] Every core is an item, a track (a bar, a ring, a graph's cell).</summary>
    protected override bool PaintsItems => true;

    /// <summary>
    /// [S-6310] Each processor's load beside the total's series; [S-6318] the graphs keep each
    /// processor's history for their span.
    /// </summary>
    public SystemInterests SystemWants(string systemRoot)
        => new([FeedKey.Create(SystemSource.Cpu), FeedKey.Create(SystemSource.CpuCores)], [new SeriesInterest(FeedKey.Create(SystemSource.Cpu), string.Empty)])
        {
            CoreHistorySeconds = Style == CpuCoresStyle.Graphs ? Math.Max(1, GraphSpanSeconds) : 0,
        };

    public override WidgetConfig Clone()
    {
        var copy = new CpuCoresConfig { Style = Style };
        CopyTo(copy);
        return copy;
    }
}

/// <summary>[S-11011] The cores as the hub reads them, each in its own usage pressure's role ([S-6301]).</summary>
internal sealed class CpuCoresMeter : MeterPart
{
    public override WidgetId Kind => new("cpuCores");

    public override bool Sized => true;

    public override MeterAtom Atom => MeterAtom.Items;

    public override CardOpen? Opens(WidgetConfig config) => CpuCoresModule.Opens;

    public override MeterPressure PressureOf(MeterReading reading)
        => MeterPressures.For(PressureScale.Usage, reading.Available ? reading.Fraction : null);

    public override MeterLook Look(WidgetConfig config) => (config as CpuCoresConfig)?.Style switch
    {
        CpuCoresStyle.Grid => MeterLook.Tiles,
        CpuCoresStyle.Graphs => MeterLook.Graphs,
        _ => MeterLook.Columns,
    };

    /// <summary>[S-6310] Each logical processor, from the hub's one per-core read; [S-6318] with each one's history.</summary>
    public override MeterItemsWatch? WatchItems(ISystemFeeds hub, Action<IReadOnlyList<MeterItem>> onItems)
        => MeterItemsWatch.Of(
            hub,
            Sources.CpuCores,
            cores => [.. cores.Loads.Select(load => new MeterItem(load, MeterPressures.For(PressureScale.Usage, load)))],
            onItems,
            static (cores, core, max) => cores.History.Newest(core, max),
            static cores => cores.History.Added);
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(CpuCoresConfig))]
internal sealed partial class CpuCoresJsonContext : JsonSerializerContext;
