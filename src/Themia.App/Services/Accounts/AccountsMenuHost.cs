using Microsoft.UI.Dispatching;
using Themia.Core.Accounts;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Settings;
using Themia.Core.Sync;
using Themia.Sdk;
using Themia.Sdk.Services;
using Windows.ApplicationModel.DataTransfer;

namespace Themia.App.Services.Accounts;

/// <summary>
/// [S-3550]..[S-3557] What ⚙ › Accounts does, over the one account service: adding, proving,
/// renaming and signing out of accounts, run from the menu's lines. One for the app (every display's ⚙ reads the
/// same forms and statuses); it follows the accounts and their numbers while a menu is open
/// ([S-3559]) and, when the last one closes, gives up every sign-in and try still out and empties
/// every form ([S-3557]), as leaving the page did. Each flow says how it goes under the line it was
/// started from; nothing it says carries a password or a token.
/// </summary>
internal sealed class AccountsMenuHost : IAccountsMenuHost, IDisposable
{
    private const string Scope = "accounts-menu";

    private readonly DispatcherQueue _dispatcher;
    private readonly Dictionary<string, CancellationTokenSource> _flows = new(StringComparer.Ordinal);
    private readonly Dictionary<AccountForm, CancellationTokenSource> _tries = [];
    private int _open;
    private bool _disposed;

    public AccountsMenuHost(DispatcherQueue dispatcher)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    /// <summary>Raised on the UI thread when what the menu shows of the accounts changed: the open menus draw it.</summary>
    public event Action? Changed;

    public AccountsMenuState Menu { get; } = new();

    private static AccountService? Service => AppServices.Accounts;

    public IReadOnlyList<AccountInfo> Accounts => Service?.Accounts ?? [];

    public string? LoadProblem => Service?.LoadProblem;

    public DateTimeOffset Now => DateTimeOffset.Now;

    public IReadOnlyList<ShelfStats> Stats(string accountId) => AppServices.Sync?.Ledger.For(accountId) ?? [];

    public string? Unavailable(ProviderId provider) => AccountService.UnavailableReason(provider);

    /// <summary>[S-3559] A settings menu opened: the accounts and their numbers are followed while one is.</summary>
    public void Opened()
    {
        if (_open++ > 0 || _disposed)
        {
            return;
        }

        Menu.Changed += Raise;
        if (Service is { } service)
        {
            service.AccountsChanged += Raise;
        }

        if (AppServices.Sync is { } sync)
        {
            sync.StatsChanged += Raise;
        }
    }

    /// <summary>[S-3557] A settings menu closed; with the last one every flow is given up and every form emptied.</summary>
    public void Closed()
    {
        if (_open == 0 || --_open > 0)
        {
            return;
        }

        if (Service is { } service)
        {
            service.AccountsChanged -= Raise;
        }

        if (AppServices.Sync is { } sync)
        {
            sync.StatsChanged -= Raise;
        }

        // Given up, not disposed: a flow still out holds its token until it notices.
        foreach (var flow in _flows.Values.Concat(_tries.Values))
        {
            flow.Cancel();
        }

        _flows.Clear();
        _tries.Clear();
        Menu.Changed -= Raise;
        Menu.Close();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _open = 1;
        Closed();
        Changed = null;
    }

    // -----------------------------------------------------------------------------------------
    // Sign-ins: the browser, a device code
    // -----------------------------------------------------------------------------------------

    public void SignIn(ProviderId provider, ServiceAuth member, string row)
    {
        if (Service is not { } service)
        {
            Menu.Say(row, new AccountStatus(Str.Shell_Accounts_Unavailable.Text(), Failed: true));
            return;
        }

        var cancellation = Begin(row);
        switch (member)
        {
            case OAuthPkceLoopback:
                if (AccountService.UnavailableReason(provider) is { } reason)
                {
                    Menu.Say(row, new AccountStatus(reason, Failed: true));
                    End(row);
                    return;
                }

                Menu.Say(row, new AccountStatus(Str.Shell_Accounts_OpeningBrowser.Text(), Busy: true));
                _ = RunBrowserAsync(service, provider, row, cancellation);
                break;

            case OAuthDeviceCode:
                Menu.Say(row, new AccountStatus(Str.SignIn_WaitingCode.Text(), Busy: true));
                _ = RunDeviceAsync(service, provider, row, cancellation);
                break;

            default:
                Log.Warn(Scope, $"{provider} offers no sign-in a line can run");
                End(row);
                break;
        }
    }

    public void Cancel(string row)
    {
        if (_flows.Remove(row, out var flow))
        {
            flow.Cancel();
        }

        Menu.Say(row, null);
    }

    /// <summary>[S-1783] The browser sign-in: the browser opened, waited for, who signed in said under the line.</summary>
    private async Task RunBrowserAsync(AccountService service, ProviderId provider, string row, CancellationToken cancellation)
    {
        try
        {
            var account = await service.SignInWithBrowserAsync(provider, url =>
            {
                var opened = ShellLauncher.Open(url.AbsoluteUri) == LaunchOutcome.Opened;
                if (opened)
                {
                    Post(() => Menu.Say(row, new AccountStatus(Str.Shell_Accounts_WaitingBrowser.Text(), Busy: true)));
                }

                return opened;
            }, cancellation).ConfigureAwait(true);

            Say(row, new AccountStatus(Str.Shell_Accounts_SignedInAs.Format(("provider", provider.Label()), ("address", account.Address))), cancellation);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // ignore: the user cancelled, or the menu closed; there is no one to tell.
        }
        catch (AccountException ex)
        {
            Say(row, new AccountStatus(ex.Message, Failed: true), cancellation);
        }
        catch (Exception ex)
        {
            Log.Error(Scope, $"{provider} sign-in failed unexpectedly", ex);
            Say(row, new AccountStatus(Str.Shell_Accounts_FlowFailed.Format(("reason", ex.Message)), Failed: true), cancellation);
        }
        finally
        {
            End(row, cancellation);
        }
    }

    /// <summary>[S-9919] The device-code sign-in: the code and its page under the line, then who signed in.</summary>
    private async Task RunDeviceAsync(AccountService service, ProviderId provider, string row, CancellationToken cancellation)
    {
        try
        {
            // The code arrives off the UI thread (the request runs on the pool); it is shown on it.
            var account = await service.SignInWithDeviceCodeAsync(provider, session => Post(() =>
            {
                if (!cancellation.IsCancellationRequested)
                {
                    Menu.Say(row, new AccountStatus(Str.SignIn_DeviceExplain.Format(("provider", provider.Label())), Busy: true)
                    {
                        Code = session.UserCode,
                        Page = session.VerificationUri,
                    });
                }
            }), cancellation).ConfigureAwait(true);

            Say(row, new AccountStatus(Str.Shell_Accounts_SignedInAs.Format(("provider", provider.Label()), ("address", account.Address))), cancellation);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // ignore: the user cancelled, or the menu closed.
        }
        catch (AccountException ex)
        {
            Say(row, new AccountStatus(ex.Message, Failed: true), cancellation);
        }
        catch (Exception ex)
        {
            Log.Error(Scope, $"{provider} sign-in failed unexpectedly", ex);
            Say(row, new AccountStatus(Str.Shell_Accounts_FlowFailed.Format(("reason", ex.Message)), Failed: true), cancellation);
        }
        finally
        {
            End(row, cancellation);
        }
    }

    // -----------------------------------------------------------------------------------------
    // The forms: Test connection and Add
    // -----------------------------------------------------------------------------------------

    public void Try(AccountForm form, AccountTry what)
    {
        ArgumentNullException.ThrowIfNull(form);
        if (Service is not { } service || !form.Begin(what))
        {
            return;
        }

        var cancellation = new CancellationTokenSource();
        _tries[form] = cancellation;
        Menu.Touch();

        // What the try needs, read now: the form takes no edit while it is out.
        var server = form.ServerTried;
        var port = form.PortTried;
        var user = form.UserTried;
        var address = form.Address;
        var nickname = form.Nickname;
        var provider = form.Provider;
        var kind = form.Kind;
        var password = form.Password();

        _ = RunTryAsync(form, what, cancellation.Token, async token => kind switch
        {
            AccountFormKind.Imap when what == AccountTry.Test
                => Str.SettingsAccounts_ConnectedTo.Format(("host", await service.TestImapAsync(server, port, user, password, token).ConfigureAwait(true))),
            AccountFormKind.Imap
                => Str.Shell_Accounts_Connected.Format(("address", (await service.AddImapAsync(address, server, port, user, password, nickname, token).ConfigureAwait(true)).Address)),
            AccountFormKind.Token
                => Str.Shell_Accounts_SignedInAs.Format(("provider", provider.Label()), ("address", (await service.AddPastedTokenAsync(provider, password, token).ConfigureAwait(true)).Address)),
            _ when what == AccountTry.Test
                => Str.SettingsAccounts_ConnectedTo.Format(("host", await service.TestDavAsync(provider, server, address, password, token).ConfigureAwait(true))),
            AccountFormKind.CalDav
                => Str.Shell_Accounts_ConnectedCalendar.Format(("address", (await service.AddDavAsync(provider, server, address, password, nickname, token).ConfigureAwait(true)).Address)),
            _ => Str.Shell_Accounts_ConnectedContacts.Format(("address", (await service.AddDavAsync(provider, server, address, password, nickname, token).ConfigureAwait(true)).Address)),
        });
    }

    /// <summary>
    /// [S-3555] The one way a form's try runs: busy while it is out, how it went on the form once it
    /// is back (an add empties the form), nothing at all when the menu closed meanwhile.
    /// </summary>
    private async Task RunTryAsync(AccountForm form, AccountTry what, CancellationToken cancellation, Func<CancellationToken, Task<string>> run)
    {
        string text;
        var failed = false;
        try
        {
            text = await run(cancellation).ConfigureAwait(true);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // ignore: the menu closed; the form was emptied with it.
            return;
        }
        catch (AccountException ex)
        {
            text = ex.Message;
            failed = true;
        }
        catch (Exception ex)
        {
            Log.Error(Scope, $"{form} could not be {(what == AccountTry.Test ? "tested" : "added")}", ex);
            text = Str.Shell_Accounts_DidNotWork.Format(("reason", ex.Message));
            failed = true;
        }

        if (cancellation.IsCancellationRequested || !_tries.Remove(form, out var source))
        {
            return;
        }

        source.Dispose();
        form.End(text, failed);
        Menu.Touch();
    }

    // -----------------------------------------------------------------------------------------
    // An account: its nickname, signing out
    // -----------------------------------------------------------------------------------------

    public void Rename(string accountId, string nickname, string row)
    {
        if (Service is not { } service)
        {
            return;
        }

        _ = RunAsync(row, "renaming an account", () => service.RenameAsync(accountId, nickname));
    }

    public void SignOut(string accountId, string row)
    {
        if (Service is not { } service)
        {
            return;
        }

        _ = RunAsync(row, "signing out", () => service.SignOutAsync(accountId));
    }

    /// <summary>A change of an account: nothing under its line once it is done, why it was not done otherwise.</summary>
    private async Task RunAsync(string row, string what, Func<Task> change)
    {
        try
        {
            Menu.Say(row, null);
            await change().ConfigureAwait(true);
        }
        catch (AccountException ex)
        {
            Menu.Say(row, new AccountStatus(ex.Message, Failed: true));
        }
        catch (Exception ex)
        {
            Log.Error(Scope, $"{what} failed unexpectedly", ex);
            Menu.Say(row, new AccountStatus(Str.Shell_Accounts_DidNotWork.Format(("reason", ex.Message)), Failed: true));
        }
    }

    public void Open(Uri page)
    {
        ArgumentNullException.ThrowIfNull(page);
        if (ShellLauncher.Open(page.AbsoluteUri) != LaunchOutcome.Opened)
        {
            Log.Warn(Scope, "a provider's page could not be opened");
        }
    }

    public void Copy(string text)
    {
        try
        {
            var package = new DataPackage();
            package.SetText(text);
            Clipboard.SetContent(package);
        }
        catch (Exception ex)
        {
            // The clipboard can be held by another app for a moment: the code stays on the line.
            Log.Warn(Scope, "the code could not be copied", ex);
        }
    }

    // -----------------------------------------------------------------------------------------
    // Small pieces
    // -----------------------------------------------------------------------------------------

    /// <summary>A flow under <paramref name="row"/> begins, replacing one already there.</summary>
    private CancellationToken Begin(string row)
    {
        Cancel(row);
        var flow = new CancellationTokenSource();
        _flows[row] = flow;
        return flow.Token;
    }

    /// <summary>A flow under <paramref name="row"/> has ended (its words stay); one that replaced it is left alone.</summary>
    private void End(string row, CancellationToken? of = null)
    {
        if (_flows.TryGetValue(row, out var flow) && (of is null || flow.Token == of))
        {
            _flows.Remove(row);
            flow.Dispose();
        }
    }

    /// <summary>Say how a flow went, unless it was given up meanwhile.</summary>
    private void Say(string row, AccountStatus status, CancellationToken cancellation)
    {
        if (!cancellation.IsCancellationRequested)
        {
            Menu.Say(row, status);
        }
    }

    /// <summary>Run on the UI thread, where the menu is.</summary>
    private void Post(Action action)
    {
        if (_dispatcher.HasThreadAccess)
        {
            action();
        }
        else if (!_dispatcher.TryEnqueue(() => action()))
        {
            Log.Warn(Scope, "could not post a sign-in's progress to the UI thread");
        }
    }

    private void Raise()
    {
        try
        {
            Changed?.Invoke();
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "drawing the accounts menu again failed", ex);
        }
    }
}
