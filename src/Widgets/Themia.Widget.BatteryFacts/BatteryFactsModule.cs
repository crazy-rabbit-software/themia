using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Themia.App.Shell;
using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.App.Widgets.Details;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Themia.Widget.BatteryFacts;

[assembly: ThemiaWidget("batteryFacts", typeof(BatteryFactsModule))]

namespace Themia.Widget.BatteryFacts;

/// <summary>[S-2423] What the battery itself reports: the charge and status, time left, health, cycles, voltage and rate. SPEC.md beside this file.</summary>
public sealed class BatteryFactsModule : IWidgetModule
{
    /// <summary>[S-2023] A click on the card opens Windows' Power & battery page.</summary>
    internal static CardOpen? Opens => CardOpen.Settings("batterysaver");

    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("batteryFacts"),
        ConfigType = typeof(BatteryFactsConfig),
        CreateDefault = () => new BatteryFactsConfig(),
        Json = BatteryFactsJsonContext.Default,
        Keywords = ["power", "battery health", "wear", "cycles", "voltage", "laptop", "time remaining"],
        Label = BatteryFactsStr.BatteryFacts_Label.Local(),
        Description = BatteryFactsStr.BatteryFacts_Desc.Local(),
        Glyph = Glyphs.Battery,
        Sizing = c => FactsRules.Sizing((FactsConfig)c),
        SizeSets = FactsRules.SizeSets,
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader,
        Add = new(AddFolderId.BatteryReading, 0, 2),
    };

    /// <summary>[S-11025] Its line: which facts, in what order ([S-11007]).</summary>
    public void MenuLines(WidgetMenuLines menu, WidgetConfig config)
        => menu.Lines.Add(AtomMenuLines.Facts(menu, (BatteryFactsConfig)config));

    public FrameworkElement CreateView(IWidgetHost host) => new DetailView(host, new BatteryFactsPart());
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(BatteryFactsConfig))]
internal sealed partial class BatteryFactsJsonContext : JsonSerializerContext;
