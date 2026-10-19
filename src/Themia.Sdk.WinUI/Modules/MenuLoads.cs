using Themia.Core.Diagnostics;

namespace Themia.Sdk.WinUI;

/// <summary>[S-3746] What a menu line has of something read asynchronously: the last answer, or that the read failed.</summary>
/// <param name="Value">The last answer, or null while there has been none.</param>
/// <param name="Failed">The last read threw (logged): the line says so rather than "reading…" for ever.</param>
public readonly record struct MenuLoaded<T>(T? Value, bool Failed)
    where T : class;

/// <summary>
/// [S-3746] Lists a menu shows that are read asynchronously (an account's calendars, its task lists),
/// by key. The lines are built synchronously, so a build reads what is held and starts a read; when an
/// answer comes that differs from what was held, every menu that was built meanwhile is told it is
/// out of date (<see cref="WidgetMenuLines.Outdate"/>) and the host builds it again. A read that
/// answers the same changes nothing, so building again never loops; one read per key at a time.
/// </summary>
public sealed class MenuLoads<T>
    where T : class
{
    private const string Scope = "menu-loads";

    private readonly Func<T, T, bool> _same;
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private readonly Lock _gate = new();

    /// <param name="same">Whether two answers say the same: an equal answer does not rebuild the menu.</param>
    public MenuLoads(Func<T, T, bool> same)
    {
        _same = same ?? throw new ArgumentNullException(nameof(same));
    }

    private sealed class Entry
    {
        public T? Value { get; set; }

        public bool Failed { get; set; }

        public bool Pending { get; set; }

        public List<WidgetMenuLines> Waiting { get; } = [];
    }

    /// <summary>
    /// What is held for <paramref name="key"/> now, starting <paramref name="read"/> unless one is out;
    /// <paramref name="menu"/> is told when its answer changes what it showed.
    /// </summary>
    public MenuLoaded<T> Read(string key, WidgetMenuLines menu, Func<Task<T>> read)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(read);

        Entry entry;
        var start = false;
        lock (_gate)
        {
            if (!_entries.TryGetValue(key, out entry!))
            {
                entry = new Entry();
                _entries[key] = entry;
            }

            if (!entry.Waiting.Contains(menu))
            {
                entry.Waiting.Add(menu);
            }

            if (!entry.Pending)
            {
                entry.Pending = true;
                start = true;
            }
        }

        if (start)
        {
            _ = ReadAsync(key, entry, read);
        }

        lock (_gate)
        {
            return new(entry.Value, entry.Failed && entry.Value is null);
        }
    }

    private async Task ReadAsync(string key, Entry entry, Func<Task<T>> read)
    {
        T? answer = null;
        var failed = false;
        try
        {
            answer = await read().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            // Started from building a menu: nothing above may see an exception.
            Log.Error(Scope, $"could not read what a menu line shows ({key})", ex);
            failed = true;
        }

        List<WidgetMenuLines> told;
        lock (_gate)
        {
            entry.Pending = false;
            var changed = failed
                ? !entry.Failed
                : entry.Value is null || entry.Failed || !_same(entry.Value, answer!);

            if (!failed)
            {
                entry.Value = answer;
            }

            entry.Failed = failed;
            told = changed ? [.. entry.Waiting] : [];
            entry.Waiting.Clear();
        }

        foreach (var menu in told)
        {
            menu.Outdate();
        }
    }
}
