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

    [Fact]
    public void S5302_the_names_have_a_wide_and_a_short_form()
    {
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
