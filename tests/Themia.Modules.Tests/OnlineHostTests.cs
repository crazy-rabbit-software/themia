using Themia.Core.Model;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-659] [S-668] [S-669] [S-728] [S-2846] Host and SDK rules a weather widget is the example of: the
/// header's place title, a hero filled past the ramp, the stat parts' grid, the day parts' steps. Moved
/// from Core's WeatherLayoutTests, HeroFitTests, WidgetPartsTests and ForecastDaysTests into the module
/// test host, which has the weather widgets once they are projects; unchanged in what they assert.
/// </summary>
public sealed class OnlineHostTests
{
    [Fact]
    public void S728_the_title_is_the_chosen_city_not_the_typed_query()
    {
        var config = new WeatherNowConfig { LocationName = "Zürich, Zurich, Switzerland", Latitude = 47.37, Longitude = 8.54 };

        Assert.Equal("Zürich", config.Title);
    }

    [Fact]
    public void S2846_with_no_place_picked_there_is_no_title_and_nothing_typed_stands_in()
        => Assert.Null(new WeatherForecastConfig().Title);

    [Fact]
    public void S659_the_minimal_temperature_fills_the_largest_tile()
    {
        var xl = new WidgetArea(800, 516);
        var box = xl.Deflate(WeatherNowLayout.Inset, WeatherNowLayout.VerticalInset);
        var step = WeatherNowStyles.Minimal(xl);
        var used = HeroLayout.ValueWidth(step, WeatherNowStyles.ValueCharacters, WeatherNowStyles.UnitCharacters);

        Assert.True(step.FontSize > FluentType.Display.FontSize * HeroLayout.MaxGrowth);
        Assert.True(used <= box.Width + 0.001 && step.LineHeight <= box.Height + 0.001);
        Assert.True(used >= box.Width * 0.97 || step.LineHeight >= box.Height * 0.97);
    }

    [Fact]
    public void S668_one_stat_line_and_one_cell_on_the_4px_grid()
    {
        Assert.Equal(28, PartMetrics.StatLine);
        Assert.Equal(36, PartMetrics.StatCell);
        Assert.Equal(0, PartMetrics.StatLine % 4);
        Assert.Equal(0, PartMetrics.StatCell % 4);

        // The weather ladder budgets with the parts it draws.
        Assert.Equal(PartMetrics.StatLine, WeatherNowLayout.StatLine);
        Assert.Equal(PartMetrics.StatCell, WeatherNowLayout.CellHeight);
    }

    [Fact]
    public void S669_the_day_parts_grow_by_whole_ramp_steps()
    {
        for (var k = 1; k <= DayScale.Largest; k++)
        {
            var smaller = DayScale.At(k - 1);
            var larger = DayScale.At(k);
            Assert.True(larger.High.FontSize > smaller.High.FontSize);
            Assert.True(larger.Icon > smaller.Icon);
            Assert.True(larger.Bar >= smaller.Bar);
        }

        Assert.Equal(PartMetrics.DayIcon, DayScale.At(0).Icon);
        Assert.True(DayScale.At(0).Bar >= 6, "a 4px bar read as a scratch");
    }
}
