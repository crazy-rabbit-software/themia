using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Themia.Widget.UvGraph;
using Themia.App.Shell;

[assembly: ThemiaWidget("uvGraph", typeof(UvGraphModule))]

namespace Themia.Widget.UvGraph;

/// <summary>[S-10300] The UV index hour by hour at a place, with the day's daylight, on the weather graphs' one frame. SPEC.md beside this file.</summary>
public sealed class UvGraphModule : IWidgetModule
{
    /// <summary>Its glyph (Segoe Fluent Icons U+E706, the sun), the module's own.</summary>
    internal static readonly string Glyph = Glyphs.Sun;

    /// <summary>[S-2037] A click on the card opens MSN Weather's forecast for its place ([S-2006]); nothing with no place.</summary>
    internal static CardOpen? Opens(WeatherConfig config, TemperatureUnit unit, Locale locale) => WeatherPages.Forecast(config, unit, locale);

    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("uvGraph"),
        ConfigType = typeof(UvGraphConfig),
        CreateDefault = () => new UvGraphConfig(),
        Json = UvGraphJsonContext.Default,
        Keywords = ["uv", "ultraviolet", "sun", "sunburn", "index", "graph", "chart", "hourly", "weather"],
        Label = UvGraphStr.UvGraph_Label.Local(),
        Description = UvGraphStr.UvGraph_Desc.Local(),
        Glyph = Glyph,
        Traits = WidgetTraits.None,
        Sizing = _ => HourGraphRules.Sizing(arrows: false),
        Add = new(AddFolderId.Weather, 2, 2),
    };

    /// <summary>[S-10304] The place, the time span and the source.</summary>
    public void MenuLines(WidgetMenuLines menu, WidgetConfig config)
    {
        ArgumentNullException.ThrowIfNull(menu);
        var graph = (UvGraphConfig)config;
        menu.Lines.AddRange(HourGraphLines.Lines(menu, graph));
        menu.Lines.Add(ItemMenuEntry.Separator);
        menu.Lines.Add(HourGraphLines.Source("Open-Meteo"));
    }

    public FrameworkElement CreateView(IWidgetHost host) => new UvGraphView(host);
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(UvGraphConfig))]
internal sealed partial class UvGraphJsonContext : JsonSerializerContext;
