using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Themia.App.Shell;
using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.App.Widgets.Meters;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Themia.Widget.Battery;

[assembly: ThemiaWidget("battery", typeof(BatteryModule))]

namespace Themia.Widget.Battery;

/// <summary>[S-1180] The battery's charge: the reading with its glyph and its history, or a battery or a ring of the charge ([S-6272]). SPEC.md beside this file.</summary>
public sealed class BatteryModule : IWidgetModule
{
    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("battery"),
        ConfigType = typeof(BatteryConfig),
        CreateDefault = () => new BatteryConfig(),
        Json = BatteryJsonContext.Default,
        Keywords = ["power", "charge", "laptop", "percent", "energy"],
        Label = BatteryStr.Widget_Battery_Label.Local(),
        Description = BatteryStr.Shell_Meters_BatteryDescription.Local(),
        Glyph = Glyphs.Battery,
        DefaultWidth = 200,
        DefaultHeight = 150,
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader,
        Add = new(AddFolderId.BatteryReading, 0, 0),
    };

    /// <summary>[S-3719] Every option is on the properties menu (the chart span under Style, [S-1122]): the widget has no settings page.</summary>
    public void MenuLines(WidgetMenuLines menu, WidgetConfig config)
    {
        MeterMenuLines.Lines(menu, (BatteryConfig)config);
    }

    public FrameworkElement CreateView(IWidgetHost host) => new MeterView(host, new BatteryMeter());
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(BatteryConfig))]
internal sealed partial class BatteryJsonContext : JsonSerializerContext;
