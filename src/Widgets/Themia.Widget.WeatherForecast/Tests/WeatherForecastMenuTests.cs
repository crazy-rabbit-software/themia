using Themia.App.Shell.Config;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.WeatherForecast.Tests;

/// <summary>[S-6173] The forecast's options on its properties menu.</summary>
public sealed class WeatherForecastMenuTests
{
    private int _saves;

    private WidgetMenuLines Build(WeatherForecastConfig config)
    {
        var lines = new WidgetMenuLines(() => _saves++, _ => { });
        WeatherForecastMenu.Lines(lines, config);
        return lines;
    }

    [Fact]
    public void S6173_appearance_then_the_location_then_days_and_precipitation_each_behind_a_divider()
    {
        var lines = Build(new WeatherForecastConfig());

        Assert.Equal(
            ["-", Str.Shell_Place_UseDevice.Text(), Str.Shell_Place_Location.Text(), "-", WeatherForecastStr.Forecast_DaysLabel.Text(), WeatherForecastStr.WidgetsC_Weather_Settings_ShowPrecipitation_Label.Text(), "-", Str.Weather_UnitLabel.Text(), Str.EditBar_Link_UpdateInterval.Text()],
            lines.Lines.Select(l => l.IsSeparator ? "-" : l.Text));
    }

    [Fact]
    public void S6173_days_are_one_to_fourteen_the_current_marked_and_a_pick_stores_it()
    {
        var config = new WeatherForecastConfig { Days = 7 };
        var days = Build(config).Lines.Single(l => l.Text == WeatherForecastStr.Forecast_DaysLabel.Text()).Children!;

        Assert.Equal(14, days.Count);
        Assert.True(days[6].Checked);

        days[2].Action!();
        Assert.Equal(3, config.Days);
        Assert.Equal(1, _saves);
    }

    [Fact]
    public void S6173_the_precipitation_switch_turns_it_the_other_way()
    {
        var config = new WeatherForecastConfig { ShowPrecipitation = true };
        Build(config).Lines.Single(l => l.Text == WeatherForecastStr.WidgetsC_Weather_Settings_ShowPrecipitation_Label.Text()).Action!();

        Assert.False(config.ShowPrecipitation);
    }
}
