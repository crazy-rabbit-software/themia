using Themia.Core.Model;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-728] [S-2846] Host and SDK rules a weather widget is the example of: the header's place
/// title (the stat and day parts went with group G3: the readings are the family's rows). Moved
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
}
