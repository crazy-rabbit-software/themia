using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-659] The fill rule for a readout that is the whole tile: measured by its glyphs, as large as
/// the box holds it at every tier and odd aspect, never past it.
/// </summary>
public sealed class HeroFitTests
{
    /// <summary>The clock's face box at the seven tiers and four odd aspects (card less its 16/8 inset).</summary>
    public static TheoryData<double, double> Boxes => new()
    {
        { 88, 104 }, { 168, 134 }, { 288, 224 }, { 448, 144 }, { 168, 384 }, { 528, 404 }, { 768, 544 },
        { 568, 134 }, { 118, 384 }, { 268, 284 }, { 768, 184 },
    };

    [Fact]
    public void S659_glyphs_are_measured_by_their_class()
    {
        Assert.Equal((4 * 0.58) + 0.25, HeroFit.Ems("88:88"), 6);
        Assert.Equal((6 * 0.58) + (2 * 0.25), HeroFit.Ems(HeroFit.WidestTime(seconds: true)), 6);
        Assert.Equal(0.72 + 0.95, HeroFit.Ems("AM"), 6);
        Assert.Equal(0.41 + (2 * 0.58) + 0.39 + 0.72, HeroFit.Ems("-18°C"), 6);
        Assert.Equal(0, HeroFit.Ems(string.Empty));

        // A script the table does not know counts a full em a character, a combining mark nothing.
        Assert.Equal(2, HeroFit.Ems("午後"), 6);
        Assert.Equal(1, HeroFit.Ems("க்"), 6);
    }

    [Theory]
    [MemberData(nameof(Boxes))]
    public void S659_the_clock_row_fits_its_box_at_every_tier_and_aspect(double width, double height)
    {
        foreach (var meridiem in new[] { string.Empty, "AM", "a. m." })
        {
            foreach (var seconds in new[] { false, true })
            {
                var row = HeroFit.Row(width, height, seconds, meridiem, 4);
                var used = (row.Time.FontSize * HeroFit.Ems(HeroFit.WidestTime(seconds)))
                    + (meridiem.Length > 0 ? (row.Tail.FontSize * HeroFit.Ems(meridiem)) + 4 : 0);

                Assert.True(used <= width + 0.001, $"{width}x{height} '{meridiem}' {seconds}: {used}");
                Assert.True(row.Time.LineHeight <= height + 0.001);
                Assert.True(row.Tail.FontSize < row.Time.FontSize);
            }
        }
    }

    [Theory]
    [MemberData(nameof(Boxes))]
    public void S659_past_the_ramp_the_clock_row_fills_its_width_or_its_height(double width, double height)
    {
        var row = HeroFit.Row(width, height, false, "AM", 4);
        if (row.Time.FontSize <= FluentType.TitleLarge.FontSize)
        {
            return;
        }

        // Within 5 %: the meridiem moves by whole ramp steps, so the row can stop short of the last few DIPs.
        var used = (row.Time.FontSize * HeroFit.Ems("88:88")) + (row.Tail.FontSize * HeroFit.Ems("AM")) + 4;
        Assert.True(used >= width * 0.95 || row.Time.LineHeight >= height * 0.97, $"{width}x{height}: {used} wide, {row.Time.LineHeight} tall");
    }

    [Fact]
    public void S659_a_12_hour_clock_is_no_longer_laid_out_for_three_full_size_meridiem_letters()
    {
        // Regression: the M tile's "10:42 AM" filled 61 % of its width, the meridiem counted as "PMx"
        // at the digits' size and the colon as a digit.
        var before = FluentType.Hero(288, 224, 5 + 3);
        var after = HeroFit.Row(288, 224, false, "AM", 4).Time;

        Assert.True(after.FontSize > before.FontSize * 1.3, $"{before.FontSize} -> {after.FontSize}");
    }

    [Fact]
    public void S659_figures_are_set_solid_past_the_ramp_and_on_it_below()
    {
        var large = HeroFit.Figures(2000, 230, HeroFit.Ems("88:88"));
        Assert.Equal(200, large.FontSize, 6);
        Assert.Equal(HeroFit.Leading, large.LineHeight / large.FontSize, 6);

        var small = HeroFit.Figures(80, 40, HeroFit.Ems("88:88"));
        Assert.Contains(small, FluentType.Ramp);
        Assert.Equal(FluentType.Subtitle, HeroFit.Figures(0, 100, 2));
        Assert.Equal(FluentType.Subtitle, HeroFit.Figures(double.NaN, 100, 2));
    }

    [Fact]
    public void S659_figures_grow_steadily_with_their_box()
    {
        var previous = 0.0;

        for (var height = 20.0; height <= 600; height += 4)
        {
            var size = HeroFit.Figures(3000, height, HeroFit.Ems("88:88")).FontSize;
            Assert.True(size >= previous, $"shrank at {height}");
            previous = size;
        }
    }

    [Theory]
    [MemberData(nameof(Boxes))]
    public void S659_stacked_digits_fit_two_lines_and_their_tail(double width, double height)
    {
        var step = HeroFit.Stacked(width, height, "AM", 4);
        var used = (step.FontSize * HeroFit.Ems("88")) + 4 + (step.FontSize * HeroFit.StackedTailScale * HeroFit.Ems("AM"));

        Assert.True(used <= width + 0.001 || step.FontSize == FluentType.Caption.FontSize);
        Assert.True((2 * step.LineHeight) <= height + 0.001 || step.FontSize == FluentType.Caption.FontSize);
        Assert.True(used >= width * 0.97 || (2 * step.LineHeight) >= height * 0.97);
    }

    [Fact]
    public void S659_the_meridiem_is_lifted_onto_the_digits_baseline()
    {
        var time = new TypeStep(200, 230, true);
        var tail = FluentType.Title;

        // Bottom-aligned, the baselines meet when the shorter line is lifted by the difference.
        var lift = HeroFit.BaselineDrop(time) - HeroFit.BaselineDrop(tail);
        Assert.Equal((230 - 36) * 0.19, lift, 6);
    }

    // ---- the temperature ----------------------------------------------------------------------

    [Fact]
    public void S659_the_reading_fills_past_Display_with_no_ceiling()
    {
        var filled = HeroLayout.Fill(FluentType.Display, 2000, 460, 3, 2, icon: false, beside: 0);

        Assert.Equal(400, filled.FontSize, 6);
        Assert.True(filled.FontSize > FluentType.Display.FontSize * HeroLayout.MaxGrowth);
    }

    [Fact]
    public void S659_the_reading_starts_filling_at_TitleLarge_and_leaves_smaller_steps_alone()
    {
        Assert.Equal(FluentType.Title, HeroLayout.Fill(FluentType.Title, 2000, 2000, 3, 2, icon: true, beside: 0));

        var grown = HeroLayout.Fill(FluentType.TitleLarge, 2000, 70, 3, 2, icon: true, beside: 0);
        Assert.True(grown.FontSize > FluentType.TitleLarge.FontSize);
        Assert.True(grown.FontSize < FluentType.Display.FontSize);

        // A box with no room for more returns the step it was given.
        Assert.Equal(FluentType.Display, HeroLayout.Fill(FluentType.Display, 100, 2000, 3, 2, icon: true, beside: 0));
    }

    [Theory]
    [InlineData(400, 300, true, 0)]
    [InlineData(400, 300, true, 120)]
    [InlineData(700, 200, false, 0)]
    [InlineData(250, 600, true, 0)]
    public void S659_the_glyph_value_unit_and_what_stands_beside_fit_the_width_together(double width, double height, bool icon, double beside)
    {
        var step = HeroLayout.Fill(FluentType.Display, width, height, 3, 2, icon, beside);
        var used = (icon ? HeroLayout.IconSize(step) + HeroLayout.IconGap : 0) + HeroLayout.ValueWidth(step, 3, 2) + beside;

        Assert.True(used <= width + 0.001, $"{used} > {width}");
        Assert.True(step.LineHeight <= height + 0.001);
        Assert.True(used >= width * 0.97 || step.LineHeight >= height * 0.97);
    }

    [Fact]
    public void S659_the_unit_grows_with_a_filled_value_from_Title()
    {
        Assert.Equal(FluentType.Title.FontSize, HeroLayout.UnitStep(FluentType.Display).FontSize);

        var unit = HeroLayout.UnitStep(FluentType.Scaled(FluentType.Display, 2));
        Assert.Equal(2 * FluentType.Title.FontSize, unit.FontSize, 6);
        Assert.False(unit.SemiBold);
    }

    [Fact]
    public void S659_the_minimal_temperature_fills_the_largest_tile()
    {
        var xl = new WidgetArea(800, 516);
        var box = xl.Deflate(WeatherNowLayout.Inset, WeatherNowLayout.VerticalInset);
        var step = WeatherNowStyles.Minimal(xl);
        var used = HeroLayout.ValueWidth(step, WeatherNowStyles.ValueCharacters, WeatherNowStyles.UnitCharacters);

        Assert.True(step.FontSize > FluentType.Display.FontSize * HeroLayout.MaxGrowth);
        Assert.True(used <= box.Width + 0.001 && step.LineHeight <= box.Height + 0.001);
        Assert.True(used >= box.Width * 0.97 || step.LineHeight >= box.Height * 0.97);
    }
}
