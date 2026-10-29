using Themia.Core.Model;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-667] [S-668] [S-669] The shared widget parts' metrics: the hero readout, the stat line and cell, the day row.</summary>
public sealed class WidgetPartsTests
{
    [Fact]
    public void S667_the_unit_is_a_smaller_regular_ramp_step_than_its_value_at_every_size()
    {
        foreach (var value in FluentType.Ramp.Where(step => step.FontSize >= FluentType.Subtitle.FontSize))
        {
            var unit = HeroLayout.UnitStep(value);

            Assert.Contains(FluentType.Ramp, step => step.FontSize == unit.FontSize);
            Assert.True(unit.FontSize < value.FontSize, $"{value.FontSize}: {unit.FontSize}");
            Assert.True(unit.FontSize >= FluentType.Caption.FontSize);
            Assert.False(unit.SemiBold);
        }
    }

    [Fact]
    public void S667_the_unit_grows_with_the_value_but_stays_about_half()
    {
        Assert.Equal(FluentType.Caption.FontSize, HeroLayout.UnitStep(FluentType.Subtitle).FontSize);
        Assert.Equal(FluentType.BodyLarge.FontSize, HeroLayout.UnitStep(FluentType.TitleLarge).FontSize);
        Assert.Equal(FluentType.Title.FontSize, HeroLayout.UnitStep(FluentType.Display).FontSize);
    }

    [Fact]
    public void S667_the_fit_counts_the_unit_and_never_leaves_the_bounds()
    {
        // The unit costs width: a value that fits alone at Display may not with its unit.
        var alone = HeroLayout.Fit(140, 200, 3, 0, FluentType.Subtitle, FluentType.Display);
        var withUnit = HeroLayout.Fit(140, 200, 3, 2, FluentType.Subtitle, FluentType.Display);
        Assert.True(withUnit.FontSize <= alone.FontSize);

        Assert.Equal(FluentType.Subtitle, HeroLayout.Fit(0, 200, 3, 2, FluentType.Subtitle, FluentType.Display));
        Assert.Equal(FluentType.Subtitle, HeroLayout.Fit(10, 10, 3, 2, FluentType.Subtitle, FluentType.Display));
        Assert.Equal(FluentType.Display, HeroLayout.Fit(2000, 2000, 3, 2, FluentType.Subtitle, FluentType.Display));
    }

    [Fact]
    public void S667_a_temperature_splits_into_its_number_and_unit()
    {
        Assert.Equal(("14", "°C"), WeatherReport.TemperatureParts(14.2, TemperatureUnit.Celsius));
        Assert.Equal(("-3", "°F"), WeatherReport.TemperatureParts(-2.6, TemperatureUnit.Fahrenheit));
        Assert.Equal(("0", "°C"), WeatherReport.TemperatureParts(-0.4, TemperatureUnit.Celsius));
        Assert.Equal(("--", string.Empty), WeatherReport.TemperatureParts(null, TemperatureUnit.Celsius));
        Assert.Equal(("--", string.Empty), WeatherReport.TemperatureParts(double.NaN, TemperatureUnit.Celsius));
    }
}
