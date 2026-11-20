using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.SystemData;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.NetworkFacts.Tests;

/// <summary>[S-2417] [S-14640] The connection's facts, a single metric each: the public address asks only for its own card.</summary>
public sealed class NetworkMetricTests
{
    [Fact]
    public void S2417_only_the_public_address_s_card_asks_the_public_service_and_its_page_says_whom()
    {
        Assert.DoesNotContain(FeedKey.Create(SystemSource.PublicAddress), SystemInterests.For(new NetworkLocalAddressConfig(), @"C:\").Feeds);
        Assert.Contains(FeedKey.Create(SystemSource.PublicAddress), SystemInterests.For(new NetworkPublicAddressConfig(), @"C:\").Feeds);

        var menu = new WidgetMenuLines(() => { });
        ((IWidgetModule)new NetworkPublicAddressModule()).MenuLines(menu, new NetworkPublicAddressConfig());
        Assert.True(Assert.Single(menu.Lines).Note);

        var local = new WidgetMenuLines(() => { });
        ((IWidgetModule)new NetworkLocalAddressModule()).MenuLines(local, new NetworkLocalAddressConfig());
        Assert.Empty(local.Lines);
    }

    [Fact]
    public void S14640_a_rate_s_line_is_the_throughput_s_series_and_the_totals_draw_no_trend()
    {
        Assert.Empty(SystemInterests.For(new NetworkDownloadConfig(), @"C:\").Series);
        Assert.Equal(SystemInterests.Chart(SystemSource.Network).Series, SystemInterests.For(new NetworkUploadConfig { Trend = MetricTrend.Line }, @"C:\").Series);
        Assert.Equal(MetricHistory.Series, new NetworkDownloadConfig().History);
        Assert.Equal(MetricHistory.None, new NetworkReceivedConfig().History);
    }

    [Fact]
    public void S14640_the_rates_carry_their_figure_for_the_arrow()
    {
        var sample = new ThroughputSample(1_200_000, 30_000, [], 5_000_000, 700_000);
        var sheet = NetworkDetailRules.Sheet(Locale.En, sample, new Dictionary<string, string>(), new PublicAddressReading(PublicAddressState.Off));

        Assert.Equal(1_200_000d, MetricRules.Pick(sheet, "download").Value.Number);
        Assert.Equal(30_000d, MetricRules.Pick(sheet, "upload").Value.Number);
        Assert.Equal(MetricValue.Unread, MetricRules.Pick(sheet, "publicAddress").Value);
    }

    [Fact]
    public void S2019_a_click_opens_its_settings_page()
        => Assert.All(NetworkKinds.All, kind => Assert.Equal("ms-settings:network-status", kind.Opens!.Target(kind.Create())!.Value));
}
