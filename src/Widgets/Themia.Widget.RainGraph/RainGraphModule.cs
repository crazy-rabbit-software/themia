using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Themia.Widget.RainGraph;
using Themia.App.Shell;

[assembly: ThemiaWidget("rainGraph", typeof(RainGraphModule))]

namespace Themia.Widget.RainGraph;

/// <summary>[S-10200] Rain hour by hour (or the next two hours' quarters) at a place, on the weather graphs' one frame. SPEC.md beside this file.</summary>
public sealed class RainGraphModule : IWidgetModule
{
    /// <summary>Its glyph (Segoe Fluent Icons U+EA91, the rain), the module's own.</summary>
    internal static readonly string Glyph = Glyphs.Rain;

    /// <summary>[S-2037] A click on the card opens MSN Weather's forecast for its place ([S-2006]); nothing with no place.</summary>
    internal static CardOpen? Opens(WeatherConfig config, TemperatureUnit unit, Locale locale) => WeatherPages.Forecast(config, unit, locale);

    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("rainGraph"),
        ConfigType = typeof(RainGraphConfig),
        CreateDefault = () => new RainGraphConfig(),
        Json = RainGraphJsonContext.Default,
        Keywords = ["rain", "precipitation", "graph", "chart", "hourly", "chance", "nowcast", "umbrella", "weather"],
        Label = RainGraphStr.RainGraph_Label.Local(),
        Description = RainGraphStr.RainGraph_Desc.Local(),
        Glyph = Glyph,
        Traits = WidgetTraits.None,
        Sizing = _ => HourGraphRules.Sizing(arrows: false),
        Add = new(AddFolderId.Weather, 2, 1),
    };

    /// <summary>[S-10204] The place, the time span (the hours look), the chance line and the source.</summary>
    public void MenuLines(WidgetMenuLines menu, WidgetConfig config)
    {
        ArgumentNullException.ThrowIfNull(menu);
        var graph = (RainGraphConfig)config;
        var lines = HourGraphLines.Lines(menu, graph).ToList();

        // The nowcast's window is the rain widget's two hours: no span to choose.
        if (graph.Style == RainGraphStyle.Nowcast)
        {
            lines.RemoveAt(lines.Count - 1);
            lines.RemoveAt(lines.Count - 1);
        }

        menu.Lines.AddRange(lines);
        if (graph.Style == RainGraphStyle.Hours)
        {
            menu.Lines.Add(menu.Toggle(Glyph, RainGraphStr.RainGraph_Chance.Text(), graph.ShowChance, on => graph.ShowChance = on));
        }

        menu.Lines.Add(ItemMenuEntry.Separator);
        menu.Lines.Add(HourGraphLines.Source("Open-Meteo"));
    }

    public FrameworkElement CreateView(IWidgetHost host) => new RainGraphView(host);
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(RainGraphConfig))]
internal sealed partial class RainGraphJsonContext : JsonSerializerContext;
