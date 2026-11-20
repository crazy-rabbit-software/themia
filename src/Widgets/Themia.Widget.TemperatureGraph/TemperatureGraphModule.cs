using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Themia.Widget.TemperatureGraph;
using Themia.App.Shell;

[assembly: ThemiaWidget("temperatureGraph", typeof(TemperatureGraphModule))]

namespace Themia.Widget.TemperatureGraph;

/// <summary>[S-10100] The temperature hour by hour at a place, on the weather graphs' one frame. SPEC.md beside this file.</summary>
public sealed class TemperatureGraphModule : IWidgetModule
{
    /// <summary>Its glyph (Segoe Fluent Icons U+E9CA, the thermometer), the module's own.</summary>
    internal static readonly string Glyph = Glyphs.Thermometer;

    /// <summary>[S-2037] A click on the card opens MSN Weather's forecast for its place ([S-2006]); nothing with no place.</summary>
    internal static CardOpen? Opens(WeatherConfig config, TemperatureUnit unit, Locale locale) => WeatherPages.Forecast(config, unit, locale);

    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("temperatureGraph"),
        ConfigType = typeof(TemperatureGraphConfig),
        CreateDefault = () => new TemperatureGraphConfig(),
        Json = TemperatureGraphJsonContext.Default,
        Keywords = ["temperature", "graph", "chart", "hourly", "feels like", "heat", "weather"],
        Label = TemperatureGraphStr.TemperatureGraph_Label.Local(),
        Description = TemperatureGraphStr.TemperatureGraph_Desc.Local(),
        Glyph = Glyph,
        Traits = WidgetTraits.None,
        Sizing = _ => HourGraphRules.Sizing(arrows: false),
        Add = new(AddFolderId.Weather, 2, 0),
    };

    /// <summary>[S-10104] The place, the time span, the feels-like line and the source.</summary>
    public void MenuLines(WidgetMenuLines menu, WidgetConfig config)
    {
        ArgumentNullException.ThrowIfNull(menu);
        var graph = (TemperatureGraphConfig)config;
        menu.Lines.AddRange(HourGraphLines.Lines(menu, graph));
        menu.Lines.Add(menu.Toggle(Glyph, TemperatureGraphStr.TemperatureGraph_FeelsLike.Text(), graph.ShowFeelsLike, on => graph.ShowFeelsLike = on));
        menu.Lines.Add(ItemMenuEntry.Separator);
        menu.Lines.Add(HourGraphLines.Source("Open-Meteo"));
    }

    public FrameworkElement CreateView(IWidgetHost host) => new TemperatureGraphView(host);
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(TemperatureGraphConfig))]
internal sealed partial class TemperatureGraphJsonContext : JsonSerializerContext;
