using Themia.App.Services.Accounts;
using Themia.Core.Accounts;
using Themia.Core.Localization;
using Themia.Core.Settings;
using Themia.Core.Sync;
using Themia.Sdk;
using Themia.Sdk.Services;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-3550]..[S-3557] [S-3561] ⚙ › Accounts as data: the accounts and what each holds, Add account's
/// kinds and forms, what a line starts and what stands under it, and that no line ever carries a
/// password. The accounts are made up and nothing is signed in to.
/// </summary>
public sealed partial class SettingsMenuTests
{
    /// <summary>A stand-in for the accounts: a list of its own, every request it gets counted.</summary>
    private sealed class FakeAccounts : IAccountsMenuHost
    {
        public List<AccountInfo> List { get; } = [];

        public IReadOnlyList<AccountInfo> Accounts => List;

        public string? LoadProblem { get; set; }

        public Dictionary<string, List<ShelfStats>> Shelves { get; } = [];

        public IReadOnlyList<ShelfStats> Stats(string accountId) => Shelves.TryGetValue(accountId, out var stats) ? stats : [];

        public DateTimeOffset Now { get; } = new(2026, 10, 25, 10, 42, 0, TimeSpan.Zero);

        public Dictionary<ProviderId, string> Refusing { get; } = [];

        public string? Unavailable(ProviderId provider) => Refusing.GetValueOrDefault(provider);

        public AccountsMenuState Menu { get; } = new();

        public List<(ProviderId Provider, ServiceAuth Member, string Row)> SignIns { get; } = [];

        public List<(AccountForm Form, AccountTry What)> Tries { get; } = [];

        public List<string> Cancelled { get; } = [];

        public List<(string Id, string Nickname, string Row)> Renamed { get; } = [];

        public List<string> SignedOut { get; } = [];

        public void SignIn(ProviderId provider, ServiceAuth member, string row) => SignIns.Add((provider, member, row));

        public void Try(AccountForm form, AccountTry what) => Tries.Add((form, what));

        public void Cancel(string row) => Cancelled.Add(row);

        public void Rename(string accountId, string nickname, string row) => Renamed.Add((accountId, nickname, row));

        public void SignOut(string accountId, string row) => SignedOut.Add(accountId);

        public void Open(Uri page)
        {
            // ignore: a test opens no page.
        }

        public void Copy(string text)
        {
            // ignore: a test never touches the clipboard.
        }
    }

    private static readonly AccountInfo Microsoft = new("ms-1", HostProviders.Microsoft, "alex@contoso.com", "Alex Wilber", null, null, null);

    private static readonly AccountInfo MailServer = new("imap:alex@imap.fabrikam.example", HostProviders.Imap, "alex@fabrikam.example", "alex@fabrikam.example", "Fabrikam", null, "The mail server refused the user name or password. Sign in again.");

    private static (FakeHost Host, FakeAccounts Accounts) WithAccounts(params AccountInfo[] accounts)
    {
        var fake = new FakeAccounts();
        fake.List.AddRange(accounts);
        return (new FakeHost { Accounts = fake }, fake);
    }

    /// <summary>Every line below <paramref name="lines"/>, depth first.</summary>
    private static IEnumerable<SettingsLine> Flat(IEnumerable<SettingsLine> lines) => lines.SelectMany(l => l switch
    {
        SettingsSubmenu s => (IEnumerable<SettingsLine>)[s, .. Flat(s.Lines)],
        SettingsChoice c => [c, .. c.Options],
        _ => [l],
    });

    /// <summary>Every word a line shows or says (its text, value, hint, note), as a screen reader or a log could meet them.</summary>
    private static IEnumerable<string> Words(SettingsLine line) => (line switch
    {
        SettingsField f => [f.Text, f.Value, f.Placeholder],
        SettingsNote n => [n.Text],
        SettingsReading r => [r.Text, r.Value],
        SettingsSubmenu s => [s.Text, s.Value],
        SettingsCommand c => [c.Text],
        SettingsToggle t => [t.Text, t.Value],
        SettingsAsk a => [a.Text, a.Value, a.Initial, a.Placeholder],
        _ => Array.Empty<string?>(),
    }).OfType<string>().Append(line.Key).Append(line.ToString());

    // ---- [S-3550] the Accounts line ---------------------------------------------------------------

    [Fact]
    public void S3550_accounts_lists_every_account_then_add_account()
    {
        var (host, _) = WithAccounts(Microsoft, MailServer);
        var accounts = At<SettingsSubmenu>(host, SettingsMenu.Keys.Accounts);
        var keys = accounts.Lines.Select(l => l is SettingsSeparator ? "-" : l.Key).ToArray();

        Assert.Equal([SettingsMenu.AccountKey("ms-1"), SettingsMenu.AccountKey(MailServer.Id), "-", SettingsMenu.Keys.AddAccount], keys);
        Assert.Equal("Accounts", accounts.Text);

        // [S-3552] One of them needs signing in again: the line that leads to it says so.
        Assert.Equal(Str.Shell_Accounts_Reconnect.Text(), accounts.Value);
    }

    [Fact]
    public void S3550_with_no_account_it_says_so_over_add_account_and_a_file_that_could_not_be_read_is_said_first()
    {
        var (host, fake) = WithAccounts();
        fake.LoadProblem = "The accounts file could not be read (broken).";
        var lines = At<SettingsSubmenu>(host, SettingsMenu.Keys.Accounts).Lines;

        Assert.Equal(fake.LoadProblem, Assert.IsType<SettingsNote>(lines[0]).Text);
        Assert.True(((SettingsNote)lines[0]).Critical);
        Assert.Equal(Str.Shell_Accounts_None.Text(), Assert.IsType<SettingsNote>(lines[1]).Text);
        Assert.Equal(SettingsMenu.Keys.AddAccount, lines[^1].Key);
    }

    [Fact]
    public void S3550_without_an_account_service_it_says_accounts_are_not_available()
    {
        var host = new FakeHost { Accounts = null };
        var line = Assert.Single(At<SettingsSubmenu>(host, SettingsMenu.Keys.Accounts).Lines);

        Assert.Equal(Str.Shell_Accounts_Unavailable.Text(), Assert.IsType<SettingsNote>(line).Text);
    }

    // ---- [S-3551] an account ----------------------------------------------------------------------

    [Fact]
    public void S3551_an_account_holds_its_address_what_is_stored_its_nickname_and_sign_out()
    {
        var (host, fake) = WithAccounts(Microsoft);
        fake.Shelves["ms-1"] = [new ShelfStats(SyncKind.Mail, 1240, 3, fake.Now.AddMinutes(-4), 2)];
        var key = SettingsMenu.AccountKey("ms-1");
        var account = At<SettingsSubmenu>(host, "accounts", key);

        Assert.Equal("Alex Wilber", account.Text);
        Assert.Null(account.Value);
        Assert.Equal("alex@contoso.com  ·  Microsoft", Assert.IsType<SettingsNote>(account.Lines[0]).Text);

        var mail = Assert.IsType<SettingsReading>(account.Lines.Single(l => l.Key == key + "/kind/Mail"));
        Assert.Equal("Mail", mail.Text);
        Assert.Equal("1,240 messages", mail.Value);
        Assert.Contains("in 3 folders", Assert.IsType<SettingsNote>(account.Lines.Single(l => l.Key == key + "/kind/Mail/detail")).Text, StringComparison.Ordinal);

        // [S-3821] Every kind its provider feeds, in the page's order, stored or not.
        Assert.Equal(
            [key + "/kind/Mail", key + "/kind/Calendar", key + "/kind/Contacts", key + "/kind/Tasks"],
            account.Lines.OfType<SettingsReading>().Select(r => r.Key));
        Assert.Contains(account.Lines, l => l.Key == key + "/nickname");
        Assert.True(Assert.IsType<SettingsCommand>(account.Lines.Single(l => l.Key == key + "/signout")).Danger);
    }

    [Fact]
    public void S3551_sign_out_asks_first_as_a_dangerous_question_and_only_the_answer_signs_out()
    {
        var (host, fake) = WithAccounts(Microsoft);
        var key = SettingsMenu.AccountKey("ms-1");
        At<SettingsCommand>(host, "accounts", key, key + "/signout").Run();

        Assert.Empty(fake.SignedOut);
        var asked = Assert.Single(host.Asked);
        Assert.True(asked.Danger);
        Assert.Equal("Sign out of Alex Wilber?", asked.Question);
        Assert.Equal(Str.Account_SignOut.Text(), asked.Accept);
        Assert.Contains("Microsoft", asked.Detail, StringComparison.Ordinal);

        asked.Done();
        Assert.Equal(["ms-1"], fake.SignedOut);
        Assert.Equal(0, host.Mutations);
    }

    [Fact]
    public void S3551_the_nickname_is_asked_for_with_the_one_there_and_may_be_emptied()
    {
        var (host, fake) = WithAccounts(MailServer);
        var key = SettingsMenu.AccountKey(MailServer.Id);
        var nickname = At<SettingsAsk>(host, "accounts", key, key + "/nickname");

        Assert.Equal("Fabrikam", nickname.Initial);
        Assert.Equal("Fabrikam", nickname.Value);
        Assert.True(nickname.AllowEmpty);

        nickname.Done(string.Empty);
        Assert.Equal([(MailServer.Id, string.Empty, key + "/nickname")], fake.Renamed);
    }

    [Fact]
    public void S3551_what_went_wrong_stands_under_the_line_it_was_done_from()
    {
        var (host, fake) = WithAccounts(Microsoft);
        var key = SettingsMenu.AccountKey("ms-1");
        fake.Menu.Say(key + "/signout", new AccountStatus("Could not save the accounts file: disk full", Failed: true));

        var note = Assert.IsType<SettingsNote>(Flat(Root(host)).Single(l => l.Key == key + "/signout/status"));
        Assert.Equal("Could not save the accounts file: disk full", note.Text);
        Assert.True(note.Critical);

        // Nothing to say: the note is there without words, so the lines keep their place.
        Assert.Equal(string.Empty, Assert.IsType<SettingsNote>(Flat(Root(host)).Single(l => l.Key == key + "/nickname/status")).Text);
    }

    // ---- [S-3552] sign in again --------------------------------------------------------------------

    [Fact]
    public void S3552_an_account_needing_a_sign_in_says_why_and_offers_the_way_back_in()
    {
        var problem = Microsoft with { Problem = "Microsoft wants you to sign in again." };
        var (host, fake) = WithAccounts(problem, MailServer);

        var ms = SettingsMenu.AccountKey("ms-1");
        var account = At<SettingsSubmenu>(host, "accounts", ms);
        Assert.Equal(Str.Shell_Accounts_Reconnect.Text(), account.Value);
        Assert.True(Assert.IsType<SettingsNote>(account.Lines.Single(l => l.Key == ms + "/problem")).Critical);

        // The browser's, for Microsoft: a click signs in again, what it reaches said under it.
        At<SettingsCommand>(host, "accounts", ms, ms + "/again").Run();
        Assert.Equal((HostProviders.Microsoft, ms + "/again"), (fake.SignIns[0].Provider, fake.SignIns[0].Row));
        Assert.IsType<OAuthPkceLoopback>(fake.SignIns[0].Member);

        // A mail server's: its form.
        var mail = SettingsMenu.AccountKey(MailServer.Id);
        var again = At<SettingsSubmenu>(host, "accounts", mail, mail + "/again");
        Assert.Contains(again.Lines, l => l is SettingsField { Masked: true });
    }

    // ---- [S-3553] Add account ----------------------------------------------------------------------

    [Fact]
    public void S3553_add_account_has_the_providers_in_their_place_then_the_password_servers()
    {
        var (host, _) = WithAccounts();
        var add = At<SettingsSubmenu>(host, "accounts", "accounts/add");
        var kinds = add.Lines.Where(l => l is SettingsCommand or SettingsSubmenu).Select(l => l.Key).ToArray();

        Assert.Equal(["accounts/add/microsoft", "accounts/add/google", "accounts/add/imap", "accounts/add/caldav", "accounts/add/carddav"], kinds);
        Assert.All(add.Lines.OfType<SettingsSubmenu>(), s => Assert.Contains(s.Lines, l => l is SettingsField { Masked: true }));
    }

    [Fact]
    public void S3553_a_sign_in_starts_from_its_line_and_while_it_waits_says_so_with_cancel()
    {
        var (host, fake) = WithAccounts();
        At<SettingsCommand>(host, "accounts", "accounts/add", "accounts/add/microsoft").Run();
        Assert.Equal("accounts/add/microsoft", Assert.Single(fake.SignIns).Row);

        fake.Menu.Say("accounts/add/microsoft", new AccountStatus("Waiting for the browser.", Busy: true));
        var add = At<SettingsSubmenu>(host, "accounts", "accounts/add");

        Assert.True(Assert.IsType<SettingsCommand>(add.Lines.Single(l => l.Key == "accounts/add/microsoft")).Disabled);
        var status = Assert.IsType<SettingsNote>(add.Lines.Single(l => l.Key == "accounts/add/microsoft/status"));
        Assert.True(status.Busy);
        Assert.Equal("Waiting for the browser.", status.Text);

        At<SettingsCommand>(host, "accounts", "accounts/add", "accounts/add/microsoft/cancel").Run();
        Assert.Equal(["accounts/add/microsoft"], fake.Cancelled);
    }

    [Fact]
    public void S3553_a_device_code_stands_under_its_line_with_copy_and_its_page()
    {
        var (host, fake) = WithAccounts();
        fake.Menu.Say("accounts/add/google", new AccountStatus("Enter this code", Busy: true) { Code = "WDJB-MJHT", Page = new Uri("https://example.com/device") });
        var add = At<SettingsSubmenu>(host, "accounts", "accounts/add");

        Assert.Equal("WDJB-MJHT", Assert.IsType<SettingsReading>(add.Lines.Single(l => l.Key == "accounts/add/google/code")).Value);
        Assert.Contains(add.Lines, l => l.Key == "accounts/add/google/copy");
        Assert.Contains(add.Lines, l => l.Key == "accounts/add/google/page");
    }

    [Fact]
    public void S3553_a_sign_in_this_build_cannot_start_is_greyed_with_why()
    {
        var (host, fake) = WithAccounts();
        fake.Refusing[HostProviders.Google] = "Google sign-in is not set up in this build of Themia.";
        var add = At<SettingsSubmenu>(host, "accounts", "accounts/add");

        Assert.True(Assert.IsType<SettingsCommand>(add.Lines.Single(l => l.Key == "accounts/add/google")).Disabled);
        Assert.Equal(fake.Refusing[HostProviders.Google], Assert.IsType<SettingsNote>(add.Lines.Single(l => l.Key == "accounts/add/google/why")).Text);
        Assert.False(Assert.IsType<SettingsCommand>(add.Lines.Single(l => l.Key == "accounts/add/microsoft")).Disabled);
    }

    // ---- [S-3554] [S-3555] the forms ---------------------------------------------------------------

    private static SettingsSubmenu ImapForm(FakeHost host) => At<SettingsSubmenu>(host, "accounts", "accounts/add", "accounts/add/imap");

    [Fact]
    public void S3554_the_mail_form_has_its_fields_and_set_the_server_myself_brings_the_servers()
    {
        var (host, fake) = WithAccounts();
        string[] Fields() => [.. ImapForm(host).Lines.OfType<SettingsField>().Select(f => f.Key["accounts/add/imap/".Length..])];

        Assert.Equal(["address", "password", "nickname"], Fields());
        Assert.True(ImapForm(host).Lines.OfType<SettingsField>().Single(f => f.Key.EndsWith("/password", StringComparison.Ordinal)).Masked);

        At<SettingsToggle>(host, "accounts", "accounts/add", "accounts/add/imap", "accounts/add/imap/own").Set(true);
        Assert.True(fake.Menu.Imap.OwnServer);
        Assert.Equal(["address", "password", "server", "port", "user", "nickname"], Fields());
        Assert.True(ImapForm(host).Lines.OfType<SettingsField>().Single(f => f.Key.EndsWith("/port", StringComparison.Ordinal)).Numeric);
    }

    [Fact]
    public void S3554_a_field_writes_the_form_and_never_the_state()
    {
        var (host, fake) = WithAccounts();
        var changes = 0;
        fake.Menu.Changed += () => changes++;

        ImapForm(host).Lines.OfType<SettingsField>().Single(f => f.Key == "accounts/add/imap/address").Set("anna@gmail.com");

        Assert.Equal("anna@gmail.com", fake.Menu.Imap.Address);
        Assert.Equal(1, changes);
        Assert.Equal(0, host.Mutations);

        // [S-522] The provider Themia found, and its advice, over the fields.
        Assert.StartsWith("Gmail: ", Assert.IsType<SettingsNote>(ImapForm(host).Lines.Single(l => l.Key == "accounts/add/imap/hint")).Text, StringComparison.Ordinal);
    }

    [Fact]
    public void S3555_test_and_add_stand_greyed_with_what_is_missing_until_the_form_can_be_tried()
    {
        var (host, fake) = WithAccounts();
        var form = ImapForm(host);

        Assert.True(Assert.IsType<SettingsCommand>(form.Lines.Single(l => l.Key == "accounts/add/imap/test")).Disabled);
        Assert.True(Assert.IsType<SettingsCommand>(form.Lines.Single(l => l.Key == "accounts/add/imap/add")).Disabled);
        Assert.Equal(Str.SettingsAccounts_EnterAddress.Text(), Assert.IsType<SettingsNote>(form.Lines.Single(l => l.Key == "accounts/add/imap/missing")).Text);

        fake.Menu.Imap.SetAddress("anna@gmail.com");
        fake.Menu.Imap.SetPassword(Guid.NewGuid().ToString("N"));
        form = ImapForm(host);

        Assert.Equal(string.Empty, Assert.IsType<SettingsNote>(form.Lines.Single(l => l.Key == "accounts/add/imap/missing")).Text);
        Assert.IsType<SettingsCommand>(form.Lines.Single(l => l.Key == "accounts/add/imap/test")).Run();
        Assert.IsType<SettingsCommand>(form.Lines.Single(l => l.Key == "accounts/add/imap/add")).Run();

        Assert.Equal([(fake.Menu.Imap, AccountTry.Test), (fake.Menu.Imap, AccountTry.Add)], fake.Tries);
    }

    [Fact]
    public void S3555_while_a_try_is_out_every_line_is_greyed_and_how_it_went_stands_under_the_line_tried()
    {
        var (host, fake) = WithAccounts();
        var imap = fake.Menu.Imap;
        imap.SetAddress("anna@gmail.com");
        imap.SetPassword(Guid.NewGuid().ToString("N"));
        imap.Begin(AccountTry.Test);

        var form = ImapForm(host);
        Assert.All(form.Lines.Where(l => l is SettingsField or SettingsToggle or SettingsCommand), l => Assert.True(l.Disabled, l.Key));
        var testing = Assert.IsType<SettingsNote>(form.Lines.Single(l => l.Key == "accounts/add/imap/test/status"));
        Assert.True(testing.Busy);
        Assert.Equal(Str.Shell_Accounts_Checking.Text(), testing.Text);

        imap.End("The server refused the user name or password.", failed: true);
        form = ImapForm(host);
        var refused = Assert.IsType<SettingsNote>(form.Lines.Single(l => l.Key == "accounts/add/imap/test/status"));
        Assert.True(refused.Critical);
        Assert.Equal("The server refused the user name or password.", refused.Text);
        Assert.Equal(string.Empty, Assert.IsType<SettingsNote>(form.Lines.Single(l => l.Key == "accounts/add/imap/add/status")).Text);
        Assert.False(Assert.IsType<SettingsCommand>(form.Lines.Single(l => l.Key == "accounts/add/imap/add")).Disabled);
    }

    [Fact]
    public void S3561_a_forms_lines_keep_their_place_while_it_is_typed_in_and_tried()
    {
        var (host, fake) = WithAccounts();
        string[] Keys() => [.. ImapForm(host).Lines.Select(l => l.Key)];
        var before = Keys();

        fake.Menu.Imap.SetAddress("anna@gmail.com");
        Assert.Equal(before, Keys());
        fake.Menu.Imap.SetPassword(Guid.NewGuid().ToString("N"));
        Assert.Equal(before, Keys());
        fake.Menu.Imap.Begin(AccountTry.Add);
        Assert.Equal(before, Keys());
        fake.Menu.Imap.End("Connected anna@gmail.com.", failed: false);
        Assert.Equal(before, Keys());
    }

    // ---- [S-3556] the password ---------------------------------------------------------------------

    [Fact]
    public void S3556_no_line_anywhere_carries_a_password_or_a_token_typed_into_a_form()
    {
        var (host, fake) = WithAccounts(Microsoft, MailServer);
        var secret = Guid.NewGuid().ToString("N");
        foreach (var form in fake.Menu.Forms.Append(fake.Menu.Token(HostProviders.Microsoft)))
        {
            form.SetAddress("anna@gmail.com");
            form.SetPassword(secret);
            form.SetOwnServer(true);
        }

        var lines = Flat(Root(host)).ToList();
        Assert.All(lines.SelectMany(Words), words => Assert.DoesNotContain(secret, words, StringComparison.Ordinal));

        // The masked fields say only that they hold one, and give it back to a box drawn again.
        var masked = lines.OfType<SettingsField>().Where(f => f.Masked).ToList();
        Assert.NotEmpty(masked);
        Assert.All(masked, f =>
        {
            Assert.Null(f.Value);
            Assert.True(f.HasValue);
            Assert.Equal(secret, f.Fill!());
        });

        // Its submenu closed: wiped.
        masked[0].Wipe!();
        Assert.False(fake.Menu.Imap.HasPassword);
    }
}
