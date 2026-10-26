using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.WeatherNow.Tests;

/// <summary>[S-6174] The current conditions' options on its properties menu, the weather family's location search among them.</summary>
public sealed class WeatherNowMenuTests
{
    private int _saves;

    private WidgetMenuLines Build(WeatherNowConfig config)
    {
        var lines = new WidgetMenuLines(() => _saves++, _ => { });
        WeatherNowMenu.Lines(lines, config);
        return lines;
    }

    [Fact]
    public void S6174_the_readings_line_is_under_style_in_the_standard_style_only()
    {
        Assert.Single(Build(new WeatherNowConfig { Style = WeatherNowStyle.Standard }).StyleLines);
        Assert.Empty(Build(new WeatherNowConfig { Style = WeatherNowStyle.LargeIcon }).StyleLines);
    }

    [Fact]
    public void S6174_the_first_level_is_appearance_then_use_my_location_and_the_location_search()
    {
        var lines = Build(new WeatherNowConfig());

        Assert.Collection(lines.Lines,
            divider => Assert.True(divider.IsSeparator),
            use => Assert.Equal(Str.Shell_Place_UseDevice.Text(), use.Text),
            location =>
            {
                Assert.NotNull(location.Search);
                Assert.Equal(Str.Shell_Place_Location.Text(), location.Text);
            });
    }

    [Fact]
    public void S6174_without_a_place_the_line_shows_none_and_clear_is_greyed()
    {
        var location = Build(new WeatherNowConfig()).Lines.First(l => l.Search is not null);

        Assert.Null(location.Search!.Value);
        Assert.True(Assert.Single(location.Search.Fixed, f => f.Text == Str.Shell_Place_Clear.Text()).Disabled);
    }

    [Fact]
    public void S6174_clear_forgets_the_place()
    {
        var config = new WeatherNowConfig { Latitude = 47.37, Longitude = 8.54, LocationName = "Zürich, Switzerland" };
        var search = Build(config).Lines.First(l => l.Search is not null).Search!;

        Assert.Equal("Zürich", search.Value);
        Assert.Single(search.Fixed, f => f.Text == Str.Shell_Place_Clear.Text()).Action!();
        Assert.False(config.HasLocation);
        Assert.Equal(1, _saves);
    }

    [Theory]
    [InlineData("")]
    [InlineData("z")]
    public async Task S6174_nothing_typed_or_one_letter_asks_nothing(string typed)
    {
        var search = Build(new WeatherNowConfig()).Lines.First(l => l.Search is not null).Search!;
        var answer = await search.Find(typed, CancellationToken.None);

        Assert.Empty(answer.Lines);
        Assert.Null(answer.Note);
    }
}
