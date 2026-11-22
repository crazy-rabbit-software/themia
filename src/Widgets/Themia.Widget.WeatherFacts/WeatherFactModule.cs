using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Themia.App.Shell;
using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;

namespace Themia.Widget.WeatherFacts;

/// <summary>
/// [S-13900] [S-14604] One kind of the weather facts family: one weather reading where the user says it is, a single
/// metric (<see cref="MetricModule"/>: the one sizing, the one face). Each kind is a sealed module of its own
/// (<see cref="WeatherFactKinds"/>); the menu and the view are this family's. SPEC.md beside this file.
/// </summary>
public abstract class WeatherFactModule(WeatherFactKind fact)
    : MetricModule(WeatherFactKinds.Of(fact), WeatherFactJsonContext.Default, AddFolderId.WeatherFacts, (int)fact)
{
    /// <summary>[S-13903] The place's search ([S-3722]); the fact is its kind, never a setting.</summary>
    protected override void SourceLines(WidgetMenuLines menu, WidgetConfig config)
    {
        var fact = (WeatherFactConfig)config;
        menu.Lines.AddRange(WeatherPlaceRows.Lines(menu, fact));

        // [S-15513] Which day, where its fact means something for one ahead: what a repeater's day series sets.
        if (fact.TakesDay)
        {
            var days = Enumerable.Range(0, WeatherReport.ForecastDays).Select(d => (d, DayName(d))).ToList();
            menu.Lines.Add(WidgetMenuLines.Shared(SharedSettings.Day, menu.Choice(Glyphs.Calendar, WeatherFactsStr.WeatherFacts_Day_Line.Text(), days, fact.Day, d => fact.Day = d)));
        }
    }

    /// <summary>[S-15513] A day from today in words: Today, Tomorrow, In 3 days.</summary>
    internal static string DayName(int day) => day switch
    {
        <= 0 => Str.Common_Today.Text(),
        1 => WeatherFactsStr.WeatherFacts_Day_Tomorrow.Text(),
        _ => WeatherFactsStr.WeatherFacts_Day_In.Plural(day),
    };

    public override FrameworkElement CreateView(IWidgetHost host) => new WeatherFactView(host, fact);
}

/// <summary>[S-6903] The family's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(WeatherTemperatureConfig))]
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
