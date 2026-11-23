using Themia.App.Services.Office;
using Themia.Core.Accounts;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Office;
using Themia.Core.Sync;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;

namespace Themia.Widget.Contacts;

/// <summary>
/// [S-588] [S-7077] The people both contacts widgets read: the office widgets' one feed over the shared
/// <see cref="IOfficeSource"/>, every chosen account's people merged; a load asked for meanwhile runs after it. A sample's
/// people stand in for it (<see cref="Held"/>).
/// </summary>
internal sealed class ContactsFeed
{
    private readonly Func<ContactsBaseConfig> _config;
    private readonly Func<int> _count;
    private readonly OfficeFeed<IReadOnlyList<Contact>> _feed;

    public ContactsFeed(Func<ContactsBaseConfig> config, Func<int> count)
    {
        _config = config;
        _count = count;
        _feed = new OfficeFeed<IReadOnlyList<Contact>>(LoadAsync, SyncSubject.Contacts, static locale => ContactsStr.WidgetsA_Contacts_ReadFailed.Text(locale));
        _feed.Changed += () => Changed?.Invoke();
    }

    /// <summary>A load went out or finished: draw again.</summary>
    public event Action? Changed;

    /// <summary>The source it reads (null: none yet).</summary>
    public IOfficeSource? Source { get; set; }

    /// <summary>[S-11850] A sample's people in place of the feed's, or null.</summary>
    public IReadOnlyList<Contact>? Held { get; set; }

    /// <summary>The key of what the last load asked for: a change of accounts or count loads again.</summary>
    public string RequestKey { get; private set; } = string.Empty;

    public bool Busy => Held is null && _feed.Busy;

    /// <summary>The people the last load (or a sample) brought.</summary>
    public IReadOnlyList<Contact> People => Held ?? _feed.Data ?? [];

    /// <summary>[S-7077] The screen the last load calls for; a sample's people are always the list.</summary>
    public KitScreen Screen(Locale locale) => Held is not null ? new(KitScreenKind.Items, string.Empty, false, []) : _feed.Screen(locale);

    /// <summary>[S-7042] The stale rule's outcomes.</summary>
    public IReadOnlyList<KitOutcome> Outcomes => _feed.Outcomes;

    public void Start() => _feed.Start();

    public void Stop() => _feed.Stop();

    /// <summary>A render's load held as if it had come back.</summary>
    public void Hold(OfficeLoad<IReadOnlyList<Contact>> load) => _feed.Hold(load);

    public string Key() => $"{string.Join(",", _config().AccountIds)}|{ContactRules.FetchSize(_count())}";

    public Task RefreshAsync(bool force) => Held is not null ? Task.CompletedTask : _feed.RefreshAsync(force);

    /// <summary>One load of every chosen account's people, merged; each account's problem by its label.</summary>
    private async Task<OfficeLoad<IReadOnlyList<Contact>>> LoadAsync(bool force, CancellationToken token)
    {
        var source = Source;
        var accounts = source?.Accounts(HostFeatures.Contacts) ?? [];
        RequestKey = Key();

        if (source is null || accounts.Count == 0)
        {
            return OfficeLoad<IReadOnlyList<Contact>>.Of(OfficeLoadState.NoAccounts);
        }

        var selected = CalendarRules.SelectAccounts(_config().AccountIds, [.. accounts.Select(a => a.Id)]);
        if (selected.Count == 0)
        {
            return OfficeLoad<IReadOnlyList<Contact>>.Of(OfficeLoadState.AccountsGone);
        }

        var labels = accounts.ToDictionary(a => a.Id, a => a.Label, StringComparer.Ordinal);
        var size = ContactRules.FetchSize(_count());
        var tasks = selected.Select(a => source.ContactsAsync(a, size, force, token)).ToList();
        await Task.WhenAll(tasks).ConfigureAwait(true);

        var results = tasks.Select(t => t.Result).ToList();
        var problems = results.Select((r, i) => new OfficeProblem(selected[i], r.Status, r.At)).Where(p => OfficeProblems.IsProblem(p.Status)).ToList();
        return new OfficeLoad<IReadOnlyList<Contact>>(OfficeLoadState.Asked, ContactRules.Merge(results.Select(r => r.Value ?? [])), results.Any(r => r.Value is not null), problems, labels);
    }
}
