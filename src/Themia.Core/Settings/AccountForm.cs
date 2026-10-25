using Themia.Core.Accounts;
using Themia.Core.Localization;
using Themia.Sdk;

namespace Themia.Core.Settings;

/// <summary>[S-3554] Which form: a mail server, a calendar or contacts server, or a service's pasted token.</summary>
public enum AccountFormKind
{
    Imap,
    CalDav,
    CardDav,
    Token,
}

/// <summary>[S-3555] What a form's try does: signs in and keeps nothing (Test), or signs in and keeps the account (Add).</summary>
public enum AccountTry
{
    Test,
    Add,
}

/// <summary>[S-3555] How a form's last try went, under the line it was tried from: its words, and whether it failed.</summary>
public sealed record AccountOutcome(AccountTry Tried, string Text, bool Failed);

/// <summary>
/// [S-3554] [S-3555] One of ⚙ › Accounts' forms as typed so far: a draft, never the state or the
/// accounts file. The menu's fields write it (<c>Set…</c>, which say whether anything changed), its
/// lines are read from it each time the menu is drawn, and only an Add that the service has proven
/// (a sign-in to the server) makes an account of it. A mail server is found from the address where
/// Themia knows its provider ([S-522]); "Set the server myself" types it instead. While a try is out
/// the form takes no edit; an edit takes the last try's outcome away.
/// <para>
/// The password is held as characters the form wipes (<see cref="WipePassword"/>) as soon as its
/// submenu closes, the account is added or the menu closes, and is handed out as a string only for
/// the try itself (<see cref="Password"/>); .NET's strings cannot be wiped, as on the page before.
/// <see cref="ToString"/> never names it.
/// </para>
/// </summary>
public sealed class AccountForm
{
    /// <summary>The port a mail server is tried on when none is typed: IMAP over TLS.</summary>
    public const int UsualImapPort = 993;

    private char[] _password = [];

    public AccountForm(AccountFormKind kind, ProviderId provider)
    {
        Kind = kind;
        Provider = provider;
    }

    public AccountFormKind Kind { get; }

    /// <summary>The provider an Add makes an account of.</summary>
    public ProviderId Provider { get; }

    /// <summary>The email address (a mail server's) or the user name (a calendar or contacts server's: often the address).</summary>
    public string Address { get; private set; } = string.Empty;

    /// <summary>[S-3554] "Set the server myself": the server (its port and user name for mail) is typed, not found from the address.</summary>
    public bool OwnServer { get; private set; }

    /// <summary>The server as typed: a mail server's host, or a calendar or contacts server's address.</summary>
    public string Server { get; private set; } = string.Empty;

    /// <summary>A mail server's port as typed; empty for the usual one (993).</summary>
    public string Port { get; private set; } = string.Empty;

    /// <summary>A mail server's user name as typed, where it is not the address.</summary>
    public string UserName { get; private set; } = string.Empty;

    /// <summary>What widgets call the account; empty for its own name.</summary>
    public string Nickname { get; private set; } = string.Empty;

    /// <summary>The try out now, or null: every field is greyed while one is.</summary>
    public AccountTry? Busy { get; private set; }

    /// <summary>How the last try went; null while the form is being filled in.</summary>
    public AccountOutcome? Outcome { get; private set; }

    /// <summary>Whether a password (or token) has been typed; what it is is never shown.</summary>
    public bool HasPassword => _password.Length > 0;

    /// <summary>[S-522] The provider Themia knows the address's mail server of, or null (a mail server's form only).</summary>
    public ImapPreset? ImapPreset => Kind == AccountFormKind.Imap ? ImapPresets.ForAddress(Address) : null;

    /// <summary>[S-522] The calendar or contacts address Themia knows for the user name's provider, or null.</summary>
    public string? DavPreset => Kind switch
    {
        AccountFormKind.CalDav => DavPresets.ForAddress(Address, DavKind.Calendar),
        AccountFormKind.CardDav => DavPresets.ForAddress(Address, DavKind.Contacts),
        _ => null,
    };

    /// <summary>The server a try goes to: the one typed while set by hand, else the one Themia knows; empty for none.</summary>
    public string ServerTried => OwnServer
        ? Server
        : Kind == AccountFormKind.Imap ? ImapPreset?.Host ?? string.Empty : DavPreset ?? string.Empty;

    /// <summary>The port a mail server is tried on: the one typed, else the known provider's, else 993; -1 for words that are no number.</summary>
    public int PortTried => !OwnServer || Port.Length == 0
        ? (OwnServer ? UsualImapPort : ImapPreset?.Port ?? UsualImapPort)
        : Number(Port);

    /// <summary>The user name a mail server is signed in to as: the one typed while set by hand, else the address.</summary>
    public string UserTried => OwnServer && UserName.Length > 0 ? UserName : Address;

    /// <summary>
    /// [S-3555] What the form lacks before it can be tried, in the page's words; null when it can be.
    /// A mail server needs the address and its password, and a server: the one Themia knows, or the
    /// one set by hand (a host name, a port from 1 to 65535). A calendar or contacts server needs the
    /// user name and its password, and its address. A pasted token needs the token.
    /// </summary>
    public string? Problem
    {
        get
        {
            if (Kind == AccountFormKind.Token)
            {
                return HasPassword ? null : Str.SettingsAccounts_PasteToken.Text();
            }

            var at = Address.LastIndexOf('@');
            var named = Kind == AccountFormKind.Imap ? at > 0 && at < Address.Length - 1 : Address.Length > 0;
            if (!named || !HasPassword)
            {
                return (Kind == AccountFormKind.Imap ? Str.SettingsAccounts_EnterAddress : Str.Shell_Accounts_EnterCredentials).Text();
            }

            if (!OwnServer && ServerTried.Length == 0)
            {
                return Str.SettingsAccounts_NoServer.Format(("domain", at >= 0 && at < Address.Length - 1 ? Address[(at + 1)..] : Address));
            }

            if (Kind == AccountFormKind.Imap)
            {
                if (OwnServer && Port.Length > 0 && PortTried < 0)
                {
                    return Str.Shell_Accounts_PortNumber.Text();
                }

                return AccountRules.ImapServerProblem(ServerTried, PortTried);
            }

            return DavUrls.FromTyped(ServerTried) is null ? Str.SettingsAccounts_DavAddress.Text() : null;
        }
    }

    public bool SetAddress(string? typed) => Edit(Address, typed, v => Address = v);

    /// <summary>
    /// [S-3554] Set the server by hand, or let Themia find it again. Switched on, the fields start
    /// from the server Themia knows for the address (where it knows one and nothing was typed there).
    /// </summary>
    public bool SetOwnServer(bool on)
    {
        if (Busy is not null || on == OwnServer)
        {
            return false;
        }

        if (on && Server.Length == 0)
        {
            Server = Kind == AccountFormKind.Imap ? ImapPreset?.Host ?? string.Empty : DavPreset ?? string.Empty;
        }

        OwnServer = on;
        Outcome = null;
        return true;
    }

    public bool SetServer(string? typed) => Edit(Server, typed, v => Server = v);

    public bool SetPort(string? typed) => Edit(Port, typed, v => Port = v);

    public bool SetUserName(string? typed) => Edit(UserName, typed, v => UserName = v);

    public bool SetNickname(string? typed) => Edit(Nickname, typed, v => Nickname = v);

    /// <summary>The password (or token) typed, in place of the one before, which is wiped. Taken as typed: a space is a letter of it.</summary>
    public bool SetPassword(string? typed)
    {
        var next = typed ?? string.Empty;
        if (Busy is not null || next.AsSpan().SequenceEqual(_password))
        {
            return false;
        }

        Array.Clear(_password);
        _password = next.ToCharArray();
        Outcome = null;
        return true;
    }

    /// <summary>The password, for a try alone: a new string each time, which nothing keeps.</summary>
    public string Password() => new(_password);

    /// <summary>[S-3557] The password wiped (its submenu closed): what else was typed stays.</summary>
    public void WipePassword()
    {
        Array.Clear(_password);
        _password = [];
    }

    /// <summary>[S-3557] Everything typed gone, the password wiped; the last try's outcome too, unless <paramref name="keepOutcome"/>.</summary>
    public void Clear(bool keepOutcome = false)
    {
        WipePassword();
        (Address, OwnServer, Server, Port, UserName, Nickname) = (string.Empty, false, string.Empty, string.Empty, string.Empty, string.Empty);
        Busy = null;
        if (!keepOutcome)
        {
            Outcome = null;
        }
    }

    /// <summary>[S-3555] A try begins: false (and nothing begins) while the form lacks something or another try is out.</summary>
    public bool Begin(AccountTry what)
    {
        if (Busy is not null || Problem is not null)
        {
            return false;
        }

        Busy = what;
        Outcome = null;
        return true;
    }

    /// <summary>
    /// [S-3555] A try came back: how it went stands under the line it was tried from. An account
    /// added empties the form (its outcome stays, saying it was added).
    /// </summary>
    public void End(string text, bool failed)
    {
        if (Busy is not { } tried)
        {
            return;
        }

        Busy = null;
        Outcome = new AccountOutcome(tried, text, failed);
        if (tried == AccountTry.Add && !failed)
        {
            Clear(keepOutcome: true);
        }
    }

    /// <summary>[S-3557] A try given up (the menu closed): nothing stands under it.</summary>
    public void Abandon()
    {
        Busy = null;
        Outcome = null;
    }

    /// <summary>Never the password, not even its length.</summary>
    public override string ToString() => $"{Kind} form for {Provider.Value}";

    /// <summary>
    /// A field set to what was typed (trimmed), unless a try is out: the last try's outcome goes with
    /// what it was for. False when nothing changed (the same words, or a try out).
    /// </summary>
    private bool Edit(string now, string? typed, Action<string> set)
    {
        var next = Clean(typed);
        if (Busy is not null || string.Equals(now, next, StringComparison.Ordinal))
        {
            return false;
        }

        set(next);
        Outcome = null;
        return true;
    }

    private static string Clean(string? typed) => typed?.Trim() ?? string.Empty;

    /// <summary>
    /// A port as typed, in any script's digits (an Arabic keyboard types ٩٩٣): its number, or -1 for
    /// words that are no whole number or one too long to be a port.
    /// </summary>
    internal static int Number(string typed)
    {
        var number = 0;
        foreach (var letter in typed)
        {
            if (!char.IsDigit(letter) || number > 99_999)
            {
                return -1;
            }

            number = (number * 10) + (int)char.GetNumericValue(letter);
        }

        return number;
    }
}
