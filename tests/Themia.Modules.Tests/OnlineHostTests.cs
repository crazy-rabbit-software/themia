using Themia.Core.Model;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-659] [S-668] [S-728] [S-2846] Host and SDK rules a weather widget is the example of: the header's
/// place title, a hero filled past the ramp, the stat parts' grid. Moved from Core's WeatherLayoutTests,
/// HeroFitTests and WidgetPartsTests into the module test host, which has the weather widgets once they
/// are projects; unchanged in what they assert.
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
    public void S728_hiding_the_location_leaves_the_header_to_the_widget_s_label()
    {
        var config = new WeatherNowConfig { LocationName = "Zürich, Zurich, Switzerland", HideLocation = true };

        Assert.Null(config.Title);
        Assert.True(((WeatherNowConfig)config.Clone()).HideLocation);
    }

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
}
