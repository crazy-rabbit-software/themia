using Themia.App.Services.Accounts;
using Themia.App.Shell;
using Themia.Core.Accounts;
using Themia.Core.Localization;
using Themia.Core.Sync;
using Themia.Sdk;
using Themia.Sdk.Services;

namespace Themia.Core.Settings;

/// <summary>
/// [S-3550]..[S-3555] [S-2312] The Accounts page: who Themia is signed in to, what each account holds on this PC,
/// and the ways to add, rename and sign out of one, after Karpo's Accounts page (karpo-app,
/// Karpo.Core/OptionAccountRows.cs <c>Page</c>, docs/widgets.md §535, §638, §685): under "Your accounts" every account
/// a card that opens (its provider's icon, its name, its address and provider under it, what it holds at its head's
/// end while shut, Sign out at its end); under "Add an account" a way to add each kind, its line under it: a sign-in
/// in the browser a row that starts it, a password server's or a token's form a card of its fields. What a line
/// starts says how it goes under it. Nothing is a page of its own.
/// </summary>
public static partial class SettingsMenu
{
    /// <summary>[S-2312] The Accounts lines: under "Your accounts" every account, then under "Add an account" its ways.</summary>
    public static SettingsSubmenu Accounts(ISettingsMenuHost host)
    {
        ArgumentNullException.ThrowIfNull(host);

        var lines = new List<SettingsLine>();
        if (host.Accounts is not { } accounts)
        {
            lines.Add(new SettingsNote(Keys.Accounts + "/unavailable", Str.Shell_Accounts_Unavailable.Text()));
            return new SettingsSubmenu(Keys.Accounts, Str.Shell_Settings_Nav_Accounts.Text(), Glyphs.Contact, lines);
        }

        if (accounts.LoadProblem is { } problem)
        {
            lines.Add(new SettingsNote(Keys.Accounts + "/file", problem) { Critical = true });
        }

        lines.Add(new SettingsHeading(Keys.YourAccounts, Str.SettingsPages_Accounts_Yours.Text()));
        var listed = accounts.Accounts;
        if (listed.Count == 0)
        {
            lines.Add(new SettingsNote(Keys.Accounts + "/none", Str.Shell_Accounts_None.Text()));
        }

        foreach (var account in listed)
        {
            lines.Add(Account(host, accounts, account));
        }

        lines.Add(new SettingsHeading(Keys.AddAccount, Str.SettingsPages_Accounts_Add.Text()));
        lines.AddRange(AddAccount(accounts));

        // [S-3552] An account that needs signing in again shows on the line that leads to it.
        var needing = listed.Count(a => a.NeedsSignIn);
        return new SettingsSubmenu(Keys.Accounts, Str.Shell_Settings_Nav_Accounts.Text(), Glyphs.Contact, lines)
        {
            Value = needing > 0 ? Str.Shell_Accounts_Reconnect.Text() : null,
        };
    }

    /// <summary>The key of an account's card: its id, which no rename or language changes.</summary>
    public static string AccountKey(string accountId) => Keys.Accounts + "/account:" + accountId;

    /// <summary>
    /// [S-3551] [S-2312] One account's card, Karpo's thing: its provider's icon (a warning while it needs signing in
    /// again), its name, its address and provider under it (its provider alone where it is named by its address), at
    /// its head's end while shut what it holds on this PC ("1,240 messages · 386 events", Karpo's §638) or Sign in
    /// again, and Sign out, asked first. Opened: what holds against it and the way back in ([S-3552]); a reading per
    /// kind its provider feeds, with what is stored and when it last synced ([S-3821]); where a service grants access
    /// per resource, the page that widens it ([S-9919]); its Nickname; how a sign-out went.
    /// </summary>
    private static SettingsSubmenu Account(ISettingsMenuHost settings, IAccountsMenuHost host, AccountInfo account)
    {
        var key = AccountKey(account.Id);
        var provider = account.Provider.Label();
        var lines = new List<SettingsLine>();

        if (account.Problem is { } problem)
        {
            lines.Add(new SettingsNote(key + "/problem", problem) { Critical = true });
            lines.AddRange(SignInAgain(host, account, key + "/again"));
            lines.Add(new SettingsSeparator(key + "/-kinds"));
        }

        var kinds = SyncStats.Summarize(account.Features, host.Stats(account.Id));
        foreach (var kind in kinds)
        {
            var count = SyncStats.Count(kind);
            lines.Add(new SettingsReading(key + "/kind/" + kind.Kind, SyncStats.Label(kind.Kind), KindGlyph(kind.Kind), count.Length > 0 ? count : null));
            lines.Add(new SettingsNote(key + "/kind/" + kind.Kind + "/detail", SyncStats.Detail(kind, host.Now)));
        }

        lines.Add(new SettingsSeparator(key + "/-own"));
        if (ProviderCatalog.Of(account.Provider).SignIn.Find<OAuthDeviceCode>() is { GrantPage: { } grant })
        {
            lines.Add(new SettingsCommand(key + "/grant", Str.SignIn_GrantPage.Text(), Glyphs.Link, () => host.Open(grant)));
            lines.Add(new SettingsNote(key + "/grant/note", Str.SignIn_GrantPageDesc.Format(("provider", provider))));
        }

        var id = account.Id;
        lines.Add(new SettingsAsk(
            key + "/nickname",
            Str.Connect_Field_Nickname.Text(),
            Glyphs.Rename,
            Str.Connect_Field_Nickname.Text(),
            account.DisplayName.Length > 0 ? account.DisplayName : account.Address,
            Str.Common_Save.Text(),
            account.Nickname ?? string.Empty,
            _ => null,
            typed => host.Rename(id, typed, key + "/nickname"))
        {
            AllowEmpty = true,
            Value = account.Nickname,
        });
        lines.Add(StatusNote(host, key + "/nickname"));
        lines.Add(StatusNote(host, key + "/signout"));

        // What it holds, its first two kinds (the head's end has room for two; opened, every kind is a row).
        var holds = string.Join(" · ", kinds.Select(SyncStats.Count).Where(c => c.Length > 0).Take(2));
        return new SettingsSubmenu(key, account.Label, account.NeedsSignIn ? Glyphs.Warning : ProviderGlyph(account.Provider), lines)
        {
            Card = true,
            Caption = string.Equals(account.Label, account.Address, StringComparison.Ordinal)
                ? provider
                : Str.Shell_Accounts_Detail.Format(("address", account.Address), ("provider", provider)),
            Value = account.NeedsSignIn ? Str.Shell_Accounts_Reconnect.Text() : holds.Length > 0 ? holds : null,
            Tools =
            [
                new SettingsTool(key + "/signout", Glyphs.SignOut, Str.Account_SignOut.Text(), SettingsToolKey.Delete)
                {
                    Run = () => settings.Confirm(new SettingsConfirm(
                        Str.Shell_Accounts_SignOutConfirm.Format(("name", account.Label)),
                        Str.Account_SignOut.Text(),
                        () => host.SignOut(id, key + "/signout"),
                        Str.Shell_Accounts_SignOutBody.Format(("provider", provider)),
                        Danger: true)),
                },
            ],
        };
    }

    /// <summary>
    /// [S-3552] The way back in for an account that needs it, in its card: the sign-in that added it (the
    /// browser, a code), which replaces its sign-in in place; for a password server or a token, its form's lines.
    /// </summary>
    private static IEnumerable<SettingsLine> SignInAgain(IAccountsMenuHost host, AccountInfo account, string row)
    {
        if (host.Menu.FormFor(account.Provider) is { } form)
        {
            return FormLines(host, form, row);
        }

        var module = ProviderCatalog.Of(account.Provider);
        var member = module.SignIn.Members.FirstOrDefault(m => m is not AnonymousAuth);
        if (member is PastedToken)
        {
            return FormLines(host, host.Menu.Token(account.Provider), row);
        }

        return member is null ? [] : SignInLines(host, account.Provider, member, row, Str.Shell_Accounts_Reconnect.Text(), Glyphs.Refresh);
    }

    /// <summary>
    /// [S-3553] [S-2312] "Add an account": every offered provider ([S-3562]) with a sign-in of its own, in its place
    /// ([S-9910]), then the host's password servers, each a card of its form ([S-3554]); each under its name says
    /// what it brings (its provider's kinds: "Mail, Calendar, Contacts, Tasks").
    /// </summary>
    private static List<SettingsLine> AddAccount(IAccountsMenuHost host)
    {
        var lines = new List<SettingsLine>();
        foreach (var provider in ProviderCatalog.Offered.Where(p => p.SignIn.Members.Any(m => m is not AnonymousAuth)))
        {
            lines.AddRange(ProviderLines(host, provider));
        }

        foreach (var (form, text, glyph) in new[]
        {
            (host.Menu.Imap, Str.Shell_Accounts_MenuImap, Glyphs.Mail),
            (host.Menu.CalDav, Str.Shell_Accounts_MenuCalDav, Glyphs.Calendar),
            (host.Menu.CardDav, Str.Shell_Accounts_MenuCardDav, Glyphs.Contact),
        })
        {
            if (!ProviderCatalog.Of(form.Provider).Offered)
            {
                continue;
            }

            var row = Keys.AddAccount + "/" + form.Provider.Value;
            lines.Add(new SettingsSubmenu(row, text.Text(), glyph, FormLines(host, form, row))
            {
                Card = true,
                Caption = Brings(form.Provider),
            });
        }

        return lines;
    }

    /// <summary>[S-2312] What a way to add an account brings, under its name: its provider's kinds in the pages' order.</summary>
    public static string Brings(ProviderId provider)
        => string.Join(", ", SyncStats.Summarize(ProviderCatalog.FeaturesOf(provider), []).Select(k => SyncStats.Label(k.Kind)));

    /// <summary>
    /// [S-3553] A provider's lines in "Add an account": one sign-in, its row (a click starts it) with what stands
    /// under it; a pasted token, a card of its form; several, a card with each way in it (a token's form under its
    /// heading).
    /// </summary>
    private static IEnumerable<SettingsLine> ProviderLines(IAccountsMenuHost host, IProviderModule provider)
    {
        var row = Keys.AddAccount + "/" + provider.Id.Value;
        var members = provider.SignIn.Members.Where(m => m is not AnonymousAuth).ToList();
        var label = Str.SettingsPages_Accounts_AddProvider.Format(("provider", provider.Label.Text()));
        var glyph = ProviderGlyph(provider.Id);
        var brings = Brings(provider.Id);

        if (members is [PastedToken])
        {
            return [new SettingsSubmenu(row, label, glyph, FormLines(host, host.Menu.Token(provider.Id), row)) { Card = true, Caption = brings }];
        }

        if (members is [var only])
        {
            return SignInLines(host, provider.Id, only, row, label, glyph).Select(l => l is SettingsCommand c && c.Key == row ? c with { Caption = brings } : l);
        }

        var ways = new List<SettingsLine>();
        foreach (var member in members)
        {
            var (way, words) = member switch
            {
                OAuthPkceLoopback => ("browser", Str.SignIn_UseBrowser),
                OAuthDeviceCode => ("code", Str.SignIn_UseCode),
                _ => ("token", Str.SignIn_UseToken),
            };

            ways.AddRange(member is PastedToken
                ? [new SettingsHeading(row + "/" + way, words.Text()), .. FormLines(host, host.Menu.Token(provider.Id), row + "/" + way)]
                : SignInLines(host, provider.Id, member, row + "/" + way, words.Text(), null));
        }

        return [new SettingsSubmenu(row, label, glyph, ways) { Card = true, Caption = brings }];
    }

    /// <summary>
    /// [S-1783] [S-9919] A sign-in's line and what stands under it: greyed, with why, where this build
    /// cannot start it ([S-536]); while it waits, how far it is (a ring), the code to enter with Copy
    /// code and the page it is entered on (a device code), and Cancel; once back, who it signed in or
    /// why it did not.
    /// </summary>
    private static IEnumerable<SettingsLine> SignInLines(IAccountsMenuHost host, ProviderId provider, ServiceAuth member, string row, string text, string? glyph)
    {
        var status = host.Menu.Status(row);
        var unavailable = member is OAuthPkceLoopback ? host.Unavailable(provider) : null;
        var lines = new List<SettingsLine>
        {
            new SettingsCommand(row, text, glyph, () => host.SignIn(provider, member, row))
            {
                Disabled = unavailable is not null || status is { Busy: true },
            },
        };

        if (unavailable is not null)
        {
            lines.Add(new SettingsNote(row + "/why", unavailable));
        }

        lines.Add(StatusNote(host, row));

        if (status is { Code: { } code })
        {
            lines.Add(new SettingsReading(row + "/code", Str.SettingsAccounts_Code.Text(), null, code));
            lines.Add(new SettingsCommand(row + "/copy", Str.SignIn_CopyCode.Text(), Glyphs.Copy, () => host.Copy(code)));
            if (status.Page is { } page)
            {
                lines.Add(new SettingsCommand(row + "/page", Str.SignIn_OpenPage.Text(), Glyphs.Link, () => host.Open(page)));
            }
        }

        if (status is { Busy: true })
        {
            lines.Add(new SettingsCommand(row + "/cancel", Str.Common_Cancel.Text(), null, () => host.Cancel(row)));
        }

        return lines;
    }

    /// <summary>
    /// [S-3554] [S-3555] A form's lines: its fields (typed in the menu; the password masked, [S-3556]),
    /// "Set the server myself" and the fields it brings, then Test connection and Add, greyed with what
    /// the form lacks under them, every line greyed while a try is out, and how the last try went under
    /// the line it was tried from. The lines keep their place while the form is typed in: the words
    /// under a line change in place, so the box being typed in keeps the keyboard.
    /// </summary>
    internal static List<SettingsLine> FormLines(IAccountsMenuHost host, AccountForm form, string prefix)
    {
        var busy = form.Busy is not null;
        var lines = new List<SettingsLine>();

        Action<string> Writes(Func<string, bool> set) => typed =>
        {
            if (set(typed))
            {
                host.Menu.Touch();
            }
        };

        SettingsField Field(string key, Str text, string value, string placeholder, Func<string, bool> set, bool numeric = false)
            => new(prefix + "/" + key, text.Text(), value, placeholder, Writes(set)) { Disabled = busy, Numeric = numeric };

        var password = new SettingsField(
            prefix + "/password",
            (form.Kind == AccountFormKind.Token ? Str.SignIn_Token : Str.Connect_Field_Password).Text(),
            null,
            string.Empty,
            Writes(form.SetPassword))
        {
            Masked = true,
            HasValue = form.HasPassword,
            Fill = form.Password,
            Wipe = form.WipePassword,
            Disabled = busy,
        };

        SettingsToggle Own() => new(prefix + "/own", Str.SettingsAccounts_OwnServer.Text(), null, form.OwnServer, on =>
        {
            if (form.SetOwnServer(on))
            {
                host.Menu.Touch();
            }
        })
        {
            Disabled = busy,
        };

        switch (form.Kind)
        {
            case AccountFormKind.Imap:
                // [S-522] The provider Themia found for the address, and its advice, over the fields.
                var preset = form.OwnServer ? null : form.ImapPreset;
                lines.Add(new SettingsNote(prefix + "/hint", preset is null
                    ? string.Empty
                    : Str.Shell_Accounts_PresetHint.Format(("provider", preset.Name), ("note", preset.Note ?? Str.Shell_Accounts_PresetFilled.Text()))));
                lines.Add(Field("address", Str.Shell_Accounts_EmailAddress, form.Address, "you@example.com", form.SetAddress));
                lines.Add(password);

                // Where Themia knows the provider its own advice stands over the fields instead.
                lines.Add(new SettingsNote(prefix + "/passwordNote", preset is null ? Str.Shell_Accounts_PasswordDesc.Text() : string.Empty));
                lines.Add(Own());
                if (form.OwnServer)
                {
                    lines.Add(Field("server", Str.Shell_Accounts_Server, form.Server, "imap.example.com", form.SetServer));
                    lines.Add(Field("port", Str.Connect_Field_Port, form.Port, AccountForm.UsualImapPort.ToString(System.Globalization.CultureInfo.InvariantCulture), form.SetPort, numeric: true));
                    lines.Add(Field("user", Str.Shell_Accounts_UserName, form.UserName, Str.Shell_Accounts_UsuallyAddress.Text(), form.SetUserName));
                    lines.Add(new SettingsNote(prefix + "/tls", Str.Shell_Accounts_ImapDesc.Text()));
                }

                lines.Add(Field("nickname", Str.Connect_Field_Nickname, form.Nickname, Str.Shell_Accounts_Optional.Text(), form.SetNickname));
                break;

            case AccountFormKind.CalDav or AccountFormKind.CardDav:
                lines.Add(Field("address", Str.Shell_Accounts_UserName, form.Address, Str.Shell_Accounts_OftenAddress.Text(), form.SetAddress));
                lines.Add(password);
                lines.Add(new SettingsNote(prefix + "/passwordNote", Str.Shell_Accounts_PasswordDesc.Text()));
                lines.Add(Own());
                if (form.OwnServer)
                {
                    lines.Add(Field("server", Str.Shell_Accounts_Server, form.Server, "https://cloud.example.com", form.SetServer));
                    lines.Add(new SettingsNote(prefix + "/serverNote", (form.Kind == AccountFormKind.CalDav ? Str.Shell_Accounts_CalDavServerDesc : Str.Shell_Accounts_CardDavServerDesc).Text()));
                }

                lines.Add(Field("nickname", Str.Connect_Field_Nickname, form.Nickname, Str.Shell_Accounts_Optional.Text(), form.SetNickname));
                break;

            default:
                var module = ProviderCatalog.Of(form.Provider);
                lines.Add(new SettingsNote(prefix + "/explain", Str.SignIn_PasteExplain.Format(("provider", module.Label.Text()))));
                if (module.SignIn.Find<PastedToken>() is { } paste)
                {
                    lines.Add(new SettingsCommand(prefix + "/create", Str.SignIn_CreateToken.Text(), Glyphs.Link, () => host.Open(paste.TokenPage)));
                }

                lines.Add(password);
                break;
        }

        var problem = form.Problem;
        lines.Add(new SettingsSeparator(prefix + "/-try"));
        lines.Add(new SettingsNote(prefix + "/missing", busy || form.Outcome is not null ? string.Empty : problem ?? string.Empty));
        if (form.Kind != AccountFormKind.Token)
        {
            lines.Add(new SettingsCommand(prefix + "/test", Str.SettingsAccounts_Test.Text(), Glyphs.Network, () => host.Try(form, AccountTry.Test))
            {
                Disabled = busy || problem is not null,
            });
            lines.Add(TryNote(form, AccountTry.Test, prefix + "/test/status"));
        }

        lines.Add(new SettingsCommand(prefix + "/add", (form.Kind == AccountFormKind.Token ? Str.SignIn_AddAccount : Str.Common_Add).Text(), Glyphs.Add, () => host.Try(form, AccountTry.Add))
        {
            Disabled = busy || problem is not null,
        });
        lines.Add(TryNote(form, AccountTry.Add, prefix + "/add/status"));
        return lines;
    }

    /// <summary>[S-3555] Under Test or Add: "Checking…" while that try is out, how it went once it is back, nothing otherwise.</summary>
    private static SettingsNote TryNote(AccountForm form, AccountTry what, string key)
        => form.Busy == what
            ? new SettingsNote(key, (form.Kind == AccountFormKind.Token ? Str.SignIn_CheckingToken : Str.Shell_Accounts_Checking).Text()) { Busy = true }
            : form.Outcome is { } outcome && outcome.Tried == what
                ? new SettingsNote(key, outcome.Text) { Critical = outcome.Failed }
                : new SettingsNote(key, string.Empty);

    /// <summary>What stands under a line (its status, or nothing): always there, so its words change in place.</summary>
    private static SettingsNote StatusNote(IAccountsMenuHost host, string row)
        => host.Menu.Status(row) is { } status
            ? new SettingsNote(row + "/status", status.Text) { Busy = status.Busy, Critical = status.Failed }
            : new SettingsNote(row + "/status", string.Empty);

    /// <summary>A kind's glyph on an account's reading.</summary>
    private static string KindGlyph(SyncKind kind) => kind switch
    {
        SyncKind.Mail => Glyphs.Mail,
        SyncKind.Calendar => Glyphs.Calendar,
        SyncKind.Contacts => Glyphs.Contact,
        _ => Glyphs.Task,
    };

    /// <summary>A provider's glyph: Microsoft's cloud, Google's globe, a server's kind.</summary>
    public static string ProviderGlyph(ProviderId provider) => provider switch
    {
        var pid when pid == HostProviders.Microsoft => char.ConvertFromUtf32(0xE753),
        var pid when pid == HostProviders.Google => Glyphs.Globe,
        var pid when pid == HostProviders.Imap => Glyphs.Mail,
        var pid when pid == HostProviders.CalDav => Glyphs.Calendar,
        var pid when pid == HostProviders.CardDav => Glyphs.Contact,
        _ => Glyphs.Info,
    };
}
