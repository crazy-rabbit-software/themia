using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Modules.Tests;

/// <summary>
/// [S-2260] A header names the widget; the place it is about stands in its trailing label: over the six
/// widgets about a place, with a place, without one and with the place cleared.
/// </summary>
public sealed class HeaderAboutTests
{
    public static TheoryData<string> Places() => ["weatherNow", "weatherForecast", "airQuality", "rainNextHour", "sun", "moon"];

    private static WidgetConfig Placed(string kind, string? name)
    {
        var config = WidgetInstance.Create(new WidgetId(kind), new FracRect(0, 0, 0.2, 0.2)).Config;
        switch (config)
        {
            case WeatherConfig weather:
                weather.Latitude = 47.37;
                weather.Longitude = 8.54;
                weather.LocationName = name ?? string.Empty;
                break;
            case SkyPlaceConfig sky:
                sky.Place = name ?? string.Empty;
                break;
        }

        return config;
    }

    private static string? Label(WidgetConfig config) => config switch
    {
        WeatherConfig weather => weather.PlaceLabel,
        SkyPlaceConfig sky => sky.PlaceLabel,
        _ => throw new InvalidOperationException(config.GetType().Name),
    };

    [Theory]
    [MemberData(nameof(Places))]
    public void S2260_the_place_is_the_label_and_never_the_name(string kind)
    {
        var config = Placed(kind, "Zürich, Zurich, Switzerland");

        Assert.StartsWith("Zürich", Label(config), StringComparison.Ordinal);
        Assert.DoesNotContain("Zürich", ModuleCatalog.Of(new WidgetId(kind)).Name, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Places))]
    public void S2260_no_place_name_no_label(string kind)
        => Assert.Null(Label(Placed(kind, "  ")));

    [Theory]
    [MemberData(nameof(Places))]
    public void S2260_a_cleared_place_has_no_label(string kind)
    {
        var config = Placed(kind, "Zürich");
        ((IPlaced)config).ClearLocation();

        Assert.Null(Label(config));
    }

    [Fact]
    public void S2260_the_label_is_trimmed_and_blank_is_none()
    {
        Assert.Equal("1.1.1.1", HeaderRules.About("  1.1.1.1 "));
        Assert.Null(HeaderRules.About(" "));
        Assert.Null(HeaderRules.About(null));
    }

    [Fact]
    public void S2260_a_part_added_later_stands_in_the_headers_order()
    {
        HeaderPartKind[] kinds = [HeaderPartKind.Problem, HeaderPartKind.Busy, HeaderPartKind.Button, HeaderPartKind.Count];

        Assert.Equal(2, HeaderRules.InsertAt(kinds, HeaderPartKind.Label));
        Assert.Equal(1, HeaderRules.InsertAt([HeaderPartKind.Problem], HeaderPartKind.Label));
        Assert.Equal(3, HeaderRules.InsertAt(kinds, HeaderPartKind.Button));
        Assert.Equal(0, HeaderRules.InsertAt([], HeaderPartKind.Label));
    }
}
