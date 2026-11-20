using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Widget.Date.Tests;

/// <summary>[S-10800]..[S-10804] The date widget's words, sizes and fill.</summary>
public sealed class DateTests
{
    private static readonly DateOnly Day = new(2026, 11, 17);

    [Fact]
    public void S10800_a_clone_keeps_the_style_and_there_is_no_size()
    {
        var config = new DateConfig { Style = DateWidgetStyle.Page };
        var copy = Assert.IsType<DateConfig>(config.Clone());
        Assert.NotSame(config, copy);
        Assert.Equal(DateWidgetStyle.Page, copy.Style);
        Assert.Null(typeof(DateConfig).GetProperty("Size"));
    }

    [Fact]
    public void S10801_each_style_says_the_day_through_the_languages_format()
    {
        Assert.Equal("Tuesday, November 17", DateLayout.Line(DateWidgetStyle.Long, Locale.En, Day));
        Assert.Equal(LocaleFormat.Date(Locale.En, Day, DateStyle.WeekdayMonthDayShort), DateLayout.Line(DateWidgetStyle.Short, Locale.En, Day));
        Assert.Equal(LocaleFormat.Date(Locale.En, Day, DateStyle.Short), DateLayout.Line(DateWidgetStyle.Numeric, Locale.En, Day));
        Assert.Equal(LocaleFormat.Date(Locale.De, Day, DateStyle.Short), DateLayout.Line(DateWidgetStyle.Numeric, Locale.De, Day));
        Assert.Equal(("Tuesday", "17", "November"), DateLayout.Page(Locale.En, Day));
        Assert.Equal(("Dienstag", "17", "November"), DateLayout.Page(Locale.De, Day));
    }

    [Theory]
    [InlineData(DateWidgetStyle.Long)]
    [InlineData(DateWidgetStyle.Short)]
    [InlineData(DateWidgetStyle.Numeric)]
    [InlineData(DateWidgetStyle.Page)]
    public void S10802_the_natural_card_holds_the_languages_widest_day_and_scales_down_to_its_floor(DateWidgetStyle style)
    {
        var sizing = DateLayout.Sizing(new DateConfig { Style = style }, Locale.En);
        var medium = sizing.Natural;
        Assert.Empty(sizing.Problems());

        // [S-12833] The card's size is the user's ([S-2449] retired): down to its main reading at 9 (the test below).
        Assert.True(sizing.Min.Height < medium.Height && sizing.Min.Width < medium.Width);

        // The widest day of the two years measured fits the face: a long September Wednesday, every day of the month.
        var face = DateLayout.Face(new DateConfig { Style = style }, Locale.En);
        Assert.Equal(medium.Width, face.Width + (2 * FamilySpace.Inset));
        for (var i = 0; i < 366; i++)
        {
            var day = new DateOnly(2026, 1, 1).AddDays(i);
            var text = style == DateWidgetStyle.Page ? DateLayout.Page(Locale.En, day).Weekday : DateLayout.Line(style, Locale.En, day);
            var step = style == DateWidgetStyle.Page ? FamilyType.Under(DateLayout.NumberStep) : DateLayout.LineStep;
            Assert.True(HeroFit.Ems(text) * step.FontSize <= face.Width + 0.5, $"{style}: {text}");
        }
    }

    [Fact]
    public void S10802_the_page_is_its_number_and_a_line_over_and_under_it()
    {
        var face = DateLayout.Face(new DateConfig { Style = DateWidgetStyle.Page }, Locale.En);
        var number = FamilyType.Hero68;
        Assert.Equal(number.LineHeight + (2 * FamilyType.Under(number).LineHeight), face.Height);
    }

    [Theory]
    [InlineData(DateWidgetStyle.Long)]
    [InlineData(DateWidgetStyle.Short)]
    [InlineData(DateWidgetStyle.Numeric)]
    [InlineData(DateWidgetStyle.Page)]
    public void S10802_S12833_the_date_scales_down_to_its_main_reading_at_the_floor_in_the_inset(DateWidgetStyle style)
    {
        // Owner, 2026-11-19: "for the widgets that can scale there should basically be no lower limit except for one that
        // makes sense so you can still see the widget". Before, the page's weekday and month at 9 held its number large
        // (101 tall); now it goes down to its number alone at 9.
        var config = new DateConfig { Style = style };
        var sizing = DateLayout.Sizing(config, Locale.En);
        var main = DateLayout.PartSizes(config).Max();
        var alone = DateLayout.Plan(config, Locale.En, main);
        Assert.Equal(Math.Max(WidgetSizing.LeastScaled, Math.Ceiling(alone.Width * ScaleFloor.Text / main) + (2 * FamilySpace.Inset)), sizing.Min.Width, 6);
        Assert.Equal(WidgetSizing.LeastScaled, sizing.Min.Height);

        // At the natural box every part is drawn; at the least the main reading alone, at the floor.
        var natural = DateLayout.Fit(sizing.Natural.Width - 24, sizing.Natural.Height - 24, config, Locale.En);
        Assert.All(DateLayout.PartSizes(config), part => Assert.True(natural.Draws(part)));
        var least = DateLayout.Fit(sizing.Min.Width - 24, sizing.Min.Height - 24, config, Locale.En);
        Assert.All(DateLayout.PartSizes(config).Where(size => size < main), size => Assert.False(least.Draws(size)));
        Assert.True(least.Draws(main));
        Assert.False(DateLayout.Lines(config, least.Floor));
        Assert.InRange(least.Scale * main, ScaleFloor.Text - 1e-6, ScaleFloor.Text + 3);
    }

    [Fact]
    public void S10802_S12833_the_pages_weekday_and_month_give_way_together_and_the_number_only_shrinks()
    {
        var config = new DateConfig { Style = DateWidgetStyle.Page };
        var sizing = DateLayout.Sizing(config, Locale.En);
        double lastScale = double.PositiveInfinity;
        var lines = true;
        for (var t = 1.0; t >= 0; t -= 0.02)
        {
            var width = sizing.Min.Width + (t * (sizing.Natural.Width - sizing.Min.Width)) - 24;
            var height = sizing.Min.Height + (t * (sizing.Natural.Height - sizing.Min.Height)) - 24;
            var fit = DateLayout.Fit(width, height, config, Locale.En);
            var drawn = DateLayout.Lines(config, fit.Floor);

            Assert.True(fit.Scale <= lastScale + 1e-9, $"{width}x{height}: {fit.Scale} over {lastScale}");
            Assert.True(lines || !drawn, $"{width}x{height}: the lines came back");
            Assert.True(!drawn || FamilyType.Under(DateLayout.NumberStep).FontSize * fit.Scale >= ScaleFloor.Text - 1e-6);
            Assert.Equal(drawn ? DateLayout.Face(config, Locale.En) : new SizeDip(Math.Ceiling(HeroFit.Ems("88") * DateLayout.NumberStep.FontSize), DateLayout.NumberStep.LineHeight), DateLayout.Face(config, Locale.En, fit.Floor));
            lastScale = fit.Scale;
            lines = drawn;
        }

        Assert.False(lines);
    }
}
