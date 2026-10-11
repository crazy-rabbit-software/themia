using Themia.App.Services.Sync;
using Themia.Core.Widgets;

namespace Themia.App.Services.Web;

/// <summary>
/// [S-2661] The feeds every Rss widget in every view reads, kept fresh in the background: the one place
/// a feed is fetched. A widget draws what <see cref="Peek"/> holds (restored from the store at
/// launch, [S-1300]) and hears <see cref="Updated"/>; a view switch, a card rebuild or a resize asks
/// nothing. The cadence, back-off, network wake and Refresh are <see cref="AnswerSync{T}"/>'s, shared
/// with weather and quotes ([S-2663]).
/// <para>
/// UI thread: the ticker and the widgets are there, and each fetch resumes there.
/// </para>
/// </summary>
internal sealed class FeedSync : IDisposable
{
    private readonly SyncHost _sync;
    private readonly AnswerSync<FeedDocument> _answers;
    private bool _disposed;

    public FeedSync(SyncHost sync, Ticker ticker)
    {
        _sync = sync;
        _answers = new AnswerSync<FeedDocument>("rss", ticker);
        _sync.InterestsChanged += Reconcile;
        _sync.NetworkAvailable += _answers.Wake;
        Reconcile();
    }

    /// <summary>Raised on the UI thread with the <see cref="Key"/> of a feed whose answer or state changed.</summary>
    public event Action<string>? Updated
    {
        add => _answers.Updated += value;
        remove => _answers.Updated -= value;
    }

    /// <summary>The key a feed at a cap is synced under, as <see cref="Updated"/> names it.</summary>
    public static string Key(string url, int maxItems) => FeedService.Key(url, maxItems);

    /// <summary>The last good answer for a feed at a cap, regardless of age; never asks.</summary>
    public static Fetched<FeedDocument>? Peek(string url, int maxItems) => FeedService.Peek(url, maxItems);

    /// <summary>How the last attempt for a feed went (its value may be null: a failure with nothing held).</summary>
    public Fetched<FeedDocument>? Last(string url, int maxItems) => _answers.Last(Key(url, maxItems));

    public bool Busy(string url, int maxItems) => _answers.Busy(Key(url, maxItems));

    /// <summary>
    /// A shown widget's feeds. Nothing is asked for a feed already held; one never fetched (a feed
    /// just typed in) is asked now.
    /// </summary>
    public void Show(IReadOnlyList<string> urls, int maxItems, TimeSpan interval)
    {
        ArgumentNullException.ThrowIfNull(urls);
        _answers.Show(urls.Select(url => Want(url, maxItems, interval)));
    }

    /// <summary>[S-1303] The user's Refresh: these feeds now, past freshness and back-off.</summary>
    public void Refresh(IReadOnlyList<string> urls, int maxItems, TimeSpan interval)
    {
        ArgumentNullException.ThrowIfNull(urls);
        _answers.Refresh(urls.Select(url => Want(url, maxItems, interval)));
    }

    private static AnswerWant<FeedDocument> Want(string url, int maxItems, TimeSpan interval)
        => new(
            Key(url, maxItems),
            interval,
            (freshFor, force) => FeedService.LoadAsync(url, maxItems, freshFor, CancellationToken.None, force),
            () => Peek(url, maxItems));

    private void Reconcile()
    {
        if (!_disposed)
        {
            _answers.Reconcile(_sync.Interests.Feeds.Select(f => Want(f.Url, f.MaxItems, f.Interval)));
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _sync.InterestsChanged -= Reconcile;
        _sync.NetworkAvailable -= _answers.Wake;
        _answers.Dispose();
    }
}
