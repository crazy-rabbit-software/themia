using Themia.Core.Links;
using Themia.Core.Localization;
using Themia.Core.SystemData;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-11910]..[S-11914] A moment on a history chart: its subject, where it stands, the sample then, its caption.</summary>
public sealed class LinkInstantsTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 12, 14, 5, 0, TimeSpan.FromHours(1));

    [Fact]
    public void S11912_a_reading_and_its_history_have_one_subject()
    {
        Assert.Equal("cpu", LinkInstants.Subject(new SeriesInterest(FeedKey.Create(SystemSource.Cpu), string.Empty)));
        Assert.Equal("ping/example.com", LinkInstants.Subject(new SeriesInterest(FeedKey.Create(SystemSource.Ping, " Example.com "), string.Empty)));
        Assert.Equal(@"diskactivity//c:\", LinkInstants.Subject(new SeriesInterest(FeedKey.Create(SystemSource.DiskActivity), @"C:\")));
        Assert.Equal("stock/msft", LinkInstants.Subject("stock", " MSFT "));
        Assert.Equal("rate/chf/eur", LinkInstants.Subject("rate", "CHF/EUR"));
    }

    [Fact]
    public void S11911_a_moment_and_its_place_on_a_chart_are_one_and_the_same()
    {
        var span = TimeSpan.FromSeconds(60);
        Assert.Equal(Now, LinkInstants.At(102, 2, 102, span, Now));
        Assert.Equal(Now.AddSeconds(-60), LinkInstants.At(2, 2, 102, span, Now));
        Assert.Equal(Now.AddSeconds(-30), LinkInstants.At(52, 2, 102, span, Now));

        // Beyond the ends the pointer stands at the end; a moment outside the span is not on the chart.
        Assert.Equal(Now.AddSeconds(-60), LinkInstants.At(-40, 2, 102, span, Now));
        Assert.Equal(52, LinkInstants.X(Now.AddSeconds(-30), 2, 102, span, Now));
        Assert.Null(LinkInstants.X(Now.AddSeconds(-61), 2, 102, span, Now));
        Assert.Null(LinkInstants.X(Now.AddSeconds(1), 2, 102, span, Now));
        Assert.Null(LinkInstants.X(Now, 5, 5, span, Now));
    }

    [Fact]
    public void S11913_the_value_of_an_even_series_at_a_moment_is_its_nearest_sample_and_none_outside()
    {
        var second = TimeSpan.FromSeconds(1);

        // Sixty samples a second apart, the newest now.
        Assert.Equal(59, LinkInstants.Index(60, second, Now, Now));
        Assert.Equal(29, LinkInstants.Index(60, second, Now, Now.AddSeconds(-30)));
        Assert.Equal(29, LinkInstants.Index(60, second, Now, Now.AddSeconds(-30.4)));
        Assert.Equal(0, LinkInstants.Index(60, second, Now, Now.AddSeconds(-59)));
        Assert.Null(LinkInstants.Index(60, second, Now, Now.AddSeconds(-61)));
        Assert.Null(LinkInstants.Index(60, second, Now, Now.AddSeconds(2)));
        Assert.Null(LinkInstants.Index(0, second, Now, Now));
    }

    [Fact]
    public void S11913_the_value_of_a_timed_series_at_a_moment_is_its_nearest_sample_and_none_outside()
    {
        DateTimeOffset[] times = [Now.AddMinutes(-10), Now.AddMinutes(-5), Now];
        Assert.Equal(1, LinkInstants.Nearest(times, Now.AddMinutes(-6)));
        Assert.Equal(0, LinkInstants.Nearest(times, Now.AddMinutes(-10)));
        Assert.Equal(2, LinkInstants.Nearest(times, Now.AddMinutes(-2)));
        Assert.Null(LinkInstants.Nearest(times, Now.AddMinutes(-11)));
        Assert.Null(LinkInstants.Nearest(times, Now.AddMinutes(1)));
        Assert.Null(LinkInstants.Nearest([], Now));
    }

    [Fact]
    public void S11913_a_daily_rate_at_a_moment_is_its_days_and_none_outside_them()
    {
        var days = new[] { new DateOnly(2026, 11, 10), new DateOnly(2026, 11, 11), new DateOnly(2026, 11, 12) };
        var rate = new PairRate("CHF", "EUR", 1.07, null, [1.05, 1.06, 1.07], days[2], days);
        Assert.Equal((1.06, days[1]), rate.At(new DateTimeOffset(2026, 11, 11, 9, 0, 0, TimeSpan.Zero)));
        Assert.Equal((1.07, days[2]), rate.At(new DateTimeOffset(2026, 11, 12, 23, 0, 0, TimeSpan.Zero)));
        Assert.Null(rate.At(new DateTimeOffset(2026, 11, 9, 9, 0, 0, TimeSpan.Zero)));
        Assert.Null(rate.At(new DateTimeOffset(2026, 11, 14, 9, 0, 0, TimeSpan.Zero)));
        Assert.Null((rate with { Days = null }).At(Now));
    }

    [Fact]
    public void S11914_the_caption_says_how_long_ago_or_when()
    {
        Assert.Equal("32 s ago", LinkInstants.Caption(Locale.En, Now.AddSeconds(-32), Now));
        Assert.Equal("4 min ago", LinkInstants.Caption(Locale.En, Now.AddMinutes(-4.5), Now));
        Assert.Equal(LocaleFormat.Time(Locale.En, new TimeOnly(11, 5)), LinkInstants.Caption(Locale.En, Now.AddHours(-3), Now));
        Assert.Equal(
            $"{Formatting.DayHeading(Locale.En, new DateOnly(2026, 11, 11), new DateOnly(2026, 11, 12))}, {LocaleFormat.Time(Locale.En, new TimeOnly(14, 5))}",
            LinkInstants.Caption(Locale.En, Now.AddDays(-1), Now, InstantCaption.DayAndTime));
        Assert.Equal(Formatting.DayHeading(Locale.En, new DateOnly(2026, 11, 12), new DateOnly(2026, 11, 12)), LinkInstants.Caption(Locale.En, Now, Now, InstantCaption.Day));
    }
}
