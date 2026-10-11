namespace Themia.Core.Widgets;

/// <summary>
/// [S-2505] [S-3413] Shared things counted by who holds them: made by the first holder, kept while
/// any holds it, and kept <see cref="Linger"/> after the last one leaves, so a view switch, or a card
/// built again within it, finds what it left (a chart's history, a folder's pictures). Then the
/// owner drops it. Monotonic milliseconds throughout; touched on one thread (the UI thread). The
/// system hub's feeds (<c>FeedTable</c>) and the gallery's sources are both kept in one.
/// </summary>
/// <typeparam name="TKey">What identifies a shared thing: everything that changes what it holds.</typeparam>
/// <typeparam name="TValue">What the owner keeps for it.</typeparam>
public sealed class LingerTable<TKey, TValue>
    where TKey : notnull
    where TValue : class
{
    /// <summary>How long a thing nobody holds is kept, unless its owner says otherwise.</summary>
    public static readonly TimeSpan DefaultLinger = TimeSpan.FromMinutes(2);

    private readonly Dictionary<TKey, Entry> _entries;
    private readonly Func<TKey, TValue> _create;
    private readonly long _lingerMs;

    public LingerTable(Func<TKey, TValue> create, TimeSpan? linger = null, IEqualityComparer<TKey>? comparer = null)
    {
        ArgumentNullException.ThrowIfNull(create);
        _create = create;
        _lingerMs = (long)(linger ?? DefaultLinger).TotalMilliseconds;
        _entries = new Dictionary<TKey, Entry>(comparer);
    }

    /// <summary>How long a thing nobody holds is kept.</summary>
    public TimeSpan Linger => TimeSpan.FromMilliseconds(_lingerMs);

    /// <summary>Everything kept, held or lingering.</summary>
    public int Count => _entries.Count;

    /// <summary>Things nobody holds, kept until their linger runs out.</summary>
    public int Lingering => _entries.Values.Count(e => e.Holders == 0);

    /// <summary>Every key kept, held or lingering.</summary>
    public IEnumerable<TKey> Keys => _entries.Keys;

    /// <summary>
    /// Hold a thing: the one already there (lingering or not: what it holds is kept), or a new one.
    /// <paramref name="created"/> says which, so the owner reads a new one at once.
    /// </summary>
    public TValue Join(TKey key, out bool created)
    {
        if (_entries.TryGetValue(key, out var entry))
        {
            entry.Holders++;
            entry.ReleasedAt = null;
            created = false;
            return entry.Value;
        }

        entry = new Entry(_create(key)) { Holders = 1 };
        _entries[key] = entry;
        created = true;
        return entry.Value;
    }

    /// <summary>
    /// Let go of a thing. The last holder starts its linger at <paramref name="nowMs"/>. False for a
    /// key nobody holds (a double release), which changes nothing.
    /// </summary>
    public bool Leave(TKey key, long nowMs)
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

    /// <summary>How many hold the thing; 0 when it is lingering or gone.</summary>
    public int Holders(TKey key) => _entries.TryGetValue(key, out var entry) ? entry.Holders : 0;

    public bool TryGet(TKey key, out TValue value)
    {
        if (_entries.TryGetValue(key, out var entry))
        {
            value = entry.Value;
            return true;
        }

        value = null!;
        return false;
    }

    /// <summary>
    /// The earliest moment (monotonic ms) a lingering thing runs out, or null when nothing lingers:
    /// when an owner with no clock of its own has to look again.
    /// </summary>
    public long? NextExpiry
    {
        get
        {
            long? next = null;

            foreach (var entry in _entries.Values)
            {
                if (entry.Holders == 0 && entry.ReleasedAt is { } at && (next is null || at + _lingerMs < next))
                {
                    next = at + _lingerMs;
                }
            }

            return next;
        }
    }

    /// <summary>
    /// Drop everything whose linger has run out by <paramref name="nowMs"/> and hand them back so the
    /// owner can close what they hold. A clock that reads earlier than the release is not trusted to
    /// say the linger is over.
    /// </summary>
    public IReadOnlyList<KeyValuePair<TKey, TValue>> Expire(long nowMs)
    {
        List<KeyValuePair<TKey, TValue>>? expired = null;

        foreach (var (key, entry) in _entries)
        {
            if (entry.Holders == 0 && entry.ReleasedAt is { } at && nowMs - at >= _lingerMs)
            {
                (expired ??= []).Add(new(key, entry.Value));
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

    /// <summary>Everything kept, as pairs. A copy; safe to enumerate while the table changes.</summary>
    public IReadOnlyList<KeyValuePair<TKey, TValue>> Pairs()
        => [.. _entries.Select(e => new KeyValuePair<TKey, TValue>(e.Key, e.Value.Value))];

    /// <summary>Forget everything (the owner is closing) and hand it back.</summary>
    public IReadOnlyList<KeyValuePair<TKey, TValue>> Clear()
    {
        var all = Pairs();
        _entries.Clear();
        return all;
    }

    private sealed class Entry(TValue value)
    {
        public TValue Value { get; } = value;

        public int Holders { get; set; }

        public long? ReleasedAt { get; set; }
    }
}
