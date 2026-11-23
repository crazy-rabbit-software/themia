using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Widget.WeatherForecast.Tests;

/// <summary>[S-6171] [S-11146] [S-14815] The forecast's grid: its cells of one size from the card's edges, its lines and its size.</summary>
public class ForecastDaysTests
{
    [Fact]
    public void S724_without_a_forecast_the_days_stay_from_today_with_nothing_read()
    {
        var today = new DateOnly(2026, 11, 19);

        var days = ForecastDefaults.Unread(today, 5);

        Assert.Equal(Enumerable.Range(0, 5).Select(i => today.AddDays(i)), days.Select(d => d.Day));
        Assert.All(days, d =>
        {
            Assert.Null(d.Code);
            Assert.Null(d.Low);
            Assert.Null(d.High);
            Assert.Equal("--", WeatherReport.Degrees(d.High));
        });
    }

    /// <summary>[S-16441] The problem glyph says a stale forecast's lines, or a failure where no state's row says it.</summary>
    [Fact]
    public void S16441_the_problem_glyph_says_a_stale_answer_or_a_failure_with_no_row()
    {
        string[] stale = ["No network"];
        string[] failure = ["Can't reach the service"];

        Assert.Equal(stale, ForecastDefaults.Problems(stale, [], stated: false));
        Assert.Equal(failure, ForecastDefaults.Problems([], failure, stated: false));
        Assert.Empty(ForecastDefaults.Problems([], failure, stated: true));
        Assert.Empty(ForecastDefaults.Problems([], [], stated: false));
    }
}
