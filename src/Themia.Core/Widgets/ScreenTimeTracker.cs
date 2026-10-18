namespace Themia.Core.Widgets;

/// <summary>
/// A moment in local time: Unix seconds, plus the local date and how far into it, so a midnight
/// split needs no time-zone arithmetic.
/// </summary>
public readonly record struct LocalStamp(long Epoch, string Date, int SecondsIntoDay)
{
    /// <summary>The stamp for a moment, read in that moment's own offset.</summary>
    public static LocalStamp From(DateTimeOffset moment)
        => new(moment.ToUnixTimeSeconds(), LocalDay.From(moment).Key, (int)moment.TimeOfDay.TotalSeconds);
}

/// <summary>Why accrual is paused, if it is.</summary>
public enum ScreenTimePause
{
    None = 0,
    Locked = 1,
    Idle = 2,
}

/// <summary>
/// The screen-time ledger: which app had the foreground, for how long, per local day.
/// <para>
/// Pure and clock-free — every event carries the <see cref="LocalStamp"/> it happened at — so the
/// accrual rules are tested at any date, across midnight and across a suspend, without a desktop.
/// The service in the app feeds it from the foreground hook, the session-lock notification, the
/// idle query and a sampling tick, all on one thread; nothing here is thread-safe.
/// </para>
/// </summary>
public sealed class ScreenTimeTracker
{
    /// <summary>[S-459] Only the most recent dates survive a save.</summary>
    public const int RetentionDays = 90;

    /// <summary>[S-458] Input idle this long pauses accrual.</summary>
    public const int IdleThresholdSeconds = 300;

    /// <summary>[S-458] The sampling cadence: an open interval is at most this long.</summary>
    public const int TickSeconds = 30;

    /// <summary>
    /// [S-458] The longest gap that still counts as use. The tick arrives every
    /// <see cref="TickSeconds"/> while the machine runs; anything longer is a suspend, a clock jump
    /// or a stalled process, and crediting it would hand hours of phantom use to the app that
    /// happened to be in front when the lid closed.
    /// </summary>
    public const int MaxCreditSeconds = 2 * TickSeconds;

    private const int SecondsPerDay = 86_400;

    private string? _openApp;
    private LocalStamp _anchor;
    private ScreenTimePreferences _preferences = new();
    private bool _preferencesSet;

    /// <summary>date → app → seconds.</summary>
    public SortedDictionary<string, SortedDictionary<string, long>> Days { get; } = new(StringComparer.Ordinal);

    /// <summary>app → display name, resolved from the executable once.</summary>
    public SortedDictionary<string, string> Names { get; } = new(StringComparer.Ordinal);

    /// <summary>app → last known executable path, so history can show icons too.</summary>
    public SortedDictionary<string, string> Paths { get; } = new(StringComparer.Ordinal);

    /// <summary>date → how many times the foreground app changed.</summary>
    public SortedDictionary<string, long> Switches { get; } = new(StringComparer.Ordinal);

    public ScreenTimePause Paused { get; private set; }

    /// <summary>
    /// [S-3322] What every widget hides and judges against. Replaced whole, never edited in place,
    /// so a reader holding the old set never sees half a change.
    /// </summary>
    public ScreenTimePreferences Preferences
    {
        get => _preferences;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _preferences = value.Copy();
            _preferencesSet = true;
            Dirty = true;
        }
    }

    /// <summary>True when something changed that the file does not have yet.</summary>
    public bool Dirty { get; set; }

    /// <summary>The app accruing right now, or null.</summary>
    public string? OpenApp => _openApp;

    /// <summary>
    /// [S-458] Seconds of <paramref name="anchor"/>..<paramref name="now"/> that belong to
    /// <paramref name="date"/>, splitting once at the anchor's local midnight. A gap spanning more
    /// than one midnight collapses into the later day; the tick keeps gaps to one tick, so that case
    /// stays theoretical.
    /// </summary>
    public static long SplitSeconds(LocalStamp anchor, LocalStamp now, string date)
    {
        if (now.Epoch <= anchor.Epoch)
        {
            return 0;
        }

        var total = now.Epoch - anchor.Epoch;

        if (anchor.Date == now.Date)
        {
            return date == anchor.Date ? total : 0;
        }

        var untilMidnight = Math.Min(SecondsPerDay - Math.Clamp(anchor.SecondsIntoDay, 0, SecondsPerDay), total);

        if (date == anchor.Date)
        {
            return untilMidnight;
        }

        return date == now.Date ? total - untilMidnight : 0;
    }

    /// <summary>
    /// [S-458] The foreground changed to <paramref name="app"/>. While paused the remembered app still
    /// changes, but nothing accrues and nothing counts as a switch.
    /// </summary>
    public void OnForeground(LocalStamp stamp, string app, string? name = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(app);

        if (!string.IsNullOrEmpty(name) && (!Names.TryGetValue(app, out var known) || known != name))
        {
            Names[app] = name;
            Dirty = true;
        }

        if (Paused == ScreenTimePause.None)
        {
            Credit(stamp);

            // [S-453] A real change of app, while accruing. The first app of a session is a start,
            // not a switch, and the hook repeating the same app is not a switch either.
            if (_openApp is not null && _openApp != app)
            {
                Switches[stamp.Date] = Switches.GetValueOrDefault(stamp.Date) + 1;
                Dirty = true;
            }
        }

        _openApp = app;
        _anchor = stamp;
    }

    /// <summary>[S-458] The sampling tick: accrue, and enter or leave the idle pause.</summary>
    public void OnTick(LocalStamp stamp, int idleSeconds)
    {
        switch (Paused)
        {
            case ScreenTimePause.Locked:
                // Only unlocking resumes.
                break;

            case ScreenTimePause.Idle:
                if (idleSeconds < IdleThresholdSeconds)
                {
                    // Back: the idle stretch itself is never credited.
                    _anchor = stamp;
                    Paused = ScreenTimePause.None;
                }

                break;

            default:
                Credit(stamp);

                if (idleSeconds >= IdleThresholdSeconds)
                {
                    Paused = ScreenTimePause.Idle;
                }

                break;
        }
    }

    /// <summary>
    /// [S-458] Themia's own window has the foreground. Accrue up to now and close the interval, so
    /// time spent in Themia is never credited to the app that was in front before it.
    /// </summary>
    public void OnOwnForeground(LocalStamp stamp)
    {
        if (Paused == ScreenTimePause.None)
        {
            Credit(stamp);
        }

        _openApp = null;
    }

    /// <summary>[S-458] The session locked: accrue up to the lock, then stop.</summary>
    public void OnLock(LocalStamp stamp)
    {
        if (Paused == ScreenTimePause.None)
        {
            Credit(stamp);
        }

        Paused = ScreenTimePause.Locked;
    }

    /// <summary>[S-458] The session unlocked: resume from now. Idle re-pauses on the next tick if it must.</summary>
    public void OnUnlock(LocalStamp stamp)
    {
        if (Paused != ScreenTimePause.Locked)
        {
            return;
        }

        _anchor = stamp;
        Paused = ScreenTimePause.None;
    }

    /// <summary>Accrue the open interval up to <paramref name="stamp"/>, as before a flush at exit.</summary>
    public void Settle(LocalStamp stamp)
    {
        if (Paused == ScreenTimePause.None)
        {
            Credit(stamp);
        }
    }

    /// <summary>[S-454] Remember where an app's executable is. Empty values are ignored; a move marks dirty.</summary>
    public void NoteExePath(string app, string path)
    {
        if (string.IsNullOrEmpty(app) || string.IsNullOrEmpty(path))
        {
            return;
        }

        if (Paths.TryGetValue(app, out var known) && known == path)
        {
            return;
        }

        Paths[app] = path;
        Dirty = true;
    }

    /// <summary>[S-458] Record a resolved display name, unless one already landed.</summary>
    public void NoteName(string app, string name)
    {
        if (string.IsNullOrEmpty(app) || string.IsNullOrEmpty(name) || Names.TryGetValue(app, out var known) && known == name)
        {
            return;
        }

        Names[app] = name;
        Dirty = true;
    }

    /// <summary>
    /// [S-454] One day's usage, busiest first, including the interval still open up to
    /// <paramref name="now"/> — unless accrual is paused, or the open interval is older than a
    /// suspend would leave it. Read-only.
    /// </summary>
    public ScreenTimeDay Day(string date, LocalStamp? now)
    {
        var per = new Dictionary<string, long>(StringComparer.Ordinal);

        if (Days.TryGetValue(date, out var stored))
        {
            foreach (var (app, seconds) in stored)
            {
                per[app] = seconds;
            }
        }

        if (Paused == ScreenTimePause.None && _openApp is { } open && now is { } at
            && at.Epoch - _anchor.Epoch <= MaxCreditSeconds)
        {
            var live = SplitSeconds(_anchor, at, date);
            if (live > 0)
            {
                per[open] = per.GetValueOrDefault(open) + live;
            }
        }

        var apps = new List<ScreenTimeApp>(per.Count);
        long total = 0;

        foreach (var (app, seconds) in per)
        {
            total += seconds;
            apps.Add(new ScreenTimeApp(
                app,
                Names.TryGetValue(app, out var name) ? name : AppNames.Prettify(app),
                seconds,
                Paths.GetValueOrDefault(app)));
        }

        apps.Sort(static (a, b) =>
        {
            var bySeconds = b.Seconds.CompareTo(a.Seconds);
            return bySeconds != 0 ? bySeconds : string.CompareOrdinal(a.App, b.App);
        });

        return new ScreenTimeDay(date, total, apps, Switches.GetValueOrDefault(date));
    }

    /// <summary>
    /// [S-454] The rolling window that ends on <paramref name="now"/>'s date: one entry per calendar
    /// day, oldest first, days without use included as zeros so a gap draws as a gap. The length is
    /// clamped to 1..<see cref="RetentionDays"/>.
    /// </summary>
    public List<ScreenTimeDay> Range(LocalStamp now, int days)
    {
        var length = Math.Clamp(days, 1, RetentionDays);
        var result = new List<ScreenTimeDay>(length);

        if (!LocalDay.TryParse(now.Date, out var today))
        {
            result.Add(Day(now.Date, now));
            return result;
        }

        for (var back = length - 1; back >= 0; back--)
        {
            result.Add(Day(today.AddDays(-back).Key, now));
        }

        return result;
    }

    /// <summary>[S-454] App keys in history with no path yet: their icons can be filled from a running process.</summary>
    public List<string> AppsMissingPaths() => HistoryApps(app => !Paths.ContainsKey(app));

    /// <summary>[S-458] App keys in history still showing a prettified key rather than a real name.</summary>
    public List<string> AppsMissingNames() => HistoryApps(app => !Names.ContainsKey(app));

    /// <summary>[S-459] Drop dates beyond retention, and the switch counts of dates that went with them.</summary>
    public void Prune()
    {
        while (Days.Count > RetentionDays)
        {
            Days.Remove(Days.Keys.First());
        }

        foreach (var date in Switches.Keys.ToList())
        {
            if (!Days.ContainsKey(date))
            {
                Switches.Remove(date);
            }
        }
    }

    /// <summary>
    /// [S-458] Fold a loaded history into this one: the hook can fire before the file has been read,
    /// so the loaded seconds and switch counts add, and names and paths seen live win.
    /// </summary>
    public void Merge(ScreenTimeTracker loaded)
    {
        ArgumentNullException.ThrowIfNull(loaded);

        foreach (var (date, per) in loaded.Days)
        {
            foreach (var (app, seconds) in per)
            {
                Add(date, app, seconds);
            }
        }

        foreach (var (app, name) in loaded.Names)
        {
            Names.TryAdd(app, name);
        }

        foreach (var (app, path) in loaded.Paths)
        {
            Paths.TryAdd(app, path);
        }

        foreach (var (date, count) in loaded.Switches)
        {
            Switches[date] = Switches.GetValueOrDefault(date) + count;
        }

        // [S-3322] The file's preferences, unless the user changed them before it was read.
        if (!_preferencesSet)
        {
            _preferences = loaded._preferences.Copy();
        }
    }

    /// <summary>[S-3322] Load-time only: the preferences as read, without marking the history dirty.</summary>
    internal void LoadPreferences(ScreenTimePreferences preferences) => _preferences = preferences.Copy();

    /// <summary>Add seconds to a bucket. Zero is not a change.</summary>
    public void Add(string date, string app, long seconds)
    {
        if (seconds <= 0)
        {
            return;
        }

        if (!Days.TryGetValue(date, out var per))
        {
            per = new SortedDictionary<string, long>(StringComparer.Ordinal);
            Days[date] = per;
        }

        per[app] = per.GetValueOrDefault(app) + seconds;
    }

    private void Credit(LocalStamp upto)
    {
        if (_openApp is not { } app)
        {
            return;
        }

        // [S-458] A gap longer than two ticks is suspend or a clock jump: re-anchor, credit nothing.
        if (upto.Epoch - _anchor.Epoch > MaxCreditSeconds)
        {
            _anchor = upto;
            return;
        }

        var first = SplitSeconds(_anchor, upto, _anchor.Date);
        Add(_anchor.Date, app, first);
        var credited = first;

        if (upto.Date != _anchor.Date)
        {
            var rest = SplitSeconds(_anchor, upto, upto.Date);
            Add(upto.Date, app, rest);
            credited += rest;
        }

        if (credited > 0)
        {
            Dirty = true;
        }

        _anchor = upto;
    }

    private List<string> HistoryApps(Func<string, bool> wanted)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<string>();

        foreach (var per in Days.Values)
        {
            foreach (var app in per.Keys)
            {
                if (wanted(app) && seen.Add(app))
                {
                    result.Add(app);
                }
            }
        }

        return result;
    }
}
