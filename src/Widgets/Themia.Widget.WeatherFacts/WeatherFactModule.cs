using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Themia.App.Shell;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;

namespace Themia.Widget.WeatherFacts;

/// <summary>
/// [S-13900] One kind of the weather facts family: one weather reading where the user says it is, its one fact row. Each
/// kind is a sealed module of its own (<see cref="WeatherFactKinds"/>); the descriptor, the menu and the view are
/// this family's. SPEC.md beside this file.
/// </summary>
public abstract class WeatherFactModule : IWidgetModule
{
    private readonly WeatherFactKindInfo _kind;

    protected WeatherFactModule(WeatherFactKind fact)
    {
        _kind = WeatherFactKinds.Of(fact);
        Descriptor = new()
        {
            Id = new(_kind.Id),
            ConfigType = _kind.ConfigType,
            CreateDefault = _kind.Create,
            Json = WeatherFactJsonContext.Default,
            Keywords = _kind.Keywords,
            Label = _kind.Label,
            Description = _kind.Description,
            Glyph = WeatherFactView.FactGlyph(fact),
            Sizing = _ => Sizing,
            Traits = WidgetTraits.HasItems,
            Add = new(AddFolderId.WeatherFacts, 0, (int)fact),
        };
    }

    /// <summary>[S-2035] A click on the card opens MSN Weather's forecast for its place, as the reading's does; nothing with no place.</summary>
    internal static CardOpen? Opens(WeatherConfig config, TemperatureUnit unit, Locale locale) => WeatherPages.Forecast(config, unit, locale);

    /// <summary>[S-13901] Its one fact row under the inset, over the inset; 200 wide at least, a new one 240 (relaxed, [S-12411]).</summary>
    internal static WidgetSizing Sizing { get; } = WidgetSizing.Range(
        new(SkyFacts.MinWidth, Height), new(SkyFacts.NaturalWidth, Height), new(SkyFacts.MaxWidth, Height));

    /// <summary>[S-13901] The content top, one 28 row and the bottom inset: 52.</summary>
    internal const double Height = FamilySpace.ContentTop + FamilyShape.Row1 + FamilySpace.Inset;

    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; }

    /// <summary>[S-13903] Every option is on the properties menu: the widget has no settings page.</summary>
    public void MenuLines(WidgetMenuLines menu, WidgetConfig config)
        => WeatherFactMenu.Lines(menu, (WeatherFactConfig)config);

    public FrameworkElement CreateView(IWidgetHost host) => new WeatherFactView(host, _kind.Fact);
}

/// <summary>[S-6903] The family's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(WeatherHighLowConfig))]
[JsonSerializable(typeof(WeatherFeelsLikeConfig))]
[JsonSerializable(typeof(WeatherHumidityConfig))]
[JsonSerializable(typeof(WeatherWindConfig))]
[JsonSerializable(typeof(WeatherPrecipitationConfig))]
[JsonSerializable(typeof(WeatherPressureConfig))]
[JsonSerializable(typeof(WeatherUvConfig))]
[JsonSerializable(typeof(WeatherCloudCoverConfig))]
[JsonSerializable(typeof(WeatherGustsConfig))]
[JsonSerializable(typeof(WeatherSunriseConfig))]
[JsonSerializable(typeof(WeatherSunsetConfig))]
internal sealed partial class WeatherFactJsonContext : JsonSerializerContext;
