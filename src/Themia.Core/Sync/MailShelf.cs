using Themia.Core.Accounts;
using Themia.Core.Mail;
using Themia.Sdk;

namespace Themia.Core.Sync;

/// <summary>[S-2620] One mail account's shelf: the newest messages of each folder a widget reads, and the queued changes.</summary>
public sealed class MailShelf : ISyncShelf
{
    public const int Version = 1;

    public int SchemaVersion { get; set; } = Version;

    public string Owner { get; set; } = string.Empty;

    public List<MailFolderShelf> Folders { get; set; } = [];

    public List<PendingWrite> Pending { get; set; } = [];
}

/// <summary>One folder as last synced.</summary>
public sealed class MailFolderShelf
{
    public MailFolder Folder { get; set; }

    /// <summary>When the list was last fetched.</summary>
    public DateTimeOffset At { get; set; }

    /// <summary>How many of the newest messages the fetch asked for.</summary>
    public int Count { get; set; }

    public long? Total { get; set; }

    public long? Unread { get; set; }

    public List<StoredMail> Messages { get; set; } = [];

    /// <summary>[S-2680] Where the provider's changes to this folder continue from; null when it offers none.</summary>
    public SyncCursor? Cursor { get; set; }
}

/// <summary>A message as stored: what a row shows and what a change addresses. Never a body.</summary>
public sealed class StoredMail
{
    public string RawId { get; set; } = string.Empty;

    public string Subject { get; set; } = string.Empty;

    public string FromName { get; set; } = string.Empty;

    public string FromAddress { get; set; } = string.Empty;

    public DateTimeOffset? Received { get; set; }

    public bool IsRead { get; set; }

    public string Preview { get; set; } = string.Empty;

    public string WebLink { get; set; } = string.Empty;
}

/// <summary>[S-2620]..[S-2623] The mail shelf's bounds, validation and overlay.</summary>
public static class MailShelves
{
    /// <summary>[S-2620] The most messages kept per folder: the most any Mail widget shows ([S-540]).</summary>

    private const int MaxSubject = 1000;
    private const int MaxName = 500;
    private const int MaxPreview = 400;
    private const int MaxLink = 2048;

    /// <summary>The folders a widget lists; Archive is only ever a destination.</summary>
    public static bool Listed(MailFolder folder) => folder is not MailFolder.Archive;

    public static StoredMail Store(MailMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        return new StoredMail
        {
            RawId = message.RawId,
            Subject = Cut(message.Subject, MaxSubject),
            FromName = Cut(message.FromName, MaxName),
            FromAddress = Cut(message.FromAddress, MaxName),
            Received = message.Received,
            IsRead = message.IsRead,
            Preview = Cut(message.Preview, MaxPreview),
            WebLink = message.WebLink.Length > MaxLink ? string.Empty : message.WebLink,
        };
    }

    public static MailMessage Restore(StoredMail stored, string accountId, ProviderId provider, MailFolder folder)
    {
        ArgumentNullException.ThrowIfNull(stored);
        return new MailMessage(
            accountId,
            provider,
            folder,
            stored.RawId,
            stored.Subject,
            stored.FromName,
            stored.FromAddress,
            stored.Received,
            stored.IsRead,
            stored.Preview,
            stored.WebLink);
    }

    /// <summary>
    /// [S-2621] The trust boundary for a mail shelf that opened: folders repeat or are not listed →
    /// the first listed one kept; messages without an id or repeated → dropped; strings cut to their
    /// bounds; at most <see cref="MailLimits.PerFolder"/> a folder; counts never negative; the queue through
    /// <see cref="WriteQueue.Validate"/> with only mail changes kept.
    /// </summary>
    public static MailShelf Validate(MailShelf shelf, ref int dropped)
    {
        ArgumentNullException.ThrowIfNull(shelf);

        var folders = new List<MailFolderShelf>();
        var seen = new HashSet<MailFolder>();

        foreach (var folder in shelf.Folders ?? [])
        {
            if (folder is null || !Enum.IsDefined(folder.Folder) || !Listed(folder.Folder) || !seen.Add(folder.Folder))
            {
                dropped++;
                continue;
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            var messages = new List<StoredMail>();

            foreach (var message in folder.Messages ?? [])
            {
                if (message is null || string.IsNullOrEmpty(message.RawId) || !ids.Add(message.RawId) || messages.Count >= MailLimits.PerFolder)
                {
                    dropped++;
                    continue;
                }

                messages.Add(new StoredMail
                {
                    RawId = message.RawId,
                    Subject = Cut(message.Subject, MaxSubject),
                    FromName = Cut(message.FromName, MaxName),
                    FromAddress = Cut(message.FromAddress, MaxName),
                    Received = message.Received,
                    IsRead = message.IsRead,
                    Preview = Cut(message.Preview, MaxPreview),
                    WebLink = message.WebLink is { Length: <= MaxLink } link ? link : string.Empty,
                });
            }

            folders.Add(new MailFolderShelf
            {
                Folder = folder.Folder,
                At = folder.At,
                Count = Math.Clamp(folder.Count, 0, MailLimits.PerFolder),
                Total = folder.Total is >= 0 ? folder.Total : null,
                Unread = folder.Unread is >= 0 ? folder.Unread : null,
                Messages = messages,
                Cursor = SyncCursors.Validate(folder.Cursor, ref dropped),
            });
        }

        var pending = WriteQueue.Validate(shelf.Pending, ref dropped);
        var mailOnly = pending.Where(w => IsMail(w.Kind) && MailNames.ParseFolder(w.Container) is not null).ToList();
        dropped += pending.Count - mailOnly.Count;

        return new MailShelf { Owner = shelf.Owner, Folders = folders, Pending = mailOnly };
    }

    /// <summary>[S-2687] What a folder's cursor was issued for: the folder, and how many of its newest messages are held.</summary>
    public static string CursorScope(MailFolder folder, int count) => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{folder.Wire()}|{count}");

    public static bool IsMail(PendingKind kind)
        => kind is PendingKind.MailMarkRead or PendingKind.MailMarkUnread or PendingKind.MailArchive or PendingKind.MailJunk or PendingKind.MailDelete;

    public static PendingKind Kind(MailChange change) => change switch
    {
        MailChange.MarkRead => PendingKind.MailMarkRead,
        MailChange.MarkUnread => PendingKind.MailMarkUnread,
        MailChange.Archive => PendingKind.MailArchive,
        MailChange.Junk => PendingKind.MailJunk,
        _ => PendingKind.MailDelete,
    };

    public static MailChange Change(PendingKind kind) => kind switch
    {
        PendingKind.MailMarkRead => MailChange.MarkRead,
        PendingKind.MailMarkUnread => MailChange.MarkUnread,
        PendingKind.MailArchive => MailChange.Archive,
        PendingKind.MailJunk => MailChange.Junk,
        _ => MailChange.Delete,
    };

    /// <summary>
    /// [S-2622] What a folder shows: what the provider last said with every queued change on top, in
    /// order, so a sync landing while a change waits (offline, or still on the wire) cannot flash
    /// the old state. A read flag sets the flag; a move takes the message out of the folder it was in.
    /// </summary>
    public static IReadOnlyList<MailMessage> Overlay(IReadOnlyList<MailMessage> messages, IReadOnlyList<PendingWrite> pending, MailFolder folder)
    {
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(pending);

        if (pending.Count == 0)
        {
            return messages;
        }

        var wire = folder.Wire();
        var list = messages.ToList();

        foreach (var write in pending)
        {
            if (!string.Equals(write.Container, wire, StringComparison.Ordinal))
            {
                continue;
            }

            var index = list.FindIndex(m => m.RawId == write.Target);
            if (index < 0)
            {
                continue;
            }

            switch (write.Kind)
            {
                case PendingKind.MailMarkRead:
                    list[index] = list[index] with { IsRead = true };
                    break;
                case PendingKind.MailMarkUnread:
                    list[index] = list[index] with { IsRead = false };
                    break;
                default:
                    list.RemoveAt(index);
                    break;
            }
        }

        return list;
    }

    private static string Cut(string? text, int max)
        => text is null ? string.Empty : text.Length <= max ? text : text[..max];
}
