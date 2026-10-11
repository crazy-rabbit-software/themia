namespace Themia.Core.Widgets;

/// <summary>
/// [S-3322] What every screen-time widget leaves out and judges against: the hidden apps and the
/// daily limits. One set for the machine, not one per widget, so the chart, the donut and the app
/// list on a view always add up to the same figures. Kept with the history in
/// <c>usage_stats.json</c> ([S-459]): app keys are what the user did with their day, so they never
/// travel with a layout, an export or a copied widget.
/// </summary>
public sealed class ScreenTimePreferences
{
    /// <summary>[S-455] App keys left out of every widget.</summary>
    public HashSet<string> Hidden { get; } = new(StringComparer.Ordinal);

    /// <summary>[S-457] A daily limit on the whole, in seconds; zero or less is none.</summary>
    public long GoalSeconds { get; set; }

    /// <summary>[S-457] Daily limits on single apps, by key, in seconds. Only positive limits are kept.</summary>
    public Dictionary<string, long> AppLimits { get; } = new(StringComparer.Ordinal);

    public bool IsEmpty => Hidden.Count == 0 && GoalSeconds <= 0 && AppLimits.Count == 0;

    public ScreenTimePreferences Copy()
    {
        var copy = new ScreenTimePreferences { GoalSeconds = GoalSeconds };
        copy.Hidden.UnionWith(Hidden);

        foreach (var (app, seconds) in AppLimits)
        {
            copy.AppLimits[app] = seconds;
        }

        return copy;
    }

    /// <summary>
    /// [S-3322] The preferences, cleaned the way a file is read: no blank keys, no limit of zero or
    /// less, keys trimmed. A hand-edited file is repaired here, never half-applied.
    /// </summary>
    public static ScreenTimePreferences From(IEnumerable<string>? hidden, long goalSeconds, IEnumerable<KeyValuePair<string, long>>? limits)
    {
        var clean = new ScreenTimePreferences { GoalSeconds = Math.Max(0, goalSeconds) };

        foreach (var key in hidden ?? [])
        {
            if (!string.IsNullOrWhiteSpace(key))
            {
                clean.Hidden.Add(key.Trim());
            }
        }

        foreach (var (app, seconds) in limits ?? [])
        {
            if (!string.IsNullOrWhiteSpace(app) && seconds > 0)
            {
                clean.AppLimits[app.Trim()] = seconds;
            }
        }

        return clean;
    }
}
