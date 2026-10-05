using Themia.Core.Widgets;

namespace Themia.App.Services.Web;

/// <summary>
/// RSS and Atom feeds, fetched and parsed, one address at a time; the widget merges them
/// ([S-1302]).
/// <para>
/// Whatever addresses the user typed, and nothing else — no discovery, no following a
/// <c>&lt;link rel="alternate"&gt;</c> out of an HTML page, no guessing at <c>/feed</c>. A widget
/// that fetches a URL the user did not write is a widget whose settings do not describe what it
/// does. Every request goes through the network guard ([S-1301]): a feed address is the user's,
/// but where it redirects to is the host's.
/// </para>
/// </summary>
internal static class FeedService
{
    private const string Scope = "rss";

    /// <summary>The shared answers; also what <see cref="NetworkCache"/> saves and restores ([S-1300]).</summary>
    internal static WebCache<FeedDocument> Cache { get; } = new(Scope);

    /// <summary>
    /// The feed at <paramref name="url"/>, from the cache when it is fresh.
    /// <para>
    /// The item cap is part of the key, so raising it re-reads rather than serving a list that was
    /// already trimmed — and widgets on the same feed at the same cap share one request.
    /// </para>
    /// </summary>
    /// <param name="force">[S-1303] The Refresh action: past the freshness check.</param>
    public static async Task<Fetched<FeedDocument>> LoadAsync(
        string url,
        int maxItems,
        TimeSpan freshFor,
        CancellationToken token,
        bool force = false)
    {
        var result = await Cache
            .GetAsync(Key(url, maxItems), freshFor, ct => LoadOnceAsync(url, maxItems, ct), token, force)
            .ConfigureAwait(false);

        if (result.Status == FetchStatus.Ok && !result.Restored)
        {
            NetworkCache.SaveSoon();
        }

        return result;
    }

    public static Fetched<FeedDocument>? Peek(string url, int maxItems) => Cache.Peek(Key(url, maxItems));

    internal static string Key(string url, int maxItems) => $"{url}|{maxItems}";

    private static async Task<Fetched<FeedDocument>> LoadOnceAsync(string url, int maxItems, CancellationToken token)
    {
        var now = DateTimeOffset.Now;

        // Checked again here, against the same function the settings box uses. The address reaches
        // this method from the config, which reaches the config from a JSON file anybody can edit,
        // so the check at the text box is not the boundary — this is.
        var checkedUrl = WebInput.Url(url);

        if (!checkedUrl.IsValid)
        {
            return Fetched<FeedDocument>.Failed(FetchStatus.Refused, WebInput.Explain(checkedUrl.Problem), now);
        }

        // Asking for XML specifically. A host that sees */* very often serves the human page to a
        // feed reader, which then parses as "not a feed" and gets reported as a broken feed.
        var response = await WebFetch
            .GetAsync(new Uri(checkedUrl.Url), FeedParser.Accept, token)
            .ConfigureAwait(false);

        if (response.Status != FetchStatus.Ok)
        {
            return Fetched<FeedDocument>.Failed(response.Status, response.Detail, now);
        }

        var parsed = FeedParser.Parse(response.Body, maxItems);

        return parsed.Status switch
        {
            FeedParseStatus.Ok => new Fetched<FeedDocument>(FetchStatus.Ok, parsed.Document, null, now),

            // Parsed, and genuinely held nothing. Its own state, and not a failure: the address is
            // right, the host answered, and there is simply no news.
            FeedParseStatus.Empty => Fetched<FeedDocument>.Failed(FetchStatus.Empty, "the feed has no items", now),

            // Well-formed and not a feed: almost always an HTML page, a consent wall or a
            // "subscribe" landing page served with a 200.
            FeedParseStatus.NotFeed => Fetched<FeedDocument>.Failed(
                FetchStatus.BadResponse,
                "that address serves a page, not a feed",
                now),

            _ => Fetched<FeedDocument>.Failed(FetchStatus.BadResponse, "the feed would not parse", now),
        };
    }

}
