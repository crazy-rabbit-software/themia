using Themia.App.Services.Mail;
using Themia.Core.Mail;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;

namespace Themia.Widget.Email;

/// <summary>
/// [S-6421] The mail feed of one widget as the kit reads it: the host's one <see cref="IMailSource"/>
/// opened for the config's accounts, folder and count, its messages as kit items, each account's
/// last fetch as a <see cref="KitOutcome"/>, the accounts as <see cref="KitAccount"/>s, and the
/// widget's own writes (a change waiting or refused, [S-2624]) as the header's notes. It asks the
/// network for nothing itself: the feed is pushed, a Refresh is the feed's.
/// </summary>
internal sealed class MailKitSource : IKitSource, IDisposable
{
    private readonly IMailSource? _source;
    private readonly Func<EmailConfig> _config;
    private readonly Dictionary<string, MailMessage> _messages = new(StringComparer.Ordinal);

    private IMailFeed? _feed;
    private string? _feedKey;
    private string? _problem;
    private bool _refreshing;
    private bool _disposed;

    /// <param name="source">The host's mail, or null where there is none (the widget then waits for setup).</param>
    public MailKitSource(IMailSource? source, Func<EmailConfig> config)
    {
        _source = source;
        _config = config;

        if (_source is not null)
        {
            _source.AccountsChanged += OnChanged;
        }
    }

    public event Action? Changed;

    /// <summary>The accounts it reads from, signed in or not: where a new message can come from ([S-558]).</summary>
    public IReadOnlyList<MailAccount> Targets
    {
        get
        {
            if (_source is null)
            {
                return [];
            }

            var chosen = _config().EffectiveAccountIds;
            return chosen.Count == 0 ? _source.Accounts : [.. _source.Accounts.Where(a => chosen.Contains(a.Id, StringComparer.Ordinal))];
        }
    }

    /// <summary>The messages the feed holds now, as they came (for compose to find the account's web host).</summary>
    public IReadOnlyList<MailMessage> Held => _feed?.Snapshot.Messages ?? [];

    /// <summary>[S-6421] A ring only for a refresh the user asked for ([S-542]).</summary>
    public bool Busy => _refreshing && _feed is { Snapshot.Busy: true };

    public void Show(bool refresh)
    {
        Reopen();

        if (refresh && _feed is not null)
        {
            _refreshing = true;
            _feed.Refresh();
            Changed?.Invoke();
        }
    }

    /// <summary>The message a row shows, by its item id.</summary>
    public MailMessage? Message(string id) => _messages.GetValueOrDefault(id);

    /// <summary>Whether the message's account lets Themia change it (Google is read-only, [S-544]).</summary>
    public bool CanChange(MailMessage message)
        => _source?.Accounts.FirstOrDefault(a => a.Id == message.AccountId) is { ReadOnly: false };

    /// <summary>A command that failed, in the header's notes until the next update replaces it.</summary>
    public void Problem(string problem)
    {
        _problem = problem;
        Changed?.Invoke();
    }

    public Task<string?> ChangeAsync(MailMessage message, MailChange change)
        => _source?.ChangeAsync(message, change) ?? Task.FromResult<string?>(null);

    public bool Open(MailMessage message) => _source?.Open(message) == true;

    public bool Reply(MailMessage message) => _source?.Reply(message) == true;

    public bool Compose(MailAccount account) => _source?.Compose(account, Held) == true;

    public KitRead Read()
    {
        _messages.Clear();

        var config = _config();
        var accounts = _source?.Accounts ?? [];
        var snapshot = _feed?.Snapshot ?? MailFeedSnapshot.Empty;
        var byId = accounts.ToDictionary(a => a.Id, StringComparer.Ordinal);

        var messages = config.SortOrder == MailSortOrder.OldestFirst ? Enumerable.Reverse(snapshot.Messages).ToList() : snapshot.Messages;
        var items = new List<KitItem>(messages.Count);
        foreach (var message in messages)
        {
            _messages[message.Key] = message;
            items.Add(MailSpec.Item(message, byId.GetValueOrDefault(message.AccountId)));
        }

        var unread = MailList.Unread(messages);
        var notes = new List<string>();
        if (_problem is { } problem)
        {
            notes.Add(problem);
        }

        if (snapshot.WriteNote is { } note)
        {
            notes.Add(note);
        }

        return new KitRead(
            items,
            Outcomes(Targets, snapshot.Outcomes),
            config.Folder.Label(),
            Accounts: [.. accounts.Select(a => new KitAccount(a.Id, a.Label, a.Problem is null ? KitAccountState.Ok : KitAccountState.SignInRequired))],
            Chosen: config.EffectiveAccountIds)
        {
            Count = unread > 0 ? unread : null,
            Notes = notes,
        };
    }

    /// <summary>
    /// [S-6421] Each account read, in the accounts' order: one waiting for its sign-in says so, one not
    /// answered yet is loading, a failed one its status, with when its stored list is from.
    /// </summary>
    public static IReadOnlyList<KitOutcome> Outcomes(IReadOnlyList<MailAccount> targets, IReadOnlyList<MailAccountOutcome> outcomes)
    {
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(outcomes);

        var byId = outcomes.GroupBy(o => o.AccountId, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Last(), StringComparer.Ordinal);
        return
        [
            .. targets.Select(account =>
            {
                if (account.Problem is not null)
                {
                    return new KitOutcome(account.Label, FetchStatus.SignInRequired);
                }

                if (!byId.TryGetValue(account.Id, out var outcome) || outcome.State == MailAccountState.Pending)
                {
                    return new KitOutcome(account.Label, FetchStatus.Loading, HeldAt: outcome?.At);
                }

                return new KitOutcome(account.Label, outcome.State == MailAccountState.Ok ? FetchStatus.Ok : outcome.Status, HeldAt: outcome.At);
            }),
        ];
    }

    private void Reopen()
    {
        if (_source is null || _disposed)
        {
            return;
        }

        var config = _config();
        var query = new MailQuery(config.EffectiveAccountIds, config.Folder, EmailConfig.FetchCount);
        if (_feed is not null && query.Key == _feedKey)
        {
            return;
        }

        CloseFeed();
        _feedKey = query.Key;
        _feed = _source.Open(query);
        _feed.Updated += OnFeedUpdated;
    }

    private void CloseFeed()
    {
        if (_feed is null)
        {
            return;
        }

        _feed.Updated -= OnFeedUpdated;
        _feed.Dispose();
        _feed = null;
        _feedKey = null;
    }

    private void OnFeedUpdated()
    {
        if (_refreshing && _feed is { Snapshot.Busy: false })
        {
            _refreshing = false;
        }

        _problem = null;
        Changed?.Invoke();
    }

    private void OnChanged() => Changed?.Invoke();

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        CloseFeed();

        if (_source is not null)
        {
            _source.AccountsChanged -= OnChanged;
        }
    }
}
