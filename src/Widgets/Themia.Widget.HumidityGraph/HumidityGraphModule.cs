using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Themia.Widget.HumidityGraph;
using Themia.App.Shell;

[assembly: ThemiaWidget("humidityGraph", typeof(HumidityGraphModule))]

namespace Themia.Widget.HumidityGraph;

/// <summary>[S-10600] Relative humidity hour by hour at a place, on the weather graphs' one frame. SPEC.md beside this file.</summary>
public sealed class HumidityGraphModule : IWidgetModule
{
    /// <summary>Its glyph (Segoe Fluent Icons U+EB42, the droplet), the module's own.</summary>
    internal static readonly string Glyph = Glyphs.Droplet;

    /// <summary>[S-2037] A click on the card opens MSN Weather's forecast for its place ([S-2006]); nothing with no place.</summary>
    internal static CardOpen? Opens(WeatherConfig config, TemperatureUnit unit, Locale locale) => WeatherPages.Forecast(config, unit, locale);

    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("humidityGraph"),
        ConfigType = typeof(HumidityGraphConfig),
        CreateDefault = () => new HumidityGraphConfig(),
        Json = HumidityGraphJsonContext.Default,
        Keywords = ["humidity", "humid", "damp", "dry air", "dew", "graph", "chart", "hourly", "weather"],
        Label = HumidityGraphStr.HumidityGraph_Label.Local(),
        Description = HumidityGraphStr.HumidityGraph_Desc.Local(),
        Glyph = Glyph,
        Traits = WidgetTraits.None,
        Sizing = _ => HourGraphRules.Sizing(arrows: false),
        Add = new(AddFolderId.Weather, 2, 5),
    };

    /// <summary>[S-10604] The place, the time span and the source.</summary>
    public void MenuLines(WidgetMenuLines menu, WidgetConfig config)
    {
        ArgumentNullException.ThrowIfNull(menu);
        var graph = (HumidityGraphConfig)config;
        menu.Lines.AddRange(HourGraphLines.Lines(menu, graph));
        menu.Lines.Add(ItemMenuEntry.Separator);
        menu.Lines.Add(HourGraphLines.Source("Open-Meteo"));
    }

    public FrameworkElement CreateView(IWidgetHost host) => new HumidityGraphView(host);
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(HumidityGraphConfig))]
internal sealed partial class HumidityGraphJsonContext : JsonSerializerContext;
