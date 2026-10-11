using System.Text.Json.Serialization;

namespace Themia.Core.Model;

/// <summary>[S-724] How the forecast lays out its days.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ForecastLayout>))]
public enum ForecastLayout
{
    /// <summary>Columns on a strip, rows everywhere else: the layout follows the tile.</summary>
    Auto = 0,

    /// <summary>One day per row, the original's default.</summary>
    Rows = 1,

    /// <summary>The days side by side.</summary>
    Columns = 2,

    /// <summary>A card per day, two or more across.</summary>
    Grid = 3,
}

/// <summary>
/// The next few days, from open-meteo's <c>daily</c> block.
/// <para>
/// A separate widget from <see cref="WeatherNowConfig"/> rather than a mode of it, because the two
/// answer different questions and want different shapes on the desktop: one is a readout, the other
/// is a list.
/// </para>
/// </summary>
public sealed class WeatherForecastConfig : WeatherConfig
{
    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.WeatherForecast;

    /// <summary>How many days to ask for, 1 to 14 — open-meteo's own limit.</summary>
    public int Days { get; set; } = 7;

    /// <summary>Show each day's precipitation total beside its temperatures.</summary>
    public bool ShowPrecipitation { get; set; } = true;

    /// <summary>[S-724] Rows, columns or a grid; Auto picks by the tile's shape.</summary>
    public ForecastLayout Layout { get; set; } = ForecastLayout.Auto;

    /// <summary>The day count actually sent, clamped, so a hand-edited 0 or 99 cannot reach the URL.</summary>
    [JsonIgnore]
    public int EffectiveDays => Math.Clamp(Days, 1, 14);

    public override WidgetConfig Clone()
    {
        var copy = new WeatherForecastConfig
        {
            Days = Days,
            ShowPrecipitation = ShowPrecipitation,
            Layout = Layout,
        };

        CopyTo(copy);
        return copy;
    }
}
