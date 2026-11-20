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

        // [S-12833] The card's size is the user's ([S-2449] retired): down until its smallest text is at 9.
        Assert.Equal(Math.Max(WidgetSizing.LeastScaled, Math.Ceiling(medium.Height * 9 / DateLayout.SmallestText(new DateConfig { Style = style }))), sizing.Min.Height);

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
}
