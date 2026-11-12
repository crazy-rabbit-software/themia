#if DEBUG
using Themia.App.Services.Web;
using Themia.App.Shell.Gallery;
using Themia.Core.Widgets;
using Themia.Sdk.Samples;
using Themia.Widget.Rss;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only ([S-1917], [S-2056]): <c>sample</c> is the feeds' sample (<see cref="ShowSampleAsync"/>);
/// <c>single</c> is one feed (its own title in the header); <c>stale</c> is a held list with one feed
/// failing (the age in the caution colour); <c>failed</c> is every feed failing with nothing held;
/// <c>empty</c> a feed that answered with no items; <c>loading</c> the first fetch out.
/// </summary>
internal sealed partial class RssView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["sample", "single", "stale", "failed", "empty", "loading"];

    public Task ShowPreviewStateAsync(string state)
    {
        if (state == "sample")
        {
            return ShowSampleAsync();
        }

        if (!PreviewStates.Contains(state, StringComparer.Ordinal))
        {
            return Task.CompletedTask;
        }

        var now = SampleDay.Now;
        var (newsDoc, blogDoc, techDoc) = SampleFeeds(now);
        const string news = SampleNews;
        const string blog = SampleBlog;
        const string tech = SampleTech;

        var feeds = state == "single" || state == "empty" ? new[] { news } : new[] { news, blog, tech };
        var parts = new Dictionary<string, Fetched<FeedDocument>>(StringComparer.Ordinal);

        switch (state)
        {
            case "single":
                parts[news] = SampleAnswer(newsDoc, now, 2);
                break;

            case "stale":
                parts[news] = SampleAnswer(newsDoc, now, 50);
                parts[blog] = SampleAnswer(blogDoc, now, 50).Stale(FetchStatus.Offline, null);
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

        HoldSample(feeds, parts, now);
        return Task.CompletedTask;
    }
}
#endif
