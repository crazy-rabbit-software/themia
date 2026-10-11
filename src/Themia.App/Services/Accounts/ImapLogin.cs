using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;
using Themia.App.Services.Net;
using Themia.App.Services.Web;
using Themia.Core.Accounts;
using Themia.Core.Localization;

namespace Themia.App.Services.Accounts;

/// <summary>Where and as whom an IMAP account signs in. Holds a plaintext password: keep it short-lived.</summary>
internal sealed record ImapCredentials(string Host, int Port, string Username, string Password)
{
    /// <summary>Never prints the password.</summary>
    public override string ToString() => $"{Username}@{Host}:{Port}";
}

/// <summary>
/// [S-528] Proving an IMAP login before the account is saved: connect with TLS (implicit, as the
/// classic app always did — port 993), read the greeting, LOGIN, LOGOUT. Nothing is selected and
/// nothing is read, so a check can never mark a message read or move one.
/// <para>
/// The user name and password go as IMAP quoted strings, or as literals when they hold a character
/// a quoted string cannot carry, so a password containing a quote or a non-ASCII letter logs in
/// rather than producing a syntax error that reads like a wrong password.
/// </para>
/// </summary>
internal static class ImapLogin
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(30);

    public static async Task VerifyAsync(ImapCredentials login, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(login);

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(Deadline);
        var token = deadline.Token;

        try
        {
            // [S-960] The one guarded connect path: the resolved address is checked at connect (a NAS is
            // reached because the user typed its address, [S-3843]), a proxy is tunnelled through.
            await using var tls = await GuardedTcp.OpenTlsAsync(login.Host, login.Port, Themia.Core.Net.LocalGrants.ForTypedHost(login.Host), token).ConfigureAwait(false);

            var session = new Session(tls);

            var greeting = ImapProtocol.ParseGreeting(await session.ReadLineAsync(token).ConfigureAwait(false));
            switch (greeting)
            {
                case ImapGreeting.Bye:
                    throw new AccountException(AccountError.Refused, $"{login.Host} turned the connection away.");
                case ImapGreeting.NotImap:
                    throw new AccountException(AccountError.BadResponse, $"{login.Host}:{login.Port} did not answer as a mail (IMAP) server.");
                case ImapGreeting.PreAuth:
                    return;
            }

            var answer = await session.LoginAsync("a1", login.Username, login.Password, token).ConfigureAwait(false);
            if (answer.Status != ImapStatus.Ok)
            {
                throw new AccountException(AccountError.SignInRequired,
                    answer.Text.Length > 0
                        ? Str.Shell_Accounts_ServerRefusedReason.Format(("reason", answer.Text))
                        : Str.Shell_Accounts_ServerRefused.Text());
            }

            // ignore: a failed LOGOUT after a good LOGIN changes nothing about the verdict.
            _ = await session.CommandAsync("a2", "LOGOUT", token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException ex)
        {
            throw new AccountException(AccountError.TimedOut, Str.Shell_Net_NoAnswerWithin.Plural((long)Deadline.TotalSeconds, ("host", login.Host)), 0, ex);
        }
        catch (AuthenticationException ex)
        {
            throw new AccountException(AccountError.Unreachable,
                Str.Shell_Net_ImapTlsFailed.Format(("host", $"{login.Host}:{login.Port}")), 0, ex);
        }
        catch (SocketException ex)
        {
            throw new AccountException(AccountError.Unreachable, Str.Shell_Net_CouldNotConnectCode.Format(("host", $"{login.Host}:{login.Port}"), ("code", ex.SocketErrorCode.ToString())), 0, ex);
        }
        catch (NetRefusedException ex)
        {
            throw new AccountException(AccountError.Refused, ex.Message, 0, ex);
        }
        catch (ProxyTunnelException ex)
        {
            throw new AccountException(AccountError.Unreachable, ex.Message, 0, ex);
        }
        catch (IOException ex)
        {
            throw new AccountException(AccountError.Unreachable, Str.Shell_Net_Dropped.Format(("host", login.Host)), 0, ex);
        }
    }

    /// <summary>Line-oriented reading and writing over the TLS stream.</summary>
    private sealed class Session(Stream stream)
    {
        private readonly byte[] _one = new byte[1];

        public async Task<string?> ReadLineAsync(CancellationToken cancellation)
        {
            var bytes = new List<byte>(128);

            while (bytes.Count < 64 * 1024)
            {
                var read = await stream.ReadAsync(_one, cancellation).ConfigureAwait(false);
                if (read == 0)
                {
                    return bytes.Count == 0 ? null : Encoding.UTF8.GetString([.. bytes]);
                }

                if (_one[0] == (byte)'\n')
                {
                    break;
                }

                if (_one[0] != (byte)'\r')
                {
                    bytes.Add(_one[0]);
                }
            }

            return Encoding.UTF8.GetString([.. bytes]);
        }

        public async Task<ImapTagged> CommandAsync(string tag, string command, CancellationToken cancellation)
        {
            await WriteAsync($"{tag} {command}\r\n", cancellation).ConfigureAwait(false);
            return await ReadTaggedAsync(tag, cancellation).ConfigureAwait(false);
        }

        public async Task<ImapTagged> LoginAsync(string tag, string user, string password, CancellationToken cancellation)
        {
            await WriteAsync($"{tag} LOGIN", cancellation).ConfigureAwait(false);

            foreach (var value in new[] { user, password })
            {
                if (ImapProtocol.Quote(value) is { } quoted)
                {
                    await WriteAsync(" " + quoted, cancellation).ConfigureAwait(false);
                    continue;
                }

                // A literal: announce the byte count, wait for the server's go-ahead, then send the
                // bytes verbatim.
                var bytes = Encoding.UTF8.GetBytes(value);
                await WriteAsync(" " + ImapProtocol.LiteralPrefix(bytes.Length) + "\r\n", cancellation).ConfigureAwait(false);

                var line = await ReadLineAsync(cancellation).ConfigureAwait(false);
                if (!ImapProtocol.IsContinuation(line))
                {
                    return ImapProtocol.ParseTagged(line, tag) ?? new ImapTagged(ImapStatus.Bad, "the server did not accept the login");
                }

                await stream.WriteAsync(bytes, cancellation).ConfigureAwait(false);
            }

            await WriteAsync("\r\n", cancellation).ConfigureAwait(false);
            return await ReadTaggedAsync(tag, cancellation).ConfigureAwait(false);
        }

        private async Task<ImapTagged> ReadTaggedAsync(string tag, CancellationToken cancellation)
        {
            while (true)
            {
                var line = await ReadLineAsync(cancellation).ConfigureAwait(false)
                    ?? throw new IOException("the server closed the connection");

                if (ImapProtocol.ParseTagged(line, tag) is { } tagged)
                {
                    return tagged;
                }
            }
        }

        private async Task WriteAsync(string text, CancellationToken cancellation)
        {
            await stream.WriteAsync(Encoding.UTF8.GetBytes(text), cancellation).ConfigureAwait(false);
            await stream.FlushAsync(cancellation).ConfigureAwait(false);
        }
    }
}
