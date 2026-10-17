using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;
using Themia.Core.Accounts;

namespace Themia.Core.Mail;

/// <summary>A Graph message, as <c>$select=id,subject,from,receivedDateTime,isRead,bodyPreview,webLink</c> returns it.</summary>
public sealed class GraphMailMessage
{
    public string? Id { get; set; }

    public string? Subject { get; set; }

    public GraphRecipient? From { get; set; }

    public string? ReceivedDateTime { get; set; }

    public bool? IsRead { get; set; }

    public string? BodyPreview { get; set; }

    public string? WebLink { get; set; }
}

public sealed class GraphRecipient
{
    public GraphEmailAddress? EmailAddress { get; set; }
}

public sealed class GraphEmailAddress
{
    public string? Name { get; set; }

    public string? Address { get; set; }
}

/// <summary>A Graph mail folder's counts.</summary>
public sealed class GraphMailFolder
{
    public long? UnreadItemCount { get; set; }

    public long? TotalItemCount { get; set; }
}

/// <summary>One entry of Gmail's message list: ids only.</summary>
public sealed class GmailListItem
{
    public string? Id { get; set; }

    public string? ThreadId { get; set; }
}

/// <summary>A Gmail message fetched with <c>format=metadata</c>.</summary>
public sealed class GmailMessage
{
    public string? Id { get; set; }

    public string? ThreadId { get; set; }

    public List<string>? LabelIds { get; set; }

    public string? Snippet { get; set; }

    public string? InternalDate { get; set; }

    public GmailPayload? Payload { get; set; }
}

public sealed class GmailPayload
{
    public List<GmailHeader>? Headers { get; set; }
}

public sealed class GmailHeader
{
    public string? Name { get; set; }

    public string? Value { get; set; }
}

/// <summary>A Gmail label's counts.</summary>
public sealed class GmailLabel
{
    public long? MessagesTotal { get; set; }

    public long? MessagesUnread { get; set; }
}

/// <summary>Source-generated contracts for the mail APIs. Native AOT has no reflection serializer.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(GraphMailMessage))]
[JsonSerializable(typeof(GraphMailFolder))]
[JsonSerializable(typeof(GmailListItem))]
[JsonSerializable(typeof(GmailMessage))]
[JsonSerializable(typeof(GmailLabel))]
public sealed partial class MailWireContext : JsonSerializerContext;

/// <summary>
/// [S-545] Each provider's answer reduced to a <see cref="MailMessage"/>, and the addresses asked.
/// Pure, so the mapping of every field is tested with the providers' own JSON shapes.
/// </summary>
public static class MailWire
{
    private const string Graph = "https://graph.microsoft.com/v1.0/me";
    private const string Gmail = "https://gmail.googleapis.com/gmail/v1/users/me";

    /// <summary>The newest <paramref name="count"/> messages of a Graph folder, newest first.</summary>
    public static Uri GraphMessages(MailFolder folder, int count) => new(string.Create(CultureInfo.InvariantCulture,
        $"{Graph}/mailFolders/{folder.GraphName()}/messages?$top={count}&$select=id,subject,from,receivedDateTime,isRead,bodyPreview,webLink&$orderby=receivedDateTime%20desc"));

    /// <summary>
    /// [S-2687] The start of a folder's delta round: the fields a row shows, at most 50 a page, bounded
    /// to messages received since <paramref name="since"/> when the folder is full (only those can
    /// be among the newest the shelf keeps). Later rounds follow the delta link Graph returns.
    /// </summary>
    public static Uri GraphMessagesDelta(MailFolder folder, DateTimeOffset? since)
    {
        var filter = since is { } at
            ? "&$filter=receivedDateTime%20ge%20" + at.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture)
            : string.Empty;
        return new(string.Create(CultureInfo.InvariantCulture,
            $"{Graph}/mailFolders/{folder.GraphName()}/messages/delta?$select=id,subject,from,receivedDateTime,isRead,bodyPreview,webLink{filter}"));
    }

    public static Uri GraphFolder(MailFolder folder)
        => new($"{Graph}/mailFolders/{folder.GraphName()}?$select=unreadItemCount,totalItemCount");

    public static Uri GraphMessage(string id) => new($"{Graph}/messages/{Uri.EscapeDataString(id)}");

    public static Uri GraphMove(string id) => new($"{Graph}/messages/{Uri.EscapeDataString(id)}/move");

    /// <summary>The ids of a Gmail label's newest messages, newest first.</summary>
    public static Uri GmailList(MailFolder folder, int count) => new(string.Create(CultureInfo.InvariantCulture,
        $"{Gmail}/messages?labelIds={folder.GmailLabel()}&maxResults={count}"));

    /// <summary>[S-2623] The newest unread ids of a folder: the read state of messages already held, without asking for each.</summary>
    public static Uri GmailUnreadList(MailFolder folder, int count) => new(string.Create(CultureInfo.InvariantCulture,
        $"{Gmail}/messages?labelIds={folder.GmailLabel()}&labelIds=UNREAD&maxResults={count}"));

    public static Uri GmailMetadata(string id)
        => new($"{Gmail}/messages/{Uri.EscapeDataString(id)}?format=metadata&metadataHeaders=Subject&metadataHeaders=From&metadataHeaders=Date");

    public static Uri GmailLabelCounts(MailFolder folder) => new($"{Gmail}/labels/{folder.GmailLabel()}");

    /// <summary>A Graph message, or null when it has no id (nothing could act on it or tell it apart).</summary>
    public static MailMessage? FromGraph(GraphMailMessage message, string accountId, MailFolder folder)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (string.IsNullOrEmpty(message.Id))
        {
            return null;
        }

        return new MailMessage(
            accountId,
            HostProviders.Microsoft,
            folder,
            message.Id,
            message.Subject ?? string.Empty,
            message.From?.EmailAddress?.Name?.Trim() ?? string.Empty,
            message.From?.EmailAddress?.Address?.Trim() ?? string.Empty,
            DateTimeOffset.TryParse(message.ReceivedDateTime, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var received)
                ? received.ToUniversalTime()
                : null,
            message.IsRead ?? true,
            MimeText.OneLine(message.BodyPreview ?? string.Empty, MimePreview.MaxLength),
            message.WebLink ?? string.Empty);
    }

    /// <summary>A Gmail message, or null when it has no id.</summary>
    /// <param name="address">The account's address, so the web link opens the right signed-in Gmail.</param>
    public static MailMessage? FromGmail(GmailMessage message, string accountId, string address, MailFolder folder)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (string.IsNullOrEmpty(message.Id))
        {
            return null;
        }

        string subject = string.Empty, from = string.Empty;

        foreach (var header in message.Payload?.Headers ?? [])
        {
            if (string.Equals(header.Name, "Subject", StringComparison.OrdinalIgnoreCase))
            {
                subject = header.Value ?? string.Empty;
            }
            else if (string.Equals(header.Name, "From", StringComparison.OrdinalIgnoreCase))
            {
                from = header.Value ?? string.Empty;
            }
        }

        var (name, fromAddress) = MailHeaders.ParseFrom(from);

        DateTimeOffset? received = long.TryParse(message.InternalDate, NumberStyles.None, CultureInfo.InvariantCulture, out var ms)
            && ms is > 0 and < 253_402_300_800_000
                ? DateTimeOffset.FromUnixTimeMilliseconds(ms)
                : null;

        var unread = message.LabelIds?.Contains("UNREAD", StringComparer.Ordinal) ?? false;

        // "#all/" rather than "#inbox/": the message may be in Sent or Junk. authuser picks the
        // right Google account when several are signed in to the browser, where "u/0" was a guess.
        var link = message.ThreadId is { Length: > 0 } thread
            ? $"https://mail.google.com/mail/?authuser={Uri.EscapeDataString(address)}#all/{Uri.EscapeDataString(thread)}"
            : string.Empty;

        return new MailMessage(
            accountId,
            HostProviders.Google,
            folder,
            message.Id,
            subject,
            name.Trim(),
            fromAddress.Trim(),
            received,
            !unread,
            // Gmail's snippet is HTML-escaped ("it&#39;s").
            MimeText.OneLine(MimeText.DecodeEntities(message.Snippet ?? string.Empty), MimePreview.MaxLength),
            link);
    }

    /// <summary>An IMAP message from its FETCH, or null when the server sent no UID (nothing could act on it).</summary>
    public static MailMessage? FromImap(ImapFetched fetched, string accountId, MailFolder folder, uint? uidValidity)
    {
        ArgumentNullException.ThrowIfNull(fetched);

        if (fetched.Uid is not { } uid)
        {
            return null;
        }

        // Lossless for 8-bit legacy headers: a strict UTF-8 read would blank the whole header block
        // at the first ISO-8859-1 byte (subject, sender and date gone for that message).
        var headers = fetched.Header is { } h ? MimeText.HeaderText(h) : string.Empty;
        var (name, address) = MailHeaders.ParseFrom(MailHeaders.Field(headers, "from"));

        return new MailMessage(
            accountId,
            HostProviders.Imap,
            folder,
            new ImapMessageId(folder, uid, uidValidity).ToString(),
            MailHeaders.Field(headers, "subject"),
            name,
            address,
            MailHeaders.ParseDate(MailHeaders.Field(headers, "date")),
            fetched.Seen,
            fetched.Text is { } text ? MimePreview.Build(headers, text) : string.Empty,
            string.Empty);
    }

    /// <summary>The JSON body of a Graph move.</summary>
    public static string GraphMoveBody(MailFolder destination)
        => $"{{\"destinationId\":\"{destination.GraphName()}\"}}";

    /// <summary>The JSON body of a Graph read/unread patch.</summary>
    public static string GraphReadBody(bool read) => read ? "{\"isRead\":true}" : "{\"isRead\":false}";

    /// <summary>Percent-encode every byte outside RFC 3986's unreserved set.</summary>
    public static string PercentEncode(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var output = new StringBuilder(value.Length * 3);
        foreach (var b in Encoding.UTF8.GetBytes(value))
        {
            if (b is (>= (byte)'A' and <= (byte)'Z') or (>= (byte)'a' and <= (byte)'z') or (>= (byte)'0' and <= (byte)'9')
                or (byte)'-' or (byte)'_' or (byte)'.' or (byte)'~')
            {
                output.Append((char)b);
            }
            else
            {
                output.Append('%').Append(b.ToString("X2", CultureInfo.InvariantCulture));
            }
        }

        return output.ToString();
    }
}
