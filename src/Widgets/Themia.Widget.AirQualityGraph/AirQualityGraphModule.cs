using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Themia.Widget.AirQualityGraph;
using Themia.App.Shell;

[assembly: ThemiaWidget("airQualityGraph", typeof(AirQualityGraphModule))]

namespace Themia.Widget.AirQualityGraph;

/// <summary>[S-10400] The air hour by hour for the next days at a place, on the weather graphs' one frame. SPEC.md beside this file.</summary>
public sealed class AirQualityGraphModule : IWidgetModule
{
    /// <summary>Its glyph (Segoe Fluent Icons U+EC0A, the air widget's), the module's own.</summary>
    internal static readonly string Glyph = Glyphs.EnergySaver;

    /// <summary>[S-2037] A click on the card opens MSN Weather's forecast for its place ([S-2006]); nothing with no place.</summary>
    internal static CardOpen? Opens(WeatherConfig config, TemperatureUnit unit, Locale locale) => WeatherPages.Forecast(config, unit, locale);

    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("airQualityGraph"),
        ConfigType = typeof(AirQualityGraphConfig),
        CreateDefault = () => new AirQualityGraphConfig(),
        Json = AirQualityGraphJsonContext.Default,
        Keywords = ["air", "air quality", "aqi", "pollution", "smog", "pm2.5", "pm10", "ozone", "no2", "graph", "chart", "weather"],
        Label = AirQualityGraphStr.AirQualityGraph_Label.Local(),
        Description = AirQualityGraphStr.AirQualityGraph_Desc.Local(),
        Glyph = Glyph,
        Traits = WidgetTraits.None,
        Sizing = _ => HourGraphRules.Sizing(arrows: false),
        Add = new(AddFolderId.Weather, 2, 3),
    };

    /// <summary>[S-10404] The place, the time span, what it shows, the index's scale (for the index) and the source.</summary>
    public void MenuLines(WidgetMenuLines menu, WidgetConfig config)
    {
        ArgumentNullException.ThrowIfNull(menu);
        var graph = (AirQualityGraphConfig)config;
        menu.Lines.AddRange(HourGraphLines.Lines(menu, graph));

        (AirMeasure, string)[] measures =
        [
            (AirMeasure.Index, AirQualityGraphStr.AirQualityGraph_Measure_Index.Text()),
            (AirMeasure.Pm25, "PM2.5"),
            (AirMeasure.Pm10, "PM10"),
            (AirMeasure.Ozone, "O₃"),
            (AirMeasure.NitrogenDioxide, "NO₂"),
        ];
        menu.Lines.Add(menu.Choice(Glyph, AirQualityGraphStr.AirQualityGraph_Measure.Text(), measures, graph.Measure, value => graph.Measure = value));

        if (graph.Measure == AirMeasure.Index)
        {
            (AirQualityScale, string)[] scales =
            [
                (AirQualityScale.Auto, AirQualityGraphStr.AirQualityGraph_Scale_Auto.Text()),
                (AirQualityScale.European, AirQualityGraphStr.AirQualityGraph_Scale_Eu.Text()),
                (AirQualityScale.Us, AirQualityGraphStr.AirQualityGraph_Scale_Us.Text()),
            ];
            menu.Lines.Add(menu.Choice(null, AirQualityGraphStr.AirQualityGraph_Scale.Text(), scales, graph.Scale, value => graph.Scale = value));
        }

        menu.Lines.Add(ItemMenuEntry.Separator);
        menu.Lines.Add(HourGraphLines.Source("Copernicus Atmosphere Monitoring Service (CAMS) · Open-Meteo"));
    }

    public FrameworkElement CreateView(IWidgetHost host) => new AirQualityGraphView(host);
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(AirQualityGraphConfig))]
internal sealed partial class AirQualityGraphJsonContext : JsonSerializerContext;
