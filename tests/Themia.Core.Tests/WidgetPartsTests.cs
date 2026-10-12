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
    public void S667_the_token_beside_a_value_steps_up_once_with_it()
    {
        Assert.Equal(FluentType.Body, HeroLayout.TrailingStep(FluentType.Subtitle));
        Assert.Equal(FluentType.BodyLarge, HeroLayout.TrailingStep(FluentType.Title));
        Assert.Equal(FluentType.BodyLarge, HeroLayout.TrailingStep(FluentType.Display));
    }

    [Fact]
    public void S667_the_glyph_grows_with_the_value_within_24_to_72()
    {
        Assert.Equal(24, HeroLayout.IconSize(FluentType.Subtitle));
        Assert.Equal(72, HeroLayout.IconSize(FluentType.Scaled(FluentType.Display, 3)));
        Assert.True(HeroLayout.IconSize(FluentType.TitleLarge) < HeroLayout.IconSize(FluentType.Display));
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
    public void S669_a_day_row_figure_holds_the_widest_temperature()
    {
        // "-18°" at Body Strong: the degree sign is half a character.
        Assert.True(FluentType.EstimateWidth(FluentType.BodyStrong, 3.5) <= PartMetrics.FigureWidth);
        Assert.True(PartMetrics.DayIcon > PartMetrics.LineIcon);
    }

    [Fact]
    public void S667_a_value_grows_past_display_only_from_display_and_at_most_half_again()
    {
        Assert.Equal(FluentType.Title, HeroLayout.Grow(FluentType.Title, 2000, 2000, 3, 2));

        var grown = HeroLayout.Grow(FluentType.Display, 2000, 2000, 3, 2);
        Assert.Equal(FluentType.Display.FontSize * HeroLayout.MaxGrowth, grown.FontSize, 6);

        // Held by the box: no taller than the height, no wider than the width.
        var held = HeroLayout.Grow(FluentType.Display, 2000, 110, 3, 2);
        Assert.True(held.LineHeight <= 110 + 0.001);
        Assert.Equal(FluentType.Display, HeroLayout.Grow(FluentType.Display, 100, 2000, 3, 2));
    }
}
