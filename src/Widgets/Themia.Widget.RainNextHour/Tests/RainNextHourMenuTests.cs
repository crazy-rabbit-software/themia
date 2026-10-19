using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.RainNextHour.Tests;

/// <summary>[S-5127] The rain widget's options on its properties menu: Appearance, the weather family's location search, the source.</summary>
public sealed class RainNextHourMenuTests
{
    private int _saves;

    private WidgetMenuLines Build(RainNextHourConfig config)
    {
        var lines = new WidgetMenuLines(() => _saves++, _ => { });
        RainNextHourMenu.Lines(lines, config);
        return lines;
    }

    [Fact]
    public void S5127_the_first_level_is_appearance_the_location_and_the_source()
    {
        var lines = Build(new RainNextHourConfig());

        Assert.Equal(0, lines.CardAt);
        Assert.Empty(lines.StyleLines);
        Assert.Collection(lines.Lines,
            divider => Assert.True(divider.IsSeparator),
            use => Assert.Equal(Str.Shell_Place_UseDevice.Text(), use.Text),
            location => Assert.Equal(Str.Shell_Place_Location.Text(), location.Text),
            name => Assert.Equal(Str.Weather_LocationNameLabel.Text(), name.Text),
            divider => Assert.True(divider.IsSeparator),
            source =>
            {
                Assert.True(source.Note);
                Assert.Equal(Str.Online_Source.Format(("name", "Open-Meteo")), source.Text);
            });
    }

    [Fact]
    public void S5127_the_location_name_switch_hides_the_city_and_saves_once()
    {
        var config = new RainNextHourConfig { Latitude = 47.37, Longitude = 8.54, LocationName = "Zürich, Switzerland" };
        var lines = Build(config).Lines;

        Assert.Equal("Zürich", lines.First(l => l.Search is not null).Search!.Value);
        Assert.Single(lines, l => l.Text == Str.Weather_LocationNameLabel.Text()).Action!();
        Assert.True(config.HideLocation);
        Assert.Equal(1, _saves);
    }
}
