using Themia.App.Services.Accounts;
using Themia.App.Services.Office;
using Themia.Core.Accounts;
using Themia.Core.Model;
using Themia.Core.Office;

namespace Themia.App.Widgets;

/// <summary>What a calendar fetch could say before it had anything to show.</summary>
internal enum CalendarSliceState
{
    /// <summary>No account can feed a calendar.</summary>
    NoAccounts,

    /// <summary>The accounts the widget chose are all signed out.</summary>
    AccountsGone,

    /// <summary>Asked: events, birthdays and the problems met on the way are in the slice.</summary>
    Asked,
}

/// <summary>
/// [S-2701] One fetch of the calendar for a date range: the events (stamped with their account,
/// calendar and fetch index, which pick their colour), the birthdays, the targets asked, each
/// account's provider (for opening an event), and every problem met, by account.
/// </summary>
internal sealed record CalendarSlice(
    CalendarSliceState State,
    IReadOnlyList<CalendarEvent> Events,
    IReadOnlyList<Birthday> Birthdays,
    IReadOnlyList<CalendarTarget> Targets,
    IReadOnlyDictionary<string, AccountProvider> Providers,
    IReadOnlyDictionary<string, string> Labels,
    IReadOnlyList<(string Account, SyncProblem Problem)> Problems,
    bool AnyData)
{
    public static CalendarSlice Empty(CalendarSliceState state)
        => new(state, [], [], [], new Dictionary<string, AccountProvider>(), new Dictionary<string, string>(), [], false);
}

/// <summary>
/// [S-2701] The calendar widget's one seam to its data: everything it shows for a range of days
/// comes through <see cref="LoadAsync"/>, so the source behind it (the live account source, an
/// offline store, a render's fixture) is a one-place swap. The view asks for exactly the range its
/// layout draws ([S-568]).
/// </summary>
internal interface ICalendarData
{
    /// <summary>Raised on the UI thread when what a load would return may have changed (an account came or went).</summary>
    event Action? Changed;

    /// <summary>The events and birthdays from <paramref name="first"/> to <paramref name="last"/>, both inclusive, local days.</summary>
    Task<CalendarSlice> LoadAsync(CalendarConfig config, DateOnly first, DateOnly last, bool force, CancellationToken cancellation);
}

/// <summary>[S-2701] The live seam: Themia's own accounts through the shared <see cref="IOfficeSource"/> ([S-588]).</summary>
internal sealed class OfficeCalendarData(IOfficeSource source) : ICalendarData
{
    public event Action? Changed
    {
        add => source.Changed += value;
        remove => source.Changed -= value;
    }

    public async Task<CalendarSlice> LoadAsync(CalendarConfig config, DateOnly first, DateOnly last, bool force, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(config);

        var accounts = source.Accounts(AccountFeatures.Calendar);
        if (accounts.Count == 0)
        {
            return CalendarSlice.Empty(CalendarSliceState.NoAccounts);
        }

        var selected = CalendarRules.SelectAccounts(config.AccountIds, [.. accounts.Select(a => a.Id)]);
        if (selected.Count == 0)
        {
            return CalendarSlice.Empty(CalendarSliceState.AccountsGone);
        }

        var labels = accounts.ToDictionary(a => a.Id, a => a.Label, StringComparer.Ordinal);
        var providers = accounts.ToDictionary(a => a.Id, a => a.Provider, StringComparer.Ordinal);
        var targets = CalendarRules.Targets(selected, config.Calendars);
        var (from, to) = CalendarRules.FetchWindow(first, last, TimeZoneInfo.Local);

        var eventTasks = targets.Select(t => source.EventsAsync(t, from, to, force, cancellation)).ToList();
        var birthdayTasks = config.ShowBirthdays
            ? selected.Select(a => source.BirthdaysAsync(a, force, cancellation)).ToList()
            : [];

        await Task.WhenAll(eventTasks.Cast<Task>().Concat(birthdayTasks)).ConfigureAwait(true);

        var events = new List<CalendarEvent>();
        var problems = new List<(string Account, SyncProblem Problem)>();
        var anyData = false;

        for (var i = 0; i < targets.Count; i++)
        {
            var result = eventTasks[i].Result;
            if (result.Value is { } list)
            {
                anyData = true;
                events.AddRange(list.Select(e => e with { AccountId = targets[i].AccountId, CalendarId = targets[i].CalendarId, SourceIndex = i }));
            }

            if (result.Problem != SyncProblem.None)
            {
                problems.Add((targets[i].AccountId, result.Problem));
            }
        }

        var birthdays = new List<Birthday>();
        for (var i = 0; i < birthdayTasks.Count; i++)
        {
            var result = birthdayTasks[i].Result;
            birthdays.AddRange(result.Value ?? []);

            // [S-575] A refused address book costs the birthdays, never the calendar: it is a
            // warning on the header, not an error over the events.
            if (result.Problem != SyncProblem.None)
            {
                problems.Add((selected[i], result.Problem));
            }
        }

        return new CalendarSlice(CalendarSliceState.Asked, events, BirthdayRules.Distinct(birthdays), targets, providers, labels, problems, anyData);
    }
}
