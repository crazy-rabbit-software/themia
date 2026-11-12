using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Themia.App.Shell;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Themia.Widget.WeatherFacts;

[assembly: ThemiaWidget("weatherFacts", typeof(WeatherFactsModule))]

namespace Themia.Widget.WeatherFacts;

/// <summary>[S-11105] The weather's readings where the user says they are, as fact rows they pick. SPEC.md beside this file.</summary>
public sealed class WeatherFactsModule : IWidgetModule
{
    /// <summary>[S-2035] A click on the card opens MSN Weather's forecast for its place, as the reading's does; nothing with no place.</summary>
    internal static CardOpen? Opens(WeatherConfig config, TemperatureUnit unit, Locale locale) => WeatherPages.Forecast(config, unit, locale);

    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("weatherFacts"),
        ConfigType = typeof(WeatherFactsConfig),
        CreateDefault = () => new WeatherFactsConfig(),
        Json = WeatherFactsJsonContext.Default,
        Keywords = ["humidity", "wind", "pressure", "uv", "feels like", "gusts", "precipitation", "weather"],
        Label = WeatherFactsStr.WeatherFacts_Label.Local(),
        Description = WeatherFactsStr.WeatherFacts_Desc.Local(),
        Glyph = Glyphs.Thermometer,
        Sizing = c => Sizing((WeatherFactsConfig)c),
        SizeSets = (c, size) => SkyFacts.SizeSets(c, size, ((WeatherFactsConfig)c).Facts, WeatherFactRules.All, (copy, facts) => ((WeatherFactsConfig)copy).Facts = facts),
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader,
        Add = new(AddFolderId.Weather, 0, 1),
    };

    /// <summary>[S-11106] One 28 row a picked fact under the header, 200..420 wide (a new one 240).</summary>
    internal static WidgetSizing Sizing(WeatherFactsConfig config) => SkyFacts.Sizing(config.Facts.Count, WeatherFactRules.All.Count);

    /// <summary>[S-11107] Every option is on the properties menu: the widget has no settings page.</summary>
    public void MenuLines(WidgetMenuLines menu, WidgetConfig config)
        => WeatherFactsMenu.Lines(menu, (WeatherFactsConfig)config);

    public FrameworkElement CreateView(IWidgetHost host) => new WeatherFactsView(host);
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(WeatherFactsConfig))]
internal sealed partial class WeatherFactsJsonContext : JsonSerializerContext;
