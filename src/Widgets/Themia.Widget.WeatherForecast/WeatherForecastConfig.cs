using System.Text.Json.Serialization;
using Themia.Core.Localization;
using Themia.Core.Sync;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Widget.WeatherForecast;

namespace Themia.Core.Model;

/// <summary>
/// [S-6163] The forecast's styles: the same days, four looks. What was its layout setting
/// ([S-724]); its Auto is gone (Rows that turned into Columns on a strip), and its Grid (the
/// columns wrapped into cards) came back as a style of its own, cells shaped to the tile ([S-6171]).
/// </summary>
[JsonConverter(typeof(StyleJsonConverter<ForecastStyle>))]
public enum ForecastStyle
{
    /// <summary>[S-725] One day per row, the original's default.</summary>
    Rows = 0,

    /// <summary>[S-726] The days side by side.</summary>
    Columns = 1,

    /// <summary>[S-6164] The days' highs and lows as two smooth curves across the week.</summary>
    Curve = 2,

    /// <summary>[S-6171] The days as cells in a grid shaped to the tile.</summary>
    Grid = 3,
}

/// <summary>
/// The next few days, from open-meteo's <c>daily</c> block.
/// <para>
/// A separate widget from the weather now (<c>weatherSky</c>, <c>weatherCondition</c>, <c>weatherTemperature</c>) rather than a mode of it, because the two
/// answer different questions and want different shapes on the desktop: one is a readout, the other
/// is a list.
/// </para>
/// </summary>
public sealed class WeatherForecastConfig : WeatherConfig, IStyled, IInterests
{
    [JsonIgnore]
    public override bool ReadsWeather => true;

    /// <summary>[S-11145] [S-14502] It asks for every day open-meteo gives: how many it shows is its card's.</summary>
    [JsonIgnore]
    public override int? ForecastDays => MostDays;

    /// <summary>[S-2608] What the background sync keeps fresh for it.</summary>
    public void AddInterests(InterestsBuilder interests)
    {
        if (WeatherInterests.For(this, interests.Unit, UpdateCadence.Interval(interests.Refresh, HostCadences.Weather)) is { } wants)
        {
            interests.Of<WeatherInterest>().Add(wants);
        }
    }

    private static readonly StyleOption[] ForecastStyles =
    [
        StyleOption.Of(ForecastStyle.Rows, WeatherForecastStr.Forecast_LayoutRows.Local(), 0xEA37),
        StyleOption.Of(ForecastStyle.Columns, WeatherForecastStr.Forecast_LayoutColumns.Local(), 0xE907),
        StyleOption.Of(ForecastStyle.Curve, WeatherForecastStr.Styles_Forecast_Curve.Local(), 0xE9D2),
        StyleOption.Of(ForecastStyle.Grid, WeatherForecastStr.Styles_Forecast_Grid.Local(), 0xF0E2),
    ];


    /// <summary>
    /// [S-11145] [S-14410] [S-14502] How many days a new card shows: its natural size's. The days shown after are as many as
    /// its card holds (1 to <see cref="MostDays"/>, open-meteo's own limit), never stored.
    /// </summary>
    public const int DefaultDays = 5;

    /// <summary>[S-11145] The most days it shows and asks for: open-meteo's own limit.</summary>
    public const int MostDays = 14;

    /// <summary>[S-11145] The rows style's lines per row: 1 (the day, its range and figures) or 2 (the sky in words and the rain under the day).</summary>
    public int Lines { get; set; } = 1;

    /// <summary>The lines held to 1 or 2.</summary>
    [JsonIgnore]
    public int EffectiveLines => Math.Clamp(Lines, 1, 2);

    /// <summary>Show each day's precipitation total beside its temperatures.</summary>
    public bool ShowPrecipitation { get; set; } = true;

    /// <summary>[S-6163] Rows, columns, the temperature curve or the grid.</summary>
    public ForecastStyle Style { get; set; } = ForecastStyle.Rows;

    IReadOnlyList<StyleOption> IStyled.Styles => ForecastStyles;

    int IStyled.StyleValue
    {
        get => (int)Style;
        set => Style = (ForecastStyle)value;
    }

    /// <summary>[S-11145] A line count out of range is held to it, and says so.</summary>
    public override IReadOnlyList<string> Repair()
    {
        var repairs = new List<string>();
        if (Lines != EffectiveLines)
        {
            repairs.Add($"forecast: {Lines} lines is out of 1..2; {EffectiveLines}");
            Lines = EffectiveLines;
        }

        return repairs;
    }

    public override WidgetConfig Clone()
    {
        var copy = new WeatherForecastConfig
        {
            Lines = Lines,
            ShowPrecipitation = ShowPrecipitation,
            Style = Style,
        };

        CopyTo(copy);
        return copy;
    }
}
