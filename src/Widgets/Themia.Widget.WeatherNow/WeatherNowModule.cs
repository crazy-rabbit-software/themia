using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Themia.App.Shell;
using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Themia.Widget.WeatherNow;

[assembly: ThemiaWidget("weatherNow", typeof(WeatherNowModule))]

namespace Themia.Widget.WeatherNow;

/// <summary>[S-723] The weather where the user says they are: the temperature as the hero readout, then its readings as the tile grows. SPEC.md beside this file.</summary>
public sealed class WeatherNowModule : IWidgetModule
{
    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("weatherNow"),
        ConfigType = typeof(WeatherNowConfig),
        CreateDefault = () => new WeatherNowConfig(),
        Json = WeatherNowJsonContext.Default,
        Keywords = ["temperature", "forecast", "rain", "wind", "humidity", "climate", "degrees"],
        Label = WeatherNowStr.Widget_WeatherCurrent_Label.Local(),
        Description = WeatherNowStr.WidgetsA_Catalog_Weather_Description.Local(),
        Glyph = Glyphs.Weather,
        DefaultWidth = 260,
        DefaultHeight = 200,
        Traits = WidgetTraits.CanHaveHeader,
        Add = new(AddFolderId.Weather, 0, 0, Popular: 1),
    };

    public void SettingsRows(WidgetSettingsRows rows, WidgetConfig config)
        => WeatherNowSettings.Rows(rows.Rows, (WeatherNowConfig)config, rows.Save);

    public FrameworkElement CreateView(IWidgetHost host) => new WeatherNowView(host);
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(WeatherNowConfig))]
internal sealed partial class WeatherNowJsonContext : JsonSerializerContext;
