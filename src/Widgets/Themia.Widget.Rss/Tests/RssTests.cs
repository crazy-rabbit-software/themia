using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Widget.Rss.Tests;

/// <summary>[S-1302] [S-1305] [S-1306] The feed widget's own rules: merging several feeds, its config, its kit spec and keys.</summary>
public sealed class RssTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 4, 15, 0, 0, TimeSpan.FromHours(2));

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


    // ---- [S-1305] the ladder -----------------------------------------------------------------

    // The size tiers as content slots (the card less a 36 header).
    private static KitPlan Plan(RssConfig config, double width)
        => KitLadder.Plan(new WidgetArea(width, 400), RssSpec.Spec, ((IKitConfig)config).Choices);

    [Fact]
    public void S11403_a_new_feed_is_five_headlines_of_two_lines_in_one_column_at_any_width()
    {
        var config = new RssConfig();
        var narrow = Plan(config, 220);
        var wide = Plan(config, 520);

        Assert.Equal(narrow with { ColumnWidth = 0 }, wide with { ColumnWidth = 0 });
        Assert.Equal((1, 2), (wide.Columns, wide.Lines));
        Assert.True(wide.Lead && wide.Subtitle && wide.When);
        Assert.False(wide.Preview);
        Assert.Equal(new Themia.Sdk.SizeDip(300, 36 + (5 * 40) + 12), new RssModule().Descriptor.Sizing(config).Natural);
    }

    [Fact]
    public void S11403_three_lines_add_the_summary_and_two_columns_double_the_width()
    {
        var config = new RssConfig { Lines = 3, Columns = 2 };
        var plan = Plan(config, 600);
        var sizing = new RssModule().Descriptor.Sizing(config);

        Assert.True(plan.Preview);
        Assert.Equal(2, plan.Columns);
        Assert.Equal((440, 600, 1040), (sizing.Min.Width, sizing.Natural.Width, sizing.Max.Width));
    }

    [Fact]
    public void S11403_switched_off_fields_take_their_lines_with_them()
    {
        var config = new RssConfig { ShowSource = false, ShowPreview = false, ShowDate = false, Lines = 3 };
        var plan = Plan(config, 400);

        Assert.Equal(1, config.RowsShown.Lines);
        Assert.False(plan.Lead || plan.Subtitle || plan.When || plan.Preview);
        Assert.Equal(1, plan.Lines);
    }

    [Fact]
    public void S11403_rows_lines_and_columns_out_of_range_are_repaired_and_copied()
    {
        var config = new RssConfig { Rows = 40, Lines = 0, Columns = 3 };
        Assert.NotEmpty(config.Repair());
        var copy = (RssConfig)config.Clone();
        Assert.Equal((20, 1, 2), (copy.Rows, copy.Lines, copy.Columns));
    }

    /// <summary>[S-1306] With nothing focused, every movement key but End lands on the newest headline, as every kit list ([S-7020]); it was the last one for Up and Left.</summary>
    [Theory]
    [InlineData(WidgetKey.Up, 0)]
    [InlineData(WidgetKey.Left, 0)]
    [InlineData(WidgetKey.Down, 0)]
    [InlineData(WidgetKey.End, 9)]
    public void S1306_up_with_no_headline_focused_lands_on_the_first(WidgetKey key, int lands)
        => Assert.Equal(new KitKeyResult(KitKeyCommand.Focus, lands), KitKeys.Decide(key, -1, 10));
}
