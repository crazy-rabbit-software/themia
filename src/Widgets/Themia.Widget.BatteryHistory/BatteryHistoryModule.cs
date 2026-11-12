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
using Themia.Widget.BatteryHistory;

[assembly: ThemiaWidget("batteryHistory", typeof(BatteryHistoryModule))]

namespace Themia.Widget.BatteryHistory;

/// <summary>[S-11024] The battery's charge over time: its history alone, edge to edge. SPEC.md beside this file.</summary>
public sealed class BatteryHistoryModule : IWidgetModule
{
    /// <summary>[S-11024] A click on the card opens what the reading's opens.</summary>
    internal static CardOpen? Opens => CardOpen.Settings("batterysaver");

    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("batteryHistory"),
        ConfigType = typeof(BatteryHistoryConfig),
        CreateDefault = () => new BatteryHistoryConfig(),
        Json = BatteryHistoryJsonContext.Default,
        Keywords = ["battery", "charge", "laptop", "graph", "chart", "history"],
        Label = BatteryHistoryStr.BatteryHistory_Label.Local(),
        Description = BatteryHistoryStr.BatteryHistory_Desc.Local(),
        Glyph = Glyphs.Battery,
        Sizing = _ => MeterSizing.History,
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader | WidgetTraits.StartsHeaderless,
        Add = new(AddFolderId.BatteryReading, 0, 1),
    };

    /// <summary>[S-11024] Its one setting: how much history the chart spans ([S-1121]).</summary>
    public void MenuLines(WidgetMenuLines menu, WidgetConfig config)
        => menu.Lines.Add(AtomMenuLines.Span(menu, (BatteryHistoryConfig)config));

    public FrameworkElement CreateView(IWidgetHost host) => new MeterView(host, new BatteryHistoryMeter());
}

/// <summary>[S-11024] What one batteryHistory remembers: its span.</summary>
public sealed class BatteryHistoryConfig : HistoryConfig, IReadsSystem
{
    public BatteryHistoryConfig()
        : base(graphSpanSeconds: 900)
    {
    }

    /// <summary>[S-2513] The reading's own series, so the two agree to the point.</summary>
    public SystemInterests SystemWants(string systemRoot) => SystemInterests.Chart(SystemSource.Battery);

    public override WidgetConfig Clone()
    {
        var copy = new BatteryHistoryConfig();
        CopyTo(copy);
        return copy;
    }
}

/// <summary>[S-11024] The history in its pressure's role ([S-1123]).</summary>
internal sealed class BatteryHistoryMeter : MeterPart
{
    public override WidgetId Kind => new("batteryHistory");

    public override MeterAtom Atom => MeterAtom.History;

    public override MeterLook Look(WidgetConfig config) => MeterLook.History;

    public override CardOpen? Opens(WidgetConfig config) => BatteryHistoryModule.Opens;

    public override MeterPressure PressureOf(MeterReading reading)
        => MeterPressures.For(PressureScale.Charge, reading.Available ? reading.Fraction : null, reading.Charging);
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(BatteryHistoryConfig))]
internal sealed partial class BatteryHistoryJsonContext : JsonSerializerContext;
