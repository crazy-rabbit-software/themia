using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Themia.App.Shell;
using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.Core.Links;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Themia.Widget.WeatherForecast;

[assembly: ThemiaWidget("weatherForecast", typeof(WeatherForecastModule))]

namespace Themia.Widget.WeatherForecast;

/// <summary>[S-724] The forecast curve: the next few days' highs and lows at a place, from open-meteo. SPEC.md beside this file.</summary>
public sealed class WeatherForecastModule : IWidgetModule
{
    /// <summary>[S-2036] A click on the card opens MSN Weather's forecast for its place ([S-2006]); nothing with no place.</summary>
    internal static CardOpen? Opens(WeatherConfig config, TemperatureUnit unit, Locale locale) => WeatherPages.Forecast(config, unit, locale);

    /// <summary>Its glyph, the module's own (U+E9CA, the weather set's thermometer, checked against the font, [S-652]).</summary>
    internal static readonly string Glyph = Glyphs.Thermometer;

    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("weatherForecast"),
        ConfigType = typeof(WeatherForecastConfig),
        CreateDefault = () => new WeatherForecastConfig(),
        Json = WeatherForecastJsonContext.Default,
        Keywords = ["weather", "forecast", "temperature", "curve", "graph", "chart", "rain", "week", "outlook", "climate", "degrees"],
        Label = WeatherForecastStr.ForecastCurve_Label.Local(),
        Description = WeatherForecastStr.ForecastCurve_Desc.Local(),
        Glyph = Glyph,
        Sizing = c => ForecastCurve.Sizing((WeatherForecastConfig)c),
        Traits = WidgetTraits.HasItems,
        Links = LinkUse.Of([LinkTopicId.HoveredDate], [LinkTopicId.HoveredDate, LinkTopicId.SelectedDate]),
        // [S-15540] Among the weather graphs (their run, after them); still one of Popular's handful.
        Add = new(AddFolderId.Weather, 2, 7),
    };

    /// <summary>[S-3719] Every option is on the selection's page: the widget has no settings page.</summary>
    public void MenuLines(WidgetMenuLines menu, WidgetConfig config)
        => WeatherForecastMenu.Lines(menu, (WeatherForecastConfig)config);

    public FrameworkElement CreateView(IWidgetHost host) => new WeatherForecastView(host);
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(WeatherForecastConfig))]
internal sealed partial class WeatherForecastJsonContext : JsonSerializerContext;
