using Themia.Core.Model;

namespace Themia.Core.Sync;

/// <summary>The four kinds of online source whose cadence is set once for every widget ([S-3780]).</summary>
public enum UpdateSource
{
    Weather = 0,
    Markets = 1,
    Feeds = 2,
    Accounts = 3,
}

/// <summary>
/// [S-3780] How often each kind of online source is asked, for every widget at once. Set in
/// Settings › Widgets, never per widget: two weather widgets on one desktop asking at different
/// rates is a setting nobody can reason about, and the services behind them are shared and free.
/// <para>
/// Each source offers a short closed list; anything else (a hand edit) snaps to the nearest
/// choice. <see cref="Current"/> is what the running app uses, set from the state whenever it
/// changes; the pure overloads take the preferences, for the sync and the tests.
/// </para>
/// </summary>
public static class UpdateCadence
{
    private static volatile RefreshPreferences _current = new();

    /// <summary>[S-3781] Weather: never faster than open-meteo's own 15-minute cycle.</summary>
    public static IReadOnlyList<int> WeatherChoices { get; } = [15, 30, 60, 120];

    /// <summary>[S-3781] Markets: a minute is the floor, as both quote services are unauthenticated.</summary>
    public static IReadOnlyList<int> MarketsChoices { get; } = [1, 5, 15, 30, 60];

    /// <summary>[S-3781] Feeds: publishers rarely post more than a few times an hour.</summary>
    public static IReadOnlyList<int> FeedsChoices { get; } = [15, 30, 60, 120];

    /// <summary>[S-3781] Accounts: mail is probed cheaply (counts only), so it may go to a minute.</summary>
    public static IReadOnlyList<int> AccountsChoices { get; } = [1, 2, 5, 15, 30];

    /// <summary>The preferences the running app follows. Replaced whole, never edited in place.</summary>
    public static RefreshPreferences Current
    {
        get => _current;
        set => _current = (value ?? throw new ArgumentNullException(nameof(value))).Clone();
    }

    public static IReadOnlyList<int> Choices(UpdateSource source) => source switch
    {
        UpdateSource.Weather => WeatherChoices,
        UpdateSource.Markets => MarketsChoices,
        UpdateSource.Feeds => FeedsChoices,
        _ => AccountsChoices,
    };

    /// <summary>The stored minutes for <paramref name="source"/>, snapped to an offered choice.</summary>
    public static int Minutes(RefreshPreferences preferences, UpdateSource source)
    {
        ArgumentNullException.ThrowIfNull(preferences);

        var stored = source switch
        {
            UpdateSource.Weather => preferences.WeatherMinutes,
            UpdateSource.Markets => preferences.MarketsMinutes,
            UpdateSource.Feeds => preferences.FeedsMinutes,
            _ => preferences.AccountsMinutes,
        };

        return Snap(stored, Choices(source));
    }

    public static TimeSpan Interval(RefreshPreferences preferences, UpdateSource source)
        => TimeSpan.FromMinutes(Minutes(preferences, source));

    /// <summary>The running app's interval for <paramref name="source"/>.</summary>
    public static TimeSpan Interval(UpdateSource source) => Interval(_current, source);

    public static void Set(RefreshPreferences preferences, UpdateSource source, int minutes)
    {
        ArgumentNullException.ThrowIfNull(preferences);

        var value = Snap(minutes, Choices(source));
        switch (source)
        {
            case UpdateSource.Weather:
                preferences.WeatherMinutes = value;
                break;
            case UpdateSource.Markets:
                preferences.MarketsMinutes = value;
                break;
            case UpdateSource.Feeds:
                preferences.FeedsMinutes = value;
                break;
            default:
                preferences.AccountsMinutes = value;
                break;
        }
    }

    /// <summary>[S-3781] Every cadence back on an offered choice; each change is said.</summary>
    public static IReadOnlyList<string> Repair(RefreshPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);

        var repairs = new List<string>();

        foreach (var source in Enum.GetValues<UpdateSource>())
        {
            var before = source switch
            {
                UpdateSource.Weather => preferences.WeatherMinutes,
                UpdateSource.Markets => preferences.MarketsMinutes,
                UpdateSource.Feeds => preferences.FeedsMinutes,
                _ => preferences.AccountsMinutes,
            };

            var after = Minutes(preferences, source);
            if (after != before)
            {
                repairs.Add($"refresh: {source} every {before} min is not offered; every {after} min");
                Set(preferences, source, after);
            }
        }

        return repairs;
    }

    /// <summary>The nearest offered choice; a tie goes to the slower one (kinder to the service).</summary>
    public static int Snap(int minutes, IReadOnlyList<int> choices)
    {
        ArgumentNullException.ThrowIfNull(choices);

        var best = choices[0];
        foreach (var choice in choices)
        {
            if (Math.Abs(choice - minutes) <= Math.Abs(best - minutes))
            {
                best = choice;
            }
        }

        return best;
    }
}
