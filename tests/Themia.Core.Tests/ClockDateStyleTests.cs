using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-5300]..[S-5304] The clock's Date style (what was the Date widget, issues #150, #131).</summary>
public class ClockDateStyleTests
{
    private static readonly DateOnly Wednesday = new(2026, 9, 30);

    public static TheoryData<string> Tiers() => [.. WidgetTiers.All.Select(t => t.Name)];

    /// <summary>A tier's content area as the clock lays it out (the card less the clock's inset).</summary>
    private static WidgetArea Box(string tier)
    {
        var t = WidgetTiers.All.Single(x => x.Name == tier);
        return new WidgetArea(t.Width - 32, t.Height - 16);
    }

    [Fact]
    public void S5300_date_is_the_clocks_last_style_with_no_time_and_a_minute_tick()
    {
        var styles = WidgetStyles.For(ClockConfig.Descriptor.Id);
        Assert.Equal("date", styles[^1].Name);
        Assert.Equal((int)ClockStyle.Date, styles[^1].Value);

        var clock = new ClockConfig { Style = ClockStyle.Date, ShowSeconds = true };
        Assert.False(clock.DrawsSeconds);
        Assert.Equal(TimeSpan.FromMinutes(1), clock.TickInterval);
        Assert.Equal(ClockStyle.Date, ((ClockConfig)ConfigJson.Read("Clock", """{"kind":"clock","style":"date","align":"Right"}""")).Style);
    }

    [Theory]
    [MemberData(nameof(Tiers))]
    public void S5301_every_tier_shows_the_day_and_never_more_than_fits(string tier)
    {
        var box = Box(tier);
        var plan = DateLayout.Plan(box);

        Assert.True(plan.Number.FontSize >= FluentType.Subtitle.FontSize, $"{tier}: day {plan.Number.FontSize}");

        if (plan.Face == DateFace.Column)
        {
            var used = plan.Number.LineHeight
                + (plan.Weekday ? plan.WeekdayStep.LineHeight : 0)
                + (plan.Month ? plan.MonthStep.LineHeight : 0);
            Assert.True(used <= box.Height + Disclosure.Tolerance, $"{tier}: {used} > {box.Height}");
        }
        else
        {
            Assert.True(plan.Number.LineHeight <= box.Height + Disclosure.Tolerance, tier);
            Assert.True(plan.TextWidth > 0, tier);
        }
    }

    [Fact]
    public void S5301_a_strip_puts_the_words_beside_the_day_and_a_larger_tile_a_larger_day()
    {
        Assert.Equal(DateFace.Row, DateLayout.Plan(Box("Wide")).Face);
        Assert.Equal(DateFace.Column, DateLayout.Plan(Box("M")).Face);
        Assert.Equal(DateFace.Column, DateLayout.Plan(Box("Tall")).Face);

        var m = DateLayout.Plan(Box("M"));
        var l = DateLayout.Plan(Box("L"));
        var xl = DateLayout.Plan(Box("XL"));
        Assert.True(l.Number.FontSize > m.Number.FontSize);
        Assert.True(xl.Number.FontSize > l.Number.FontSize);

        // [S-666] Concise: the extra room goes to the date itself, both words shown from M up.
        Assert.True(m.Weekday && m.Month);
        Assert.True(xl.Weekday && xl.Month);
    }

    [Fact]
    public void S5302_a_narrow_tile_shortens_the_names()
    {
        var narrow = DateLayout.Plan(Box("XS"));
        Assert.True(narrow.ShortWeekday);
        Assert.True(DateLayout.Plan(new WidgetArea(56, 120)).ShortMonth);

        var wide = DateLayout.Plan(Box("M"));
        Assert.False(wide.ShortWeekday);
        Assert.False(wide.ShortMonth);

        Assert.Equal("Wednesday", DateText.Weekday(Locale.En, Wednesday, shortName: false));
        Assert.Equal("Wed", DateText.Weekday(Locale.En, Wednesday, shortName: true));
        Assert.Equal("30", DateText.Number(Locale.En, Wednesday));
        Assert.Equal("September", DateText.Month(Locale.En, Wednesday, shortName: false));
        Assert.Equal("Sep", DateText.Month(Locale.En, Wednesday, shortName: true));
    }

    [Fact]
    public void S5302_the_words_come_from_the_language_never_from_culture_data()
    {
        Assert.Equal("Mittwoch", DateText.Weekday(Locale.De, Wednesday, shortName: false));
        Assert.Equal("September", DateText.Month(Locale.De, Wednesday, shortName: false));

        // [S-1115] A language with its own digits writes the day in them.
        Assert.Equal(LocaleFormat.Integer(Locale.Ar, 30), DateText.Number(Locale.Ar, Wednesday));
        Assert.NotEqual("30", DateText.Number(Locale.Ar, Wednesday));
    }
}
