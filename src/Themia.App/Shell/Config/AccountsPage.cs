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
using Themia.Core.Widgets;
using Windows.ApplicationModel.DataTransfer;

namespace Themia.App.Shell.Config;

/// <summary>
/// [S-539] The Accounts page of the settings window: who Themia is signed in to, and the way to add,
/// rename and sign out of an account.
/// <para>
/// One flow at a time, drawn in place under the Add account row rather than in a second window: a
/// device code, a browser wait, a paste box or a short form. Everything that can go wrong is said on
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

    private static readonly string OpenGlyph = char.ConvertFromUtf32(0xE8A7);
    private static readonly string KeyGlyph = char.ConvertFromUtf32(0xE8D7);

    private readonly AccountService _service;
    /// <summary>[S-1541] The page; its title goes to the window's breadcrumb.</summary>
    private readonly SettingsPage _root = new(
        Str.Shell_Settings_Nav_Accounts.Text(),
        Str.Shell_Accounts_Subtitle.Text());
    private readonly StackPanel _list = new() { Spacing = Fluent.Space1 };
    private readonly StackPanel _flow = new() { Spacing = Fluent.Space1 };

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
            case "microsoft": ShowBrowserFlow(AccountProvider.Microsoft); break;
            case "google": ShowBrowserFlow(AccountProvider.Google); break;
            case "imap": StartImap(); break;
            case "caldav": StartDav(AccountProvider.CalDav); break;
            case "github-token": StartGitHubToken(); break;
            case "claude": StartClaude(); break;
        }
#endif

        // Subscribed while on screen only: the window replaces the page on navigation, and a page
        // that kept listening would keep itself (and its flow) alive behind the next one.
        _root.Loaded += (_, _) =>
        {
            if (!_subscribed)
            {
                _service.AccountsChanged += OnAccountsChanged;
                _subscribed = true;
            }
        };

        _root.Unloaded += (_, _) =>
        {
            if (_subscribed)
            {
                _service.AccountsChanged -= OnAccountsChanged;
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

        var accounts = _service.Accounts;
        if (accounts.Count == 0)
        {
            _list.Children.Add(FluentLayout.Note(Str.Shell_Accounts_None.Text()));
            return;
        }

        foreach (var account in accounts)
        {
            _list.Children.Add(AccountCard(account));
        }
    }

    private FrameworkElement AccountCard(AccountInfo account)
    {
        var icon = new FontIcon
        {
            Glyph = ProviderGlyph(account.Provider),
            FontSize = Fluent.IconSize,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, Fluent.Space4, 0),
        };

        var name = Fluent.Text(FluentType.BodyStrong);
        name.Text = account.Label;

        var detail = Fluent.Text(FluentType.Caption, "TextFillColorSecondaryBrush");
        detail.Text = account.Label == account.Address
            ? account.Provider.Label()
            : Str.Shell_Accounts_Detail.Format(("address", account.Address), ("provider", account.Provider.Label()));

        var text = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center, Children = { name, detail } };

        if (account.Problem is { } problem)
        {
            var warning = Fluent.Text(FluentType.Caption, "SystemFillColorCriticalBrush");
            warning.Text = problem;
            warning.TextWrapping = TextWrapping.Wrap;
            warning.TextTrimming = TextTrimming.None;
            text.Children.Add(warning);
        }

        var rename = FluentLayout.IconCommand(Glyphs.Rename, Str.Common_Rename.Text());
        rename.Flyout = RenameFlyout(account);

        var signOut = new Button { Content = Str.Account_SignOut.Text(), VerticalAlignment = VerticalAlignment.Center };
        signOut.Click += (_, _) => _ = SignOutAsync(account, signOut);

        var commands = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = Fluent.Space2,
            Margin = new Thickness(Fluent.Space3, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Children = { rename, signOut },
        };

        Grid.SetColumn(icon, 0);
        Grid.SetColumn(text, 1);
        Grid.SetColumn(commands, 2);

        return FluentLayout.Card(new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto },
            },
            Children = { icon, text, commands },
        });
    }

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

        void Add(string text, AccountProvider provider, Action start)
        {
            var item = new MenuFlyoutItem { Text = text, Icon = new FontIcon { Glyph = ProviderGlyph(provider) } };
            item.Click += (_, _) => start();
            menu.Items.Add(item);
        }

        Add("Microsoft", AccountProvider.Microsoft, () => StartBrowser(AccountProvider.Microsoft));
        Add("Google", AccountProvider.Google, () => StartBrowser(AccountProvider.Google));
        menu.Items.Add(new MenuFlyoutSeparator());
        Add(Str.Shell_Accounts_MenuImap.Text(), AccountProvider.Imap, StartImap);
        Add(Str.Shell_Accounts_MenuCalDav.Text(), AccountProvider.CalDav, () => StartDav(AccountProvider.CalDav));
        Add(Str.Shell_Accounts_MenuCardDav.Text(), AccountProvider.CardDav, () => StartDav(AccountProvider.CardDav));
        menu.Items.Add(new MenuFlyoutSeparator());
        Add("GitHub", AccountProvider.GitHub, StartGitHubDevice);
        Add(Str.Shell_Accounts_MenuGitHubToken.Text(), AccountProvider.GitHub, StartGitHubToken);
        Add("Claude", AccountProvider.Claude, StartClaude);

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

    // ---- device code: GitHub --------------------------------------------------------------

    private void StartGitHubDevice()
    {
        const AccountProvider provider = AccountProvider.GitHub;

        var code = Fluent.Text(FluentType.TitleLarge);
        code.Text = "········";
        code.FontFamily = new FontFamily("Cascadia Mono, Consolas");
        code.IsTextSelectionEnabled = true;

        var where = Fluent.Text(FluentType.Body, "TextFillColorSecondaryBrush");
        where.TextWrapping = TextWrapping.Wrap;
        where.Text = Str.Shell_Accounts_AskingCode.Format(("provider", provider.Label()));

        var copy = new Button { Content = Labelled(Glyphs.Copy, Str.Shell_Accounts_CopyCode.Text()), IsEnabled = false };
        var open = new Button { Content = Labelled(OpenGlyph, Str.Shell_Accounts_OpenSignIn.Text()), IsEnabled = false, Style = AccentStyle() };
        var cancel = new Button { Content = Str.Common_Cancel.Text() };
        cancel.Click += (_, _) => CloseFlow();

        var (status, ring, line) = StatusLine(Str.Shell_Accounts_Starting.Text());

        var body = new StackPanel
        {
            Spacing = Fluent.Space3,
            Children =
            {
                where,
                code,
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = Fluent.Space2, Children = { open, copy, cancel } },
                status,
            },
        };

        var token = ShowFlow(Str.Shell_Accounts_SignInTo.Format(("provider", provider.Label())), FluentLayout.Card(body));
        _ = RunGitHubDeviceAsync(code, where, copy, open, ring, line, token);
    }

    private async Task RunGitHubDeviceAsync(
        TextBlock code,
        TextBlock where,
        Button copy,
        Button open,
        ProgressRing ring,
        TextBlock line,
        CancellationToken cancellation)
    {
        try
        {
            var signIn = await _service.BeginGitHubSignInAsync(cancellation).ConfigureAwait(true);
            var session = signIn.Session;

            code.Text = session.UserCode;
            where.Text = Str.Shell_Accounts_EnterCode.Format(("site", $"{session.VerificationUri.Host}{session.VerificationUri.AbsolutePath.TrimEnd('/')}"));

            copy.IsEnabled = true;
            open.IsEnabled = true;
            copy.Click += (_, _) => Copy(session.UserCode, line);
            open.Click += (_, _) => Open(session.VerificationUri, line);

            Say(line, Str.Shell_Accounts_WaitingApproval.Format(("time", LocaleFormat.Time(Strings.Current, session.ExpiresAt.ToLocalTime().DateTime))), critical: false);

            var account = await _service.CompleteGitHubSignInAsync(signIn, cancellation).ConfigureAwait(true);
            CloseFlow(Str.Shell_Accounts_SignedInAs.Format(("provider", "GitHub"), ("address", account.Address)));
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // ignore: the user cancelled or left the page; nothing to say.
        }
        catch (AccountException ex)
        {
            Fail(ring, line, ex.Message, copy, open);
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "GitHub sign-in failed unexpectedly", ex);
            Fail(ring, line, Str.Shell_Accounts_FlowFailed.Format(("reason", ex.Message)), copy, open);
        }
    }

    // ---- browser: Microsoft, Google --------------------------------------------------------

    /// <summary>[S-1783] Microsoft and Google sign in the same way, so they share one flow.</summary>
    private void StartBrowser(AccountProvider provider)
    {
        if (provider == AccountProvider.Google && AccountService.GoogleUnavailableReason is { } reason)
        {
            ShowFlow(Str.Shell_Accounts_SignInTo.Format(("provider", provider.Label())), Message(reason, critical: true));
            return;
        }

        var (ring, line, token) = ShowBrowserFlow(provider);
        _ = RunBrowserAsync(provider, ring, line, token);
    }

    /// <summary>The card a browser sign-in waits in: what is about to happen, a live status line, Cancel.</summary>
    private (ProgressRing Ring, TextBlock Line, CancellationToken Token) ShowBrowserFlow(AccountProvider provider)
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

    private async Task RunBrowserAsync(AccountProvider provider, ProgressRing ring, TextBlock line, CancellationToken cancellation)
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

    // ---- Claude ----------------------------------------------------------------------------

    private void StartClaude()
    {
        var signIn = AccountService.BeginClaudeSignIn();

        var steps = Fluent.Text(FluentType.Body, "TextFillColorSecondaryBrush");
        steps.TextWrapping = TextWrapping.Wrap;
        steps.Text = Str.Shell_Accounts_ClaudeSteps.Text();

        var open = new Button { Content = Labelled(OpenGlyph, Str.Shell_Accounts_OpenClaude.Text()), Style = AccentStyle() };

        var code = new TextBox { PlaceholderText = Str.Shell_Accounts_PasteHere.Text(), Width = 360 };
        var nickname = new TextBox { PlaceholderText = Str.Shell_Accounts_Optional.Text(), Width = 360 };
        var connect = new Button { Content = Str.Connect_Connect.Text(), Style = AccentStyle() };
        var cancel = new Button { Content = Str.Common_Cancel.Text() };

        var (status, ring, line) = StatusLine(string.Empty);
        status.Visibility = Visibility.Collapsed;

        open.Click += (_, _) => Open(signIn.AuthorizeUrl, line, status);
        cancel.Click += (_, _) => CloseFlow();

        var token = ShowFlow(
            Str.Shell_Accounts_ConnectClaude.Text(),
            FluentLayout.Card(new StackPanel { Spacing = Fluent.Space3, Children = { steps, open } }),
            FluentLayout.Row(KeyGlyph, Str.Shell_Accounts_Code.Text(), Str.Shell_Accounts_CodeDesc.Text(), code),
            FluentLayout.Row(Glyphs.Rename, Str.Connect_Field_Nickname.Text(), null, nickname),
            Buttons(connect, cancel),
            status);

        connect.Click += (_, _) => _ = RunFormAsync(connect, status, ring, line,
            () => _service.CompleteClaudeSignInAsync(signIn, code.Text, nickname.Text, token),
            account => account.Nickname is null
                ? Str.Shell_Accounts_ConnectedClaude.Text()
                : Str.Shell_Accounts_ConnectedClaudeAs.Format(("name", account.Nickname)));
    }

    // ---- forms: IMAP, CalDAV, CardDAV, GitHub token ----------------------------------------

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

    private void StartDav(AccountProvider provider)
    {
        var kind = provider == AccountProvider.CalDav ? DavKind.Calendar : DavKind.Contacts;

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

    private void StartGitHubToken()
    {
        var tokenBox = new PasswordBox { Width = 280 };
        var api = new TextBox { PlaceholderText = Str.Shell_Accounts_ApiEmpty.Text(), Width = 280 };
        var nickname = new TextBox { PlaceholderText = Str.Shell_Accounts_Optional.Text(), Width = 280 };

        var create = new HyperlinkButton { Content = Str.Shell_Accounts_CreateToken.Text() };

        var connect = new Button { Content = Str.Connect_Connect.Text(), Style = AccentStyle() };
        var cancel = new Button { Content = Str.Common_Cancel.Text() };
        cancel.Click += (_, _) => CloseFlow();

        var (status, ring, line) = StatusLine(string.Empty);
        status.Visibility = Visibility.Collapsed;
        create.Click += (_, _) => Open(OAuthClients.GitHub.NewTokenPage, line, status);

        var token = ShowFlow(
            Str.Shell_Accounts_AddGitHubToken.Text(),
            FluentLayout.Note(Str.Shell_Accounts_GitHubTokenNote.Text()),
            FluentLayout.Row(KeyGlyph, Str.Shell_Accounts_Token.Text(), null, tokenBox),
            FluentLayout.Row(Glyphs.Link, Str.Shell_Accounts_ApiAddress.Text(), Str.Shell_Accounts_ApiAddressDesc.Text(), api),
            FluentLayout.Row(Glyphs.Rename, Str.Connect_Field_Nickname.Text(), null, nickname),
            Buttons(connect, cancel, create),
            status);

        connect.Click += (_, _) => _ = RunFormAsync(connect, status, ring, line,
            () => _service.AddGitHubTokenAsync(tokenBox.Password, api.Text, nickname.Text, token),
            account => Str.Shell_Accounts_ConnectedGitHub.Format(("name", account.DisplayName)));
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

    private static string ProviderGlyph(AccountProvider provider) => provider switch
    {
        AccountProvider.Microsoft => char.ConvertFromUtf32(0xE753),
        AccountProvider.Google => char.ConvertFromUtf32(0xE774),
        AccountProvider.Imap => Glyphs.Mail,
        AccountProvider.CalDav => Glyphs.Calendar,
        AccountProvider.CardDav => Glyphs.Contact,
        AccountProvider.GitHub => char.ConvertFromUtf32(0xE943),
        AccountProvider.Claude => char.ConvertFromUtf32(0xE99A),
        _ => Glyphs.Info,
    };

    private static StackPanel Labelled(string glyph, string text) => new()
    {
        Orientation = Orientation.Horizontal,
        Spacing = Fluent.Space2,
        Children = { new FontIcon { Glyph = glyph, FontSize = Fluent.IconSize }, new TextBlock { Text = text } },
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
        line.Foreground = FluentLayout.Themed(critical ? "SystemFillColorCriticalBrush" : "TextFillColorSecondaryBrush", critical ? Colors.Red : Colors.Gray);
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

    private static void Copy(string text, TextBlock line)
    {
        try
        {
            var package = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
            package.SetText(text);
            Clipboard.SetContent(package);
            Say(line, Str.Shell_Accounts_CodeCopied.Text(), critical: false);
        }
        catch (Exception ex)
        {
            // Another process can hold the clipboard open; say so rather than claim it copied.
            Log.Warn(Scope, "could not copy the sign-in code", ex);
            Say(line, Str.Shell_Accounts_ClipboardBusy.Text(), critical: true);
        }
    }

    private static void Open(Uri url, TextBlock line, FrameworkElement? status = null)
    {
        if (ShellLauncher.Open(url.AbsoluteUri) != LaunchOutcome.Opened)
        {
            if (status is not null)
            {
                status.Visibility = Visibility.Visible;
            }

            Say(line, Str.Shell_Accounts_BrowserFailed.Format(("site", url.Host)), critical: true);
        }
    }

    private static Style? AccentStyle()
        => Application.Current.Resources.TryGetValue("AccentButtonStyle", out var style) ? style as Style : null;
}
