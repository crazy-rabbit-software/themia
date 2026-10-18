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
using Themia.Widget.BatteryDetails;

[assembly: ThemiaWidget("batteryDetails", typeof(BatteryDetailsModule))]

namespace Themia.Widget.BatteryDetails;

/// <summary>[S-2423] What the battery itself reports: the charge and status, time left, health, cycles, voltage and rate. SPEC.md beside this file.</summary>
public sealed class BatteryDetailsModule : IWidgetModule
{
    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("batteryDetails"),
        ConfigType = typeof(BatteryDetailsConfig),
        CreateDefault = () => new BatteryDetailsConfig(),
        Json = BatteryDetailsJsonContext.Default,
        Keywords = ["power", "battery health", "wear", "cycles", "voltage", "laptop", "time remaining"],
        Label = BatteryDetailsStr.Details_Catalog_Battery_Label.Local(),
        Description = BatteryDetailsStr.Details_Catalog_Battery_Desc.Local(),
        Glyph = Glyphs.Battery,
        DefaultWidth = 280,
        DefaultHeight = 240,
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader,
        Add = new(AddFolderId.BatteryReading, 0, 1),
    };

    public FrameworkElement CreateView(IWidgetHost host) => new DetailView(host, new BatteryDetailPart());
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(BatteryDetailsConfig))]
internal sealed partial class BatteryDetailsJsonContext : JsonSerializerContext;
