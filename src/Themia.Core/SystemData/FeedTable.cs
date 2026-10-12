namespace Themia.Core.SystemData;

/// <summary>
/// [S-2505] The hub's feeds and why each one lives. A feed is <b>wanted</b> while a widget in any
/// view needs it (<see cref="SystemInterests"/>, from the whole state, so a view switched to already
/// has its history) and <b>held</b> by every watch on it (a render, a preview). It is made by the first
/// of either and dropped, with its history, the moment it has neither: no linger, because a widget
/// that is still in the state is still wanted. Touched on one thread (the UI thread).
/// </summary>
/// <typeparam name="TFeed">What the hub keeps per feed (its reader, readings and history).</typeparam>
public sealed class FeedTable<TFeed>
    where TFeed : class
{
    private readonly Func<FeedKey, TFeed> _create;
    private readonly Dictionary<FeedKey, Entry> _entries = [];

    public FeedTable(Func<FeedKey, TFeed> create)
    {
        ArgumentNullException.ThrowIfNull(create);
        _create = create;
    }

    /// <summary>Every feed, wanted or held.</summary>
    public int Count => _entries.Count;

    /// <summary>Feeds the state wants.</summary>
    public int Wanted => _entries.Values.Count(e => e.Wanted);

    /// <summary>
    /// Hold a feed for a watch: the one already there (its history is kept), or a new one.
    /// <paramref name="created"/> says which, so the hub reads a new feed at once.
    /// </summary>
    public TFeed Join(FeedKey key, out bool created)
    {
        var entry = Ensure(key, out created);
        entry.Holders++;
        return entry.Feed;
    }

    /// <summary>
    /// Let go of a watch's hold. The feed handed back when this left it with neither a holder nor a
    /// want, so the hub closes what it read; null otherwise, and for a key nobody holds (a double
    /// release), which changes nothing.
    /// </summary>
    public TFeed? Leave(FeedKey key, out bool known)
    {
        known = _entries.TryGetValue(key, out var entry) && entry.Holders > 0;

        if (!known)
        {
            return null;
        }

        entry!.Holders--;
        return Drop(key, entry);
    }

    /// <summary>
    /// [S-2505] Make <paramref name="keys"/> the wanted set: a feed newly wanted is made (and handed
    /// back in <paramref name="created"/>, to be read at once), a feed no longer wanted and not held is
    /// handed back in the result to be closed. A feed both held and wanted is the same feed either way.
    /// </summary>
    public IReadOnlyList<TFeed> Want(IEnumerable<FeedKey> keys, out IReadOnlyList<TFeed> created)
    {
        ArgumentNullException.ThrowIfNull(keys);
        var wanted = new HashSet<FeedKey>(keys);
        var made = new List<TFeed>();
        var dropped = new List<TFeed>();

        foreach (var key in wanted)
        {
            var entry = Ensure(key, out var isNew);
            entry.Wanted = true;

            if (isNew)
            {
                made.Add(entry.Feed);
            }
        }

        foreach (var (key, entry) in _entries.ToArray())
        {
            if (entry.Wanted && !wanted.Contains(key))
            {
                entry.Wanted = false;

                if (Drop(key, entry) is { } gone)
                {
                    dropped.Add(gone);
                }
            }
        }

        created = made;
        return dropped;
    }

    /// <summary>How many watches hold the feed; 0 when only wanted or gone.</summary>
    public int Holders(FeedKey key) => _entries.TryGetValue(key, out var entry) ? entry.Holders : 0;

    /// <summary>Whether the state wants the feed.</summary>
    public bool IsWanted(FeedKey key) => _entries.TryGetValue(key, out var entry) && entry.Wanted;

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

    /// <summary>Every feed, in a stable order: by source (the read order, [S-2501]), then target.</summary>
    public IReadOnlyList<KeyValuePair<FeedKey, TFeed>> Ordered()
        => _entries
            .OrderBy(e => e.Key.Source)
            .ThenBy(e => e.Key.Target, StringComparer.Ordinal)
            .Select(e => new KeyValuePair<FeedKey, TFeed>(e.Key, e.Value.Feed))
            .ToList();

    /// <summary>Forget everything (the hub is closing) and hand every feed back.</summary>
    public IReadOnlyList<TFeed> Clear()
    {
        var all = _entries.Values.Select(e => e.Feed).ToList();
        _entries.Clear();
        return all;
    }

    private Entry Ensure(FeedKey key, out bool created)
    {
        created = !_entries.TryGetValue(key, out var entry);

        if (created)
        {
            entry = new Entry(_create(key));
            _entries[key] = entry;
        }

        return entry!;
    }

    private TFeed? Drop(FeedKey key, Entry entry)
    {
        if (entry.Holders > 0 || entry.Wanted)
        {
            return null;
        }

        _entries.Remove(key);
        return entry.Feed;
    }

    private sealed class Entry(TFeed feed)
    {
        public TFeed Feed { get; } = feed;

        public int Holders { get; set; }

        public bool Wanted { get; set; }
    }
}
