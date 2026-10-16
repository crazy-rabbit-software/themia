using System.Text.Json.Serialization;
using Themia.Core.Localization;
using Themia.Core.Widgets;

namespace Themia.Core.Model;

/// <summary>
/// [S-6163] The forecast's styles: the same days, three looks. What was its layout setting
/// ([S-724]); its Auto and Grid are gone (Auto was Rows that turned into Columns on a strip, the
/// grid was the columns wrapped into cards).
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
}

/// <summary>
/// The next few days, from open-meteo's <c>daily</c> block.
/// <para>
/// A separate widget from <see cref="WeatherNowConfig"/> rather than a mode of it, because the two
/// answer different questions and want different shapes on the desktop: one is a readout, the other
/// is a list.
/// </para>
/// </summary>
public sealed class WeatherForecastConfig : WeatherConfig, IStyled
{
    private static readonly StyleOption[] ForecastStyles =
    [
        StyleOption.Of(ForecastStyle.Rows, Str.Forecast_LayoutRows, 0xEA37),
        StyleOption.Of(ForecastStyle.Columns, Str.Forecast_LayoutColumns, 0xE907),
        StyleOption.Of(ForecastStyle.Curve, Str.Styles_Forecast_Curve, 0xE9D2),
    ];

    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.WeatherForecast;

    /// <summary>How many days to ask for, 1 to 14 — open-meteo's own limit.</summary>
    public int Days { get; set; } = 7;

    /// <summary>Show each day's precipitation total beside its temperatures.</summary>
    public bool ShowPrecipitation { get; set; } = true;

    /// <summary>[S-6163] Rows, columns or the temperature curve.</summary>
    public ForecastStyle Style { get; set; } = ForecastStyle.Rows;

    IReadOnlyList<StyleOption> IStyled.Styles => ForecastStyles;

    int IStyled.StyleValue
    {
        get => (int)Style;
        set => Style = (ForecastStyle)value;
    }

    /// <summary>The day count actually sent, clamped, so a hand-edited 0 or 99 cannot reach the URL.</summary>
    [JsonIgnore]
    public int EffectiveDays => Math.Clamp(Days, 1, 14);

    public override WidgetConfig Clone()
    {
        var copy = new WeatherForecastConfig
        {
            Days = Days,
            ShowPrecipitation = ShowPrecipitation,
            Style = Style,
        };

        CopyTo(copy);
        return copy;
    }
}
