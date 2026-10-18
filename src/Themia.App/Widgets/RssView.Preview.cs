#if DEBUG
using Themia.App.Services.Web;
using Themia.App.Shell.Gallery;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only ([S-1917]): sample feeds held in memory, so <c>--widget-png rss --state sample</c>
/// draws the rows, the header's age and the empty and failure sentences without the network. The
/// sites are on the reserved <c>.example</c> domain, so no icon is ever found and every row keeps its
/// globe: a render is the same on every run. <c>single</c> is one feed (its own title in the header);
/// <c>stale</c> is a held list with one feed failing (the age in the caution colour); <c>failed</c>
/// is every feed failing with nothing held; <c>empty</c> a feed that answered with no items;
/// <c>loading</c> the first fetch out.
/// </summary>
internal sealed partial class RssView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["sample", "single", "stale", "failed", "empty", "loading"];

    public Task ShowPreviewStateAsync(string state)
    {
        if (!PreviewStates.Contains(state, StringComparer.Ordinal))
        {
            return Task.CompletedTask;
        }

        var now = DateTimeOffset.Now;
        const string news = "https://news.example/rss";
        const string blog = "https://blog.example/feed.xml";
        const string tech = "https://tech.example/atom";

        FeedEntry Entry(string site, string slug, string title, double hoursAgo, string summary)
            => new(title, $"https://{site}/{slug}", hoursAgo < 0 ? null : now.AddHours(-hoursAgo), summary);

        var newsDoc = new FeedDocument("World News",
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

        var blogDoc = new FeedDocument("Field notes",
        [
            Entry("blog.example", "sourdough", "What a year of sourdough taught me about patience", 1.5,
                "Most of it is waiting, and the rest is learning which waiting matters."),
            Entry("blog.example", "untitled", string.Empty, 50, string.Empty),
            new FeedEntry("Notes from a week without a phone", string.Empty, now.AddHours(-30), "No link on this one: it opens nothing."),
        ]);

        var techDoc = new FeedDocument("Tech Daily",
        [
            Entry("tech.example", "chips", "New laptop chips promise a full day on one charge", 0.6,
                "Early tests agree on the battery, less so on the price."),
            Entry("tech.example", "standards", "A standards body publishes the first draft of the new image format", 7,
                "Smaller files at the same quality, and a royalty-free licence."),
            Entry("tech.example", "undated", "An undated item sinks below every dated one", -1, "Its feed gave no date."),
        ]);

        Fetched<FeedDocument> Ok(FeedDocument doc, double minutesAgo)
            => new(FetchStatus.Ok, doc, null, now.AddMinutes(-minutesAgo));

        var feeds = state == "single" || state == "empty" ? new[] { news } : new[] { news, blog, tech };
        var config = new RssConfig { Feeds = [.. feeds.Select(url => new RssFeed { Url = url })] };
        var parts = new Dictionary<string, Fetched<FeedDocument>>(StringComparer.Ordinal);

        switch (state)
        {
            case "sample":
                parts[news] = Ok(newsDoc, 4);
                parts[blog] = Ok(blogDoc, 4);
                parts[tech] = Ok(techDoc, 4);
                break;

            case "single":
                parts[news] = Ok(newsDoc, 2);
                break;

            case "stale":
                parts[news] = Ok(newsDoc, 50);
                parts[blog] = Ok(blogDoc, 50).Stale(FetchStatus.Offline, null);
                parts[tech] = Fetched<FeedDocument>.Failed(FetchStatus.BadResponse, "HTTP 503 Service Unavailable", now);
                break;

            case "failed":
                parts[news] = Fetched<FeedDocument>.Failed(FetchStatus.Unreachable, null, now);
                parts[blog] = Fetched<FeedDocument>.Failed(FetchStatus.BadResponse, "HTTP 404 Not Found", now);
                parts[tech] = Fetched<FeedDocument>.Failed(FetchStatus.TimedOut, null, now);
                break;

            case "empty":
                parts[news] = Fetched<FeedDocument>.Failed(FetchStatus.Empty, "the feed has no items", now);
                break;
        }

        _config = config;
        _applied = (RssConfig)config.Clone();
        _source.HoldForPreview(config, parts);
        _binding.ReleaseCursor();
        _binding.Apply(_source, _config, _config.TickInterval, show: false);
        return Task.CompletedTask;
    }
}
#endif
