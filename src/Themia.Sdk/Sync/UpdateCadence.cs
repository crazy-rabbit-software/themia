using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.App.Shell;
using Themia.Sdk;

namespace Themia.Core.Sync;

/// <summary>
/// [S-3780] The host's own rows of ⚙ › Online updates: the kinds of online source it asks
/// itself. A provider module with a kit service adds its own row ([S-6910]).
/// </summary>
public static class HostCadences
{
    /// <summary>[S-3781] Weather: never faster than open-meteo's own 15-minute cycle.</summary>
    public static readonly CadenceRow Weather = new()
    {
        Id = new("weather"), Label = Str.WidgetPrefs_Updates_Weather.Local(), Glyph = Glyphs.Weather,
        Choices = [15, 30, 60, 120], DefaultMinutes = 30, Rank = 0,
    };

    /// <summary>[S-3781] Markets (Yahoo, CoinGecko): a minute is the floor, as both quote services are unauthenticated.</summary>
    public static readonly CadenceRow Markets = new()
    {
        Id = new("markets"), Label = Str.WidgetPrefs_Updates_Markets.Local(), Glyph = Glyphs.Stocks,
        Choices = [1, 5, 15, 30, 60], DefaultMinutes = 5, Rank = 1,
    };

    /// <summary>[S-3781] News feeds: publishers rarely post more than a few times an hour.</summary>
    public static readonly CadenceRow Feeds = new()
    {
        Id = new("feeds"), Label = Str.WidgetPrefs_Updates_Feeds.Local(), Glyph = Glyphs.Rss,
        Choices = [15, 30, 60, 120], DefaultMinutes = 30, Rank = 2,
    };

    /// <summary>[S-3781] Mail, calendars and task lists of the signed-in accounts: mail is probed cheaply (counts only), so it may go to a minute.</summary>
    public static readonly CadenceRow Accounts = new()
    {
        Id = new("accounts"), Label = Str.WidgetPrefs_Updates_Accounts.Local(), Glyph = Glyphs.Mail,
        Choices = [1, 2, 5, 15, 30], DefaultMinutes = 2, Rank = 3,
    };

    public static IReadOnlyList<CadenceRow> All { get; } = [Weather, Markets, Feeds, Accounts];
}

/// <summary>
/// [S-3780] How often each kind of online source is asked, for every widget at once. Set in
/// ⚙ › Online updates, never per widget: two weather widgets on one desktop asking at different
/// rates is a setting nobody can reason about, and the services behind them are shared and free.
/// <para>
/// Each row offers a short closed list; anything else (a hand edit) snaps to the nearest choice,
/// and a row without an entry follows its default. <see cref="Current"/> is what the running app
/// uses, set from the state whenever it changes; the pure overloads take the preferences, for the
/// sync and the tests.
/// </para>
/// </summary>
public static class UpdateCadence
{
    private static volatile RefreshPreferences _current = RefreshPreferences.WithDefaults();

    /// <summary>Every row of the page: the host's, then the providers' own, by rank.</summary>
    public static IReadOnlyList<CadenceRow> Rows
        => [.. HostCadences.All.Concat(ProviderCatalog.All.Select(p => p.Cadence).OfType<CadenceRow>()).OrderBy(r => r.Rank)];

    /// <summary>The preferences the running app follows. Replaced whole, never edited in place.</summary>
    public static RefreshPreferences Current
    {
        get => _current;
        set => _current = (value ?? throw new ArgumentNullException(nameof(value))).Clone();
    }

    /// <summary>The stored minutes for <paramref name="row"/>, snapped to an offered choice; its default when none is stored.</summary>
    public static int Minutes(RefreshPreferences preferences, CadenceRow row)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        ArgumentNullException.ThrowIfNull(row);

        return preferences.TryGetValue(row.Id.Value, out var stored) ? Snap(stored, row.Choices) : row.DefaultMinutes;
    }

    public static TimeSpan Interval(RefreshPreferences preferences, CadenceRow row)
        => TimeSpan.FromMinutes(Minutes(preferences, row));

    /// <summary>The running app's interval for <paramref name="row"/>.</summary>
    public static TimeSpan Interval(CadenceRow row) => Interval(_current, row);

    public static void Set(RefreshPreferences preferences, CadenceRow row, int minutes)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        ArgumentNullException.ThrowIfNull(row);

        preferences[row.Id.Value] = Snap(minutes, row.Choices);
    }

    /// <summary>
    /// [S-3781] Every stored cadence back on an offered choice, and an entry that is no row's id
    /// dropped; each change is said. A row without an entry is not a repair: it follows its default.
    /// </summary>
    public static IReadOnlyList<string> Repair(RefreshPreferences preferences) => Repair(preferences, Rows);

    /// <summary>The repair over explicit rows (the tests' and a host without providers').</summary>
    public static IReadOnlyList<string> Repair(RefreshPreferences preferences, IReadOnlyList<CadenceRow> rows)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        ArgumentNullException.ThrowIfNull(rows);

        var repairs = new List<string>();
        foreach (var key in preferences.Keys.ToList())
        {
            if (rows.FirstOrDefault(r => string.Equals(r.Id.Value, key, StringComparison.Ordinal)) is not { } row)
            {
                repairs.Add($"refresh: '{key}' is no cadence of this build; dropped");
                preferences.Remove(key);
                continue;
            }

            var before = preferences[key];
            var after = Snap(before, row.Choices);
            if (after != before)
            {
                repairs.Add($"refresh: {row.Id} every {before} min is not offered; every {after} min");
                preferences[key] = after;
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
