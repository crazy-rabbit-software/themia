namespace Themia.Core.Model;

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
