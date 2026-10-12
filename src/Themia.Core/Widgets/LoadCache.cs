namespace Themia.Core.Widgets;

/// <summary>
/// [S-5175] What a folder row's icon or thumbnail comes from: one load per key in flight however many
/// rows ask, and what it found kept. Single-threaded: the caller's thread owns the maps, so every
/// await resumes on it.
/// <para>
/// A load that found nothing is <b>not</b> kept. The shell's extraction fails transiently (a second
/// <c>E_PENDING</c>, a file still being written, a handler that is busy), and remembering that as
/// "this type has no icon" left every item of that type a glyph in every folder widget for the rest
/// of the session, refresh or not (the original's issues #4 and #16). The next render asks again.
/// </para>
/// </summary>
/// <typeparam name="T">What is loaded.</typeparam>
public sealed class LoadCache<T>
    where T : class
{
    private readonly Dictionary<string, T> _found;
    private readonly Dictionary<string, Task<T?>> _pending;
    private readonly int _cap;

    /// <param name="cap">Past this many entries the cache is dropped whole; 0 keeps everything.</param>
    /// <param name="comparer">How keys compare (paths: ignoring case).</param>
    public LoadCache(int cap, IEqualityComparer<string> comparer)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(cap);
        ArgumentNullException.ThrowIfNull(comparer);
        _cap = cap;
        _found = new Dictionary<string, T>(comparer);
        _pending = new Dictionary<string, Task<T?>>(comparer);
    }

    /// <summary>How many loads are kept.</summary>
    public int Count => _found.Count;

    /// <summary>What a load already found for <paramref name="key"/>, without loading.</summary>
    public bool TryGet(string key, out T? value)
    {
        if (_found.TryGetValue(key, out var found))
        {
            value = found;
            return true;
        }

        value = null;
        return false;
    }

    /// <summary>
    /// What <paramref name="key"/> holds: kept, joined to the load already running for it, or loaded
    /// now. Null when the load found nothing; that is not kept, so the next ask loads again.
    /// </summary>
    public async Task<T?> GetAsync(string key, Func<Task<T?>> load)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(load);

        if (_found.TryGetValue(key, out var known))
        {
            return known;
        }

        if (!_pending.TryGetValue(key, out var task))
        {
            task = load();
            _pending[key] = task;
        }

        T? value;

        try
        {
            value = await task.ConfigureAwait(true);
        }
        finally
        {
            _pending.Remove(key);
        }

        if (value is null)
        {
            return null;
        }

        if (_cap > 0 && _found.Count >= _cap && !_found.ContainsKey(key))
        {
            _found.Clear();
        }

        _found[key] = value;
        return value;
    }
}
