namespace Themia.Core.SystemData;

/// <summary>
/// [S-2505] The hub's feeds and who holds them. A feed is made by its first subscriber, lives while
/// it has any, and lingers <see cref="Linger"/> after the last one leaves — still read, so a view
/// switch or a card built again within it finds the history it left — before it is dropped with its
/// history. A source no feed reads is closed by the hub. Monotonic milliseconds throughout; touched on
/// one thread (the UI thread).
/// </summary>
/// <typeparam name="TFeed">What the hub keeps per feed (its readings and history).</typeparam>
public sealed class FeedTable<TFeed>
    where TFeed : class
{
    /// <summary>How long a feed nobody holds is still read before it is dropped.</summary>
    public static readonly TimeSpan Linger = TimeSpan.FromMinutes(2);

    private readonly Dictionary<FeedKey, Entry> _entries = [];
    private readonly Func<FeedKey, TFeed> _create;

    public FeedTable(Func<FeedKey, TFeed> create)
    {
        ArgumentNullException.ThrowIfNull(create);
        _create = create;
    }

    /// <summary>Every feed, held or lingering.</summary>
    public int Count => _entries.Count;

    /// <summary>Feeds nobody holds, still read until their linger runs out.</summary>
    public int Lingering => _entries.Values.Count(e => e.Holders == 0);

    /// <summary>
    /// Hold a feed: the one already there (lingering or not: its history is kept), or a new one.
    /// <paramref name="created"/> says which, so the hub reads a new feed at once.
    /// </summary>
    public TFeed Join(FeedKey key, out bool created)
    {
        if (_entries.TryGetValue(key, out var entry))
        {
            entry.Holders++;
            entry.ReleasedAt = null;
            created = false;
            return entry.Feed;
        }

        entry = new Entry(_create(key)) { Holders = 1 };
        _entries[key] = entry;
        created = true;
        return entry.Feed;
    }

    /// <summary>
    /// Let go of a feed. The last holder starts its linger at <paramref name="nowMs"/>. False for a
    /// key nobody holds (a double release), which changes nothing.
    /// </summary>
    public bool Leave(FeedKey key, long nowMs)
    {
        if (!_entries.TryGetValue(key, out var entry) || entry.Holders == 0)
        {
            return false;
        }

        entry.Holders--;

        if (entry.Holders == 0)
        {
            entry.ReleasedAt = nowMs;
        }

        return true;
    }

    /// <summary>How many hold the feed; 0 when it is lingering or gone.</summary>
    public int Holders(FeedKey key) => _entries.TryGetValue(key, out var entry) ? entry.Holders : 0;

    public bool TryGet(FeedKey key, out TFeed feed)
    {
        if (_entries.TryGetValue(key, out var entry))
        {
            feed = entry.Feed;
            return true;
        }

        feed = null!;
        return false;
    }

    /// <summary>
    /// Drop every feed whose linger has run out by <paramref name="nowMs"/> and hand them back so the
    /// hub can close what they read. A clock that reads earlier than the release is not trusted to say
    /// the linger is over.
    /// </summary>
    public IReadOnlyList<KeyValuePair<FeedKey, TFeed>> Expire(long nowMs)
    {
        List<KeyValuePair<FeedKey, TFeed>>? expired = null;
        var linger = (long)Linger.TotalMilliseconds;

        foreach (var (key, entry) in _entries)
        {
            if (entry.Holders == 0 && entry.ReleasedAt is { } at && nowMs - at >= linger)
            {
                (expired ??= []).Add(new(key, entry.Feed));
            }
        }

        if (expired is null)
        {
            return [];
        }

        foreach (var pair in expired)
        {
            _entries.Remove(pair.Key);
        }

        return expired;
    }

    /// <summary>Every feed, in a stable order: by source (the read order, [S-2501]), then target, then interval.</summary>
    public IReadOnlyList<KeyValuePair<FeedKey, TFeed>> Ordered()
        => _entries
            .Select(e => new KeyValuePair<FeedKey, TFeed>(e.Key, e.Value.Feed))
            .OrderBy(e => e.Key.Source)
            .ThenBy(e => e.Key.Target, StringComparer.Ordinal)
            .ThenBy(e => e.Key.IntervalSeconds)
            .ToList();

    /// <summary>[S-2503] The ticker cadence every feed held or lingering needs; null with none.</summary>
    public TimeSpan? Cadence => HubCadence.Arbitrate(_entries.Keys.Select(k => k.IntervalSeconds));

    /// <summary>Whether any feed (held or lingering) reads this source and target.</summary>
    public bool Reads(SystemSource source, string target)
        => _entries.Keys.Any(k => k.Source == source && k.Target == target);

    /// <summary>Forget everything (the hub is closing).</summary>
    public IReadOnlyList<KeyValuePair<FeedKey, TFeed>> Clear()
    {
        var all = _entries.Select(e => new KeyValuePair<FeedKey, TFeed>(e.Key, e.Value.Feed)).ToList();
        _entries.Clear();
        return all;
    }

    private sealed class Entry(TFeed feed)
    {
        public TFeed Feed { get; } = feed;

        public int Holders { get; set; }

        public long? ReleasedAt { get; set; }
    }
}
