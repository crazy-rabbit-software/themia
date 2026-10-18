using Themia.Core.Model;
using Themia.Sdk;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-13a] Views measure a settings change against a clone of the config they last acted on, because
/// the settings page edits the live object in place. That only works if a clone really is a separate
/// object: a clone that shared the live instance would make every change look like no change again.
/// </summary>
public sealed class ConfigSnapshotTests
{
    [Fact]
    public void S13a_EditingTheLiveFeedConfigDoesNotReachTheSnapshot()
    {
        var live = new RssConfig { Feeds = [new RssFeed { Url = "https://a.example/feed.xml" }] };
        var snapshot = (RssConfig)live.Clone();

        live.Feeds[0].Url = "https://b.example/feed.xml";
        live.Feeds.Add(new RssFeed { Url = "https://c.example/feed.xml" });

        Assert.Equal(["https://a.example/feed.xml"], snapshot.FeedUrls);
    }

    [Fact]
    public void S13a_EditingTheLiveStocksConfigDoesNotReachTheSnapshot()
    {
        var live = new StocksConfig();
        var before = live.EffectiveSymbol;
        var snapshot = (StocksConfig)live.Clone();

        live.Symbol = before == "MSFT" ? "AAPL" : "MSFT";

        Assert.Equal(before, snapshot.EffectiveSymbol);
        Assert.NotEqual(snapshot.EffectiveSymbol, live.EffectiveSymbol);
    }

    [Fact]
    public void S13a_EditingTheLiveWeatherConfigDoesNotReachTheSnapshot()
    {
        var live = new WeatherNowConfig { Latitude = 47.42, Longitude = 9.37 };
        var snapshot = (WeatherNowConfig)live.Clone();

        live.Latitude = 51.5;
        live.Longitude = -0.12;

        Assert.Equal(47.42, snapshot.Latitude);
        Assert.Equal(9.37, snapshot.Longitude);
    }

    [Theory]
    [MemberData(nameof(EveryKind))]
    public void S13a_EveryConfigClonesToADistinctInstanceOfTheSameType(WidgetId kind)
    {
        var live = WidgetConfig.CreateDefault(kind);
        var snapshot = live.Clone();

        Assert.NotSame(live, snapshot);
        Assert.Equal(live.GetType(), snapshot.GetType());
    }

    public static TheoryData<WidgetId> EveryKind()
    {
        var data = new TheoryData<WidgetId>();
        foreach (var kind in ModuleCatalog.Widgets.Select(w => w.Id).ToArray())
        {
            data.Add(kind);
        }

        return data;
    }
}
