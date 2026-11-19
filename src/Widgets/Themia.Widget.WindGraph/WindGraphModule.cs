using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Themia.App.Shell;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Themia.Widget.WindGraph;

[assembly: ThemiaWidget("windGraph", typeof(WindGraphModule))]

namespace Themia.Widget.WindGraph;

/// <summary>[S-10500] Wind and its gusts hour by hour at a place, with where it blows from, on the weather graphs' one frame. SPEC.md beside this file.</summary>
public sealed class WindGraphModule : IWidgetModule
{
    /// <summary>
    /// Its glyph: the SDK's wind lines (<see cref="Glyphs.Wind"/>, checked against the icon font). U+E9F2, its own until
    /// 2026-11-19, is in neither Segoe Fluent Icons nor Segoe MDL2 Assets: it drew the font's empty box.
    /// </summary>
    internal static readonly string Glyph = Glyphs.Wind;

    /// <summary>[S-2037] A click on the card opens MSN Weather's forecast for its place ([S-2006]); nothing with no place.</summary>
    internal static CardOpen? Opens(WeatherConfig config, TemperatureUnit unit, Locale locale) => WeatherPages.Forecast(config, unit, locale);

    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("windGraph"),
        ConfigType = typeof(WindGraphConfig),
        CreateDefault = () => new WindGraphConfig(),
        Json = WindGraphJsonContext.Default,
        Keywords = ["wind", "gusts", "breeze", "storm", "direction", "graph", "chart", "hourly", "weather"],
        Label = WindGraphStr.WindGraph_Label.Local(),
        Description = WindGraphStr.WindGraph_Desc.Local(),
        Glyph = Glyph,
        Traits = WidgetTraits.None,
        Sizing = _ => HourGraphRules.Sizing(arrows: true),
        Add = new(AddFolderId.Weather, 2, 4),
    };

    /// <summary>[S-10504] The place, the time span, its own switch and the source.</summary>
    public void MenuLines(WidgetMenuLines menu, WidgetConfig config)
    {
        ArgumentNullException.ThrowIfNull(menu);
        var graph = (WindGraphConfig)config;
        menu.Lines.AddRange(HourGraphLines.Lines(menu, graph));
        menu.Lines.Add(menu.Toggle(Glyph, WindGraphStr.WindGraph_Gusts.Text(), graph.ShowGusts, on => graph.ShowGusts = on));
        menu.Lines.Add(ItemMenuEntry.Separator);
        menu.Lines.Add(HourGraphLines.Source("Open-Meteo"));
    }

    public FrameworkElement CreateView(IWidgetHost host) => new WindGraphView(host);
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(WindGraphConfig))]
internal sealed partial class WindGraphJsonContext : JsonSerializerContext;
