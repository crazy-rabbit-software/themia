using System.Net;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Net;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-1300] [S-1301] [S-1304] What the host keeps for the feed widget: the network guard, site icons
/// and the saved cache (the widget's own rules are its module's, <c>RssTests</c>).
/// </summary>
public sealed class FeedWidgetTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 4, 15, 0, 0, TimeSpan.FromHours(2));

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "themia-feed-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    // ---- [S-1301] network guard --------------------------------------------------------------

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.0.0.5")]
    [InlineData("172.16.0.1")]
    [InlineData("172.31.255.255")]
    [InlineData("192.168.1.1")]
    [InlineData("169.254.169.254")]
    [InlineData("0.0.0.0")]
    [InlineData("100.64.0.1")]
    [InlineData("192.0.2.10")]
    [InlineData("198.51.100.1")]
    [InlineData("203.0.113.9")]
    [InlineData("255.255.255.255")]
    [InlineData("240.0.0.1")]
    [InlineData("::1")]
    [InlineData("::")]
    [InlineData("fe80::1")]
    [InlineData("fd12:3456::1")]
    [InlineData("2001:db8::1")]
    [InlineData("::ffff:192.168.0.1")]
    public void S1301_LoopbackPrivateLinkLocalAndReservedAddressesAreBlocked(string address)
        => Assert.True(NetworkGuard.IsBlocked(IPAddress.Parse(address)));

    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("151.101.1.140")]
    [InlineData("172.32.0.1")]
    [InlineData("100.128.0.1")]
    [InlineData("2606:4700::1111")]
    [InlineData("::ffff:8.8.8.8")]
    public void S1301_PublicAddressesPass(string address)
        => Assert.False(NetworkGuard.IsBlocked(IPAddress.Parse(address)));

    [Fact]
    public void S1301_EveryResolvedAddressIsChecked_NotJustTheFirst()
    {
        var (verdict, culprit) = NetworkGuard.Judge("mixed.example", [IPAddress.Parse("8.8.8.8"), IPAddress.Parse("10.1.1.1")], proxied: false, grant: null);

        Assert.Equal(GuardVerdict.Blocked, verdict);
        Assert.Equal(IPAddress.Parse("10.1.1.1"), culprit);
    }

    [Fact]
    public void S3840_AFeedAddressTypedOnTheLocalNetworkIsAllowed()
        => Assert.Equal(GuardVerdict.Allowed,
            NetworkGuard.Judge("192.168.1.1", null, proxied: false, LocalGrants.ForTyped(new Uri("http://192.168.1.1/feed.xml"))).Verdict);

    [Fact]
    public void S1301_AnUnresolvableNameIsRefusedUnlessAProxyResolvesIt()
    {
        Assert.Equal(GuardVerdict.Unresolvable, NetworkGuard.Judge("nowhere.example", null, proxied: false, grant: null).Verdict);
        Assert.Equal(GuardVerdict.Allowed, NetworkGuard.Judge("nowhere.example", null, proxied: true, grant: null).Verdict);
    }

    [Fact]
    public void S1301_BeingProxiedIsNotABypassForAnAddressLiteral()
    {
        Assert.Equal(GuardVerdict.Blocked, NetworkGuard.Judge("127.0.0.1", null, proxied: true, grant: null).Verdict);
        Assert.Equal(GuardVerdict.Blocked, NetworkGuard.Judge("[::1]", null, proxied: true, grant: null).Verdict);
    }

    [Fact]
    public void S1301_ABareNameIsNotReadAsAnAddressShorthand()
        => Assert.False(NetworkGuard.TryLiteral("intranet", out _));

    // ---- [S-1304] site icons -----------------------------------------------------------------

    [Theory]
    [InlineData("https://www.bbc.co.uk/news/world-1?x=1#y", "https://www.bbc.co.uk/favicon.ico")]
    [InlineData("http://blog.example:8080/a/b", "http://blog.example:8080/favicon.ico")]
    public void S1304_TheIconComesFromTheSiteItself(string link, string expected)
        => Assert.Equal(expected, SiteIcons.FaviconOf(link)?.ToString());

    [Theory]
    [InlineData("")]
    [InlineData("ftp://files.example/x")]
    [InlineData("https://user:pass@site.example/")]
    [InlineData("not a link")]
    public void S1304_ALinkWithoutAWebOriginHasNoIcon(string link)
        => Assert.Null(SiteIcons.FaviconOf(link));

    [Fact]
    public void S1304_OnlyBitmapIconsAreDecoded()
    {
        Assert.True(SiteIcons.LooksLikeIcon([0, 0, 1, 0, 1, 0, 16, 16]));
        Assert.True(SiteIcons.LooksLikeIcon([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A]));
        Assert.False(SiteIcons.LooksLikeIcon("<!DOCTYPE html>"u8));
        Assert.False(SiteIcons.LooksLikeIcon("<svg xmlns='x'>"u8));
        Assert.False(SiteIcons.LooksLikeIcon([0, 0]));
    }

    // ---- [S-1300] saved cache ----------------------------------------------------------------

    private static TickerQuote Quote() => new(
        "MSFT", "Microsoft", 412.3, 0.8, "USD", 2,
        [new QuoteBar(1_759_500_000, 410), new QuoteBar(1_759_500_300, 412.3)],
        new TradingSessions(null, new SessionWindow(1_759_498_200, 1_759_521_600), null))
    {
        ExchangeOffset = TimeSpan.FromHours(-4),
        ZoneName = "EDT",
        HasPrePost = true,
    };

    [Fact]
    public void S1300_FeedsAndQuotesRoundTrip()
    {
        var feed = new FeedDocument("World", [new FeedEntry("Headline", "https://a.example/1", Now.AddHours(-1), "Summary")]);

        var json = NetworkCacheStore.Serialize(new NetworkCacheSnapshot(
            [new CachedAnswer<FeedDocument>("https://a.example/f|20", Now.AddMinutes(-30), feed)],
            [new CachedAnswer<TickerQuote>("Listed|MSFT|Day|prepost", Now.AddMinutes(-5), Quote())]));

        var snapshot = NetworkCacheStore.Parse(json, Now)!;

        var restoredFeed = Assert.Single(snapshot.Feeds);
        Assert.Equal("Headline", restoredFeed.Value.Entries[0].Title);
        Assert.Equal("https://a.example/1", restoredFeed.Value.Entries[0].Link);

        var quote = Assert.Single(snapshot.Quotes).Value;
        Assert.Equal(412.3, quote.Price);
        Assert.Equal(2, quote.Bars.Count);
        Assert.Equal(TimeSpan.FromHours(-4), quote.ExchangeOffset);
        Assert.Equal("EDT", quote.ZoneName);
        Assert.True(quote.HasPrePost);
        Assert.NotNull(quote.Sessions.Regular);
    }

    [Fact]
    public void S1300_OldEntriesAndHostileLinksAreNotRestored()
    {
        var feed = new FeedDocument("X", [new FeedEntry("Bad", "javascript:alert(1)", null, string.Empty)]);
        var json = NetworkCacheStore.Serialize(new NetworkCacheSnapshot(
            [
                new CachedAnswer<FeedDocument>("old", Now.AddDays(-30), feed),
                new CachedAnswer<FeedDocument>("new", Now.AddMinutes(-1), feed),
            ],
            []));

        var snapshot = NetworkCacheStore.Parse(json, Now)!;

        var only = Assert.Single(snapshot.Feeds);
        Assert.Equal("new", only.Key);
        Assert.Equal(string.Empty, only.Value.Entries[0].Link);
    }

    [Fact]
    public void S1300_ADocumentThatIsNotJsonIsNotTrusted()
        => Assert.Null(NetworkCacheStore.Parse("{ not json", Now));

    [Fact]
    public void S1300_AnotherSchemaVersionIsNotTrusted()
        => Assert.Null(NetworkCacheStore.Parse("{\"schemaVersion\":99,\"feeds\":[],\"quotes\":[]}", Now));
}
