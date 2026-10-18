using System.Globalization;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Themia.App.Services;
using Themia.App.Services.Accounts;
using Themia.App.Widgets;
using Themia.Core.Accounts;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Sync;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Themia.Sdk;
using Themia.Sdk.Services;
using Windows.ApplicationModel.DataTransfer;

namespace Themia.App.Shell.Config;

/// <summary>
/// [S-539] The Accounts page of the settings window: who Themia is signed in to, what each account
/// has synced to this PC ([S-3821]), and the way to add, rename and sign out of an account.
/// <para>
/// One flow at a time, drawn in place under the Add account row rather than in a second window: a
/// browser wait or a short form. Everything that can go wrong is said on
/// the page, in the line under the flow, never left to a log — a sign-in that fails silently is one
/// the user tries five times. Leaving the page cancels a flow in progress.
/// </para>
/// <para>
/// The list follows <see cref="AccountService.AccountsChanged"/>, so an account added here, renamed,
/// marked "sign in again" by a failed refresh, or imported, shows without the page being rebuilt.
/// </para>
/// </summary>
internal sealed class AccountsPage
{
    private const string Scope = "config.accounts";

    private static readonly string KeyGlyph = char.ConvertFromUtf32(0xE8D7);

    private readonly AccountService _service;
    /// <summary>[S-1541] The page; its title goes to the window's breadcrumb.</summary>
    private readonly SettingsPage _root = new(
        Str.Shell_Settings_Nav_Accounts.Text(),
        Str.Shell_Accounts_Subtitle.Text());
    private readonly StackPanel _list = new() { Spacing = Fluent.Space1 };
    private readonly StackPanel _flow = new() { Spacing = Fluent.Space1 };

    /// <summary>[S-3821] The accounts whose groups the reader opened, kept across the list's rebuilds.</summary>
    private readonly HashSet<string> _open = new(StringComparer.Ordinal);

    private CancellationTokenSource? _flowCancel;
    private bool _subscribed;

    private AccountsPage(AccountService service) => _service = service;

    /// <summary>Build the page. Without the service (a debug render, a failed start) it says so.</summary>
    public static FrameworkElement Build()
    {
        if (AppServices.Accounts is not { } service)
        {
            return new SettingsPage(Str.Shell_Settings_Nav_Accounts.Text()).With([FluentLayout.Note(Str.Shell_Accounts_Unavailable.Text())]);
        }

        return new AccountsPage(service).Compose();
    }

    private FrameworkElement Compose()
    {
        if (_service.LoadProblem is { } problem)
        {
            _root.Children.Add(Message(problem, critical: true));
        }

        _root.With([FluentLayout.Header(Str.Shell_Accounts_SignedIn.Text())]);
        _root.Children.Add(_list);

        _root.Children.Add(FluentLayout.Header(Str.Shell_Accounts_AddHeader.Text()));
        _root.Children.Add(FluentLayout.Row(Glyphs.Add, Str.Account_AddAccount.Text(), Str.Shell_Accounts_AddDesc.Text(), AddMenu()));
        _root.Children.Add(_flow);


        RebuildList();

#if DEBUG
        // Lets the debug settings render (--settings-png Accounts) draw a flow as well as the list.
        // The browser flows draw their card only: opening the browser is something a render must
        // never do.
        switch (Environment.GetEnvironmentVariable("THEMIA_DEBUG_ACCOUNTS_FLOW"))
        {
            case "microsoft": ShowBrowserFlow(HostProviders.Microsoft); break;
            case "google": ShowBrowserFlow(HostProviders.Google); break;
            case "imap": StartImap(); break;
            case "device": ShowDeviceFlow("Example", []).Code.Text = "WDJB-MJHT"; break;
            case "paste": ShowPasteFlow("Example", new Uri("https://example.com/settings/tokens"), [], null); break;
            case "caldav": StartDav(HostProviders.CalDav); break;
        }
#endif

        // Subscribed while on screen only: the window replaces the page on navigation, and a page
        // that kept listening would keep itself (and its flow) alive behind the next one.
        _root.Loaded += (_, _) =>
        {
            if (!_subscribed)
            {
                _service.AccountsChanged += OnAccountsChanged;

                // [S-3823] The numbers follow every sync that lands while the page is open.
                if (AppServices.Sync is { } sync)
                {
                    sync.StatsChanged += OnAccountsChanged;
                }

                _subscribed = true;
            }
        };

        _root.Unloaded += (_, _) =>
        {
            if (_subscribed)
            {
                _service.AccountsChanged -= OnAccountsChanged;
                if (AppServices.Sync is { } sync)
                {
                    sync.StatsChanged -= OnAccountsChanged;
                }

                _subscribed = false;
            }

            CancelFlow();
        };

        return _root;
    }

    private void OnAccountsChanged()
    {
        RebuildList();
    }

    // -----------------------------------------------------------------------------------------
    // The list
    // -----------------------------------------------------------------------------------------

    private void RebuildList()
    {
        _list.Children.Clear();

        var accounts = Listed();
        if (accounts.Count == 0)
        {
            _list.Children.Add(FluentLayout.Note(Str.Shell_Accounts_None.Text()));
            return;
        }

        var now = DateTimeOffset.Now;
        foreach (var account in accounts)
        {
            _list.Children.Add(AccountGroup(account, now));
        }
    }

    /// <summary>The accounts the list shows: the service's, or a debug render's stand-ins.</summary>
    private IReadOnlyList<AccountInfo> Listed()
    {
#if DEBUG
        if (DebugAccounts is { } stand)
        {
            // The render looks at the groups opened: that is where the numbers are.
            foreach (var account in stand)
            {
                _open.Add(account.Id);
            }

            return stand;
        }
#endif
        return _service.Accounts;
    }

#if DEBUG
    /// <summary>
    /// [S-3824] <c>--settings-png Accounts --demo-accounts</c>: two stand-in accounts, so the groups and
    /// their numbers can be looked at without signing in to anything. Their numbers come from the
    /// render host's own ledger, filled by the render; nothing here reaches a provider.
    /// </summary>
    internal static IReadOnlyList<AccountInfo>? DebugAccounts { get; set; }
#endif

    /// <summary>
    /// [S-3821] One account's group: its name, address and provider on the header; inside, a row per
    /// kind its provider feeds with what is stored on this PC and when it last synced, then its
    /// nickname, a way back in when it needs signing in again, and Sign out.
    /// </summary>
    private Border AccountGroup(AccountInfo account, DateTimeOffset now)
    {
        var id = account.Id;
        var detail = account.Label == account.Address
            ? account.Provider.Label()
            : Str.Shell_Accounts_Detail.Format(("address", account.Address), ("provider", account.Provider.Label()));

        var rows = new List<Border>();

        // [S-3822] Sign in again first: it is the one thing on the group that needs doing.
        if (account.Problem is { } problem)
        {
            var again = new Button { Content = Str.Shell_Accounts_ReconnectButton.Text(), Style = AccentStyle() };
            again.Click += (_, _) => SignInAgain(account);
            rows.Add(FluentLayout.Row(Glyphs.Warning, Str.Shell_Accounts_Reconnect.Text(), problem, again));
        }

        var stats = AppServices.Sync?.Ledger.For(id) ?? [];
        foreach (var kind in SyncStats.Summarize(account.Features, stats))
        {
            var count = Fluent.Text(FluentType.Body, "TextFillColorSecondaryBrush");
            count.Text = SyncStats.Count(kind);
            rows.Add(FluentLayout.Row(KindGlyph(kind.Kind), SyncStats.Label(kind.Kind), SyncStats.Detail(kind, now), count));
        }

        // [S-9919] A service that grants access per resource (an app installed on chosen repositories):
        // where the user widens what this account may read.
        if (ProviderCatalog.Of(account.Provider).SignIn.Find<OAuthDeviceCode>() is { GrantPage: { } grant })
        {
            var choose = new Button { Content = Str.Common_Open.Text() };
            choose.Click += (_, _) => ShellLauncher.Open(grant.AbsoluteUri);
            rows.Add(FluentLayout.Row(Glyphs.Link, Str.SignIn_GrantPage.Text(), Str.SignIn_GrantPageDesc.Format(("provider", account.Provider.Label())), choose));
        }

        var rename = new Button { Content = Str.Common_Rename.Text(), Flyout = RenameFlyout(account) };
        rows.Add(FluentLayout.Row(Glyphs.Rename, Str.Connect_Field_Nickname.Text(), Str.Shell_Accounts_NicknameDesc.Text(), rename));

        var signOut = FluentLayout.DangerButton(Str.Account_SignOut.Text());
        signOut.Click += (_, _) => _ = SignOutAsync(account, signOut);
        rows.Add(FluentLayout.DangerRow(Glyphs.SignOut, Str.Account_SignOut.Text(), Str.Shell_Accounts_SignOutDesc.Text(), signOut));

        var frame = FluentLayout.Expander(
            ProviderGlyph(account.Provider),
            account.Label,
            account.Problem is null ? detail : Str.Shell_Accounts_NeedsSignIn.Text(),
            null,
            rows,
            expanded: _open.Contains(id),
            expandedChanged: open =>
            {
                if (open)
                {
                    _open.Add(id);
                }
                else
                {
                    _open.Remove(id);
                }
            });

        return frame;
    }

    /// <summary>[S-3822] A way back in: the same flow that added the account, which replaces its sign-in in place.</summary>
    private void SignInAgain(AccountInfo account)
    {
        switch (account.Provider)
        {
            case var pid when ProviderCatalog.Of(pid).SignIn.Members.Count > 0:
                StartSignIn(account.Provider);
                break;

            case var pid when pid == HostProviders.Imap:
                StartImap();
                break;

            default:
                StartDav(account.Provider);
                break;
        }
    }

    private static string KindGlyph(SyncKind kind) => kind switch
    {
        SyncKind.Mail => Glyphs.Mail,
        SyncKind.Calendar => Glyphs.Calendar,
        SyncKind.Contacts => Glyphs.Contact,
        _ => Glyphs.Task,
    };
    private Flyout RenameFlyout(AccountInfo account)
    {
        var box = new TextBox
        {
            Header = Str.Connect_Field_Nickname.Text(),
            Text = account.Nickname ?? string.Empty,
            PlaceholderText = account.DisplayName.Length > 0 ? account.DisplayName : account.Address,
            Width = 260,
        };

        var verdict = Message(string.Empty, critical: true);
        var save = new Button { Content = Str.Common_Save.Text(), Style = AccentStyle() };
        var flyout = new Flyout
        {
            Content = new StackPanel { Spacing = Fluent.Space2, Children = { box, save, verdict } },
        };

        async Task SaveAsync()
        {
            save.IsEnabled = false;
            try
            {
                await _service.RenameAsync(account.Id, box.Text).ConfigureAwait(true);
                flyout.Hide();
            }
            catch (AccountException ex)
            {
                Say(verdict, ex.Message, critical: true);
            }
            finally
            {
                save.IsEnabled = true;
            }
        }

        save.Click += (_, _) => _ = SaveAsync();
        box.KeyDown += (_, args) =>
        {
            if (args.Key == Windows.System.VirtualKey.Enter)
            {
                args.Handled = true;
                _ = SaveAsync();
            }
        };

        return flyout;
    }

    private async Task SignOutAsync(AccountInfo account, Button source)
    {
        var confirmed = await FluentLayout.ConfirmAsync(
            source,
            Str.Shell_Accounts_SignOutConfirm.Format(("name", account.Label)),
            Str.Shell_Accounts_SignOutBody.Format(("provider", account.Provider.Label())),
            Str.Account_SignOut.Text()).ConfigureAwait(true);

        if (!confirmed)
        {
            return;
        }

        try
        {
            await _service.SignOutAsync(account.Id).ConfigureAwait(true);
        }
        catch (AccountException ex)
        {
            // The card is still there; say why under the Add row, where messages go.
            ShowFlow(null, Message(ex.Message, critical: true));
        }
    }

    // -----------------------------------------------------------------------------------------
    // Adding
    // -----------------------------------------------------------------------------------------

    private DropDownButton AddMenu()
    {
        var menu = new MenuFlyout { Placement = FlyoutPlacementMode.BottomEdgeAlignedRight };

        void Add(string text, ProviderId provider, Action start)
        {
            var item = new MenuFlyoutItem { Text = text, Icon = new FontIcon { Glyph = ProviderGlyph(provider) } };
            item.Click += (_, _) => start();
            menu.Items.Add(item);
        }

        // [S-9910] Every provider with sign-in members, in its place; then the host's password forms.
        foreach (var provider in ProviderCatalog.All.Where(p => p.SignIn.Members.Count > 0).OrderBy(p => p.AddRank))
        {
            var id = provider.Id;
            Add(provider.Label.Text(), id, () => StartSignIn(id));
        }

        menu.Items.Add(new MenuFlyoutSeparator());
        Add(Str.Shell_Accounts_MenuImap.Text(), HostProviders.Imap, StartImap);
        Add(Str.Shell_Accounts_MenuCalDav.Text(), HostProviders.CalDav, () => StartDav(HostProviders.CalDav));
        Add(Str.Shell_Accounts_MenuCardDav.Text(), HostProviders.CardDav, () => StartDav(HostProviders.CardDav));

        return new DropDownButton { Content = Str.Account_AddAccount.Text(), Flyout = menu };
    }

    /// <summary>Replace whatever flow is showing, cancelling it, and return the new flow's token.</summary>
    private CancellationToken ShowFlow(string? heading, params FrameworkElement[] content)
    {
        CancelFlow();
        _flowCancel = new CancellationTokenSource();

        _flow.Children.Clear();

        if (heading is not null)
        {
            var title = Fluent.Text(FluentType.Subtitle);
            title.Text = heading;
            title.Margin = new Thickness(0, Fluent.Space3, 0, Fluent.Space1);
            _flow.Children.Add(title);
        }

        foreach (var element in content)
        {
            _flow.Children.Add(element);
        }

        return _flowCancel.Token;
    }

    private void CancelFlow()
    {
        if (_flowCancel is null)
        {
            return;
        }

        _flowCancel.Cancel();
        _flowCancel.Dispose();
        _flowCancel = null;
    }

    private void CloseFlow(string? message = null)
    {
        CancelFlow();
        _flow.Children.Clear();

        if (message is not null)
        {
            _flow.Children.Add(Message(message, critical: false));
        }
    }

    // ---- browser: Microsoft, Google --------------------------------------------------------

    /// <summary>[S-9910] A provider's sign-in: its first member, the others offered as links under it.</summary>
    private void StartSignIn(ProviderId provider) => StartMember(provider, ProviderCatalog.Of(provider).SignIn.Members.FirstOrDefault());

    private void StartMember(ProviderId provider, ServiceAuth? member)
    {
        switch (member)
        {
            case OAuthPkceLoopback:
                StartBrowser(provider);
                break;

            case OAuthDeviceCode:
                StartDevice(provider);
                break;

            case PastedToken paste:
                ShowPasteFlow(provider.Label(), paste.TokenPage, OtherWays(provider, paste), (box, button, line) => _ = RunPasteAsync(provider, box, button, line));
                break;

            default:
                Log.Warn(Scope, $"{provider} offers no sign-in this page can run");
                break;
        }
    }

    /// <summary>[S-9910] Links to a provider's other sign-in members, under the one showing.</summary>
    private FrameworkElement[] OtherWays(ProviderId provider, ServiceAuth showing)
    {
        var links = new List<FrameworkElement>();
        foreach (var member in ProviderCatalog.Of(provider).SignIn.Members)
        {
            if (ReferenceEquals(member, showing) || member is AnonymousAuth)
            {
                continue;
            }

            var text = member switch
            {
                OAuthPkceLoopback => Str.SignIn_UseBrowser.Text(),
                OAuthDeviceCode => Str.SignIn_UseCode.Text(),
                _ => Str.SignIn_UseToken.Text(),
            };

            var link = new HyperlinkButton { Content = text, Padding = new Thickness(0) };
            var chosen = member;
            link.Click += (_, _) => StartMember(provider, chosen);
            links.Add(link);
        }

        return [.. links];
    }

    // ---- device code -------------------------------------------------------------------------

    private void StartDevice(ProviderId provider)
    {
        var member = ProviderCatalog.Of(provider).SignIn.Find<OAuthDeviceCode>()!;
        var (code, open, ring, line, token) = ShowDeviceFlow(provider.Label(), OtherWays(provider, member));
        _ = RunDeviceAsync(provider, code, open, ring, line, token);
    }

    /// <summary>
    /// [S-9919] The card a device-code sign-in waits in: what to do, the code (large, with Copy), the
    /// provider's page, a live status line, Cancel.
    /// </summary>
    private (TextBlock Code, Button Open, ProgressRing Ring, TextBlock Line, CancellationToken Token) ShowDeviceFlow(string provider, FrameworkElement[] otherWays)
    {
        var explain = Fluent.Text(FluentType.Body, "TextFillColorSecondaryBrush");
        explain.TextWrapping = TextWrapping.Wrap;
        explain.Text = Str.SignIn_DeviceExplain.Format(("provider", provider));

        var code = Fluent.Text(FluentType.Title);
        code.IsTextSelectionEnabled = true;
        code.Visibility = Visibility.Collapsed;

        var copy = new Button { Content = Str.SignIn_CopyCode.Text(), IsEnabled = false };
        copy.Click += (_, _) =>
        {
            if (code.Text.Length > 0)
            {
                var package = new DataPackage();
                package.SetText(code.Text);
                Clipboard.SetContent(package);
            }
        };

        var open = new Button { Content = Str.SignIn_OpenPage.Text(), Style = AccentStyle(), IsEnabled = false };
        var cancel = new Button { Content = Str.Common_Cancel.Text() };
        cancel.Click += (_, _) => CloseFlow();

        var (status, ring, line) = StatusLine(Str.SignIn_WaitingCode.Text());

        var card = new StackPanel { Spacing = Fluent.Space3, Children = { explain, code, status, Buttons(open, copy, cancel) } };
        foreach (var link in otherWays)
        {
            card.Children.Add(link);
        }

        code.RegisterPropertyChangedCallback(TextBlock.TextProperty, (_, _) =>
        {
            var shown = code.Text.Length > 0;
            code.Visibility = shown ? Visibility.Visible : Visibility.Collapsed;
            copy.IsEnabled = shown;
        });

        var token = ShowFlow(Str.Shell_Accounts_SignInTo.Format(("provider", provider)), FluentLayout.Card(card));
        return (code, open, ring, line, token);
    }

    private async Task RunDeviceAsync(ProviderId provider, TextBlock code, Button open, ProgressRing ring, TextBlock line, CancellationToken cancellation)
    {
        try
        {
            // The code arrives off the UI thread (the request runs on the pool); it is shown on it.
            var ui = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
            var account = await _service.SignInWithDeviceCodeAsync(provider, session => ui.TryEnqueue(() => ShowCode(session)), cancellation).ConfigureAwait(true);
            CloseFlow(Str.Shell_Accounts_SignedInAs.Format(("provider", provider.Label()), ("address", account.Address)));
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // ignore: the user cancelled or left the page.
        }
        catch (AccountException ex)
        {
            Fail(ring, line, ex.Message, open);
        }
        catch (Exception ex)
        {
            Log.Error(Scope, $"{provider} sign-in failed unexpectedly", ex);
            Fail(ring, line, Str.Shell_Accounts_FlowFailed.Format(("reason", ex.Message)), open);
        }

        void ShowCode(DeviceCodeSession session)
        {
            code.Text = session.UserCode;
            open.IsEnabled = true;
            open.Click += (_, _) => ShellLauncher.Open(session.VerificationUri.AbsoluteUri);
        }
    }

    // ---- pasted token ------------------------------------------------------------------------

    /// <summary>
    /// [S-9915] The card a pasted token is added in: where to create one (the provider's page), the box
    /// (a password box: the token is never shown), Add, a status line, Cancel.
    /// </summary>
    private void ShowPasteFlow(string provider, Uri tokenPage, FrameworkElement[] otherWays, Action<PasswordBox, Button, TextBlock>? add)
    {
        var explain = Fluent.Text(FluentType.Body, "TextFillColorSecondaryBrush");
        explain.TextWrapping = TextWrapping.Wrap;
        explain.Text = Str.SignIn_PasteExplain.Format(("provider", provider));

        var page = new HyperlinkButton { Content = Str.SignIn_CreateToken.Text(), Padding = new Thickness(0) };
        page.Click += (_, _) => ShellLauncher.Open(tokenPage.AbsoluteUri);

        var box = new PasswordBox { Header = Str.SignIn_Token.Text(), Width = 360, HorizontalAlignment = HorizontalAlignment.Left };
        var addButton = new Button { Content = Str.SignIn_AddAccount.Text(), Style = AccentStyle() };
        var cancel = new Button { Content = Str.Common_Cancel.Text() };
        cancel.Click += (_, _) => CloseFlow();

        var line = Message(string.Empty, critical: false);
        addButton.Click += (_, _) => add?.Invoke(box, addButton, line);

        var card = new StackPanel { Spacing = Fluent.Space3, Children = { explain, page, box, line, Buttons(addButton, cancel) } };
        foreach (var link in otherWays)
        {
            card.Children.Add(link);
        }

        ShowFlow(Str.Shell_Accounts_SignInTo.Format(("provider", provider)), FluentLayout.Card(card));
    }

    private async Task RunPasteAsync(ProviderId provider, PasswordBox box, Button add, TextBlock line)
    {
        add.IsEnabled = false;
        Say(line, Str.SignIn_CheckingToken.Text(), critical: false);
        var token = _flowCancel?.Token ?? CancellationToken.None;

        try
        {
            var account = await _service.AddPastedTokenAsync(provider, box.Password, token).ConfigureAwait(true);
            box.Password = string.Empty;
            CloseFlow(Str.Shell_Accounts_SignedInAs.Format(("provider", provider.Label()), ("address", account.Address)));
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // ignore: the user cancelled or left the page.
        }
        catch (AccountException ex)
        {
            Say(line, ex.Message, critical: true);
            add.IsEnabled = true;
        }
        catch (Exception ex)
        {
            Log.Error(Scope, $"{provider} token was not added", ex);
            Say(line, Str.Shell_Accounts_FlowFailed.Format(("reason", ex.Message)), critical: true);
            add.IsEnabled = true;
        }
    }

    /// <summary>[S-1783] Every browser member signs in the same way, so they share one flow.</summary>
    private void StartBrowser(ProviderId provider)
    {
        if (AccountService.UnavailableReason(provider) is { } reason)
        {
            ShowFlow(Str.Shell_Accounts_SignInTo.Format(("provider", provider.Label())), Message(reason, critical: true));
            return;
        }

        var (ring, line, token) = ShowBrowserFlow(provider);
        _ = RunBrowserAsync(provider, ring, line, token);
    }

    /// <summary>The card a browser sign-in waits in: what is about to happen, a live status line, Cancel.</summary>
    private (ProgressRing Ring, TextBlock Line, CancellationToken Token) ShowBrowserFlow(ProviderId provider)
    {
        var explain = Fluent.Text(FluentType.Body, "TextFillColorSecondaryBrush");
        explain.TextWrapping = TextWrapping.Wrap;
        explain.Text = Str.Shell_Accounts_BrowserExplain.Format(("provider", provider.Label()));

        var cancel = new Button { Content = Str.Common_Cancel.Text() };
        cancel.Click += (_, _) => CloseFlow();

        var (status, ring, line) = StatusLine(Str.Shell_Accounts_OpeningBrowser.Text());

        var token = ShowFlow(Str.Shell_Accounts_SignInTo.Format(("provider", provider.Label())), FluentLayout.Card(new StackPanel
        {
            Spacing = Fluent.Space3,
            Children = { explain, status, cancel },
        }));

        return (ring, line, token);
    }

    private async Task RunBrowserAsync(ProviderId provider, ProgressRing ring, TextBlock line, CancellationToken cancellation)
    {
        try
        {
            var account = await _service.SignInWithBrowserAsync(provider, url =>
            {
                var opened = ShellLauncher.Open(url.AbsoluteUri) == LaunchOutcome.Opened;
                if (opened)
                {
                    Say(line, Str.Shell_Accounts_WaitingBrowser.Text(), critical: false);
                }

                return opened;
            }, cancellation).ConfigureAwait(true);

            CloseFlow(Str.Shell_Accounts_SignedInAs.Format(("provider", provider.Label()), ("address", account.Address)));
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // ignore: the user cancelled or left the page.
        }
        catch (AccountException ex)
        {
            Fail(ring, line, ex.Message);
        }
        catch (Exception ex)
        {
            Log.Error(Scope, $"{provider} sign-in failed unexpectedly", ex);
            Fail(ring, line, Str.Shell_Accounts_FlowFailed.Format(("reason", ex.Message)));
        }
    }

    // ---- forms: IMAP, CalDAV, CardDAV ------------------------------------------------------

    private void StartImap()
    {
        var address = new TextBox { PlaceholderText = "you@example.com", Width = 280 };
        var host = new TextBox { PlaceholderText = "imap.example.com", Width = 280 };
        var port = new TextBox { Text = "993", Width = 280 };
        var user = new TextBox { PlaceholderText = Str.Shell_Accounts_UsuallyAddress.Text(), Width = 280 };
        var password = new PasswordBox { Width = 280 };
        var nickname = new TextBox { PlaceholderText = Str.Shell_Accounts_Optional.Text(), Width = 280 };
        var hint = FluentLayout.Note(string.Empty);
        hint.Visibility = Visibility.Collapsed;

        // The preset fills only fields the user has not typed into: a shortcut, never a cage.
        var hostTyped = false;
        var userTyped = false;
        var filling = false;
        host.TextChanged += (_, _) => hostTyped |= !filling;
        user.TextChanged += (_, _) => userTyped |= !filling;

        address.TextChanged += (_, _) =>
        {
            filling = true;
            try
            {
                if (!userTyped)
                {
                    user.Text = address.Text.Trim();
                }

                var preset = ImapPresets.ForAddress(address.Text);
                if (!hostTyped && preset is not null)
                {
                    host.Text = preset.Host;
                    port.Text = preset.Port.ToString(CultureInfo.InvariantCulture);
                }

                hint.Text = preset is null ? string.Empty : Str.Shell_Accounts_PresetHint.Format(("provider", preset.Name), ("note", preset.Note ?? Str.Shell_Accounts_PresetFilled.Text()));
                hint.Visibility = hint.Text.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
            }
            finally
            {
                filling = false;
            }
        };

        var connect = new Button { Content = Str.Connect_Connect.Text(), Style = AccentStyle() };
        var cancel = new Button { Content = Str.Common_Cancel.Text() };
        cancel.Click += (_, _) => CloseFlow();

        var (status, ring, line) = StatusLine(string.Empty);
        status.Visibility = Visibility.Collapsed;

        var token = ShowFlow(
            Str.Shell_Accounts_AddMail.Text(),
            FluentLayout.Row(Glyphs.Mail, Str.Shell_Accounts_EmailAddress.Text(), null, address),
            hint,
            FluentLayout.Row(Glyphs.Network, Str.Shell_Accounts_Server.Text(), Str.Shell_Accounts_ImapDesc.Text(), host),
            FluentLayout.Row(Glyphs.Network, Str.Connect_Field_Port.Text(), Str.Shell_Accounts_PortDesc.Text(), port),
            FluentLayout.Row(Glyphs.Contact, Str.Shell_Accounts_UserName.Text(), null, user),
            FluentLayout.Row(KeyGlyph, Str.Connect_Field_Password.Text(), Str.Shell_Accounts_PasswordDesc.Text(), password),
            FluentLayout.Row(Glyphs.Rename, Str.Connect_Field_Nickname.Text(), null, nickname),
            Buttons(connect, cancel),
            status);

        connect.Click += (_, _) => _ = RunFormAsync(connect, status, ring, line, () =>
        {
            if (!int.TryParse(port.Text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var number))
            {
                throw new AccountException(AccountError.Refused, Str.Shell_Accounts_PortNumber.Text());
            }

            return _service.AddImapAsync(address.Text, host.Text, number, user.Text, password.Password, nickname.Text, token);
        }, account => Str.Shell_Accounts_Connected.Format(("address", account.Address)));
    }

    private void StartDav(ProviderId provider)
    {
        var kind = provider == HostProviders.CalDav ? DavKind.Calendar : DavKind.Contacts;

        var url = new TextBox { PlaceholderText = "https://cloud.example.com", Width = 280 };
        var user = new TextBox { PlaceholderText = Str.Shell_Accounts_OftenAddress.Text(), Width = 280 };
        var password = new PasswordBox { Width = 280 };
        var nickname = new TextBox { PlaceholderText = Str.Shell_Accounts_Optional.Text(), Width = 280 };

        var urlTyped = false;
        var filling = false;
        url.TextChanged += (_, _) => urlTyped |= !filling;
        user.TextChanged += (_, _) =>
        {
            if (urlTyped || DavPresets.ForAddress(user.Text, kind) is not { } preset)
            {
                return;
            }

            filling = true;
            url.Text = preset;
            filling = false;
        };

        var connect = new Button { Content = Str.Connect_Connect.Text(), Style = AccentStyle() };
        var cancel = new Button { Content = Str.Common_Cancel.Text() };
        cancel.Click += (_, _) => CloseFlow();

        var (status, ring, line) = StatusLine(string.Empty);
        status.Visibility = Visibility.Collapsed;

        var calendar = kind == DavKind.Calendar;

        var token = ShowFlow(
            calendar ? Str.Shell_Accounts_AddCalendar.Text() : Str.Shell_Accounts_AddContacts.Text(),
            FluentLayout.Row(Glyphs.Contact, Str.Shell_Accounts_UserName.Text(), null, user),
            FluentLayout.Row(KeyGlyph, Str.Connect_Field_Password.Text(), Str.Shell_Accounts_PasswordDesc.Text(), password),
            FluentLayout.Row(Glyphs.Link, Str.Shell_Accounts_Server.Text(), calendar ? Str.Shell_Accounts_CalDavServerDesc.Text() : Str.Shell_Accounts_CardDavServerDesc.Text(), url),
            FluentLayout.Row(Glyphs.Rename, Str.Connect_Field_Nickname.Text(), null, nickname),
            Buttons(connect, cancel),
            status);

        connect.Click += (_, _) => _ = RunFormAsync(connect, status, ring, line,
            () => _service.AddDavAsync(provider, url.Text, user.Text, password.Password, nickname.Text, token),
            account => calendar
                ? Str.Shell_Accounts_ConnectedCalendar.Format(("address", account.Address))
                : Str.Shell_Accounts_ConnectedContacts.Format(("address", account.Address)));
    }

    /// <summary>
    /// The one way a form's Connect button runs: busy while the check is out, the verdict on the
    /// line underneath, the flow closed with a confirmation when it worked.
    /// </summary>
    private async Task RunFormAsync(
        Button connect,
        FrameworkElement status,
        ProgressRing ring,
        TextBlock line,
        Func<Task<AccountInfo>> add,
        Func<AccountInfo, string> done)
    {
        connect.IsEnabled = false;
        status.Visibility = Visibility.Visible;
        ring.IsActive = true;
        ring.Visibility = Visibility.Visible;
        Say(line, Str.Shell_Accounts_Checking.Text(), critical: false);

        try
        {
            var account = await add().ConfigureAwait(true);
            CloseFlow(done(account));
        }
        catch (OperationCanceledException)
        {
            // ignore: the page was left or the flow replaced; there is no one to tell.
        }
        catch (AccountException ex)
        {
            Fail(ring, line, ex.Message);
            connect.IsEnabled = true;
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "adding an account failed unexpectedly", ex);
            Fail(ring, line, Str.Shell_Accounts_DidNotWork.Format(("reason", ex.Message)));
            connect.IsEnabled = true;
        }
    }

    // -----------------------------------------------------------------------------------------
    // Small pieces
    // -----------------------------------------------------------------------------------------

    private static string ProviderGlyph(ProviderId provider) => provider switch
    {
        var pid when pid == HostProviders.Microsoft => char.ConvertFromUtf32(0xE753),
        var pid when pid == HostProviders.Google => char.ConvertFromUtf32(0xE774),
        var pid when pid == HostProviders.Imap => Glyphs.Mail,
        var pid when pid == HostProviders.CalDav => Glyphs.Calendar,
        var pid when pid == HostProviders.CardDav => Glyphs.Contact,
        _ => Glyphs.Info,
    };

    private static StackPanel Buttons(params FrameworkElement[] buttons)
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = Fluent.Space2,
            Margin = new Thickness(0, Fluent.Space2, 0, 0),
        };

        foreach (var button in buttons)
        {
            row.Children.Add(button);
        }

        return row;
    }

    /// <summary>A busy ring and the line under a flow that says what is happening or what went wrong.</summary>
    private static (StackPanel Panel, ProgressRing Ring, TextBlock Line) StatusLine(string text)
    {
        var ring = new ProgressRing { IsActive = true, Width = Fluent.IconSize, Height = Fluent.IconSize };
        var line = Message(text, critical: false);
        line.Margin = new Thickness(0);
        line.VerticalAlignment = VerticalAlignment.Center;

        return (new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = Fluent.Space2,
            Margin = new Thickness(0, Fluent.Space1, 0, 0),
            Children = { ring, line },
        }, ring, line);
    }

    private static TextBlock Message(string text, bool critical)
    {
        var line = Fluent.Text(FluentType.Body, critical ? "SystemFillColorCriticalBrush" : "TextFillColorSecondaryBrush");
        line.TextWrapping = TextWrapping.Wrap;
        line.TextTrimming = TextTrimming.None;
        line.Margin = new Thickness(0, Fluent.Space1, 0, Fluent.Space1);
        Say(line, text, critical);
        return line;
    }

    private static void Say(TextBlock line, string text, bool critical)
    {
        line.Text = text;
        line.Foreground = ThemeBrush.Of(critical ? "SystemFillColorCriticalBrush" : "TextFillColorSecondaryBrush", critical ? Colors.Red : Colors.Gray);
        line.Visibility = text.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private static void Fail(ProgressRing ring, TextBlock line, string message, params Button[] disable)
    {
        ring.IsActive = false;
        ring.Visibility = Visibility.Collapsed;
        Say(line, message, critical: true);

        foreach (var button in disable)
        {
            button.IsEnabled = false;
        }
    }

    private static Style? AccentStyle()
        => Application.Current.Resources.TryGetValue("AccentButtonStyle", out var style) ? style as Style : null;
}
