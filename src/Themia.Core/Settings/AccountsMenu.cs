using Themia.App.Services.Accounts;
using Themia.Core.Accounts;
using Themia.Core.Sync;
using Themia.Sdk;
using Themia.Sdk.Services;

namespace Themia.Core.Settings;

/// <summary>
/// [S-3553] What stands under a line of ⚙ › Accounts while it is being done or once it is done: a
/// browser sign-in waiting, a device code to enter (<see cref="Code"/> on <see cref="Page"/>), a
/// rename or a sign-out that failed. <see cref="Busy"/> draws the ring before the words.
/// </summary>
public sealed record AccountStatus(string Text, bool Busy = false, bool Failed = false)
{
    /// <summary>[S-9919] A device-code sign-in's code, once the provider has given it.</summary>
    public string? Code { get; init; }

    /// <summary>[S-9919] Where the code is entered.</summary>
    public Uri? Page { get; init; }
}

/// <summary>
/// [S-3554] [S-3557] What ⚙ › Accounts holds that is not the accounts: the forms being filled in
/// (one per kind, the same wherever the form is opened from) and what stands under the lines being
/// done, by the line's key. Kept while the menu is open, on every display; emptied (forms cleared,
/// passwords wiped, statuses gone) when the menu closes (<see cref="Close"/>), which the host does
/// after it has given up every try and sign-in still out.
/// </summary>
public sealed class AccountsMenuState
{
    private readonly Dictionary<string, AccountStatus> _status = new(StringComparer.Ordinal);
    private readonly Dictionary<ProviderId, AccountForm> _tokens = [];

    public AccountForm Imap { get; } = new(AccountFormKind.Imap, HostProviders.Imap);

    public AccountForm CalDav { get; } = new(AccountFormKind.CalDav, HostProviders.CalDav);

    public AccountForm CardDav { get; } = new(AccountFormKind.CardDav, HostProviders.CardDav);

    /// <summary>Raised when something the menu shows of it changed: the open menus draw it.</summary>
    public event Action? Changed;

    /// <summary>The form a password provider adds through (IMAP, CalDAV, CardDAV), or null for one that has none.</summary>
    public AccountForm? FormFor(ProviderId provider)
        => provider == HostProviders.Imap ? Imap : provider == HostProviders.CalDav ? CalDav : provider == HostProviders.CardDav ? CardDav : null;

    /// <summary>[S-9915] The pasted-token form of a provider whose sign-in is a token.</summary>
    public AccountForm Token(ProviderId provider)
    {
        if (!_tokens.TryGetValue(provider, out var form))
        {
            form = new AccountForm(AccountFormKind.Token, provider);
            _tokens[provider] = form;
        }

        return form;
    }

    /// <summary>Every form there is now.</summary>
    public IEnumerable<AccountForm> Forms => [Imap, CalDav, CardDav, .. _tokens.Values];

    /// <summary>What stands under the line of <paramref name="row"/>, or null.</summary>
    public AccountStatus? Status(string row) => _status.GetValueOrDefault(row);

    /// <summary>Say <paramref name="status"/> under the line of <paramref name="row"/> (null takes it away), and draw it.</summary>
    public void Say(string row, AccountStatus? status)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (status is null)
        {
            _status.Remove(row);
        }
        else
        {
            _status[row] = status;
        }

        Touch();
    }

    /// <summary>Something changed (a field typed, a try's end): the open menus draw it.</summary>
    public void Touch() => Changed?.Invoke();

    /// <summary>[S-3557] The menu closed: every form emptied and its password wiped, every status gone.</summary>
    public void Close()
    {
        foreach (var form in Forms)
        {
            form.Clear();
        }

        _status.Clear();
        Touch();
    }
}

/// <summary>
/// [S-3550] What ⚙ › Accounts reads and asks for: the accounts as the account service has them, what
/// each holds on this PC, the forms and statuses (<see cref="Menu"/>), and the sign-ins, tries,
/// renames and sign-outs the service carries out. Everything it starts says how it went through
/// <see cref="Menu"/>, under the line it was started from.
/// </summary>
public interface IAccountsMenuHost
{
    /// <summary>Every account, in the order they were added.</summary>
    IReadOnlyList<AccountInfo> Accounts { get; }

    /// <summary>Why the accounts file could not be used at start, or null.</summary>
    string? LoadProblem { get; }

    /// <summary>[S-3820] What is stored of an account on this PC, by kind, as the sync last reported it.</summary>
    IReadOnlyList<ShelfStats> Stats(string accountId);

    /// <summary>Now, for "synced 5 min. ago".</summary>
    DateTimeOffset Now { get; }

    /// <summary>[S-536] Why a provider's sign-in cannot start in this build (no client secret), or null.</summary>
    string? Unavailable(ProviderId provider);

    /// <summary>The forms and what stands under the lines.</summary>
    AccountsMenuState Menu { get; }

    /// <summary>[S-1783] [S-9919] Sign in to <paramref name="provider"/> with <paramref name="member"/> (the browser, a device code), saying how it goes under <paramref name="row"/>.</summary>
    void SignIn(ProviderId provider, ServiceAuth member, string row);

    /// <summary>[S-3555] Test or add what <paramref name="form"/> holds; how it went comes back on the form.</summary>
    void Try(AccountForm form, AccountTry what);

    /// <summary>Give up the sign-in under <paramref name="row"/>.</summary>
    void Cancel(string row);

    /// <summary>[S-3551] Give an account a nickname (empty for none); a failure is said under <paramref name="row"/>.</summary>
    void Rename(string accountId, string nickname, string row);

    /// <summary>[S-3551] Forget an account on this PC; a failure is said under <paramref name="row"/>.</summary>
    void SignOut(string accountId, string row);

    /// <summary>Open a provider's page (where a code is entered, where a token is made, what an account may read).</summary>
    void Open(Uri page);

    /// <summary>[S-9919] Put a device code on the clipboard.</summary>
    void Copy(string text);
}
