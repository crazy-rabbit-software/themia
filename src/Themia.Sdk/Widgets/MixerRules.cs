namespace Themia.Core.Widgets;

/// <summary>One audio session as Core Audio reported it, before grouping.</summary>
/// <param name="SessionKey">The session's instance identifier: unique per session.</param>
/// <param name="GroupKey">The lower-case executable path, <c>pid:N</c> when it could not be read, or the system-sounds key.</param>
/// <param name="Name">The name to show, or empty when nothing usable was found.</param>
/// <param name="Path">The executable, for the icon. Null for system sounds and protected processes.</param>
/// <param name="Level">The session's own volume, 0..1.</param>
public sealed record AudioSessionInfo(
    string SessionKey,
    string GroupKey,
    string Name,
    string? Path,
    double Level,
    bool Muted,
    bool Active,
    bool IsSystem);

/// <summary>
/// One row of the mixer: an application or a single session ([S-3251]: never the device's own
/// level). The volume tile reuses the shape for its one level, keyed <see cref="MixerRules.MasterKey"/>.
/// </summary>
/// <param name="Sessions">How many sessions the row drives.</param>
public sealed record MixerRow(
    string Key,
    string Name,
    string? Path,
    double Level,
    bool Muted,
    bool Active,
    bool IsSystem,
    int Sessions);

/// <summary>[S-1227] How a mixer row is laid out, by the width it has.</summary>
public enum MixerRowStyle
{
    /// <summary>No slider: icon and mute.</summary>
    Compact,

    /// <summary>Name and mute on top, the slider under them.</summary>
    TwoLine,

    /// <summary>Name, slider and mute on one line.</summary>
    OneLine,
}

/// <summary>Which rows the mixer admits, straight from its config.</summary>
public readonly record struct MixerRowOptions(bool ShowSystemSounds, bool OnlyActive);

/// <summary>
/// The mixer's rules: how sessions become rows, which rows are shown, and how a level the user has
/// dragged to is kept on screen until Windows confirms it.
/// <para>
/// Kept apart from the COM code so the part that decides what the user sees can be tested without
/// an audio device — CI has none.
/// </para>
/// </summary>
public static class MixerRules
{
    /// <summary>
    /// The system-sounds session's key. Disjoint from the other two key spaces — an executable path
    /// and a session identifier — so a write can match a key against both without being told which
    /// mode produced it.
    /// </summary>
    public const string SystemSoundsKey = "system-sounds";

    /// <summary>
    /// The device's own level as a write key (the volume tile's), disjoint from every session key,
    /// so writes dispatch on it. The mixer never shows it ([S-3251]).
    /// </summary>
    public const string MasterKey = "__master__";

    /// <summary>
    /// [S-1940] [S-1941] The row a mixer with no device to read draws, disabled: the system sounds, which
    /// every output device has, at the middle. Never written: the view refuses every write while it shows.
    /// </summary>
    public static MixerRow StandIn { get; } = new(SystemSoundsKey, string.Empty, null, Unavailable.Share, false, false, true, 1);

    /// <summary>[S-1227] From this row width a row is one line.</summary>
    public const double OneLineWidth = 360;

    /// <summary>[S-1227] Below this row width there is no room for a slider.</summary>
    public const double SliderWidth = 170;

    /// <summary>[S-1227] The row layout a width affords (the [S-449] thresholds).</summary>
    public static MixerRowStyle RowStyle(double width)
        => width >= OneLineWidth ? MixerRowStyle.OneLine
        : width >= SliderWidth ? MixerRowStyle.TwoLine
        : MixerRowStyle.Compact;

    /// <summary>
    /// [S-442] Sessions into rows. Grouped, every session of one executable is one row: the loudest
    /// level, muted only when every session is, active when any is, and a count of what it drives —
    /// a browser with three noisy tabs is one slider. Ungrouped, one row per session.
    /// <para>
    /// [S-443] Ordered by name, case-insensitively, system sounds last. Deliberately not by activity
    /// or level: a row that moves the moment an app starts playing slides out from under the slider
    /// being dragged.
    /// </para>
    /// </summary>
    public static List<MixerRow> Group(IEnumerable<AudioSessionInfo> sessions, bool grouped)
    {
        ArgumentNullException.ThrowIfNull(sessions);

        var rows = new List<MixerRow>();
        var at = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var session in sessions)
        {
            var key = grouped ? session.GroupKey : session.SessionKey;

            if (at.TryGetValue(key, out var index))
            {
                var existing = rows[index];
                rows[index] = existing with
                {
                    Level = Math.Max(existing.Level, ClampLevel(session.Level)),
                    Muted = existing.Muted && session.Muted,
                    Active = existing.Active || session.Active,
                    Sessions = existing.Sessions + 1,
                    Name = existing.Name.Length > 0 ? existing.Name : session.Name,
                    Path = existing.Path ?? session.Path,
                };

                continue;
            }

            at[key] = rows.Count;
            rows.Add(new MixerRow(
                key,
                session.Name,
                session.Path,
                ClampLevel(session.Level),
                session.Muted,
                session.Active,
                session.IsSystem,
                1));
        }

        rows.Sort(static (a, b) =>
        {
            var system = a.IsSystem.CompareTo(b.IsSystem);
            if (system != 0)
            {
                return system;
            }

            var name = string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            return name != 0 ? name : string.CompareOrdinal(a.Key, b.Key);
        });

        return rows;
    }

    /// <summary>
    /// [S-441] A session's name, in the order Task Manager prefers: the executable's description,
    /// then its file name, then the session's own display name — unless that is an unexpanded
    /// resource reference (<c>@%SystemRoot%\…,-202</c>). Empty means "nothing usable"; the view
    /// supplies its own placeholder.
    /// </summary>
    public static string SessionName(string? description, string? exePath, string? display)
    {
        if (AppNames.Sanitize(description) is { } described)
        {
            return described;
        }

        if (AppNames.ExeStem(exePath) is { } stem)
        {
            return stem;
        }

        var shown = display?.Trim() ?? string.Empty;
        return shown.Length == 0 || shown.StartsWith('@') ? string.Empty : shown;
    }

    /// <summary>
    /// [S-445]/[S-3251] The rows on screen: the app rows the options admit, in the order
    /// <see cref="Group"/> gave them, and nothing else (no master row: the device's level is the
    /// volume tile's). Only-active never hides system sounds, whose session is idle between chimes
    /// and would blink in and out.
    /// </summary>
    public static List<MixerRow> BuildRows(IReadOnlyList<MixerRow> rows, MixerRowOptions options)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var shown = new List<MixerRow>(rows.Count);

        foreach (var row in rows)
        {
            if (row.IsSystem && !options.ShowSystemSounds)
            {
                continue;
            }

            if (options.OnlyActive && !row.Active && !row.IsSystem)
            {
                continue;
            }

            shown.Add(row);
        }

        return shown;
    }

    /// <summary>
    /// [S-1228] With only-active on, the apps it left out: holding a session but silent. Shown dimmed
    /// under the playing ones when the card has room, so a large mixer is not mostly empty. Empty
    /// when only-active is off (they are already in the list) — never system sounds.
    /// </summary>
    public static List<MixerRow> QuietRows(IReadOnlyList<MixerRow> rows, MixerRowOptions options)
    {
        ArgumentNullException.ThrowIfNull(rows);

        return options.OnlyActive
            ? [.. rows.Where(static row => !row.Active && !row.IsSystem)]
            : [];
    }

    /// <summary>[S-447] The slider's whole-percent position. A muted row sits at 0 but keeps its level, so unmuting restores it.</summary>
    public static int DisplayPercent(MixerRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        return row.Muted ? 0 : (int)Math.Round(ClampLevel(row.Level) * 100, MidpointRounding.AwayFromZero);
    }

    /// <summary>0..1, and a non-finite level is 0: it crossed a COM boundary and feeds a slider.</summary>
    public static double ClampLevel(double level) => double.IsFinite(level) ? Math.Clamp(level, 0, 1) : 0;

    /// <summary>
    /// [S-446] Levels the user dragged to that Windows has not confirmed yet. An override is kept
    /// only while it still says something: dropped once a snapshot reports that level, and once its
    /// row is gone. Purely value-driven — nothing waits for the write to have "probably" landed.
    /// </summary>
    public static Dictionary<string, double> ReconcileOverrides(
        IReadOnlyDictionary<string, double> overrides,
        IReadOnlyList<MixerRow> rows,
        double epsilon = 0.005)
    {
        ArgumentNullException.ThrowIfNull(overrides);
        ArgumentNullException.ThrowIfNull(rows);

        var byKey = new Dictionary<string, MixerRow>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            byKey[row.Key] = row;
        }

        var kept = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var (key, level) in overrides)
        {
            if (!byKey.TryGetValue(key, out var row))
            {
                continue;
            }

            if (Math.Abs(ClampLevel(row.Level) - level) < epsilon)
            {
                continue;
            }

            kept[key] = level;
        }

        return kept;
    }

    /// <summary>
    /// [S-446]/[S-447] Paint the rows with any unconfirmed level. Dragging a muted row above zero also
    /// shows it unmuted, matching the write the slider sends.
    /// </summary>
    public static List<MixerRow> ApplyOverrides(IReadOnlyList<MixerRow> rows, IReadOnlyDictionary<string, double> overrides)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(overrides);

        var result = new List<MixerRow>(rows.Count);
        foreach (var row in rows)
        {
            result.Add(overrides.TryGetValue(row.Key, out var level)
                ? row with { Level = level, Muted = row.Muted && level <= 0 }
                : row);
        }

        return result;
    }
}

/// <summary>
/// [S-446] Writes waiting to be sent, newest value per key.
/// <para>
/// A slider drag raises a value per pointer move. Sending each one would queue a hundred blocking
/// calls and land a stale value after the user let go; only the newest value per row is true. The
/// worker drains this when it is free, so the next write goes out when the previous one has
/// finished — single-flight and event-driven, with no throttle interval to tune. One slot per key,
/// so muting one app never discards the level still queued for another.
/// </para>
/// </summary>
public sealed class LatestWrites<T>
{
    private readonly Lock _gate = new();
    private readonly List<string> _order = [];
    private readonly Dictionary<string, T> _values = new(StringComparer.Ordinal);

    /// <summary>Queue a value, replacing anything still waiting for the same key.</summary>
    public void Post(string key, T value)
    {
        ArgumentNullException.ThrowIfNull(key);

        lock (_gate)
        {
            if (!_values.ContainsKey(key))
            {
                _order.Add(key);
            }

            _values[key] = value;
        }
    }

    /// <summary>Take everything waiting, in the order the keys first arrived, and leave it empty.</summary>
    public List<(string Key, T Value)> Drain()
    {
        lock (_gate)
        {
            var drained = new List<(string, T)>(_order.Count);
            foreach (var key in _order)
            {
                drained.Add((key, _values[key]));
            }

            _order.Clear();
            _values.Clear();
            return drained;
        }
    }

    public bool IsEmpty
    {
        get
        {
            lock (_gate)
            {
                return _order.Count == 0;
            }
        }
    }
}
