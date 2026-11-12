using Themia.App.Services;
using Themia.App.Services.Web;
using Themia.Core.Localization;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// [S-11171] Where one answer of the background sync comes from for a view (the weather's reading or
/// forecast, the air, the rain, a quote): how to hear its changes, its key, how to read it and how to
/// say it is on screen. Built by the view from its host's answers (<c>IWidgetHost.Weather</c>, ...).
/// </summary>
public sealed record SkyFeedSource<TWant, T>(
    Action<Action<string>> Subscribe,
    Action<Action<string>> Unsubscribe,
    Func<TWant, string> Key,
    Func<TWant, Fetched<T>?> Read,
    Action<TWant, bool> Show)
    where TWant : class
    where T : class;

/// <summary>
/// [S-11171] [S-2663] The one way an atom of the weather, sky and money family follows its answer: it
/// tells the sync what it shows (nothing is asked for an answer held and fresh), hears its key change,
/// keeps the newest answer (through a failed refresh, [S-7042]) or the state to say without one
/// ([S-6734]), and redraws on its cadence's tick (the header's stale line) and when the language
/// changes. Two atoms at one place read the same answer ("agree to the digit"): the sync keys it by the
/// want, not the widget. Owned by the view: <see cref="Attach"/> in its Attach, <see cref="Detach"/> in its Detach.
/// </summary>
public sealed class SkyFeed<TWant, T>
    where TWant : class
    where T : class
{
    private readonly Func<SkyFeedSource<TWant, T>> _source;
    private readonly Func<TWant?> _wantOf;
    private readonly Action _changed;
    private SkyFeedSource<TWant, T>? _live;
    private IDisposable? _tick;
    private TimeSpan _interval;
    private Ticker? _ticker;
    private bool _detached = true;

    /// <param name="source">The answers, read when the view attaches (the host's are live by then).</param>
    /// <param name="wantOf">What the atom asks for, from its config as it is now: null without a place ([S-2848]).</param>
    /// <param name="changed">What the view does when there is something new to draw.</param>
    public SkyFeed(Func<SkyFeedSource<TWant, T>> source, Func<TWant?> wantOf, Action changed)
    {
        _source = source;
        _wantOf = wantOf;
        _changed = changed;
    }

    /// <summary>What the atom asks for now, or null without a place ([S-2848]).</summary>
    public TWant? Want { get; private set; }

    /// <summary>The newest answer with a value (kept through a failure), or null.</summary>
    public Fetched<T>? Held { get; private set; }

    /// <summary>The state to say while there is no answer: not set up, loading, or how the last sync went.</summary>
    public FetchStatus Status { get; private set; } = FetchStatus.Loading;

    /// <summary>Hear the sync and the language; redraw every <paramref name="interval"/> on <paramref name="ticker"/>.</summary>
    public void Attach(Ticker ticker, TimeSpan interval)
    {
        _detached = false;
        _ticker = ticker;
        if (_live is null)
        {
            _live = _source();
            _live.Subscribe(OnUpdated);
        }

        Strings.Changed -= OnLanguage;
        Strings.Changed += OnLanguage;
        Cadence(interval);
    }

    /// <summary>The cadence of the redraw; remade only when it changed.</summary>
    public void Cadence(TimeSpan interval)
    {
        if (_tick is not null && interval == _interval)
        {
            return;
        }

        _tick?.Dispose();
        _interval = interval;
        _tick = _ticker?.Subscribe(interval, () =>
        {
            if (!_detached)
            {
                _changed();
            }
        });
    }

    /// <summary>
    /// [S-2663] Ask for what the config wants now (null: no place, the Set up state) and draw what the
    /// sync holds. A different want drops the answer in hand: another place's reading must not linger.
    /// <paramref name="force"/> is the user's Refresh ([S-730]).
    /// </summary>
    public void Reload(bool force = false)
    {
        var want = _wantOf();
        if (!Equals(want, Want))
        {
            Held = null;
        }

        Want = want;
        if (_detached)
        {
            return;
        }

        if (want is null)
        {
            Held = null;
            Status = FetchStatus.NotConfigured;
            _changed();
            return;
        }

        _live?.Show(want, force);
        Take();
    }

    /// <summary>[S-730] The user's Refresh: the network now, past the back-off and the cache.</summary>
    public void Refresh() => Reload(force: true);

    /// <summary>[S-2663] Debug renders only: an answer put in hand without the sync.</summary>
    public void Preview(Fetched<T>? held, FetchStatus status)
    {
        Want = null;
        Held = held;
        Status = status;
        _changed();
    }

    public void Detach()
    {
        _detached = true;
        Strings.Changed -= OnLanguage;
        _tick?.Dispose();
        _tick = null;
        _live?.Unsubscribe(OnUpdated);
        _live = null;
        _ticker = null;
    }

    /// <summary>[S-1110] [S-3770] The language (or the shared temperature scale or cadence with it) changed: ask again under it and redraw.</summary>
    private void OnLanguage(Locale locale) => Reload();

    private void OnUpdated(string key)
    {
        if (!_detached && Want is { } want && _live is { } live && live.Key(want) == key)
        {
            Take();
        }
    }

    private void Take()
    {
        if (Want is not { } want || _live is not { } live)
        {
            return;
        }

        var answer = live.Read(want);
        Held = answer?.Value is not null ? answer : null;
        Status = answer?.Status ?? FetchStatus.Loading;
        _changed();
    }
}
