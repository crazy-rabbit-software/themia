using System.Globalization;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;
using Themia.App.Services.Accounts;
using Themia.App.Services.Net;
using Themia.App.Services.Web;
using Themia.Core.Accounts;
using Themia.Core.Localization;
using Themia.Core.Mail;
using Themia.Core.Sync;

namespace Themia.App.Services.Mail;

/// <summary>
/// [S-553] One IMAP session for the Mail widget: connect with implicit TLS (as account sign-in
/// proved it, <see cref="ImapLogin"/>), LOGIN, do one job, LOGOUT. A session per job rather than a
/// held connection: the list is fetched once every few minutes at most, and an idle connection per
/// account is a socket, a TLS context and a server slot held for nothing.
/// <para>
/// Listing opens the mailbox with EXAMINE (read-only) and fetches with BODY.PEEK, so looking at a
/// folder can never mark anything read. Every change selects the folder the message was listed
/// in, checks the mailbox's UIDVALIDITY against the handle's ([S-555]), and acts by UID.
/// </para>
/// <para>
/// The protocol's decisions — framing, parsing, folder resolution — are in Core
/// (<see cref="ImapParser"/>, <see cref="ImapData"/>, <see cref="MailFolders"/>). This file is the
/// socket and the order of commands.
/// </para>
/// </summary>
internal static class ImapMailClient
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(45);

    /// <summary>
    /// [S-2689] The newest <paramref name="count"/> messages of a folder, newest first, and the mark
    /// to keep. With a usable mark of the same incarnation (UIDVALIDITY), only what changed is read:
    /// nothing at all when CONDSTORE's HIGHESTMODSEQ, UIDNEXT and EXISTS stand; else the flags of the
    /// held UIDs (a UID that does not answer was expunged) and the messages that arrived since
    /// UIDNEXT. Everything else (no mark, another incarnation, a removal that left the list short of
    /// what the folder holds) is the newest N in full.
    /// </summary>
    public static Task<(IReadOnlyList<MailMessage> Messages, SyncCursor? Cursor)> SyncAsync(
        ImapCredentials login, string accountId, MailFolder folder, int count, IReadOnlyList<MailMessage> held, SyncCursor? cursor, long? total, CancellationToken cancellation)
        => RunAsync(login, cancellation, async session =>
        {
            var scope = MailShelves.CursorScope(folder, count);
            var now = DateTimeOffset.UtcNow;
            var mailbox = await session.ResolveAsync(folder).ConfigureAwait(false);
            var condstore = await session.HasCapabilityAsync("CONDSTORE").ConfigureAwait(false);
            var opened = await session.OpenAsync("EXAMINE", mailbox, condstore ? " (CONDSTORE)" : string.Empty).ConfigureAwait(false);
            var state = new ImapOpenedState(opened.Exists, opened.UidValidity, opened.UidNext, opened.HighestModSeq);
            var mark = ImapDelta.Mark(state);

            var usable = held.Count > 0 && cursor is not null && SyncCursors.Check(cursor, CursorKind.Imap, scope, now) == CursorCheck.Usable ? cursor : null;
            var plan = ImapDelta.Plan(usable?.Imap, state);

            if (usable is not null && mark is not null && plan == ImapPlan.Unchanged)
            {
                return (held, Marked(SyncCursors.Advance(usable, string.Empty, now), mark));
            }

            if (usable?.Imap is { } since && mark is not null && plan == ImapPlan.Incremental)
            {
                var (changed, removed) = await session.ChangesAsync(accountId, folder, held, since, mark, count, opened.UidValidity).ConfigureAwait(false);
                var (messages, refill) = DeltaMerge.Mail(held, changed, removed, count, DeltaMerge.ByImapUid, total ?? opened.Exists);
                if (!refill)
                {
                    return (messages, Marked(SyncCursors.Advance(usable, string.Empty, now), mark));
                }
            }

            var newest = await session.NewestAsync(accountId, folder, count, opened).ConfigureAwait(false);
            return (newest, mark is null ? null : Marked(SyncCursors.Start(CursorKind.Imap, string.Empty, scope, now), mark));
        });

    private static SyncCursor Marked(SyncCursor cursor, ImapMark mark)
    {
        cursor.Imap = mark;
        return cursor;
    }

    /// <summary>The folder's total and unread counts: the cheap probe ([S-546]).</summary>
    public static Task<MailFolderStatus> StatusAsync(ImapCredentials login, MailFolder folder, CancellationToken cancellation)
        => RunAsync(login, cancellation, async session =>
        {
            var mailbox = await session.ResolveAsync(folder).ConfigureAwait(false);
            var (_, untagged) = await session.CommandAsync($"STATUS {session.Mailbox(mailbox)} (MESSAGES UNSEEN)", required: true).ConfigureAwait(false);

            foreach (var response in untagged)
            {
                if (ImapData.Status(response) is { } status)
                {
                    return status;
                }
            }

            throw new AccountException(AccountError.BadResponse, $"{login.Host} answered STATUS without the counts.");
        });

    /// <summary>Set or clear <c>\Seen</c>.</summary>
    public static Task MarkReadAsync(ImapCredentials login, ImapMessageId id, bool read, CancellationToken cancellation)
        => RunAsync(login, cancellation, async session =>
        {
            await session.SelectForChangeAsync(id, "mark").ConfigureAwait(false);
            await session.CommandAsync(Invariant($"UID STORE {id.Uid} {(read ? '+' : '-')}FLAGS.SILENT (\\Seen)"), required: true).ConfigureAwait(false);
            return true;
        });

    /// <summary>
    /// Move a message by UID with the server-side MOVE (RFC 6851): atomic, and scoped to this one
    /// UID. A server without MOVE refuses and the move fails — strictly safer than COPY plus a bare
    /// EXPUNGE, which purges every other <c>\Deleted</c> message in the folder too.
    /// </summary>
    public static Task MoveAsync(ImapCredentials login, ImapMessageId id, MailFolder destination, CancellationToken cancellation)
        => RunAsync(login, cancellation, async session =>
        {
            var target = await session.ResolveAsync(destination).ConfigureAwait(false);
            await session.SelectForChangeAsync(id, "move").ConfigureAwait(false);
            await session.CommandAsync(Invariant($"UID MOVE {id.Uid} {session.Mailbox(target)}"), required: true).ConfigureAwait(false);
            return true;
        });

    /// <summary>
    /// Remove a message for good: <c>\Deleted</c> then UID EXPUNGE (UIDPLUS, RFC 4315), which removes
    /// only this UID. Used only for a message already in Deleted; everywhere else delete is a move
    /// there, which the user can undo.
    /// </summary>
    public static Task ExpungeAsync(ImapCredentials login, ImapMessageId id, CancellationToken cancellation)
        => RunAsync(login, cancellation, async session =>
        {
            await session.SelectForChangeAsync(id, "delete").ConfigureAwait(false);
            await session.CommandAsync(Invariant($"UID STORE {id.Uid} +FLAGS.SILENT (\\Deleted)"), required: true).ConfigureAwait(false);
            await session.CommandAsync(Invariant($"UID EXPUNGE {id.Uid}"), required: true).ConfigureAwait(false);
            return true;
        });

    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);

    /// <summary>Connect, greet, log in, run the job, log out — mapping every failure to an <see cref="AccountException"/>.</summary>
    private static async Task<T> RunAsync<T>(ImapCredentials login, CancellationToken cancellation, Func<Session, Task<T>> job)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(Deadline);
        var token = deadline.Token;

        try
        {
            // [S-960] The one guarded connect path: the resolved address is checked at connect (a NAS is
            // reached because the user typed its address, [S-3843]), a proxy is tunnelled through.
            await using var tls = await GuardedTcp.OpenTlsAsync(login.Host, login.Port, Themia.Core.Net.LocalGrants.ForTypedHost(login.Host), token).ConfigureAwait(false);

            var session = new Session(tls, login.Host, token);
            await session.GreetAndLoginAsync(login).ConfigureAwait(false);

            var result = await job(session).ConfigureAwait(false);

            try
            {
                await session.CommandAsync("LOGOUT", required: false).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or AccountException)
            {
                // ignore: the job is done; a LOGOUT the server hung up on changes nothing about it.
            }

            return result;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException ex)
        {
            throw new AccountException(AccountError.TimedOut, $"{login.Host} did not answer within {Deadline.TotalSeconds:F0} seconds.", 0, ex);
        }
        catch (AuthenticationException ex)
        {
            throw new AccountException(AccountError.Unreachable, Str.Shell_Net_TlsFailed.Format(("host", $"{login.Host}:{login.Port}")), 0, ex);
        }
        catch (SocketException ex)
        {
            var error = ex.SocketErrorCode is SocketError.NetworkUnreachable or SocketError.NetworkDown or SocketError.HostNotFound or SocketError.TryAgain
                && !System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable()
                    ? AccountError.Offline
                    : AccountError.Unreachable;
            throw new AccountException(error, Str.Shell_Net_CouldNotConnectCode.Format(("host", $"{login.Host}:{login.Port}"), ("code", ex.SocketErrorCode.ToString())), 0, ex);
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

    /// <summary>A mailbox opened with SELECT or EXAMINE: how many messages, which incarnation, the next UID and (CONDSTORE) the mod-sequence.</summary>
    private readonly record struct Opened(long Exists, uint? UidValidity, uint? UidNext, ulong? HighestModSeq);

    /// <summary>Tagged commands and buffered response reading over the TLS stream.</summary>
    private sealed class Session(Stream stream, string host, CancellationToken cancellation)
    {
        /// <summary>A response bigger than this is not one this client asked for.</summary>
        private const int MaxResponse = 16 * 1024 * 1024;

        private readonly byte[] _buffer = new byte[16 * 1024];
        private int _start;
        private int _end;
        private int _tag;
        private IReadOnlyList<ImapMailbox>? _mailboxes;
        private List<ImapResponse>? _capabilities;

        public async Task GreetAndLoginAsync(ImapCredentials login)
        {
            var greeting = await ReadResponseAsync().ConfigureAwait(false);
            var first = Encoding.ASCII.GetString(greeting, 0, Math.Min(greeting.Length, 256));

            switch (ImapProtocol.ParseGreeting(first))
            {
                case ImapGreeting.Bye:
                    throw new AccountException(AccountError.ServerError, $"{host} turned the connection away.");
                case ImapGreeting.NotImap:
                    throw new AccountException(AccountError.BadResponse, $"{host} did not answer as a mail (IMAP) server.");
                case ImapGreeting.PreAuth:
                    return;
            }

            var tag = NextTag();
            await WriteAsync($"{tag} LOGIN").ConfigureAwait(false);

            foreach (var value in new[] { login.Username, login.Password })
            {
                if (ImapProtocol.Quote(value) is { } quoted)
                {
                    await WriteAsync(" " + quoted).ConfigureAwait(false);
                    continue;
                }

                // A literal: announce the octets, wait for the go-ahead, then send them verbatim.
                var bytes = Encoding.UTF8.GetBytes(value);
                await WriteAsync(" " + ImapProtocol.LiteralPrefix(bytes.Length) + "\r\n").ConfigureAwait(false);

                var reply = await ReadResponseAsync().ConfigureAwait(false);
                if (reply.Length == 0 || reply[0] != (byte)'+')
                {
                    throw Refused(ImapParser.Parse(reply));
                }

                await stream.WriteAsync(bytes, cancellation).ConfigureAwait(false);
            }

            await WriteAsync("\r\n").ConfigureAwait(false);

            var (tagged, _) = await ReadUntilTaggedAsync(tag).ConfigureAwait(false);
            if (Status(tagged) != ImapStatus.Ok)
            {
                throw Refused(tagged);
            }
        }

        private static AccountException Refused(ImapResponse tagged)
        {
            var text = string.Join(' ', tagged.Values.Select(v => v.Text).OfType<string>().Skip(1));
            var error = ImapRules.LoginRefusal(text);
            return new AccountException(error, error == AccountError.SignInRequired
                ? (text.Length > 0 ? Str.Shell_Mail_LoginRefusedReason.Format(("reason", text)) : Str.Shell_Mail_LoginRefused.Text())
                : Str.Shell_Mail_NoLogins.Format(("reason", text)));
        }

        /// <summary>The mailbox for a folder: INBOX directly, anything else through one LIST, cached for the session.</summary>
        public async Task<string> ResolveAsync(MailFolder folder)
        {
            if (folder == MailFolder.Inbox)
            {
                return "INBOX";
            }

            if (_mailboxes is null)
            {
                var (_, untagged) = await CommandAsync("LIST \"\" \"*\"", required: true).ConfigureAwait(false);
                _mailboxes = [.. untagged.Select(ImapData.List).OfType<ImapMailbox>()];
            }

            return folder.ResolveImap(_mailboxes);
        }

        public async Task<Opened> OpenAsync(string verb, string mailbox, string parameters = "")
        {
            var (_, untagged) = await CommandAsync($"{verb} {Mailbox(mailbox)}{parameters}", required: true).ConfigureAwait(false);

            long exists = 0;
            uint? validity = null;
            uint? next = null;
            ulong? modseq = null;

            foreach (var response in untagged)
            {
                exists = ImapData.Exists(response) ?? exists;
                validity = ImapData.UidValidity(response) ?? validity;
                next = ImapDelta.UidNext(response) ?? next;
                modseq = ImapDelta.HighestModSeq(response) ?? modseq;
            }

            return new Opened(exists, validity, next, modseq);
        }

        /// <summary>Whether the server, after login, names a capability (RFC 3501 CAPABILITY, asked once per session).</summary>
        public async Task<bool> HasCapabilityAsync(string capability)
        {
            if (_capabilities is null)
            {
                var (_, untagged) = await CommandAsync("CAPABILITY", required: false).ConfigureAwait(false);
                _capabilities = untagged;
            }

            return _capabilities.Any(r => ImapDelta.HasCapability(r, capability));
        }

        /// <summary>The newest <paramref name="count"/> messages by sequence (arrival), newest first.</summary>
        public async Task<IReadOnlyList<MailMessage>> NewestAsync(string accountId, MailFolder folder, int count, Opened opened)
        {
            if (ImapRules.RecentRange(opened.Exists, count) is not { } range)
            {
                return [];
            }

            var (_, untagged) = await CommandAsync($"FETCH {range} {ImapRules.FetchItems}", required: true).ConfigureAwait(false);
            var messages = Messages(untagged, accountId, folder, opened.UidValidity);

            // Sequence order is arrival order; newest first is the reverse.
            messages.Reverse();
            return messages;
        }

        /// <summary>
        /// [S-2689] The held messages with their flags as they are now (a UID that does not answer was
        /// expunged), and the messages that arrived since the mark's UIDNEXT, at most <paramref name="count"/>.
        /// </summary>
        public async Task<(IReadOnlyList<MailMessage> Changed, IReadOnlyList<string> Removed)> ChangesAsync(
            string accountId, MailFolder folder, IReadOnlyList<MailMessage> held, ImapMark since, ImapMark now, int count, uint? validity)
        {
            var byUid = new Dictionary<uint, MailMessage>();
            foreach (var message in held)
            {
                if (ImapMessageId.Parse(message.RawId) is { } id)
                {
                    byUid.TryAdd(id.Uid, message);
                }
            }

            var changed = new List<MailMessage>();
            var present = new HashSet<uint>();

            if (ImapDelta.UidSet(byUid.Keys) is { } set)
            {
                var (_, flags) = await CommandAsync($"UID FETCH {set} (UID FLAGS)", required: true).ConfigureAwait(false);
                foreach (var response in flags)
                {
                    if (ImapData.Fetch(response) is { Uid: { } uid } fetched && byUid.TryGetValue(uid, out var message) && present.Add(uid))
                    {
                        changed.Add(message with { IsRead = fetched.Seen });
                    }
                }
            }

            var removed = byUid.Where(pair => !present.Contains(pair.Key)).Select(pair => pair.Value.RawId).ToList();

            if (now.UidNext > since.UidNext)
            {
                var (_, search) = await CommandAsync(Invariant($"UID SEARCH UID {since.UidNext}:*"), required: true).ConfigureAwait(false);
                var arrived = ImapDelta.Arrived(search.SelectMany(ImapDelta.Search), since.UidNext, count);

                if (ImapDelta.UidSet(arrived) is { } fresh)
                {
                    var (_, untagged) = await CommandAsync($"UID FETCH {fresh} {ImapRules.FetchItems}", required: true).ConfigureAwait(false);
                    changed.AddRange(Messages(untagged, accountId, folder, validity));
                }
            }

            return (changed, removed);
        }

        private static List<MailMessage> Messages(List<ImapResponse> untagged, string accountId, MailFolder folder, uint? validity)
        {
            var messages = new List<MailMessage>();
            foreach (var response in untagged)
            {
                if (ImapData.Fetch(response) is { } fetched && MailWire.FromImap(fetched, accountId, folder, validity) is { } message)
                {
                    messages.Add(message);
                }
            }

            return messages;
        }

        /// <summary>SELECT the folder a message was listed in and refuse if it is a different incarnation.</summary>
        public async Task SelectForChangeAsync(ImapMessageId id, string action)
        {
            var mailbox = await ResolveAsync(id.Folder).ConfigureAwait(false);
            var opened = await OpenAsync("SELECT", mailbox).ConfigureAwait(false);

            if (!id.StillValidIn(opened.UidValidity))
            {
                throw new AccountException(AccountError.Refused,
                    Str.Shell_Mail_FolderChanged.Format(("folder", id.Folder.Label())));
            }
        }

        /// <summary>A mailbox name as an argument. Server-listed names are 7-bit (modified UTF-7), so a quoted string fits.</summary>
        public string Mailbox(string name)
            => ImapRules.QuoteMailbox(name) ?? throw new AccountException(AccountError.BadResponse, $"{host} listed a mailbox name that cannot be sent back to it.");

        /// <summary>Send a command and collect every untagged response until its tagged answer.</summary>
        public async Task<(ImapResponse Tagged, List<ImapResponse> Untagged)> CommandAsync(string command, bool required)
        {
            var tag = NextTag();
            await WriteAsync($"{tag} {command}\r\n").ConfigureAwait(false);
            var result = await ReadUntilTaggedAsync(tag).ConfigureAwait(false);

            if (required && Status(result.Tagged) != ImapStatus.Ok)
            {
                var verb = command.Split(' ', 2)[0];
                var text = string.Join(' ', result.Tagged.Values.Select(v => v.Text).OfType<string>().Skip(1));
                throw new AccountException(AccountError.BadResponse, $"{host} refused {verb}: {text}");
            }

            return result;
        }

        private async Task<(ImapResponse Tagged, List<ImapResponse> Untagged)> ReadUntilTaggedAsync(string tag)
        {
            var untagged = new List<ImapResponse>();

            while (true)
            {
                var bytes = await ReadResponseAsync().ConfigureAwait(false);
                if (bytes.Length == 0)
                {
                    continue;
                }

                var response = ImapParser.Parse(bytes);

                if (response.Tag == tag)
                {
                    return (response, untagged);
                }

                if (response.Tag == "*" && response.Word(0) == "BYE")
                {
                    throw new IOException("the server closed the session");
                }

                if (response.Tag == "*")
                {
                    untagged.Add(response);
                }
            }
        }

        private static ImapStatus Status(ImapResponse tagged) => tagged.Word(0) switch
        {
            "OK" => ImapStatus.Ok,
            "NO" => ImapStatus.No,
            _ => ImapStatus.Bad,
        };

        private string NextTag() => "t" + (++_tag).ToString(CultureInfo.InvariantCulture);

        private async Task WriteAsync(string text)
        {
            await stream.WriteAsync(Encoding.UTF8.GetBytes(text), cancellation).ConfigureAwait(false);
            await stream.FlushAsync(cancellation).ConfigureAwait(false);
        }

        /// <summary>
        /// One complete response: a line, and when it announces a literal, exactly that many octets
        /// and the rest of the line after them, repeated until a line ends without one.
        /// </summary>
        private async Task<byte[]> ReadResponseAsync()
        {
            var response = new List<byte>(256);

            while (true)
            {
                var line = await ReadLineAsync().ConfigureAwait(false);
                response.AddRange(line);
                response.Add((byte)'\r');
                response.Add((byte)'\n');

                if (ImapParser.LiteralLength(line) is not { } length)
                {
                    return [.. response];
                }

                if (length > ImapParser.MaxLiteral || response.Count + length > MaxResponse)
                {
                    throw new AccountException(AccountError.BadResponse, $"{host} sent a {length}-byte literal; refusing to hold it.");
                }

                await ReadExactlyAsync(response, length).ConfigureAwait(false);
            }
        }

        private async Task<byte[]> ReadLineAsync()
        {
            var line = new List<byte>(128);

            while (true)
            {
                if (_start == _end && !await FillAsync().ConfigureAwait(false))
                {
                    throw new IOException("the server closed the connection");
                }

                var span = _buffer.AsSpan(_start, _end - _start);
                var newline = span.IndexOf((byte)'\n');

                if (newline < 0)
                {
                    line.AddRange(span);
                    _start = _end;
                }
                else
                {
                    line.AddRange(span[..newline]);
                    _start += newline + 1;
                    break;
                }

                if (line.Count > 1024 * 1024)
                {
                    throw new AccountException(AccountError.BadResponse, $"{host} sent a line longer than a megabyte.");
                }
            }

            if (line.Count > 0 && line[^1] == (byte)'\r')
            {
                line.RemoveAt(line.Count - 1);
            }

            return [.. line];
        }

        private async Task ReadExactlyAsync(List<byte> into, int count)
        {
            while (count > 0)
            {
                if (_start == _end && !await FillAsync().ConfigureAwait(false))
                {
                    throw new IOException("the server closed the connection mid-literal");
                }

                var take = Math.Min(count, _end - _start);
                into.AddRange(_buffer.AsSpan(_start, take));
                _start += take;
                count -= take;
            }
        }

        private async Task<bool> FillAsync()
        {
            _start = 0;
            _end = await stream.ReadAsync(_buffer, cancellation).ConfigureAwait(false);
            return _end > 0;
        }
    }
}
