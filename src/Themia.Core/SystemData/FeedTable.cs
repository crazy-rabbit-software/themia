using Themia.Core.Widgets;

namespace Themia.Core.SystemData;

/// <summary>
/// [S-2505] The hub's feeds and who holds them. A feed is made by its first subscriber, lives while
/// it has any, and lingers <see cref="Linger"/> after the last one leaves — still read, so a view
/// switch or a card built again within it finds the history it left — before it is dropped with its
/// history. A source no feed reads is closed by the hub. Monotonic milliseconds throughout; touched on
/// one thread (the UI thread). The counting and the linger are the shared <see cref="LingerTable{TKey, TValue}"/>.
/// </summary>
/// <typeparam name="TFeed">What the hub keeps per feed (its readings and history).</typeparam>
public sealed class FeedTable<TFeed>
    where TFeed : class
{
    /// <summary>How long a feed nobody holds is still read before it is dropped.</summary>
    public static readonly TimeSpan Linger = LingerTable<FeedKey, TFeed>.DefaultLinger;

    private readonly LingerTable<FeedKey, TFeed> _table;

    public FeedTable(Func<FeedKey, TFeed> create)
    {
        ArgumentNullException.ThrowIfNull(create);
        _table = new LingerTable<FeedKey, TFeed>(create, Linger);
    }

    /// <summary>Every feed, held or lingering.</summary>
    public int Count => _table.Count;

    /// <summary>Feeds nobody holds, still read until their linger runs out.</summary>
    public int Lingering => _table.Lingering;

    /// <summary>
    /// Hold a feed: the one already there (lingering or not: its history is kept), or a new one.
    /// <paramref name="created"/> says which, so the hub reads a new feed at once.
    /// </summary>
    public TFeed Join(FeedKey key, out bool created) => _table.Join(key, out created);

    /// <summary>
    /// Let go of a feed. The last holder starts its linger at <paramref name="nowMs"/>. False for a
    /// key nobody holds (a double release), which changes nothing.
    /// </summary>
    public bool Leave(FeedKey key, long nowMs) => _table.Leave(key, nowMs);

    /// <summary>How many hold the feed; 0 when it is lingering or gone.</summary>
    public int Holders(FeedKey key) => _table.Holders(key);

    public bool TryGet(FeedKey key, out TFeed feed) => _table.TryGet(key, out feed);

    /// <summary>
    /// Drop every feed whose linger has run out by <paramref name="nowMs"/> and hand them back so the
    /// hub can close what they read. A clock that reads earlier than the release is not trusted to say
    /// the linger is over.
    /// </summary>
    public IReadOnlyList<KeyValuePair<FeedKey, TFeed>> Expire(long nowMs) => _table.Expire(nowMs);

    /// <summary>Every feed, in a stable order: by source (the read order, [S-2501]), then target, then interval.</summary>
    public IReadOnlyList<KeyValuePair<FeedKey, TFeed>> Ordered()
        => _table.Pairs()
            .OrderBy(e => e.Key.Source)
            .ThenBy(e => e.Key.Target, StringComparer.Ordinal)
            .ThenBy(e => e.Key.IntervalSeconds)
            .ToList();

    /// <summary>[S-2503] The ticker cadence every feed held or lingering needs; null with none.</summary>
    public TimeSpan? Cadence => HubCadence.Arbitrate(_table.Keys.Select(k => k.IntervalSeconds));

    /// <summary>Whether any feed (held or lingering) reads this source and target.</summary>
    public bool Reads(SystemSource source, string target)
        => _table.Keys.Any(k => k.Source == source && k.Target == target);

    /// <summary>Forget everything (the hub is closing).</summary>
    public IReadOnlyList<KeyValuePair<FeedKey, TFeed>> Clear() => _table.Clear();
}
