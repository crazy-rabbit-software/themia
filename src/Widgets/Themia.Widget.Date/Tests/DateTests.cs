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
    /// <summary>
    /// [S-12833] The face as its sizing states it (<see cref="DateLayout.Plan"/>, estimated) in a content box: what the least
    /// box is made of. The view decides on the face as drawn (<c>ReadingFit</c>).
    /// </summary>
    private static FaceFit Fit(double width, double height, DateConfig config, Locale locale)
        => BlockLayout.Decide(width, height, DateLayout.PartSizes(config), floor => DateLayout.Plan(config, locale, floor));

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
        var natural = Fit(sizing.Natural.Width - 24, sizing.Natural.Height - 24, config, Locale.En);
        Assert.All(DateLayout.PartSizes(config), part => Assert.True(natural.Draws(part)));
        var least = Fit(sizing.Min.Width - 24, sizing.Min.Height - 24, config, Locale.En);
        Assert.All(DateLayout.PartSizes(config).Where(size => size < main), size => Assert.False(least.Draws(size)));
        Assert.True(least.Draws(main));
        Assert.False(DateLayout.Lines(config, least.Floor));
        Assert.InRange(least.Scale * main, ScaleFloor.Text - 1e-6, ScaleFloor.Text + 3);
    }

    [Fact]
    public void S15813_the_page_scales_down_as_one_its_weekday_and_month_kept_at_every_size()
    {
        // WR-7 (owner, 2026-11-19: "Keep them together"; "All together, scaled"): at 350x70 the page had dropped its
        // weekday and month and drawn the "22" alone, large. Now its least card is the whole page with its lines at 9.
        var config = new DateConfig { Style = DateWidgetStyle.Page };
        var sizing = DateLayout.Sizing(config, Locale.En);
        Assert.Equal([FamilyType.Under(DateLayout.NumberStep).FontSize], DateLayout.PartSizes(config));
        var page = DateLayout.Plan(config, Locale.En, 0);
        var floor = ScaleFloor.Text / FamilyType.Under(DateLayout.NumberStep).FontSize;
        // Whole DIPs, rounded up.
        Assert.InRange(sizing.Min.Width, (page.Width * floor) + (2 * FamilySpace.Inset), (page.Width * floor) + (2 * FamilySpace.Inset) + 1);
        Assert.InRange(sizing.Min.Height, (page.Ink * floor) + (2 * FamilySpace.Inset), (page.Ink * floor) + (2 * FamilySpace.Inset) + 1);

        for (var t = 1.0; t >= 0; t -= 0.02)
        {
            var width = sizing.Min.Width + (t * (sizing.Natural.Width - sizing.Min.Width)) - 24;
            var height = sizing.Min.Height + (t * (sizing.Natural.Height - sizing.Min.Height)) - 24;
            var fit = Fit(width, height, config, Locale.En);
            Assert.True(DateLayout.Lines(config, fit.Floor), $"{width}x{height}: the lines went");
            Assert.True(FamilyType.Under(DateLayout.NumberStep).FontSize * fit.Scale >= ScaleFloor.Text - 1e-6, $"{width}x{height}");
        }
    }
    [Fact]
    public void S15804_every_style_is_held_to_its_widest_day_so_the_face_never_changes_size_with_the_day()
    {
        // WR-15 (owner, 2026-11-20): a running reading never moves. At 388x60 "Sunday, November 22" stood 36 tall and
        // "Wednesday, September 30" 29.4: each day was its own box. The held text is one of the days, and none is wider.
        foreach (var locale in new[] { Locale.En, Locale.De, Locale.Ja })
        {
            foreach (var style in new[] { DateWidgetStyle.Long, DateWidgetStyle.Short, DateWidgetStyle.Numeric })
            {
                var held = DateLayout.HeldLine(style, locale);
                Assert.False(string.IsNullOrEmpty(held));
                for (var day = new DateOnly(2026, 1, 1); day.Year == 2026; day = day.AddDays(1))
                {
                    Assert.True(HeroFit.Ems(DateLayout.Line(style, locale, day)) <= HeroFit.Ems(held) + 1e-9, $"{locale} {style} {day}");
                }
            }

            var page = DateLayout.HeldPage(locale);
            for (var day = new DateOnly(2026, 1, 1); day.Year == 2026; day = day.AddDays(1))
            {
                var (weekday, number, month) = DateLayout.Page(locale, day);
                Assert.True(HeroFit.Ems(weekday) <= HeroFit.Ems(page.Weekday) + 1e-9);
                Assert.True(HeroFit.Ems(number) <= HeroFit.Ems(page.Number) + 1e-9);
                Assert.True(HeroFit.Ems(month) <= HeroFit.Ems(page.Month) + 1e-9);
            }
        }
    }
}