using System.Text.Json.Serialization;

namespace Themia.Core.Model;

/// <summary>
/// [S-702] How the app behaves, as opposed to how widgets look (<see cref="DesignState"/>).
/// <para>
/// Kept apart from the design so an appearance preset or a design reset can never switch off, say,
/// game mode by accident.
/// </para>
/// </summary>
public sealed class AppPreferences
{
    public AppLanguage Language { get; set; } = AppLanguage.System;

    /// <summary>Hide the widgets while a full-screen game or presentation has the screen.</summary>
    public bool GameModeSuspend { get; set; }

    /// <summary>Whether Explorer's desktop icons are shown under the widgets.</summary>
    public bool DesktopIconsVisible { get; set; } = true;

    /// <summary>[S-3770] How every widget writes times and dates and measures temperature.</summary>
    public RegionPreferences Region { get; set; } = new();

    /// <summary>[S-3780] How often the widgets that ask an online service ask it.</summary>
    public RefreshPreferences Refresh { get; set; } = RefreshPreferences.WithDefaults();

    public AppPreferences Clone() => new()
    {
        Language = Language,
        GameModeSuspend = GameModeSuspend,
        DesktopIconsVisible = DesktopIconsVisible,
        Region = Region.Clone(),
        Refresh = Refresh.Clone(),
    };
}

/// <summary>
/// [S-3780] The update cadences, in minutes, keyed by the id of their row of Settings › Widgets ›
/// Updates (<see cref="Sync.HostCadences"/>, and a provider's own, [S-6910]): <c>"refresh": {
/// "weather": 30, "markets": 5, ... }</c>. Each is snapped to its row's offered choices on load and an
/// entry that is no row's is dropped (<see cref="Sync.UpdateCadence.Repair(RefreshPreferences)"/>), so
/// a hand edit cannot ask a free service every second; a row without an entry follows its default.
/// </summary>
public sealed class RefreshPreferences : Dictionary<string, int>
{
    public RefreshPreferences()
        : base(StringComparer.Ordinal)
    {
    }

    private RefreshPreferences(IDictionary<string, int> entries)
        : base(entries, StringComparer.Ordinal)
    {
    }

    /// <summary>The host's rows at their defaults: what a new state writes.</summary>
    public static RefreshPreferences WithDefaults()
    {
        var preferences = new RefreshPreferences();
        foreach (var row in Sync.HostCadences.All)
        {
            preferences[row.Id.Value] = row.DefaultMinutes;
        }

        return preferences;
    }

    public RefreshPreferences Clone() => new(this);
}
