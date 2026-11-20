using System.Net.Sockets;
using Themia.Core.Accounts;
using Themia.Core.Localization;

namespace Themia.Core.Mail;

/// <summary>How an IMAP session failed, as the socket and the server showed it, before it is said to the user.</summary>
public enum ImapFailure
{
    /// <summary>The greeting was <c>* BYE</c>: the server turned the connection away.</summary>
    Bye,

    /// <summary>The port answered, but not as an IMAP server.</summary>
    NotImap,

    /// <summary>No answer within <see cref="ImapVerdict.Deadline"/>.</summary>
    TimedOut,

    /// <summary>The TLS handshake failed.</summary>
    Tls,

    /// <summary>The connect failed at the socket (<see cref="SocketError"/>).</summary>
    Socket,

    /// <summary>The guarded connect refused the address ([S-960]).</summary>
    NetRefused,

    /// <summary>The proxy tunnel failed.</summary>
    Proxy,

    /// <summary>The connection dropped mid-session (an untagged BYE included).</summary>
    Dropped,
}

/// <summary>
/// [S-528] [S-553] The one verdict on an IMAP session, for the account's sign-in and the mail sync alike (they ran two
/// clients that judged the same server differently): what each failure means for the account and what is said.
/// Only a refused LOGIN can ask for a sign-in, and only when the server does not say it is busy (<see cref="ImapRules.LoginRefusal"/>).
/// </summary>
public static class ImapVerdict
{
    /// <summary>How long one session (connect, greeting, LOGIN, the job, LOGOUT) may take.</summary>
    public static readonly TimeSpan Deadline = TimeSpan.FromSeconds(45);

    /// <summary>
    /// What <paramref name="failure"/> against <paramref name="host"/>:<paramref name="port"/> means: <paramref name="detail"/> is
    /// the guard's or the proxy's own words, <paramref name="socket"/> the socket's code; a socket that failed because this PC
    /// has no network (<paramref name="networkAvailable"/> false) is offline, not an unreachable server.
    /// </summary>
    public static AccountException Of(
        ImapFailure failure,
        string host,
        int port,
        string? detail = null,
        SocketError socket = SocketError.SocketError,
        bool networkAvailable = true,
        Exception? inner = null)
    {
        ArgumentNullException.ThrowIfNull(host);
        var at = $"{host}:{port}";
        return failure switch
        {
            ImapFailure.Bye => new AccountException(AccountError.ServerError, $"{host} turned the connection away.", 0, inner),
            ImapFailure.NotImap => new AccountException(AccountError.BadResponse, $"{at} did not answer as a mail (IMAP) server.", 0, inner),
            ImapFailure.TimedOut => new AccountException(AccountError.TimedOut, Str.Shell_Net_NoAnswerWithin.Plural((long)Deadline.TotalSeconds, ("host", host)), 0, inner),
            ImapFailure.Tls => new AccountException(AccountError.Unreachable, Str.Shell_Net_ImapTlsFailed.Format(("host", at)), 0, inner),
            ImapFailure.Socket => new AccountException(
                LostNetwork(socket, networkAvailable) ? AccountError.Offline : AccountError.Unreachable,
                Str.Shell_Net_CouldNotConnectCode.Format(("host", at), ("code", socket.ToString())),
                0,
                inner),
            ImapFailure.NetRefused => new AccountException(AccountError.Refused, detail ?? $"{at} may not be reached.", 0, inner),
            ImapFailure.Proxy => new AccountException(AccountError.Unreachable, detail ?? Str.Shell_Net_Dropped.Format(("host", host)), 0, inner),
            _ => new AccountException(AccountError.Unreachable, Str.Shell_Net_Dropped.Format(("host", host)), 0, inner),
        };
    }

    /// <summary>
    /// A refused LOGIN, with the server's <paramref name="text"/>: wrong credentials ask for a sign-in; a server that says it
    /// is unavailable, in use or at its limit is not taking logins now, which a sign-in cannot mend.
    /// </summary>
    public static AccountException LoginRefused(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var error = ImapRules.LoginRefusal(text);
        return new AccountException(error, error == AccountError.SignInRequired
            ? (text.Length > 0 ? Str.Shell_Mail_LoginRefusedReason.Format(("reason", text)) : Str.Shell_Mail_LoginRefused.Text())
            : Str.Shell_Mail_NoLogins.Format(("reason", text)));
    }

    /// <summary>A socket that failed for want of a network on this PC (no route, no name resolution, and no interface up).</summary>
    public static bool LostNetwork(SocketError socket, bool networkAvailable)
        => socket is SocketError.NetworkUnreachable or SocketError.NetworkDown or SocketError.HostNotFound or SocketError.TryAgain
            && !networkAvailable;
}
