using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Themia.Widget.PressureGraph;

[assembly: ThemiaWidget("pressureGraph", typeof(PressureGraphModule))]

namespace Themia.Widget.PressureGraph;

/// <summary>[S-10700] Air pressure at sea level hour by hour at a place, on the weather graphs' one frame. SPEC.md beside this file.</summary>
public sealed class PressureGraphModule : IWidgetModule
{
    /// <summary>Its glyph (Segoe Fluent Icons U+EC4A, the gauge), the module's own.</summary>
    internal static readonly string Glyph = char.ConvertFromUtf32(0xEC4A);

    /// <summary>[S-2037] A click on the card opens MSN Weather's forecast for its place ([S-2006]); nothing with no place.</summary>
    internal static CardOpen? Opens(WeatherConfig config, TemperatureUnit unit, Locale locale) => WeatherPages.Forecast(config, unit, locale);

    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("pressureGraph"),
        ConfigType = typeof(PressureGraphConfig),
        CreateDefault = () => new PressureGraphConfig(),
        Json = PressureGraphJsonContext.Default,
        Keywords = ["pressure", "barometer", "air pressure", "hpa", "graph", "chart", "hourly", "weather"],
        Label = PressureGraphStr.PressureGraph_Label.Local(),
        Description = PressureGraphStr.PressureGraph_Desc.Local(),
        Glyph = Glyph,
        Traits = WidgetTraits.None,
        Sizing = _ => HourGraphRules.Sizing(arrows: false),
        Add = new(AddFolderId.Weather, 2, 6),
    };

    /// <summary>[S-10704] The place, the time span and the source.</summary>
    public void MenuLines(WidgetMenuLines menu, WidgetConfig config)
    {
        ArgumentNullException.ThrowIfNull(menu);
        var graph = (PressureGraphConfig)config;
        menu.Lines.AddRange(HourGraphLines.Lines(menu, graph));
        menu.Lines.Add(ItemMenuEntry.Separator);
        menu.Lines.Add(HourGraphLines.Source("Open-Meteo"));
    }

    public FrameworkElement CreateView(IWidgetHost host) => new PressureGraphView(host);
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(PressureGraphConfig))]
internal sealed partial class PressureGraphJsonContext : JsonSerializerContext;
