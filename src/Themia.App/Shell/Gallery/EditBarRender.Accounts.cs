#if DEBUG
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services.Accounts;
using Themia.App.Widgets;
using Themia.Core.Accounts;
using Themia.Core.Localization;
using Themia.Core.Settings;
using Themia.Core.Sync;
using Themia.Sdk;
using Themia.Sdk.Services;

namespace Themia.App.Shell.Gallery;

/// <summary>
/// [S-3560] ⚙ › Accounts open on the stand-in display over three stand-in accounts (a Microsoft one
/// with numbers, a mail server that needs signing in again, a calendar server): <c>-accounts</c>,
/// <c>-account</c> (one account's submenu), <c>-account-again</c> (the one needing a sign-in),
/// <c>-signout</c> (its "are you sure?" level), <c>-add</c> (Add account ›),
/// <c>-signin</c> (Microsoft's browser sign-in waiting, Cancel under it), <c>-imap</c> (the mail
/// server's form, empty: Test and Add greyed with what is missing), <c>-imap-filled</c> (the address,
/// the password as bullets, the server set by hand), <c>-imap-address</c> (the address's field level),
/// <c>-imap-password</c> (the password's field level: bullets in a password box), <c>-imap-testing</c> (a test out: every field
/// greyed, "Checking…" under Test), <c>-imap-failed</c> (an add refused, the server's words under
/// Add), <c>-caldav</c> (the calendar server's form, filled). A stand-in host: no account is real, no
/// sign-in or server is reached, and the password is made up for the picture and never written out.
/// </summary>
internal static partial class EditBarRender
{
    private static async Task AccountsShots(Grid screen, string outDir, string theme)
    {
        const string a = SettingsMenu.Keys.Accounts;
        const string add = SettingsMenu.Keys.AddAccount;
        var ms = SettingsMenu.AccountKey(RenderAccountsHost.MicrosoftId);
        var imap = SettingsMenu.AccountKey(RenderAccountsHost.ImapId);

        (string Name, string[] Open, string? Click, Action<RenderAccountsHost>? Fill)[] cases =
        [
            ("-accounts", [a], null, null),
            ("-account", [a, ms], null, null),
            ("-account-again", [a, imap], null, null),
            ("-signout", [a, ms], ms + "/signout", null),
            ("-add", [a, add], null, null),
            ("-signin", [a, add], add + "/microsoft", null),
            ("-imap", [a, add, add + "/imap"], null, null),
            ("-imap-filled", [a, add, add + "/imap"], null, FillImap),
            ("-imap-address", [a, add, add + "/imap", add + "/imap/address"], null, null),
            ("-imap-password", [a, add, add + "/imap", add + "/imap/password"], null, FillImap),
            ("-imap-testing", [a, add, add + "/imap"], null, h =>
            {
                FillImap(h);
                h.Outcome = RenderTry.StaysOut;
                h.Try(h.Menu.Imap, AccountTry.Test);
            }),
            ("-imap-failed", [a, add, add + "/imap"], null, h =>
            {
                FillImap(h);
                h.Outcome = RenderTry.Refused;
                h.Try(h.Menu.Imap, AccountTry.Add);
            }),
            ("-caldav", [a, add, add + "/caldav"], null, h =>
            {
                h.Menu.CalDav.SetAddress("alex@fabrikam.example");
                h.Menu.CalDav.SetPassword(MadeUpPassword());
                h.Menu.CalDav.SetOwnServer(true);
                h.Menu.CalDav.SetServer("https://dav.fabrikam.example");
            }),
        ];

        foreach (var (name, open, click, fill) in cases)
        {
            var accounts = new RenderAccountsHost();
            fill?.Invoke(accounts);
            var host = new RenderSettingsHost { Accounts = accounts };
            await SettingsShot(screen, System.IO.Path.Combine(outDir, $"settings-{theme}{name}.png"), host, open, menu =>
            {
                if (click is not null && !menu.DebugClickKey(click))
                {
                    Console.WriteLine($"editbar: no line {click} to click");
                }
            }).ConfigureAwait(true);
        }
    }

    /// <summary>The mail server's form as someone fills it in: an address Themia knows no server for, so the server by hand.</summary>
    private static void FillImap(RenderAccountsHost host)
    {
        var form = host.Menu.Imap;
        form.SetAddress("alex@fabrikam.example");
        form.SetPassword(MadeUpPassword());
        form.SetOwnServer(true);
        form.SetServer("imap.fabrikam.example");
    }

    /// <summary>A password made up for the picture: drawn as bullets only, never written anywhere.</summary>
    private static string MadeUpPassword() => new('x', 12);

    /// <summary>How a stand-in try ends: it stays out (the busy picture), the server refuses, or it signs in.</summary>
    private enum RenderTry
    {
        Connects,
        StaysOut,
        Refused,
    }

    /// <summary>
    /// A stand-in for ⚙ › Accounts' host: three made-up accounts and their numbers, forms that keep
    /// what is typed, sign-ins that only say they wait and tries that end as <see cref="Outcome"/> says.
    /// </summary>
    private sealed class RenderAccountsHost : IAccountsMenuHost
    {
        public const string MicrosoftId = "render-ms";
        public const string ImapId = "imap:alex@imap.fabrikam.example";
        private const string DavId = "caldav:alex@dav.fabrikam.example/calendars/alex/home/";

        private static readonly DateTimeOffset Moment = new(2026, 10, 25, 10, 42, 0, TimeSpan.Zero);

        public AccountsMenuState Menu { get; } = new();

        public RenderTry Outcome { get; set; } = RenderTry.Connects;

        public IReadOnlyList<AccountInfo> Accounts { get; } =
        [
            new(MicrosoftId, HostProviders.Microsoft, "alex@contoso.com", "Alex Wilber", null, null, null),
            new(ImapId, HostProviders.Imap, "alex@fabrikam.example", "alex@fabrikam.example", "Fabrikam mail", null,
                Str.Shell_Accounts_MailPasswordRefused.Text()),
            new(DavId, HostProviders.CalDav, "alex@fabrikam.example", "alex@fabrikam.example", null, "https://dav.fabrikam.example/calendars/alex/home/", null),
        ];

        public string? LoadProblem => null;

        public DateTimeOffset Now => Moment;

        public IReadOnlyList<ShelfStats> Stats(string accountId) => accountId switch
        {
            MicrosoftId =>
            [
                new(SyncKind.Mail, 1240, 3, Moment.AddMinutes(-4), 2),
                new(SyncKind.Calendar, 386, 2, Moment.AddMinutes(-12), 0),
                new(SyncKind.Contacts, 214, 0, Moment.AddHours(-3), 0),
            ],
            ImapId => [new(SyncKind.Mail, 50, 1, Moment.AddDays(-2), 0)],
            _ => [new(SyncKind.Calendar, 42, 1, Moment.AddMinutes(-30), 0)],
        };

        public string? Unavailable(ProviderId provider) => null;

        public void SignIn(ProviderId provider, ServiceAuth member, string row)
            => Menu.Say(row, new AccountStatus(Str.Shell_Accounts_WaitingBrowser.Text(), Busy: true));

        public void Try(AccountForm form, AccountTry what)
        {
            if (!form.Begin(what))
            {
                return;
            }

            switch (Outcome)
            {
                case RenderTry.Refused:
                    form.End(Str.Shell_Accounts_ServerRefused.Text(), failed: true);
                    break;
                case RenderTry.Connects:
                    form.End(Str.SettingsAccounts_ConnectedTo.Format(("host", form.ServerTried)), failed: false);
                    break;
            }

            Menu.Touch();
        }

        public void Cancel(string row) => Menu.Say(row, null);

        public void Rename(string accountId, string nickname, string row)
        {
            // ignore: a render keeps no account.
        }

        public void SignOut(string accountId, string row)
        {
            // ignore: a render keeps no account.
        }

        public void Open(Uri page)
        {
            // ignore: a render opens no page.
        }

        public void Copy(string text)
        {
            // ignore: a render never touches the clipboard.
        }
    }
}
#endif
