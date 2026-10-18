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
/// [S-1300]..[S-1306] The feed widget: the network guard, merging several feeds, site icons, the
/// ladder and the saved cache.
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

    // ---- [S-1302] several feeds --------------------------------------------------------------

    private static FeedEntry Entry(string title, DateTimeOffset? at, string link = "https://a.example/x")
        => new(title, link, at, string.Empty);

    [Fact]
    public void S1302_FeedsMergeNewestFirst_UndatedLast_Capped()
    {
        var a = new FeedDocument("A", [Entry("a1", Now.AddHours(-1)), Entry("a-undated", null), Entry("a2", Now.AddHours(-5))]);
        var b = new FeedDocument("B", [Entry("b1", Now.AddMinutes(-10)), Entry("b-undated", null), Entry("b2", Now.AddHours(-3))]);

        var merged = FeedAggregate.Merge(
            [new FeedPart("https://a.example/f", a, FetchStatus.Ok, null), new FeedPart("https://b.example/f", b, FetchStatus.Ok, null)],
            cap: 5);

        Assert.Equal(["b1", "a1", "b2", "a2", "a-undated"], merged.Entries.Select(e => e.Title));
    }

    [Fact]
    public void S1302_issue_81_a_widget_set_to_50_items_keeps_50_of_a_longer_feed()
    {
        // Classic issue #81: 50 items configured, 20 shown. Neither the parser nor the merge caps
        // below the setting; the configured maximum itself is clamped to 5..50.
        var xml = "<rss version=\"2.0\"><channel><title>Long</title>"
            + string.Concat(Enumerable.Range(0, 60).Select(i => $"<item><title>i{i}</title><link>https://a.example/{i}</link></item>"))
            + "</channel></rss>";
        var document = FeedParser.Parse(xml, 50).Document;
        Assert.Equal(50, document.Entries.Count);

        var config = new Themia.Core.Model.RssConfig { MaxItems = 50 };
        var merged = FeedAggregate.Merge([new FeedPart("https://a.example/f", document, FetchStatus.Ok, null)], config.EffectiveMaxItems);
        Assert.Equal(50, merged.Entries.Count);
    }

    [Fact]
    public void S1302_APartialFailureIsNamedAfterItsFeed()
    {
        var a = new FeedDocument("Tech News", [Entry("a1", Now)]);

        FeedPart[] parts =
        [
            new FeedPart("https://a.example/f", a, FetchStatus.Ok, null),
            new FeedPart("https://www.broken.example/rss", null, FetchStatus.BadResponse, "HTTP 403 Forbidden"),
        ];
        var merged = FeedAggregate.Merge(parts, cap: 20);

        Assert.Single(merged.Entries);
        Assert.Equal(["broken.example: HTTP 403 Forbidden"], KitScreens.Decide(Locale.En, null, [], FeedAggregate.Outcomes(parts), merged.Entries.Count, "none").HeaderProblems);
    }

    [Fact]
    public void S1302_TitleIsTheSingleFeedsOwn_OrTheNamesJoined()
    {
        var one = new FeedPart("https://a.example/f", new FeedDocument("World News", [Entry("x", Now)]), FetchStatus.Ok, null);
        var two = new FeedPart("https://www.b.example/f", null, FetchStatus.Loading, null);

        Assert.Equal("World News", FeedAggregate.Title([one]));
        Assert.Equal("World News, b.example", FeedAggregate.Title([one, two]));
        Assert.Equal(string.Empty, FeedAggregate.Title([]));
    }

    [Fact]
    public void S1302_RepeatedAndBlankAddressesAreFolded_RefusedOnesAreNotFetched()
    {
        var config = new RssConfig
        {
            Feeds =
            [
                new RssFeed { Url = "https://a.example/feed" },
                new RssFeed { Url = " " },
                new RssFeed { Url = "https://a.example/feed" },
                new RssFeed { Url = "javascript:alert(1)" },
                new RssFeed { Url = "https://b.example/feed" },
            ],
        };

        Assert.Equal(3, config.ResolvedFeeds.Count);
        Assert.Equal(["https://a.example/feed", "https://b.example/feed"], config.FeedUrls);
    }

    [Fact]
    public void S1302_AHandEditedListIsRepairedToTheCap()
    {
        var config = new RssConfig { Feeds = [.. Enumerable.Range(0, 20).Select(i => new RssFeed { Url = $"https://f{i}.example/" })] };

        var repairs = config.Repair();

        Assert.Equal(RssConfig.MaxFeeds, config.Feeds.Count);
        Assert.NotEmpty(repairs);
    }

    [Theory]
    [InlineData(-86400 * 8, "Sep 26")]
    [InlineData(-86400 * 400, "Aug 30, 2025")]
    public void S1302_ItemAgeIsRelativeForAWeek_ThenADate(int seconds, string expected)
        => Assert.Equal(expected, KitText.Age(Locale.En, Now.AddSeconds(seconds), Now));

    [Fact]
    public void S1302_ItemAgeUnderAWeekIsTheSharedFreshnessAndDatesAreTheLanguages()
    {
        Assert.Equal(FetchState.Freshness(Locale.En, Now.AddSeconds(-300), Now), KitText.Age(Locale.En, Now.AddSeconds(-300), Now));
        Assert.Equal("26. Sept.", KitText.Age(Locale.De, Now.AddDays(-8), Now));
        Assert.Equal("30.08.2025", KitText.Age(Locale.De, Now.AddDays(-400), Now));
    }

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

    // ---- [S-1305] the ladder -----------------------------------------------------------------

    // The size tiers as content slots (the card less a 44px header).
    public static TheoryData<string, double, double> Tiers => new()
    {
        { "XS", 120, 76 },
        { "S", 200, 106 },
        { "M", 320, 196 },
        { "Wide", 480, 116 },
        { "Tall", 200, 356 },
        { "L", 560, 376 },
        { "XL", 800, 516 },
    };

    [Theory]
    [MemberData(nameof(Tiers))]
    public void S1305_OneWholeRowFitsAtEveryTier(string tier, double width, double height)
    {
        var plan = KitLadder.Plan(new WidgetArea(width, height), RssSpec.Spec, RssSpec.Shows(true, true, true));

        Assert.True(KitLadder.RowEstimate(plan) <= height - KitLadder.ListInset + Disclosure.Tolerance, tier);
        Assert.True(plan.TitleLines >= 1, tier);
    }

    [Fact]
    public void S1305_TheSmallestTileShowsTheHeadlineAndNoSource()
    {
        var plan = KitLadder.Plan(new WidgetArea(120, 76), RssSpec.Spec, RssSpec.Shows(true, true, true));

        Assert.Equal(1, plan.Columns);
        Assert.False(plan.Lead);
        Assert.False(plan.Subtitle);
        Assert.Equal(0, plan.PreviewLines);
    }

    [Fact]
    public void S1305_AStripAndALargeTileGoToColumns()
    {
        var wide = KitLadder.Plan(new WidgetArea(480, 116), RssSpec.Spec, RssSpec.Shows(true, true, true));
        var large = KitLadder.Plan(new WidgetArea(560, 376), RssSpec.Spec, RssSpec.Shows(true, true, true));

        Assert.True(wide.Columns > 1);
        Assert.Equal(2, wide.Columns);
        Assert.Equal(2, large.Columns);
        Assert.True(large.Subtitle);
        Assert.True(large.PreviewLines > 0);
    }

    [Fact]
    public void S1305_ATallColumnIsForHeadlines_IconWithoutName()
    {
        var plan = KitLadder.Plan(new WidgetArea(200, 356), RssSpec.Spec, RssSpec.Shows(true, true, true));

        Assert.Equal(1, plan.Columns);
        Assert.True(plan.Lead);
        Assert.False(plan.Subtitle);
        Assert.Equal(0, plan.PreviewLines);
    }

    [Fact]
    public void S1305_TheMediumTileKeepsTwoHeadlinesRatherThanOnePreview()
    {
        var plan = KitLadder.Plan(new WidgetArea(320, 196), RssSpec.Spec, RssSpec.Shows(true, true, true));

        Assert.True(plan.Subtitle);
        Assert.Equal(0, plan.PreviewLines);
    }

    [Fact]
    public void S1305_TheLargestTileShowsMoreHeadlinesNotAReadingPane()
    {
        // The feed is its headlines at every size: XL is three columns of them with their
        // summaries, never a list beside a pane reading one item (retired, concise widgets).
        var plan = KitLadder.Plan(new WidgetArea(800, 516), RssSpec.Spec, RssSpec.Shows(true, true, true));

        Assert.True(plan.Columns > 1);
        Assert.Equal(3, plan.Columns);
        Assert.True(plan.PreviewLines > 0);
    }

    [Theory]
    [InlineData(WidgetKey.Up, -1, true)]
    [InlineData(WidgetKey.Left, -1, true)]
    [InlineData(WidgetKey.Down, -1, false)]
    [InlineData(WidgetKey.Up, 3, false)]
    public void S1306_up_with_no_headline_focused_lands_on_the_last(WidgetKey key, int index, bool last)
        => Assert.Equal(last, RssKeys.StartsAtTheEnd(key, index, 10));

    [Fact]
    public void S1306_AWideButShortStripStaysInColumns()
        => Assert.True(KitLadder.Plan(new WidgetArea(800, 116), RssSpec.Spec, RssSpec.Shows(true, true, true)).Columns > 1);

    [Fact]
    public void S1305_SwitchedOffFieldsStayOff()
    {
        var plan = KitLadder.Plan(new WidgetArea(560, 376), RssSpec.Spec, RssSpec.Shows(false, false, false));

        Assert.False(plan.Lead);
        Assert.False(plan.Subtitle);
        Assert.False(plan.When);
        Assert.Equal(0, plan.PreviewLines);
    }

    [Fact]
    public void S1305_WholeRowsOnly_AtLeastOne()
    {
        Assert.Equal(2, KitLadder.WholeRows([60, 60, 60], 150));
        Assert.Equal(1, KitLadder.WholeRows([200], 100));
        Assert.Equal(0, KitLadder.WholeRows([], 100));
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
