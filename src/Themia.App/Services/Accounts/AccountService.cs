using System.Collections.Concurrent;
using System.Collections.Immutable;
using Microsoft.UI.Dispatching;
using Themia.Core.Accounts;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Storage;
using Themia.Sdk;

namespace Themia.App.Services.Accounts;

/// <summary>
/// [S-529] Themia's own accounts: who is signed in, their secrets, and every authorized request made
/// on their behalf. One instance, owned by <see cref="AppHost"/> and reached through
/// <see cref="AppServices.Accounts"/>.
/// <para>
/// <b>The seam, for a widget's data service</b> (mail, calendar, contacts, tasks). Pick accounts with <see cref="Accounts"/> / <see cref="WithFeature"/>, listen to
/// <see cref="AccountsChanged"/> (raised on the UI thread), and make requests through the methods
/// in <c>AccountService.Requests.cs</c>:
/// </para>
/// <list type="bullet">
/// <item><c>GetJsonAsync(id, url, contract)</c> — one authorized GET, parsed through a source-generated contract.</item>
/// <item><c>GetGraphPagesAsync(id, url, itemContract, max)</c> — a Graph list, following <c>@odata.nextLink</c>.</item>
/// <item><c>GetGooglePagesAsync(id, url, "items", itemContract, max)</c> — a Google list, following <c>nextPageToken</c>.</item>
/// <item><c>SendAsync(id, template)</c> / <c>SendJsonAsync(id, method, url, json)</c> — anything else (PATCH a task, POST a move).</item>
/// <item><c>DavAsync(id, "REPORT", url, xml, depth)</c> — CalDAV / CardDAV, basic auth applied, redirects followed.</item>
/// <item><c>GetImapCredentials(id)</c> — host, port and opened password for an IMAP session.</item>
/// <item><c>GetAccessTokenAsync(id)</c> — the raw bearer token, for the rare caller that needs it.</item>
/// </list>
/// <para>
/// Every request carries the right proof for its provider (a bearer token refreshed when stale, DAV
/// basic auth), retries 429/503 as the server directs, answers
/// a 401 with exactly one refresh, and fails with a typed <see cref="AccountException"/> — map it to
/// a widget state with <see cref="AccountException.ToFetchStatus"/>. A sign-in that only the user can
/// mend marks the account (<see cref="AccountInfo.Problem"/>); it never deletes it.
/// </para>
/// <para>
/// Persistence: <c>accounts.json</c> beside the app state, written atomically by one writer
/// (<see cref="AccountStore"/>), secrets DPAPI-sealed (<see cref="Dpapi"/>). Token refresh is
/// single-flight per account: two widgets asking at once cause one refresh, which matters for
/// providers that rotate the refresh token on use.
/// </para>
/// </summary>
internal sealed partial class AccountService : IDisposable
{
    private const string Scope = "accounts";

    private readonly DispatcherQueue? _dispatcher;
    private readonly AccountStore _store;
    private readonly bool _writable;
    private readonly bool _loadedWhole;
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _refreshGates = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, string> _problems = new(StringComparer.Ordinal);

    private ImmutableArray<Account> _accounts;
    private bool _disposed;

    /// <param name="dispatcher">The UI thread, where <see cref="AccountsChanged"/> is raised.</param>
    /// <param name="path">Where <c>accounts.json</c> lives.</param>
    public AccountService(DispatcherQueue? dispatcher, string path)
    {
        _dispatcher = dispatcher;
        _store = new AccountStore(path);

        var load = _store.Load(DateTimeOffset.UtcNow);
        _accounts = [.. load.Accounts];
        _writable = load.Writable;
        _loadedWhole = Themia.Core.Sync.ShelfSweep.MayRun(load);

        LoadProblem = load.Outcome == LoadOutcome.Recovered
            ? load.Writable
                ? Str.Shell_Accounts_FileRecovered.Format(("detail", load.Detail))
                : Str.Shell_Accounts_FileReadOnly.Format(("detail", load.Detail))
            : null;

        foreach (var account in _accounts)
        {
            Diagnose(account);
        }

        Log.Info(Scope, $"{_accounts.Length} account(s) loaded from {path}");
    }

    /// <summary>Raised on the UI thread whenever an account is added, renamed, removed or changes health.</summary>
    public event Action? AccountsChanged;

    /// <summary>Why the accounts file could not be used at start, or null when it was fine.</summary>
    public string? LoadProblem { get; }

    /// <summary>
    /// [S-2666] Every account's id, when the list can say which accounts do not exist: only after a
    /// file that loaded whole (no repair, writable). Null otherwise, so nothing is deleted on the
    /// strength of a list that may be missing accounts. Safe from any thread.
    /// </summary>
    public IReadOnlyCollection<string>? TrustedAccountIds => _loadedWhole ? [.. _accounts.Select(a => a.Id)] : null;

    /// <summary>Every account, in the order they were added.</summary>
    public IReadOnlyList<AccountInfo> Accounts => [.. _accounts.Select(Info)];

    /// <summary>The accounts that can feed a widget wanting <paramref name="feature"/>.</summary>
    public IReadOnlyList<AccountInfo> WithFeature(FeatureId feature)
        => [.. _accounts.Where(a => a.Provider.Features().Has(feature)).Select(Info)];

    /// <summary>One account, or null when there is no account with that id.</summary>
    public AccountInfo? Find(string? accountId)
        => accountId is not null && Get(accountId) is { } account ? Info(account) : null;

    /// <summary>Give an account a nickname, or clear it with null or whitespace.</summary>
    public Task RenameAsync(string accountId, string? nickname, CancellationToken cancellation = default)
    {
        var clean = AccountRules.CleanNickname(nickname);

        return MutateAsync(list =>
        {
            var index = list.FindIndex(a => a.Id == accountId);
            if (index < 0)
            {
                throw new AccountException(AccountError.UnknownAccount, Str.Shell_Accounts_Gone.Text());
            }

            list[index] = list[index] with { Nickname = clean };
        }, userAction: true, cancellation);
    }

    /// <summary>
    /// Forget an account and its secrets on this PC. Nothing is revoked at the provider: the user
    /// signed in there and can sign out there.
    /// </summary>
    public async Task SignOutAsync(string accountId, CancellationToken cancellation = default)
    {
        await MutateAsync(list =>
        {
            if (list.RemoveAll(a => a.Id == accountId) == 0)
            {
                throw new AccountException(AccountError.UnknownAccount, Str.Shell_Accounts_Gone.Text());
            }
        }, userAction: true, cancellation).ConfigureAwait(false);

        _problems.TryRemove(accountId, out _);
        Log.Info(Scope, $"signed out of {accountId}");

        // [S-2607] Whatever was synced for the account goes with it.
        try
        {
            SignedOut?.Invoke(accountId);
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "a sign-out listener threw", ex);
        }
    }

    /// <summary>[S-2607] Raised after an account was signed out and saved, on whichever thread the save finished.</summary>
    public event Action<string>? SignedOut;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        AccountsChanged = null;
        SignedOut = null;
    }

    // -----------------------------------------------------------------------------------------
    // State
    // -----------------------------------------------------------------------------------------

    private Account? Get(string accountId)
    {
        foreach (var account in _accounts)
        {
            if (account.Id == accountId)
            {
                return account;
            }
        }

        return null;
    }

    private Account Require(string accountId)
        => Get(accountId) ?? throw new AccountException(AccountError.UnknownAccount, Str.Shell_Accounts_NotSignedIn.Text());

    private AccountInfo Info(Account account) => new(
        account.Id,
        account.Provider,
        account.Address,
        account.DisplayName,
        account.Nickname,
        account.ServerUrl,
        _problems.TryGetValue(account.Id, out var problem) ? problem : null);

    /// <summary>
    /// Check that the secret this account needs can be opened on this PC, and say so when it
    /// cannot — a sealed blob from another user or PC is a sign-in to do again, not something to
    /// drop silently.
    /// </summary>
    private void Diagnose(Account account)
    {
        var (sealedSecret, what) = account.Provider.Auth() switch
        {
            AccountAuth.OAuthRefresh => (account.SealedRefreshToken ?? account.SealedAccessToken, SecretKind.SignIn),
            _ => (account.SealedPassword, SecretKind.Password),
        };

        var opened = Dpapi.Open(sealedSecret);

        var problem = opened.State == SecretState.Open ? null : SecretProblem(what, opened.State);

        if (problem is null)
        {
            _problems.TryRemove(account.Id, out _);
        }
        else
        {
            _problems[account.Id] = problem;
            Log.Warn(Scope, $"{account}: {problem}");
        }
    }

    /// <summary>Record that only the user can mend this account now. Raises a change when it is news.</summary>
    private void MarkProblem(string accountId, string problem)
    {
        if (_problems.TryGetValue(accountId, out var existing) && existing == problem)
        {
            return;
        }

        _problems[accountId] = problem;
        Log.Warn(Scope, $"{accountId}: {problem}");
        RaiseChanged();
    }

    private void ClearProblem(string accountId)
    {
        if (_problems.TryRemove(accountId, out _))
        {
            RaiseChanged();
        }
    }

    /// <summary>
    /// The one read-modify-write path. The change is applied to a copy under the write gate, saved,
    /// and only then made current, so a failed save of a user's action leaves nothing half-changed.
    /// </summary>
    /// <param name="userAction">
    /// True for something the user asked for: a failed save is an error they see. False for a token
    /// refresh: the new token is kept in memory either way so this session keeps working, and the
    /// failure is logged.
    /// </param>
    private async Task MutateAsync(Action<List<Account>> change, bool userAction, CancellationToken cancellation)
    {
        await _writeGate.WaitAsync(cancellation).ConfigureAwait(false);
        try
        {
            var list = _accounts.ToList();
            change(list);

            if (!_writable)
            {
                if (userAction)
                {
                    throw new AccountException(AccountError.Refused, LoadProblem ?? Str.Shell_Accounts_FileNotWritable.Text());
                }

                Log.Warn(Scope, "accounts file is read-only this session; a refreshed token is kept in memory only");
            }
            else
            {
                try
                {
                    await _store.SaveAsync(list, cancellation).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    Log.Error(Scope, "could not save accounts.json", ex);

                    if (userAction)
                    {
                        throw new AccountException(AccountError.Refused, Str.Shell_Accounts_SaveFailed.Format(("reason", ex.Message)), 0, ex);
                    }
                }
            }

            _accounts = [.. list];
        }
        finally
        {
            _writeGate.Release();
        }

        RaiseChanged();
    }

    /// <summary>
    /// Add an account, or replace the one with the same id — signing in to the same account again —
    /// keeping its nickname and when it was first added.
    /// </summary>
    private async Task<AccountInfo> UpsertAsync(Account fresh, CancellationToken cancellation)
    {
        Account? stored = null;

        await MutateAsync(list =>
        {
            var index = list.FindIndex(a => a.Id == fresh.Id);
            if (index < 0)
            {
                stored = fresh;
                list.Add(fresh);
                return;
            }

            var previous = list[index];
            stored = fresh with
            {
                Nickname = fresh.Nickname ?? previous.Nickname,
                AddedAt = previous.AddedAt,
            };
            list[index] = stored;
        }, userAction: true, cancellation).ConfigureAwait(false);

        // A fresh sign-in clears a "sign in again" from before; the list has to hear about it.
        Diagnose(stored!);
        RaiseChanged();
        Log.Info(Scope, $"saved {stored}");
        return Info(stored!);
    }

    private void RaiseChanged()
    {
        if (_disposed)
        {
            return;
        }

        if (_dispatcher is null || _dispatcher.HasThreadAccess)
        {
            Invoke();
            return;
        }

        if (!_dispatcher.TryEnqueue(Invoke))
        {
            Log.Warn(Scope, "could not post AccountsChanged to the UI thread (shutting down?)");
        }

        void Invoke()
        {
            try
            {
                AccountsChanged?.Invoke();
            }
            catch (Exception ex)
            {
                // A listener that throws must not take the others, or the caller, down with it.
                Log.Error(Scope, "an AccountsChanged listener threw", ex);
            }
        }
    }

    private static DateTimeOffset Now => DateTimeOffset.UtcNow;
}
