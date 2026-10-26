namespace Themia.Core.Widgets;

/// <summary>
/// [S-3325] The drawn parts of a widget, kept by their item's key across reads: a read that names
/// the same items reuses their parts (the caller moves each to its new figure), makes one only for an
/// item that is new and lets go of the ones for items that left. The screen-time ring's wedges and a
/// bar's app parts are kept this way, so a tick that adds thirty seconds to one app moves one arc
/// instead of building every arc, tooltip and menu of the widget again. Pure: <typeparamref name="T"/>
/// is whatever the view draws with.
/// </summary>
public sealed class KeyedParts<T>
    where T : class
{
    private readonly Dictionary<string, T> _parts = new(StringComparer.Ordinal);
    private readonly List<string> _order = [];

    /// <summary>The keys, in the order of the last <see cref="Sync"/>.</summary>
    public IReadOnlyList<string> Keys => _order;

    /// <summary>How many parts are kept.</summary>
    public int Count => _parts.Count;

    /// <summary>The part of <paramref name="key"/>, or null.</summary>
    public T? Of(string key) => _parts.GetValueOrDefault(key);

    /// <summary>
    /// Keep a part for each of <paramref name="keys"/> (distinct, in drawing order): reused where one
    /// exists, made with <paramref name="create"/> where not; every other part goes to
    /// <paramref name="drop"/>. Returns the parts in <paramref name="keys"/>' order and whether that
    /// order (or the set) differs from the last one, which is when a caller whose container draws in
    /// order lays its children again.
    /// </summary>
    public (IReadOnlyList<T> Parts, bool Reordered) Sync(IReadOnlyList<string> keys, Func<string, T> create, Action<T> drop)
    {
        ArgumentNullException.ThrowIfNull(keys);
        ArgumentNullException.ThrowIfNull(create);
        ArgumentNullException.ThrowIfNull(drop);

        var reordered = !_order.SequenceEqual(keys, StringComparer.Ordinal);
        var wanted = new HashSet<string>(keys, StringComparer.Ordinal);

        if (wanted.Count != keys.Count)
        {
            throw new ArgumentException("every key is drawn once", nameof(keys));
        }

        foreach (var key in _order)
        {
            if (!wanted.Contains(key) && _parts.Remove(key, out var gone))
            {
                drop(gone);
            }
        }

        var parts = new List<T>(keys.Count);

        foreach (var key in keys)
        {
            if (!_parts.TryGetValue(key, out var part))
            {
                part = create(key);
                _parts[key] = part;
            }

            parts.Add(part);
        }

        _order.Clear();
        _order.AddRange(keys);
        return (parts, reordered);
    }

    /// <summary>Let every part go (to <paramref name="drop"/>).</summary>
    public void Clear(Action<T> drop)
    {
        ArgumentNullException.ThrowIfNull(drop);

        foreach (var key in _order)
        {
            if (_parts.Remove(key, out var part))
            {
                drop(part);
            }
        }

        _parts.Clear();
        _order.Clear();
    }
}
