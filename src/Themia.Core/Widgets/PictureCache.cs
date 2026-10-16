namespace Themia.Core.Widgets;

/// <summary>One decoded picture: the file or address, at the decode width it was asked for.</summary>
/// <param name="Path">A validated local path or the photo of the day's https address.</param>
/// <param name="Width">The decode width in physical pixels, already bucketed by <see cref="DecodeWidth"/>.</param>
public readonly record struct PictureKey(string Path, int Width);

/// <summary>
/// [S-3411] The widths pictures are decoded at. Rounded up to a step, so a card being dragged does not
/// decode the same picture again at 301, 303 and 308 pixels, and capped, so a wall-sized card does not
/// hold a full-resolution photograph.
/// </summary>
public static class DecodeWidth
{
    public const int Step = 64;

    /// <summary>The largest decode worth holding: a full-bleed picture on a 4K display.</summary>
    public const int Max = 3840;

    public static int Bucket(double physicalWidth)
    {
        if (!double.IsFinite(physicalWidth) || physicalWidth <= 0)
        {
            return Step;
        }

        var stepped = Math.Ceiling(physicalWidth / Step) * Step;
        return (int)Math.Clamp(stepped, Step, Max);
    }
}

/// <summary>
/// [S-3411] How much the decoded-picture cache may hold. What is on screen (pinned) is never evicted;
/// besides it the cache keeps half as much again of pictures recently shown, never less than
/// <see cref="MinIdleBytes"/> (one full-bleed 4K picture, so a slideshow switched away from and back
/// finds its picture) and never more than <see cref="MaxIdleBytes"/>. [S-6605] Halved from "as much
/// again, 48 to 192 MB": what nobody shows is only a faster way back, and a desktop of seven
/// galleries kept 48 MB and more of it for as long as the app ran after their view was gone.
/// </summary>
public static class PictureBudget
{
    public const long MinIdleBytes = 32L * 1024 * 1024;

    public const long MaxIdleBytes = 96L * 1024 * 1024;

    /// <summary>How many bytes of pictures nobody shows may stay decoded beside <paramref name="pinnedBytes"/>.</summary>
    public static long IdleAllowance(long pinnedBytes) => Math.Clamp(pinnedBytes / 2, MinIdleBytes, MaxIdleBytes);

    /// <summary>
    /// The decoded size of a picture decoded at <paramref name="decodeWidth"/>: four bytes a pixel,
    /// the height following the source's aspect ratio (a 4:3 guess while the source size is unknown).
    /// </summary>
    public static long Estimate(int decodeWidth, int sourceWidth, int sourceHeight)
    {
        var width = Math.Max(1, decodeWidth);
        var height = sourceWidth > 0 && sourceHeight > 0
            ? Math.Max(1, (long)Math.Ceiling((double)width * sourceHeight / sourceWidth))
            : Math.Max(1, (long)width * 3 / 4);
        return width * height * 4;
    }
}

/// <summary>
/// [S-3411] The decoded pictures every gallery widget shares, least recently used first out. A frame
/// showing a picture pins it (<see cref="Pin"/>), and a pinned picture is never evicted; the rest are
/// kept within <see cref="PictureBudget.IdleAllowance"/>. A picture asked for at a width no larger
/// than one already decoded is served by that one, so a card shrinking decodes nothing. UI thread
/// only; the values are opaque here (bitmaps in the app).
/// </summary>
public sealed class PictureCache<TValue>
    where TValue : class
{
    private readonly Dictionary<PictureKey, LinkedListNode<Entry>> _entries = [];

    /// <summary>Most recently used first.</summary>
    private readonly LinkedList<Entry> _order = new();

    public int Count => _entries.Count;

    /// <summary>The estimated decoded size of everything held.</summary>
    public long Bytes { get; private set; }

    /// <summary>The estimated decoded size of what is on screen.</summary>
    public long PinnedBytes { get; private set; }

    public bool Contains(PictureKey key) => _entries.ContainsKey(key);

    public int Pins(PictureKey key) => _entries.TryGetValue(key, out var node) ? node.Value.Pins : 0;

    /// <summary>
    /// The picture for <paramref name="path"/> decoded at <paramref name="width"/> or the smallest
    /// width above it, made the most recently used. False when none is held.
    /// </summary>
    public bool TryGet(string path, int width, out PictureKey key, out TValue value)
    {
        LinkedListNode<Entry>? best = null;

        foreach (var node in _entries.Values)
        {
            var entry = node.Value;

            if (entry.Key.Width >= width
                && string.Equals(entry.Key.Path, path, StringComparison.Ordinal)
                && (best is null || entry.Key.Width < best.Value.Key.Width))
            {
                best = node;
            }
        }

        if (best is null)
        {
            key = default;
            value = null!;
            return false;
        }

        _order.Remove(best);
        _order.AddFirst(best);
        key = best.Value.Key;
        value = best.Value.Value;
        return true;
    }

    /// <summary>
    /// Hold a picture just decoded, as the most recently used, and evict past the budget. The picture
    /// just added is never what is evicted: the frame that asked for it is about to pin it.
    /// </summary>
    public void Add(PictureKey key, TValue value, long bytes)
    {
        ArgumentNullException.ThrowIfNull(value);

        var pins = 0;

        if (_entries.TryGetValue(key, out var existing))
        {
            pins = existing.Value.Pins;
            Drop(existing);
        }

        var entry = new Entry(key, value) { Bytes = Math.Max(1, bytes), Pins = pins };
        var node = _order.AddFirst(entry);
        _entries[key] = node;
        Bytes += entry.Bytes;

        if (pins > 0)
        {
            PinnedBytes += entry.Bytes;
        }

        Evict(keep: node);
    }

    /// <summary>A frame shows the picture: it stays until every frame showing it lets go.</summary>
    public void Pin(PictureKey key)
    {
        if (!_entries.TryGetValue(key, out var node))
        {
            return;
        }

        if (node.Value.Pins++ == 0)
        {
            PinnedBytes += node.Value.Bytes;
        }
    }

    /// <summary>A frame let go of the picture. The last one makes it evictable (it is then the most recent).</summary>
    public void Unpin(PictureKey key)
    {
        if (!_entries.TryGetValue(key, out var node) || node.Value.Pins == 0)
        {
            return;
        }

        if (--node.Value.Pins == 0)
        {
            PinnedBytes -= node.Value.Bytes;
            Evict();
        }
    }

    /// <summary>The real size is known now (a remote picture opened): account for it.</summary>
    public void Resize(PictureKey key, long bytes)
    {
        if (!_entries.TryGetValue(key, out var node))
        {
            return;
        }

        var entry = node.Value;
        var next = Math.Max(1, bytes);
        Bytes += next - entry.Bytes;

        if (entry.Pins > 0)
        {
            PinnedBytes += next - entry.Bytes;
        }

        entry.Bytes = next;
        Evict();
    }

    /// <summary>Forget one picture, pinned or not (it failed to load: the next ask tries again).</summary>
    public void Remove(PictureKey key)
    {
        if (_entries.TryGetValue(key, out var node))
        {
            Drop(node);
        }
    }

    /// <summary>[S-3413] Forget the pictures nobody shows whose path matches (a released source's). Returns how many.</summary>
    public int RemoveUnpinned(Func<string, bool> path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var gone = _entries.Values.Where(n => n.Value.Pins == 0 && path(n.Value.Key.Path)).ToList();

        foreach (var node in gone)
        {
            Drop(node);
        }

        return gone.Count;
    }

    /// <summary>[S-3411] Forget every picture nobody shows (the desktop is suspended). Returns how many.</summary>
    public int Trim() => RemoveUnpinned(static _ => true);

    /// <summary>Forget everything.</summary>
    public void Clear()
    {
        _entries.Clear();
        _order.Clear();
        Bytes = 0;
        PinnedBytes = 0;
    }

    private void Evict(LinkedListNode<Entry>? keep = null)
    {
        var allowance = PictureBudget.IdleAllowance(PinnedBytes);
        var node = _order.Last;

        while (Bytes - PinnedBytes > allowance && node is not null)
        {
            var previous = node.Previous;

            if (node.Value.Pins == 0 && !ReferenceEquals(node, keep))
            {
                Drop(node);
            }

            node = previous;
        }
    }

    private void Drop(LinkedListNode<Entry> node)
    {
        _entries.Remove(node.Value.Key);
        _order.Remove(node);
        Bytes -= node.Value.Bytes;

        if (node.Value.Pins > 0)
        {
            PinnedBytes -= node.Value.Bytes;
        }
    }

    private sealed class Entry(PictureKey key, TValue value)
    {
        public PictureKey Key { get; } = key;

        public TValue Value { get; } = value;

        public long Bytes { get; set; }

        public int Pins { get; set; }
    }
}
