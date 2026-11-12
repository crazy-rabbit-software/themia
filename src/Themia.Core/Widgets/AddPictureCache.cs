namespace Themia.Core.Widgets;

/// <summary>
/// [S-11853] The add page's tile pictures kept between openings: bounded by how many and by how many bytes
/// their pixels take, the least recently shown going first when either is passed. A picture alone larger
/// than the byte budget is not kept (it is drawn again when wanted). The values are opaque here (bitmaps
/// in the app). Not thread-safe: the UI thread's.
/// </summary>
public sealed class AddPictureCache<T>
    where T : class
{
    private readonly Dictionary<string, LinkedListNode<(string Key, T Value, long Bytes)>> _byKey = new(StringComparer.Ordinal);

    /// <summary>Most recently used first.</summary>
    private readonly LinkedList<(string Key, T Value, long Bytes)> _order = new();

    public AddPictureCache(int maxCount, long maxBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBytes);
        MaxCount = maxCount;
        MaxBytes = maxBytes;
    }

    public int MaxCount { get; }

    public long MaxBytes { get; }

    public int Count => _byKey.Count;

    /// <summary>The bytes the kept pictures take, as they were put.</summary>
    public long Bytes { get; private set; }

    /// <summary>The picture under <paramref name="key"/>, now the most recently used; null when not kept.</summary>
    public T? Get(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (!_byKey.TryGetValue(key, out var node))
        {
            return null;
        }

        _order.Remove(node);
        _order.AddFirst(node);
        return node.Value.Value;
    }

    /// <summary>Keep <paramref name="value"/> under <paramref name="key"/> (replacing one there), then drop the least recently used while over a bound.</summary>
    public void Put(string key, T value, long bytes)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);
        ArgumentOutOfRangeException.ThrowIfNegative(bytes);
        Remove(key);
        if (bytes > MaxBytes)
        {
            return;
        }

        _byKey[key] = _order.AddFirst((key, value, bytes));
        Bytes += bytes;
        while (_order.Count > MaxCount || Bytes > MaxBytes)
        {
            Remove(_order.Last!.Value.Key);
        }
    }

    /// <summary>Keep nothing.</summary>
    public void Clear()
    {
        _byKey.Clear();
        _order.Clear();
        Bytes = 0;
    }

    private void Remove(string key)
    {
        if (_byKey.Remove(key, out var node))
        {
            _order.Remove(node);
            Bytes -= node.Value.Bytes;
        }
    }
}
