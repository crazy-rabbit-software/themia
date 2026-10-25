using Themia.Core.Accounts;
using Themia.Core.Localization;
using Themia.Core.Settings;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-3554]..[S-3557] ⚙ › Accounts' forms as drafts: what a form finds from the address, what it
/// lacks in the page's words, how a try greys it and how it ends, and that the password is never
/// said and is wiped. The passwords here are made up per run.
/// </summary>
[Collection(GlobalLocaleCollection.Name)]
public sealed class AccountFormTests
{
    private static string MadeUp() => Guid.NewGuid().ToString("N");

    private static AccountForm Imap() => new(AccountFormKind.Imap, HostProviders.Imap);

    private static AccountForm CalDav() => new(AccountFormKind.CalDav, HostProviders.CalDav);

    [Fact]
    public void S3554_an_address_themia_knows_finds_its_mail_server()
    {
        var form = Imap();
        form.SetAddress(" anna@gmail.com ");

        Assert.Equal("anna@gmail.com", form.Address);
        Assert.Equal("imap.gmail.com", form.ServerTried);
        Assert.Equal(993, form.PortTried);
        Assert.Equal("anna@gmail.com", form.UserTried);
        Assert.Equal("Gmail", form.ImapPreset?.Name);
    }

    [Fact]
    public void S3554_setting_the_server_myself_starts_from_the_one_themia_knows_and_takes_what_is_typed()
    {
        var form = Imap();
        form.SetAddress("anna@gmail.com");
        form.SetOwnServer(true);

        Assert.Equal("imap.gmail.com", form.Server);

        form.SetServer("mail.example.org");
        form.SetPort("143");
        form.SetUserName("anna");

        Assert.Equal("mail.example.org", form.ServerTried);
        Assert.Equal(143, form.PortTried);
        Assert.Equal("anna", form.UserTried);

        // Switched off, the address finds the server again; what was typed waits for the next time.
        form.SetOwnServer(false);
        Assert.Equal("imap.gmail.com", form.ServerTried);
        Assert.Equal("anna@gmail.com", form.UserTried);
    }

    [Fact]
    public void S3555_a_mail_form_lacks_what_the_page_asked_for_in_its_words()
    {
        var form = Imap();
        Assert.Equal(Str.SettingsAccounts_EnterAddress.Text(), form.Problem);

        form.SetAddress("anna@unknown.example");
        Assert.Equal(Str.SettingsAccounts_EnterAddress.Text(), form.Problem);

        form.SetPassword(MadeUp());
        Assert.Equal(Str.SettingsAccounts_NoServer.Format(("domain", "unknown.example")), form.Problem);

        form.SetOwnServer(true);
        Assert.Equal(Str.Shell_Accounts_HostName.Text(), form.Problem);

        form.SetServer("https://mail.unknown.example");
        Assert.Equal(Str.Shell_Accounts_HostName.Text(), form.Problem);

        form.SetServer("mail.unknown.example");
        Assert.Null(form.Problem);

        form.SetPort("nine");
        Assert.Equal(Str.Shell_Accounts_PortNumber.Text(), form.Problem);

        form.SetPort("70000");
        Assert.Equal(Str.Shell_Accounts_PortRange.Text(), form.Problem);

        form.SetPort(string.Empty);
        Assert.Null(form.Problem);
        Assert.Equal(993, form.PortTried);

        // An Arabic or Persian keyboard's digits are digits too.
        form.SetPort("٩٩٣");
        Assert.Null(form.Problem);
        Assert.Equal(993, form.PortTried);

        form.SetPort("99999999999");
        Assert.Equal(Str.Shell_Accounts_PortNumber.Text(), form.Problem);

        form.SetPort("0");
        Assert.Equal(Str.Shell_Accounts_PortRange.Text(), form.Problem);
    }

    [Fact]
    public void S3555_a_calendar_form_needs_the_user_name_its_password_and_a_server_address()
    {
        var form = CalDav();
        Assert.Equal(Str.Shell_Accounts_EnterCredentials.Text(), form.Problem);

        form.SetAddress("anna@fastmail.com");
        form.SetPassword(MadeUp());
        Assert.Null(form.Problem);
        Assert.Equal("https://caldav.fastmail.com/dav/", form.ServerTried);

        form.SetAddress("anna");
        Assert.Equal(Str.SettingsAccounts_NoServer.Format(("domain", "anna")), form.Problem);

        form.SetOwnServer(true);
        form.SetServer("ftp://cloud.example.com");
        Assert.Equal(Str.SettingsAccounts_DavAddress.Text(), form.Problem);

        form.SetServer("cloud.example.com");
        Assert.Null(form.Problem);
    }

    [Fact]
    public void S3555_a_token_form_needs_the_token()
    {
        var form = new AccountForm(AccountFormKind.Token, HostProviders.Microsoft);
        Assert.Equal(Str.SettingsAccounts_PasteToken.Text(), form.Problem);

        form.SetPassword(MadeUp());
        Assert.Null(form.Problem);
    }

    [Fact]
    public void S3555_a_try_begins_only_on_a_whole_form_and_greys_it_while_it_is_out()
    {
        var form = Imap();
        Assert.False(form.Begin(AccountTry.Test));
        Assert.Null(form.Busy);

        form.SetAddress("anna@gmail.com");
        form.SetPassword(MadeUp());
        Assert.True(form.Begin(AccountTry.Test));
        Assert.Equal(AccountTry.Test, form.Busy);

        // No edit while it is out, and no second try.
        Assert.False(form.SetAddress("bert@gmail.com"));
        Assert.False(form.SetOwnServer(true));
        Assert.False(form.SetPassword(MadeUp()));
        Assert.False(form.Begin(AccountTry.Add));
        Assert.Equal("anna@gmail.com", form.Address);
    }

    [Fact]
    public void S3555_a_test_keeps_the_form_and_an_edit_takes_its_outcome_away()
    {
        var form = Imap();
        form.SetAddress("anna@gmail.com");
        form.SetPassword(MadeUp());
        form.Begin(AccountTry.Test);
        form.End("Connected to imap.gmail.com.", failed: false);

        Assert.Null(form.Busy);
        Assert.Equal(new AccountOutcome(AccountTry.Test, "Connected to imap.gmail.com.", false), form.Outcome);
        Assert.Equal("anna@gmail.com", form.Address);
        Assert.True(form.HasPassword);

        // The same words change nothing; other words take the outcome with them.
        Assert.False(form.SetAddress("anna@gmail.com"));
        Assert.NotNull(form.Outcome);
        Assert.True(form.SetNickname("Mail"));
        Assert.Null(form.Outcome);
    }

    [Fact]
    public void S3555_a_refused_add_keeps_what_was_typed_and_says_why()
    {
        var form = Imap();
        form.SetAddress("anna@gmail.com");
        form.SetPassword(MadeUp());
        form.Begin(AccountTry.Add);
        form.End("The server refused the user name or password.", failed: true);

        Assert.Equal(new AccountOutcome(AccountTry.Add, "The server refused the user name or password.", true), form.Outcome);
        Assert.Equal("anna@gmail.com", form.Address);
        Assert.True(form.HasPassword);
    }

    [Fact]
    public void S3555_an_added_account_empties_the_form_wipes_its_password_and_says_it_was_added()
    {
        var form = Imap();
        form.SetAddress("anna@gmail.com");
        form.SetPassword(MadeUp());
        form.SetNickname("Mail");
        form.Begin(AccountTry.Add);
        form.End("Connected anna@gmail.com.", failed: false);

        Assert.Equal(string.Empty, form.Address);
        Assert.Equal(string.Empty, form.Nickname);
        Assert.False(form.HasPassword);
        Assert.Equal(string.Empty, form.Password());
        Assert.Equal(new AccountOutcome(AccountTry.Add, "Connected anna@gmail.com.", false), form.Outcome);
    }

    [Fact]
    public void S3556_the_password_is_taken_as_typed_never_said_and_wiped_on_its_own()
    {
        var secret = " " + MadeUp() + " ";
        var form = Imap();
        form.SetAddress("anna@gmail.com");
        form.SetPassword(secret);

        Assert.Equal(secret, form.Password());
        Assert.DoesNotContain(secret.Trim(), form.ToString(), StringComparison.Ordinal);
        Assert.False(form.SetPassword(secret));

        // Its submenu closed: the password goes, what else was typed stays.
        form.WipePassword();
        Assert.False(form.HasPassword);
        Assert.Equal("anna@gmail.com", form.Address);
    }

    [Fact]
    public void S3557_closing_the_menu_empties_every_form_and_takes_every_status_away()
    {
        var state = new AccountsMenuState();
        state.Imap.SetAddress("anna@gmail.com");
        state.Imap.SetPassword(MadeUp());
        state.CalDav.SetAddress("anna@fastmail.com");
        state.Token(HostProviders.Microsoft).SetPassword(MadeUp());
        state.Say("accounts/add/microsoft", new AccountStatus("Waiting", Busy: true));
        var changes = 0;
        state.Changed += () => changes++;

        state.Close();

        Assert.All(state.Forms, f =>
        {
            Assert.Equal(string.Empty, f.Address);
            Assert.False(f.HasPassword);
        });
        Assert.Null(state.Status("accounts/add/microsoft"));
        Assert.Equal(1, changes);
    }

    [Fact]
    public void S3554_each_password_server_has_its_one_form_wherever_it_is_opened_from()
    {
        var state = new AccountsMenuState();

        Assert.Same(state.Imap, state.FormFor(HostProviders.Imap));
        Assert.Same(state.CalDav, state.FormFor(HostProviders.CalDav));
        Assert.Same(state.CardDav, state.FormFor(HostProviders.CardDav));
        Assert.Null(state.FormFor(HostProviders.Microsoft));
        Assert.Same(state.Token(HostProviders.Google), state.Token(HostProviders.Google));
    }

    [Fact]
    public void S3555_the_mail_server_rule_is_the_one_the_service_checks_with()
    {
        Assert.Null(AccountRules.ImapServerProblem(" imap.example.com ", 993));
        Assert.Equal(Str.Shell_Accounts_HostName.Text(), AccountRules.ImapServerProblem("imap example", 993));
        Assert.Equal(Str.Shell_Accounts_HostName.Text(), AccountRules.ImapServerProblem(string.Empty, 993));
        Assert.Equal(Str.Shell_Accounts_PortRange.Text(), AccountRules.ImapServerProblem("imap.example.com", 0));
    }
}
