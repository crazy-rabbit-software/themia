using Themia.App.Services.Web;
using Themia.Core.Widgets;

namespace Themia.Sdk.WinUI;

/// <summary>
/// [S-2661] The feeds every Rss widget reads (<see cref="IWidgetHost.Feeds"/>): the host's one feed
/// sync, the one place a feed is fetched. A view says which feeds are on screen (<see cref="Show"/>),
/// draws what is held and hears <see cref="Updated"/>; it never fetches from its tick. Where the sync
/// is not running (a debug render), what the shared cache holds.
/// </summary>
public interface IFeedAnswers
{
    /// <summary>Raised on the UI thread with the <see cref="Key"/> of a feed whose answer or state changed.</summary>
    event Action<string>? Updated;

    /// <summary>The key a feed at a cap is synced under, as <see cref="Updated"/> names it.</summary>
    string Key(string url, int maxItems);

    /// <summary>How the last attempt for a feed went (its value may be null: a failure with nothing held).</summary>
    Fetched<FeedDocument>? Last(string url, int maxItems);

    /// <summary>The last good answer for a feed at a cap, regardless of age (a stored one at launch, [S-1300]); never asks.</summary>
    Fetched<FeedDocument>? Held(string url, int maxItems);

    /// <summary>A fetch for the feed is out ([S-1303]).</summary>
    bool Busy(string url, int maxItems);

    /// <summary>A shown widget's feeds: one never fetched is asked now; <paramref name="refresh"/> is the user's Refresh, past freshness and back-off.</summary>
    void Show(IReadOnlyList<string> urls, int maxItems, TimeSpan interval, bool refresh);
}
