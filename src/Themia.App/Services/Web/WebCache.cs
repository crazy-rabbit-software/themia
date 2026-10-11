using Themia.Core.Diagnostics;
using Themia.Core.Widgets;

namespace Themia.App.Services.Web;

/// <summary>
/// One answer from a service, with the two things a network widget has to show beside it: how it
/// went, and when.
/// </summary>
/// <param name="Status">The named outcome of the attempt that produced this.</param>
/// <param name="Value">What came back, or null when the attempt failed and nothing was cached.</param>
/// <param name="Detail">One line for the log. Never rendered verbatim: it is a diagnostic, not a sentence.</param>
/// <param name="At">When <paramref name="Value"/> was actually fetched — not when it was handed over.</param>
internal sealed record Fetched<T>(FetchStatus Status, T? Value, string? Detail, DateTimeOffset At)
    where T : class
{
    /// <summary>A failure with nothing to show for it.</summary>
    public static Fetched<T> Failed(FetchStatus status, string? detail, DateTimeOffset at)
        => new(status, null, detail, at);

    /// <summary>
    /// The cached value, kept on screen, annotated with the failure that has just happened to it.
    /// <para>
    /// This is how a widget stays useful through an outage without lying: the numbers are the ones
    /// it had, <see cref="At"/> still says when they were true, and <see cref="Status"/> says that
    /// the last attempt to improve on them failed.
    /// </para>
    /// </summary>
    public Fetched<T> Stale(FetchStatus status, string? detail) => this with { Status = status, Detail = detail };

    /// <summary>
    /// [S-729] Read back from the store at launch rather than fetched by this run. Drawn with its real
    /// age in the caution colour until a fetch replaces it; fresh for its real age ([S-2662]), so a
    /// restart a minute after the last fetch asks nothing.
    /// </summary>
    public bool Restored { get; init; }
}

/// <summary>
/// The shared answer for one request, so four identical widgets cost one request.
/// <para>
/// Two jobs, and both of them exist because a widget's refresh is not the only thing that asks. A
/// relayout, a settings change and a monitor being plugged in all re-run a view, and without a
/// cache each of those is another request at a free public API that has asked, in its terms of
/// use, not to be treated that way.
/// </para>
/// <list type="number">
/// <item>An answer younger than the widget's own refresh interval is handed straight back.</item>
/// <item>
/// A request already in flight for the same key is <i>awaited</i> rather than started again. Two
/// weather widgets on the same city, added a second apart, would otherwise open two sockets for
/// the identical URL and then both write the identical answer.
/// </item>
/// </list>
/// <para>
/// In memory. A service that wants its answers to survive a restart (weather, [S-729]) seeds the
/// cache with <see cref="Restore"/> at launch and saves <see cref="Snapshot"/> itself, to its own
/// file — never the state file, which is not the place for somebody else's data. A restored
/// answer is marked <see cref="Fetched{T}.Restored"/>: drawn at once, with its real age, and fresh
/// only for that age ([S-2662]).
/// </para>
/// </summary>
internal sealed class WebCache<T>(string scope)
    where T : class
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, Fetched<T>> _entries = [];
    private readonly Dictionary<string, Task<Fetched<T>>> _inFlight = [];

    /// <summary>
    /// How many distinct keys to keep. A key is a location, a symbol or a feed; a desktop has a
    /// handful, and this only matters because a user editing a symbol letter by letter creates one
    /// key per keystroke.
    /// </summary>
    private const int MaxEntries = 64;

    /// <summary>The cached answer for a key regardless of age, or null. Never starts a request.</summary>
    public Fetched<T>? Peek(string key)
    {
        lock (_gate)
        {
            return _entries.TryGetValue(key, out var hit) ? hit : null;
        }
    }

    /// <summary>
    /// The answer for <paramref name="key"/>: cached if it is fresh enough, shared if a request is
    /// already out for it, fetched otherwise.
    /// </summary>
    /// <param name="key">Everything that changes the answer — the URL, the unit, the range.</param>
    /// <param name="freshFor">How long an answer stays good; the widget's own refresh interval.</param>
    /// <param name="load">Performs the request. Called at most once per key per flight.</param>
    /// <param name="force">
    /// [S-730] Skip the freshness check: the user asked for "Refresh", which means the network, not
    /// the answer from two minutes ago. A request already in flight is still joined.
    /// </param>
    public async Task<Fetched<T>> GetAsync(
        string key,
        TimeSpan freshFor,
        Func<CancellationToken, Task<Fetched<T>>> load,
        CancellationToken token,
        bool force = false)
    {
        var now = DateTimeOffset.Now;

        lock (_gate)
        {
            // A fresh answer, and no request in flight to join: nothing to do. An entry is only
            // ever stored on success, so this can never hand back a cached failure. A restored
            // answer is judged by when it was really fetched ([S-2662]).
            if (!force
                && !_inFlight.ContainsKey(key)
                && _entries.TryGetValue(key, out var hit)
                && hit.Status == FetchStatus.Ok
                && now - hit.At < freshFor
                && hit.At <= now)
            {
                return hit;
            }
        }

        // Joining a flight that is already out happens inside here, under the same lock that
        // registers one, so two callers arriving together cannot both start a request.
        return await StartAsync(key, load, token).ConfigureAwait(false);
    }

    /// <summary>
    /// Start a request for a key, registering it so a second caller joins it instead of opening a
    /// second socket.
    /// </summary>
    private async Task<Fetched<T>> StartAsync(
        string key,
        Func<CancellationToken, Task<Fetched<T>>> load,
        CancellationToken token)
    {
        Task<Fetched<T>> flight;

        lock (_gate)
        {
            if (_inFlight.TryGetValue(key, out var pending))
            {
                flight = pending;
            }
            else
            {
                // Deliberately NOT passed the caller's token: the flight is shared, and the second
                // widget to join it must not have its answer cancelled because the first one was
                // torn down. Each caller abandons its own await instead.
                flight = Task.Run(() => load(CancellationToken.None), CancellationToken.None);
                _inFlight[key] = flight;
            }
        }

        try
        {
            var result = await flight.WaitAsync(token).ConfigureAwait(false);

            lock (_gate)
            {
                if (result.Status == FetchStatus.Ok && result.Value is not null)
                {
                    Remember(key, result);
                }

                _inFlight.Remove(key);
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            // This caller walked away. The flight itself runs on and its result is dropped, which
            // is correct: there is no longer anybody to show it to, and cancelling it would take
            // the answer away from whoever else joined.
            lock (_gate)
            {
                if (_inFlight.TryGetValue(key, out var current) && ReferenceEquals(current, flight) && flight.IsCompleted)
                {
                    _inFlight.Remove(key);
                }
            }

            throw;
        }
        catch (Exception ex)
        {
            lock (_gate)
            {
                _inFlight.Remove(key);
            }

            // A loader is not supposed to throw — every network failure is a status — so one that
            // does is a bug in this app, not a network state. Logged as such and reported as a bad
            // response rather than taking the widget down from inside a tick.
            Log.Error(scope, "a fetch threw instead of returning a status", ex);
            return Fetched<T>.Failed(FetchStatus.BadResponse, ex.Message, DateTimeOffset.Now);
        }
    }

    /// <summary>
    /// [S-729] Seed an answer read back from disk. Never replaces one this run has fetched: the
    /// file is older than anything in memory by construction.
    /// </summary>
    public void Restore(string key, T value, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(value);

        lock (_gate)
        {
            if (!_entries.ContainsKey(key))
            {
                Remember(key, new Fetched<T>(FetchStatus.Ok, value, null, at) { Restored = true });
            }
        }
    }

    /// <summary>Every good answer held, for a service that saves them. A copy; safe to enumerate.</summary>
    public List<(string Key, Fetched<T> Entry)> Snapshot()
    {
        lock (_gate)
        {
            return [.. _entries.Where(pair => pair.Value.Value is not null).Select(pair => (pair.Key, pair.Value))];
        }
    }

    /// <summary>Store an answer, dropping the oldest when the table is full.</summary>
    private void Remember(string key, Fetched<T> value)
    {
        _entries[key] = value;

        if (_entries.Count <= MaxEntries)
        {
            return;
        }

        var oldestKey = key;
        var oldest = DateTimeOffset.MaxValue;

        foreach (var (candidate, entry) in _entries)
        {
            if (entry.At < oldest)
            {
                oldest = entry.At;
                oldestKey = candidate;
            }
        }

        _entries.Remove(oldestKey);
    }
}
