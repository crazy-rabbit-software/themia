using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.Sun.Tests;

/// <summary>[S-3107] The Sun's options on its properties menu, the sky's shared location search among them ([S-3101]).</summary>
public sealed class SunMenuTests
{
    private int _saves;

    private WidgetTextAsk? _asked;

    private WidgetMenuLines Build(SunConfig config)
    {
        var lines = new WidgetMenuLines(() => _saves++, ask => _asked = ask);
        SunMenu.Lines(lines, config);
        return lines;
    }

    private MenuSearch Location(SunConfig config) => Assert.Single(Build(config).Lines, l => l.Search is not null).Search!;

    private static ItemMenuEntry Fixed(MenuSearch search, string text) => Assert.Single(search.Fixed, f => f.Text == text);

    [Fact]
    public void S3107_the_events_switch_is_under_style_except_in_the_times_style()
    {
        var events = Assert.Single(Build(new SunConfig { Style = SunStyle.Arc, ShowEvents = false }).StyleLines);
        Assert.Equal(SunStr.SunMoon_Events_Label.Text(), events.Text);
        Assert.False(events.Checked);

        Assert.Single(Build(new SunConfig { Style = SunStyle.DayBar }).StyleLines);
        Assert.Empty(Build(new SunConfig { Style = SunStyle.Times }).StyleLines);
    }

    [Fact]
    public void S3107_the_events_switch_turns_them_off_and_saves_once()
    {
        var config = new SunConfig { ShowEvents = true };
        Assert.Single(Build(config).StyleLines).Action!();

        Assert.False(config.ShowEvents);
        Assert.Equal(1, _saves);
    }

    [Fact]
    public void S3107_the_first_level_is_appearance_then_use_my_location_and_the_location_search()
    {
        var lines = Build(new SunConfig());

        Assert.True(lines.Lines[0].IsSeparator);
        Assert.Equal(Str.Shell_Place_UseDevice.Text(), lines.Lines[1].Text);
        var location = Assert.Single(lines.Lines, l => l.Search is not null);
        Assert.Equal(Str.Shell_Place_Location.Text(), location.Text);
        Assert.NotNull(location.Search);
        Assert.Equal(
            [Str.Shell_Place_Clear.Text(), Str.SunMoon_Latitude_Label.Text(), Str.SunMoon_Longitude_Label.Text()],
            location.Search!.Fixed.Select(f => f.Text));
    }

    [Fact]
    public void S3107_the_line_names_the_place_its_coordinates_or_nothing()
    {
        Assert.Equal("Zürich", Location(new SunConfig { Place = "Zürich, Zurich, Switzerland" }).Value);
        Assert.Equal("-33.8688, 151.2093", Location(new SunConfig { Latitude = -33.8688, Longitude = 151.2093, Place = "" }).Value);

        var cleared = new SunConfig();
        cleared.ClearLocation();
        var search = Location(cleared);
        Assert.Null(search.Value);
        Assert.True(Fixed(search, Str.Shell_Place_Clear.Text()).Disabled);
        Assert.Null(Fixed(search, Str.SunMoon_Latitude_Label.Text()).Value);
    }

    [Fact]
    public void S3107_clear_forgets_the_place_and_saves()
    {
        var config = new SunConfig();
        Fixed(Location(config), Str.Shell_Place_Clear.Text()).Action!();

        Assert.False(config.HasLocation);
        Assert.Equal(string.Empty, config.Place);
        Assert.Equal(1, _saves);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("91")]
    [InlineData("-90.5")]
    [InlineData("NaN")]
    [InlineData("")]
    public void S3107_a_latitude_that_is_not_a_number_in_range_is_refused(string typed)
    {
        var config = new SunConfig();
        Fixed(Location(config), Str.SunMoon_Latitude_Label.Text()).Action!();

        var ask = Assert.IsType<WidgetTextAsk>(_asked);
        Assert.Equal(Str.WidgetsC_SunMoon_Settings_CoordinateRange.Format(("min", "-90"), ("max", "90")), ask.Refuse(typed));
        ask.Done(typed);
        Assert.Equal(SkyPlaceConfig.DefaultLatitude, config.Latitude);
        Assert.Equal(0, _saves);
    }

    [Fact]
    public void S3107_a_typed_longitude_moves_the_observer_and_the_same_one_saves_nothing()
    {
        var config = new SunConfig();
        Fixed(Location(config), Str.SunMoon_Longitude_Label.Text()).Action!();
        var ask = Assert.IsType<WidgetTextAsk>(_asked);

        Assert.Equal("8.54", ask.Initial);
        Assert.Null(ask.Refuse("-179.5"));
        ask.Done("8.54");
        Assert.Equal(0, _saves);

        ask.Done(" -179.5 ");
        Assert.Equal(-179.5, config.Longitude);
        Assert.Equal(1, _saves);
    }

    [Theory]
    [InlineData("")]
    [InlineData("z")]
    [InlineData("42")]
    public async Task S3107_nothing_typed_one_letter_or_no_letter_asks_no_geocoder(string typed)
    {
        var answer = await Location(new SunConfig()).Find(typed, CancellationToken.None);

        Assert.Empty(answer.Lines);
    }
}
