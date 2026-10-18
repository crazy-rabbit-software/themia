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
using Themia.Widget.WeatherForecast;

[assembly: ThemiaWidget("weatherForecast", typeof(WeatherForecastModule))]

namespace Themia.Widget.WeatherForecast;

/// <summary>[S-724] The next few days at a place, from open-meteo: rows, columns, the temperature curve or the grid. SPEC.md beside this file.</summary>
public sealed class WeatherForecastModule : IWidgetModule
{
    /// <summary>Its glyph, the module's own (U+E9CA, the weather set's thermometer, checked against the font, [S-652]).</summary>
    internal static readonly string Glyph = char.ConvertFromUtf32(0xE9CA);

    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("weatherForecast"),
        ConfigType = typeof(WeatherForecastConfig),
        CreateDefault = () => new WeatherForecastConfig(),
        Json = WeatherForecastJsonContext.Default,
        Keywords = ["weather", "temperature", "rain", "week", "outlook", "climate", "degrees"],
        Label = WeatherForecastStr.WidgetsA_Catalog_Forecast_Label.Local(),
        Description = WeatherForecastStr.WidgetsA_Catalog_Forecast_Description.Local(),
        Glyph = Glyph,
        DefaultWidth = 380,
        DefaultHeight = 220,
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader,
        Add = new(AddFolderId.Weather, 0, 1, Popular: 2),
    };

    public void SettingsRows(WidgetSettingsRows rows, WidgetConfig config)
        => WeatherForecastSettings.Rows(rows.Rows, (WeatherForecastConfig)config, rows.Save);

    public FrameworkElement CreateView(IWidgetHost host) => new WeatherForecastView(host);
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(WeatherForecastConfig))]
internal sealed partial class WeatherForecastJsonContext : JsonSerializerContext;
