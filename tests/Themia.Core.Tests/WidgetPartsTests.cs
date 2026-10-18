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
    public void S667_S659_the_glyph_grows_with_the_value_from_24()
    {
        Assert.Equal(24, HeroLayout.IconSize(FluentType.Subtitle));
        Assert.True(HeroLayout.IconSize(FluentType.TitleLarge) < HeroLayout.IconSize(FluentType.Display));

        // [S-659] No ceiling past the ramp: a filled reading keeps its glyph in proportion.
        Assert.Equal(0.8 * 3 * FluentType.Display.FontSize, HeroLayout.IconSize(FluentType.Scaled(FluentType.Display, 3)), 6);
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

    [Theory]
    [InlineData("42%", "42", "%", false)]
    [InlineData("42 %", "42", "%", true)]
    [InlineData("1.2 MB/s", "1.2", "MB/s", true)]
    [InlineData("4,48 GHz", "4,48", "GHz", true)]
    [InlineData("24 ms", "24", "ms", true)]
    [InlineData("٤٢٪", "٤٢", "٪", false)]
    public void S3860_a_reading_splits_into_its_number_and_the_unit_after_it(string text, string value, string unit, bool spaced)
        => Assert.Equal(new HeroText(value, unit, spaced), HeroLayout.Split(text));

    [Theory]
    [InlineData("--")]
    [InlineData("%42")]
    [InlineData("1 234 ms")]
    [InlineData("42")]
    [InlineData("")]
    [InlineData("‏42%")]
    [InlineData("12. GB")]
    public void S3860_a_reading_that_does_not_lead_with_a_figure_and_a_unit_stays_whole(string text)
        => Assert.Equal(new HeroText(text, string.Empty, false), HeroLayout.Split(text));

    [Fact]
    public void S3860_the_tier_step_grows_with_the_tiles_smaller_class()
    {
        Assert.Equal(FluentType.Title, HeroLayout.TierStep(new WidgetArea(120, 76)));
        Assert.Equal(FluentType.Title, HeroLayout.TierStep(new WidgetArea(320, 196)));
        Assert.Equal(FluentType.Title, HeroLayout.TierStep(new WidgetArea(200, 356)));
        Assert.Equal(FluentType.TitleLarge, HeroLayout.TierStep(new WidgetArea(560, 376)));
        Assert.Equal(FluentType.Display, HeroLayout.TierStep(new WidgetArea(800, 516)));
    }
}
