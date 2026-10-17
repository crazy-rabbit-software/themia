using Themia.Core.Localization;
using Themia.Core.Model;

namespace Themia.Core.Sync;

/// <summary>
/// [S-2608] [S-6906] What a widget wants the background sync to keep fresh. Implemented by its
/// config, so <see cref="SyncInterests.From"/> collects every widget's wants without a switch over
/// config types; a config without it wants nothing synced.
/// </summary>
public interface IInterests
{
    void AddInterests(InterestsBuilder interests);
}

/// <summary>
/// [S-2608] The wants of every widget on every view, collected once per state change: each widget's
/// <see cref="IInterests"/> adds to the list its source reads, at the shared cadences ([S-3780]).
/// </summary>
public sealed class InterestsBuilder
{
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

    public List<MailInterest> Mail { get; } = [];

    public List<CalendarInterest> Calendar { get; } = [];

    public List<ContactsInterest> Contacts { get; } = [];

    public List<TaskInterest> Tasks { get; } = [];

    public List<FeedInterest> Feeds { get; } = [];

    public List<WeatherInterest> Weather { get; } = [];

    public List<QuoteInterest> Quotes { get; } = [];

    public List<AirInterest> Air { get; } = [];

    public List<RainInterest> Rain { get; } = [];

    /// <summary>[S-5140] The one reference-rate fetch every exchange-rates widget shares.</summary>
    public RatesInterest? Rates { get; set; }

    public SyncInterests Build() => new(Mail, Calendar, Contacts, Tasks, Feeds, Weather, Quotes)
    {
        AccountInterval = AccountInterval,
        Air = Air,
        Rain = Rain,
        Rates = Rates,
    };
}
