using Themia.App.Services.Web;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Samples;
using Themia.Widget.Rss;

namespace Themia.App.Widgets;

/// <summary>
/// [S-11850] [S-2056] The feeds' sample: three feeds held in memory (ten items, ages from the sample
/// moment), so the rows and the header's age draw without the network. The sites are on the reserved
/// <c>.example</c> domain, so no icon is ever found and every row keeps its globe.
/// </summary>
internal sealed partial class RssView : IWidgetSample
{
    internal const string SampleNews = "https://news.example/rss";
    internal const string SampleBlog = "https://blog.example/feed.xml";
    internal const string SampleTech = "https://tech.example/atom";

    public Task ShowSampleAsync()
    {
        var now = SampleDay.Now;
        var (news, blog, tech) = SampleFeeds(now);
        HoldSample([SampleNews, SampleBlog, SampleTech], new Dictionary<string, Fetched<FeedDocument>>(StringComparer.Ordinal)
        {
            [SampleNews] = SampleAnswer(news, now, 4),
            [SampleBlog] = SampleAnswer(blog, now, 4),
            [SampleTech] = SampleAnswer(tech, now, 4),
        }, now);
        return Task.CompletedTask;
    }

    /// <summary>A feed's answer fetched <paramref name="minutesAgo"/> before <paramref name="now"/>.</summary>
    internal static Fetched<FeedDocument> SampleAnswer(FeedDocument doc, DateTimeOffset now, double minutesAgo)
        => new(FetchStatus.Ok, doc, null, now.AddMinutes(-minutesAgo));

    /// <summary>The sample's three feeds as their documents, aged from <paramref name="now"/>.</summary>
    internal static (FeedDocument News, FeedDocument Blog, FeedDocument Tech) SampleFeeds(DateTimeOffset now)
    {
        FeedEntry Entry(string site, string slug, string title, double hoursAgo, string summary)
            => new(title, $"https://{site}/{slug}", hoursAgo < 0 ? null : now.AddHours(-hoursAgo), summary);

        var news = new FeedDocument("World News",
        [
            Entry("news.example", "harbour", "Harbour reopens after the storm as ferries resume their winter timetable", 0.1,
                "Crews worked through the night to clear the quay; the first ferry left at six with a full deck."),
            Entry("news.example", "budget", "City council agrees next year's budget", 3,
                "Libraries and the night buses keep their funding; the new pool waits another year."),
            Entry("news.example", "trains", "Rail strike called off at the last minute", 26,
                "Talks ran past midnight and ended with an offer both sides could sign."),
            Entry("news.example", "museum", "A forgotten painting turns up in the museum's basement", 24 * 9,
                "Restorers think it is the missing panel of an altarpiece split up two centuries ago."),
        ]);

        var blog = new FeedDocument("Field notes",
        [
            Entry("blog.example", "sourdough", "What a year of sourdough taught me about patience", 1.5,
                "Most of it is waiting, and the rest is learning which waiting matters."),
            Entry("blog.example", "lake", "A winter swim club that meets every Sunday at eight", 50,
                "Nobody stays in long, and everybody comes back the next week."),
            new FeedEntry("Notes from a week without a phone", string.Empty, now.AddHours(-30), "Less scrolling, more walking, and a lot of old paper maps."),
        ]);

        var tech = new FeedDocument("Tech Daily",
        [
            Entry("tech.example", "chips", "New laptop chips promise a full day on one charge", 0.6,
                "Early tests agree on the battery, less so on the price."),
            Entry("tech.example", "standards", "A standards body publishes the first draft of the new image format", 7,
                "Smaller files at the same quality, and a royalty-free licence."),
            Entry("tech.example", "keyboards", "Why quiet keyboards are winning the open office", 20,
                "Lower switches, softer caps and fewer complaints from the next desk."),
        ]);

        return (news, blog, tech);
    }

    /// <summary>
    /// Show <paramref name="feeds"/> with <paramref name="parts"/> held as their answers, the clock at
    /// <paramref name="now"/>. [S-2448] The card's own config (its rows and lines: what its size was drawn for) with the sample feeds.
    /// </summary>
    internal void HoldSample(IReadOnlyList<string> feeds, IReadOnlyDictionary<string, Fetched<FeedDocument>> parts, DateTimeOffset now)
    {
        var config = (RssConfig)_config.Clone();
        config.Feeds = [.. feeds.Select(url => new RssFeed { Url = url })];
        _config = config;
        _applied = (RssConfig)config.Clone();
        _source.HoldForPreview(config, parts);
        _binding.ReleaseCursor();
        _binding.Clock = () => now;
        _binding.Apply(_source, _config, _config.TickInterval, show: false);
    }
}
