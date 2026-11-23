using Themia.App.Shell.Config;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.WeatherForecast.Tests;

/// <summary>[S-6173] The forecast's options on its page.</summary>
public sealed class WeatherForecastMenuTests
{
    private int _saves;

    private WidgetMenuLines Build(WeatherForecastConfig config)
    {
        var lines = new WidgetMenuLines(() => _saves++);
        WeatherForecastMenu.Lines(lines, config);
        return lines;
    }

    [Fact]
    public void S6173_S14410_appearance_then_the_location_then_precipitation_each_behind_a_divider_and_no_days_line()
    {
        var lines = Build(new WeatherForecastConfig());

        // [S-14410] How many days is the card's: no line sets it.
        Assert.Equal(
            ["-", Str.Shell_Place_UseDevice.Text(), Str.Shell_Place_Location.Text(), "-", WeatherForecastStr.WidgetsC_Weather_Settings_ShowPrecipitation_Label.Text(), Str.Items_Parts.Text(), Str.Items_ColumnTitles.Text()],
            lines.Lines.Select(l => l.IsSeparator ? "-" : l.Text));
        foreach (var style in Enum.GetValues<ForecastStyle>())
        {
            var menu = Build(new WeatherForecastConfig { Style = style });
            Assert.DoesNotContain(menu.StyleLines.Concat(menu.Lines), l => l.Text == "Days shown");
        }
    }

    [Fact]
    public void S6173_the_precipitation_switch_turns_it_the_other_way()
    {
        var config = new WeatherForecastConfig { ShowPrecipitation = true };
        Build(config).Lines.Single(l => l.Text == WeatherForecastStr.WidgetsC_Weather_Settings_ShowPrecipitation_Label.Text()).Action!();

        Assert.False(config.ShowPrecipitation);
    }

    [Fact]
    public void S16974_the_item_styles_offer_a_days_parts_and_the_gap_or_the_tables_titles()
    {
        var table = Build(new WeatherForecastConfig()).Lines.Select(l => l.Text).ToList();
        Assert.Contains(Str.Items_Parts.Text(), table);
        Assert.Contains(Str.Items_ColumnTitles.Text(), table);
        Assert.Contains(Str.Items_Gap.Text(), Build(new WeatherForecastConfig { Style = ForecastStyle.Grid }).Lines.Select(l => l.Text));

        // The curve is the forecast's own chart: no parts, no gap.
        Assert.DoesNotContain(Str.Items_Parts.Text(), Build(new WeatherForecastConfig { Style = ForecastStyle.Curve }).Lines.Select(l => l.Text));
    }
}
