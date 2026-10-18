using System.Buffers.Binary;
using System.Net;
using System.Text;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Tests;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Widget.PingDetails.Tests;

/// <summary>[S-2427]..[S-2429] The ping details widget: its window and the statistics over it. Moved with the widget from Core's SystemDetailsTests, unchanged in what they assert.</summary>
public sealed class PingDetailsTests
{
    [Fact]
    public void S2427_the_ping_window_is_clamped()
    {
        var config = new PingDetailsConfig { Window = 1 };

        Assert.Single(config.Repair());
        Assert.Equal(PingDetailsConfig.MinWindow, config.Window);
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

        var sheet = PingDetailRules.Sheet(Locale.En, window.Stats(), "1.1.1.1", PingStatus.Offline);

        Assert.False(sheet.HeroAvailable);
        Assert.Equal("Offline", sheet.Caption);
        Assert.Equal("1.1.1.1 · Offline", sheet.Note);
        Assert.Contains(sheet.Facts, f => f.Label == "Packet loss" && f.Value == "100%");
    }
}
