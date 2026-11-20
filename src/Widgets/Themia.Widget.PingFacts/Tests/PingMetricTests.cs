using System.Buffers.Binary;
using System.Net;
using System.Text;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.SystemData;
using Themia.Core.Tests;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Widget.PingFacts.Tests;

/// <summary>[S-2427]..[S-2429] [S-14650] The ping statistics: the window, the statistics over it, a statistic a card.</summary>
public sealed class PingMetricTests
{
    [Fact]
    public void S2427_the_ping_window_is_clamped()
    {
        var config = new PingAverageConfig { Window = 1 };

        Assert.Single(config.Repair());
        Assert.Equal(PingMetricConfig.MinWindow, config.Window);
    }

    [Fact]
    public void S2428_statistics_over_the_window()
    {
        var window = new PingWindow(5);
        foreach (var probe in new double?[] { 10, 20, null, 14, 30 })
        {
            window.Add(probe);
        }

        var stats = window.Stats();

        Assert.Equal((5, 4), (stats.Sent, stats.Answered));
        Assert.Equal((10.0, 18.5, 30.0), (stats.Min!.Value, stats.Average!.Value, stats.Max!.Value));
        Assert.Equal((10 + 6 + 16) / 3.0, stats.Jitter!.Value, 6);
        Assert.Equal(0.2, stats.Loss);
    }

    [Fact]
    public void S2427_the_window_drops_its_oldest_probe_and_one_answer_has_no_jitter()
    {
        var window = new PingWindow(2);
        window.Add(null);
        window.Add(5);
        window.Add(double.NaN);

        var stats = window.Stats();
        Assert.Equal((2, 1), (stats.Sent, stats.Answered));
        Assert.Null(stats.Jitter);
        Assert.Null(stats.Last);
        Assert.Equal(new PingWindowStats(0, 0, null, null, null, null, null, null), new PingWindow(3).Stats());
    }

    [Fact]
    public void S2429_a_lost_probe_dims_the_hero_and_says_why()
    {
        var window = new PingWindow(10);
        window.Add(null);

        var sheet = PingDetailRules.Sheet(Locale.En, window.Stats(), PingStatus.Offline);

        Assert.False(sheet.HeroAvailable);
        Assert.Equal("Offline", sheet.Caption);
        Assert.Equal("Offline", sheet.Note); // [S-12801] the host is the Header widget's to name
        Assert.Contains(sheet.Facts, f => f.Label == "Packet loss" && f.Value == "100%");
    }

    [Fact]
    public void S14650_each_statistic_is_a_card_reading_the_host_s_prober_and_answered_draws_no_trend()
    {
        var average = new PingAverageConfig { Host = " 1.1.1.1 " };
        Assert.True(SystemInterests.Chart(FeedKey.Create(SystemSource.Ping, "1.1.1.1")).SameAs(average.SystemWants(@"C:\")));
        Assert.Equal(MetricHistory.Series, average.History);
        Assert.Equal(MetricHistory.None, new PingAnsweredConfig().History);
        Assert.Equal(CardOpenKind(), PingMetricModule.Opens!.Value);

        var copy = Assert.IsType<PingAverageConfig>(new PingAverageConfig { Host = "9.9.9.9", Window = 120, Trend = MetricTrend.Line }.Clone());
        Assert.Equal(("9.9.9.9", 120, MetricTrend.Line), (copy.Host, copy.Window, copy.Trend));
    }

    [Fact]
    public void S14650_its_menu_is_the_host_the_window_then_the_trend()
    {
        var menu = new Themia.Sdk.WinUI.WidgetMenuLines(() => { });
        ((Themia.Sdk.WinUI.IWidgetModule)new PingJitterModule()).MenuLines(menu, new PingJitterConfig());

        Assert.Equal(3, menu.Lines.Count);
        Assert.Equal(Themia.Core.Localization.Str.Metric_Trend.Text(), menu.Lines[^1].Text);
    }

    private static string CardOpenKind() => "ms-settings:network-status";
}
