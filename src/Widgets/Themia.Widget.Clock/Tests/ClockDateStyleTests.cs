using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Core.Tests;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Widget.Clock.Tests;

/// <summary>[S-5300]..[S-5304] The clock's Date style (what was the Date widget, issues #150, #131).</summary>
public class ClockDateStyleTests
{
    private static readonly DateOnly Wednesday = new(2026, 9, 30);

    public static TheoryData<string> Tiers() => [.. WidgetTiers.All.Select(t => t.Name)];

    /// <summary>A tier as the clock lays it out: the card's area, and its content box (the card less its inset of 16 on every side).</summary>
    private static (WidgetArea Area, double Width, double Height) Box(string tier)
    {
        var t = WidgetTiers.All.Single(x => x.Name == tier);
        return (new WidgetArea(t.Width, t.Height), t.Width - 32, t.Height - 32);
    }

    private static ClockDatePlan Plan(string tier)
    {
        var (area, width, height) = Box(tier);
        return ClockFace.Date(area, width, height);
    }

    [Fact]
    public void S5300_date_is_the_clocks_last_style_with_no_time_and_a_minute_tick()
    {
        var styles = WidgetStyles.For(new WidgetId("clock"));
        Assert.Equal("date", styles[^1].Name);
        Assert.Equal((int)ClockStyle.Date, styles[^1].Value);

        var clock = new ClockConfig { Style = ClockStyle.Date, ShowSeconds = true };
        Assert.False(clock.DrawsSeconds);
        Assert.Equal(TimeSpan.FromMinutes(1), clock.TickInterval);
        Assert.Equal(ClockStyle.Date, ((ClockConfig)ConfigJson.Read("Clock", """{"kind":"clock","style":"date","align":"Right"}""")).Style);
    }

    [Theory]
    [MemberData(nameof(Tiers))]
    public void S6744_every_tier_shows_the_day_on_a_family_step_and_never_more_than_fits(string tier)
    {
        var (_, width, height) = Box(tier);
        var plan = Plan(tier);
        var line = FamilyType.Under(plan.Step);

        Assert.True(FamilyType.BaseOf(plan.Step) >= 28, $"{tier}: day {plan.Step.FontSize}");
        Assert.Contains(FamilyType.BaseOf(plan.Step), new double[] { 28, 40, 68, 96, 136, 192 });

        if (plan.Face == DateFace.Column)
        {
            var used = plan.Step.LineHeight + (plan.Weekday ? line.LineHeight : 0) + (plan.Month ? line.LineHeight : 0);
            Assert.True(used <= height, $"{tier}: {used} > {height}");
        }
        else
        {
            Assert.True(plan.Step.LineHeight <= height, tier);
            Assert.True(plan.WordsWidth > 0 && plan.WordsWidth < width, tier);
        }
    }

    [Fact]
    public void S6744_a_strip_puts_the_words_beside_the_day_and_a_larger_tile_a_larger_step()
    {
        Assert.Equal(DateFace.Row, Plan("Wide").Face);
        Assert.Equal(DateFace.Column, Plan("M").Face);
        Assert.Equal(DateFace.Column, Plan("Tall").Face);

        var m = Plan("M");
        var l = Plan("L");
        var xl = Plan("XL");
        Assert.True(l.Step.FontSize > m.Step.FontSize);
        Assert.True(xl.Step.FontSize >= l.Step.FontSize);

        // [S-666] Concise: the extra room goes to the date itself, both words shown from M up.
        Assert.True(m.Weekday && m.Month);
        Assert.True(xl.Weekday && xl.Month);
    }

    [Fact]
    public void S5302_a_narrow_tile_shortens_the_names()
    {
        Assert.True(Plan("XS").ShortWeekday);
        Assert.True(ClockFace.Date(new WidgetArea(88, 152), 56, 120).ShortMonth);

        var wide = Plan("M");
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
