namespace Themia.Core.Widgets;

/// <summary>
/// Conditions right now at one point, already validated, plus the little of today and the next
/// days that the current-conditions widget shows once it has the room. [S-720]
/// <para>
/// One request carries all of it (open-meteo's <c>current</c> and <c>daily</c> blocks together), so a larger tile costs no extra traffic: the extras are on hand from the
/// first fetch and the layout only decides whether to draw them.
/// </para>
/// </summary>
/// <param name="Temperature">In the unit that was asked for, or null if the service omitted it.</param>
/// <param name="FeelsLike">Apparent temperature, same unit.</param>
/// <param name="Humidity">Relative humidity, 0 to 100.</param>
/// <param name="WindKilometresPerHour">Wind speed at 10m.</param>
/// <param name="Code">WMO 4677 condition code, or null.</param>
public sealed record CurrentWeather(
    double? Temperature,
    double? FeelsLike,
    double? Humidity,
    double? WindKilometresPerHour,
    int? Code)
{
    /// <summary>Precipitation over the last hour, in millimetres.</summary>
    public double? Precipitation { get; init; }

    /// <summary>Sea-level pressure, hPa.</summary>
    public double? Pressure { get; init; }

    /// <summary>Where the wind comes <i>from</i>, degrees clockwise from north.</summary>
    public double? WindDirection { get; init; }

    /// <summary>Gusts at 10m, km/h.</summary>
    public double? WindGusts { get; init; }

    /// <summary>Cloud cover, 0 to 100.</summary>
    public double? CloudCover { get; init; }

    public double? UvIndex { get; init; }

    /// <summary>Today first, then the next few days, in the place's own calendar.</summary>
    public IReadOnlyList<ForecastDay> Days { get; init; } = [];

    /// <summary>Today's entry, when the reply carried one.</summary>
    public ForecastDay? Today => Days.Count > 0 ? Days[0] : null;
}

/// <summary>One day of a forecast.</summary>
public sealed record ForecastDay(DateOnly Day, int? Code, double? Low, double? High, double? Precipitation)
{
    /// <summary>The day's highest hourly chance of precipitation, 0 to 100.</summary>
    public double? PrecipitationChance { get; init; }

    /// <summary>The day's strongest sustained wind, km/h.</summary>
    public double? WindMax { get; init; }

    public double? UvMax { get; init; }

    /// <summary>Local wall-clock sunrise at the place, or null in a polar day or night.</summary>
    public TimeOnly? Sunrise { get; init; }

    public TimeOnly? Sunset { get; init; }
}

/// <summary>A forecast, in day order.</summary>
public sealed record Forecast(IReadOnlyList<ForecastDay> Days);
