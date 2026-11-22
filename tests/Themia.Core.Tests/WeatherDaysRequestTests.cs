using Themia.Tests;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-720] [S-15560] Regression (audit, 2026-11-22): the current weather request asked open-meteo for fourteen days and kept
/// six (<c>ParseDays(daily, 6)</c>), so a fact set to a day past the fifth read nothing. The request asks for
/// <see cref="WeatherReport.ForecastDays"/> and keeps every one of them. The service is host code no test assembly loads,
/// so its source is read.
/// </summary>
public sealed class WeatherDaysRequestTests
{
    [Fact]
    public void S15560_the_current_request_keeps_every_day_it_asks_for()
    {
        var code = File.ReadAllText(RepoFiles.At("src", "Themia.App", "Services", "Web", "WeatherService.cs"));
        var current = code[code.IndexOf("Task<Fetched<CurrentWeather>> LoadCurrentAsync(", StringComparison.Ordinal)..code.IndexOf("Task<Fetched<Forecast>> LoadForecastAsync(", StringComparison.Ordinal)];

        Assert.Contains("\"&forecast_days=\" + WeatherReport.ForecastDays", current, StringComparison.Ordinal);
        Assert.Contains("ParseDays(daily, WeatherReport.ForecastDays)", current, StringComparison.Ordinal);
        Assert.Equal(14, WeatherReport.ForecastDays);
    }
}
