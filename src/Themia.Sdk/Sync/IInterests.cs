using Themia.Core.Localization;
using Themia.Core.Model;

namespace Themia.Core.Sync;

/// <summary>
/// [S-2608] [S-6906] What a widget wants the background sync to keep fresh. Implemented by its
/// config, so the host's <c>SyncInterests.From</c> collects every widget's wants without a switch over
/// config types; a config without it wants nothing synced.
/// </summary>
public interface IInterests
{
    void AddInterests(InterestsBuilder interests);
}

/// <summary>
/// [S-2608] The wants of every widget on every view, collected once per state change: each widget's
/// <see cref="IInterests"/> adds to the list its source reads (<see cref="Of{T}"/>, one list per
/// interest type), at the shared cadences ([S-3780]). The SDK names no source: the host reads back
/// the lists of the interest types its services serve.
/// </summary>
public sealed class InterestsBuilder
{
    private readonly Dictionary<Type, object> _lists = [];

    public InterestsBuilder(RefreshPreferences refresh, TemperatureUnit unit)
    {
        ArgumentNullException.ThrowIfNull(refresh);

        Refresh = refresh;
        Unit = unit;
        AccountInterval = UpdateCadence.Interval(refresh, HostCadences.Accounts);
        FeedInterval = UpdateCadence.Interval(refresh, HostCadences.Feeds);
    }

    /// <summary>[S-3780] The shared cadences, from the state itself, so the sync follows a change on the same state change.</summary>
    public RefreshPreferences Refresh { get; }

    /// <summary>[S-3770] The shared temperature scale.</summary>
    public TemperatureUnit Unit { get; }

    public TimeSpan AccountInterval { get; }

    public TimeSpan FeedInterval { get; }

    /// <summary>The wants of one interest type, in the order the widgets added them.</summary>
    public List<T> Of<T>()
        where T : class
    {
        if (_lists.TryGetValue(typeof(T), out var list))
        {
            return (List<T>)list;
        }

        var created = new List<T>();
        _lists[typeof(T)] = created;
        return created;
    }
}
