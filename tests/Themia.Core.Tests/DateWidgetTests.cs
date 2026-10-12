using System.Text.Json;
using Themia.Core.Accounts;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-5300]..[S-5304] The Date widget (issues #150, #131).</summary>
public class DateWidgetTests
{
    private static readonly DateOnly Wednesday = new(2026, 9, 30);

    /// <summary>The seven tiers' content areas as the view lays them out (the card less the clock's inset).</summary>
    public static TheoryData<string> Tiers() => [.. WidgetTiers.All.Select(t => t.Name)];

    private static WidgetArea Box(string tier)
    {
        var t = WidgetTiers.All.Single(x => x.Name == tier);
        return new WidgetArea(t.Width - 32, t.Height - 16);
    }

    [Fact]
    public void S5300_the_config_round_trips_starts_headerless_and_needs_no_setup()
    {
        var config = new DateConfig { Align = ClockAlign.Left, ShowYear = true };
        var json = JsonSerializer.Serialize<WidgetConfig>(config, AppStateJsonContext.Default.WidgetConfig);
        Assert.Contains("\"date\"", json, StringComparison.Ordinal);

        var back = Assert.IsType<DateConfig>(JsonSerializer.Deserialize(json, AppStateJsonContext.Default.WidgetConfig));
        Assert.Equal(ClockAlign.Left, back.Align);
        Assert.True(back.ShowYear);
        Assert.Empty(back.Repair());
        Assert.True(Assert.IsType<DateConfig>(back.Clone()).ShowYear);

        Assert.Equal(WidgetKind.Date, WidgetConfig.CreateDefault(WidgetKind.Date).Kind);
        Assert.Equal(HeaderPlacement.Hidden, WidgetInstance.DefaultHeader(WidgetKind.Date));
        Assert.True(WidgetInstance.CanHaveHeader(WidgetKind.Date));
        Assert.False(WidgetSetup.Needed(new DateConfig(), AccountFeatures.None));
        Assert.Equal(TimeSpan.FromMinutes(1), new DateConfig().TickInterval);
    }

    [Theory]
    [MemberData(nameof(Tiers))]
    public void S5301_every_tier_shows_the_day_and_never_more_than_fits(string tier)
    {
        var box = Box(tier);
        var plan = DateLayout.Plan(box, showYear: true);

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
        Assert.Equal(DateFace.Row, DateLayout.Plan(Box("Wide"), false).Face);
        Assert.Equal(DateFace.Column, DateLayout.Plan(Box("M"), false).Face);
        Assert.Equal(DateFace.Column, DateLayout.Plan(Box("Tall"), false).Face);

        var m = DateLayout.Plan(Box("M"), false);
        var l = DateLayout.Plan(Box("L"), false);
        var xl = DateLayout.Plan(Box("XL"), false);
        Assert.True(l.Number.FontSize > m.Number.FontSize);
        Assert.True(xl.Number.FontSize > l.Number.FontSize);

        // [S-666] Concise: the extra room goes to the date itself, both words shown from M up.
        Assert.True(m.Weekday && m.Month);
        Assert.True(xl.Weekday && xl.Month);
    }

    [Fact]
    public void S5302_a_narrow_tile_shortens_the_names_and_drops_the_year_with_the_month()
    {
        var narrow = DateLayout.Plan(Box("XS"), showYear: true);
        Assert.True(narrow.ShortWeekday);
        Assert.True(narrow.ShortMonth);

        var wide = DateLayout.Plan(Box("M"), showYear: true);
        Assert.False(wide.ShortWeekday);
        Assert.False(wide.ShortMonth);

        Assert.Equal("Wednesday", DateText.Weekday(Locale.En, Wednesday, shortName: false));
        Assert.Equal("Wed", DateText.Weekday(Locale.En, Wednesday, shortName: true));
        Assert.Equal("30", DateText.Number(Locale.En, Wednesday));
        Assert.Equal("September", DateText.Month(Locale.En, Wednesday, shortName: false, showYear: false));
        Assert.Contains("2026", DateText.Month(Locale.En, Wednesday, shortName: false, showYear: true), StringComparison.Ordinal);
        Assert.DoesNotContain("2026", DateText.Month(Locale.En, Wednesday, shortName: true, showYear: true), StringComparison.Ordinal);
    }

    [Fact]
    public void S5302_the_words_come_from_the_language_never_from_culture_data()
    {
        Assert.Equal("Mittwoch", DateText.Weekday(Locale.De, Wednesday, shortName: false));
        Assert.Equal("September", DateText.Month(Locale.De, Wednesday, shortName: false, showYear: false));

        // [S-1115] A language with its own digits writes the day in them.
        Assert.Equal(LocaleFormat.Integer(Locale.Ar, 30), DateText.Number(Locale.Ar, Wednesday));
        Assert.NotEqual("30", DateText.Number(Locale.Ar, Wednesday));
    }

    [Fact]
    public void S5303_an_unknown_alignment_is_centred_and_said()
    {
        var config = new DateConfig { Align = (ClockAlign)9 };
        Assert.Single(config.Repair());
        Assert.Equal(ClockAlign.Center, config.Align);
    }

    [Fact]
    public void S5300_a_saved_date_widget_reads_back_and_a_wrong_config_is_replaced_by_its_own()
    {
        Assert.IsType<DateConfig>(ConfigJson.Read("Date", """{"kind":"date","align":"Right","showYear":true}"""));
        Assert.IsType<DateConfig>(ConfigJson.Read("Date", """{"kind":"clock"}"""));
    }
}
