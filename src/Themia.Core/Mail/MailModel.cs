using System.Text.Json;
using System.Text.Json.Serialization;
using Themia.Core.Accounts;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;

namespace Themia.Core.Mail;

/// <summary>
/// [S-541] The folders a Mail widget can show. A closed set: the classic app's wire names, mapped to
/// a Graph well-known folder, a Gmail system label and an IMAP SPECIAL-USE attribute in
/// <see cref="MailFolders"/>, so no provider ever sees a free-form string.
/// </summary>
[JsonConverter(typeof(MailFolderConverter))]
public enum MailFolder
{
    Inbox,
    Sent,
    Drafts,
    Deleted,
    Junk,

    /// <summary>Only a move destination; the widget does not list it.</summary>
    Archive,
}

/// <summary>[S-542] Newest first or oldest first, over the same newest N messages.</summary>
[JsonConverter(typeof(MailSortOrderConverter))]
public enum MailSortOrder
{
    NewestFirst,
    OldestFirst,
}

/// <summary>
/// [S-543] The optional lines of the list and compact styles. Sender and time always show; the
/// preview is the List style's own line ([S-6403]), not a field.
/// </summary>
public enum MailListField
{
    Subject,
    Account,
}

/// <summary>[S-543] The columns of the row view, in the order the user put them.</summary>
public enum MailColumn
{
    From,
    Subject,
    Date,
    Preview,
    Account,
}

/// <summary>
/// One message, as every provider is reduced to it.
/// <para>
/// <see cref="RawId"/> is the provider's own handle and only means something to that provider: a
/// Graph id, a Gmail id, or for IMAP <c>folder|uid|uidvalidity</c> (<see cref="ImapMessageId"/>).
/// The account travels separately rather than glued onto the id, so nothing ever has to split a
/// string to find out whose message it is.
/// </para>
/// </summary>
public sealed record MailMessage(
    string AccountId,
    AccountProvider Provider,
    MailFolder Folder,
    string RawId,
    string Subject,
    string FromName,
    string FromAddress,
    DateTimeOffset? Received,
    bool IsRead,
    string Preview,
    string WebLink)
{
    /// <summary>Who it is from, in the words the list shows: the name, else the address.</summary>
    public string Sender => FromName.Length > 0 ? FromName : FromAddress.Length > 0 ? FromAddress : "(unknown sender)";

    /// <summary>What the subject line shows; an empty subject is named rather than left as a gap.</summary>
    public string SubjectLine => string.IsNullOrWhiteSpace(Subject) ? "(no subject)" : Subject;

    /// <summary>Unique across accounts: the same raw id can exist in two mailboxes.</summary>
    public string Key => AccountId + "\n" + RawId;
}

/// <summary>[S-544] What a message's right-click menu can change.</summary>
public enum MailChange
{
    MarkRead,
    MarkUnread,
    Archive,
    Junk,

    /// <summary>Move to Deleted; from Deleted itself, remove for good.</summary>
    Delete,
}

/// <summary>[S-546] How many messages a folder holds and how many are unread: the cheap change probe.</summary>
public readonly record struct MailFolderStatus(long Total, long Unread);

/// <summary>A mail-capable account as the Mail widget sees it: no secrets, plus whether it works.</summary>
/// <param name="Problem">Why it cannot be used until the user signs in again, or null.</param>
public sealed record MailAccount(string Id, AccountProvider Provider, string Label, string Address, string? Problem)
{
    /// <summary>[S-544] Gmail is read through the read-only scope: nothing in it can be changed from here.</summary>
    public bool ReadOnly => Provider == AccountProvider.Google;
}

/// <summary>Writing and reading the closed sets under their stable wire names.</summary>
public static class MailNames
{
    private const string Scope = "mail";

    public static string Wire(this MailFolder folder) => folder switch
    {
        MailFolder.Sent => "sentitems",
        MailFolder.Drafts => "drafts",
        MailFolder.Deleted => "deleteditems",
        MailFolder.Junk => "junkemail",
        MailFolder.Archive => "archive",
        _ => "inbox",
    };

    public static MailFolder? ParseFolder(string? wire) => wire?.Trim().ToLowerInvariant() switch
    {
        "inbox" => MailFolder.Inbox,
        "sent" or "sentitems" => MailFolder.Sent,
        "drafts" => MailFolder.Drafts,
        "deleteditems" or "deleted" or "trash" => MailFolder.Deleted,
        "junkemail" or "junk" or "spam" => MailFolder.Junk,
        "archive" => MailFolder.Archive,
        _ => null,
    };

    /// <summary>What the header and the folder box call a folder.</summary>
    public static string Label(this MailFolder folder) => folder.Label(Strings.Current);

    /// <summary><see cref="Label(MailFolder)"/> in <paramref name="locale"/>.</summary>
    public static string Label(this MailFolder folder, Locale locale) => Strings.Get(locale, folder switch
    {
        MailFolder.Sent => Str.Email_FolderSent,
        MailFolder.Drafts => Str.Email_FolderDrafts,
        MailFolder.Deleted => Str.Email_FolderDeleted,
        MailFolder.Junk => Str.Email_FolderJunk,
        MailFolder.Archive => Str.WidgetsB_Email_FolderArchive,
        _ => Str.Email_FolderInbox,
    });

    public static string Wire(this MailSortOrder order) => order == MailSortOrder.OldestFirst ? "asc" : "desc";

    public static MailSortOrder? ParseSortOrder(string? wire) => wire?.Trim().ToLowerInvariant() switch
    {
        "desc" => MailSortOrder.NewestFirst,
        "asc" => MailSortOrder.OldestFirst,
        _ => null,
    };

    public static string Wire(this MailListField field) => field switch
    {
        MailListField.Account => "account",
        _ => "subject",
    };

    public static MailListField? ParseListField(string? wire) => wire?.Trim().ToLowerInvariant() switch
    {
        "subject" => MailListField.Subject,
        "account" => MailListField.Account,
        _ => null,
    };

    public static string Wire(this MailColumn column) => column switch
    {
        MailColumn.Subject => "subject",
        MailColumn.Date => "date",
        MailColumn.Preview => "preview",
        MailColumn.Account => "account",
        _ => "from",
    };

    public static MailColumn? ParseColumn(string? wire) => wire?.Trim().ToLowerInvariant() switch
    {
        "from" => MailColumn.From,
        "subject" => MailColumn.Subject,
        "date" => MailColumn.Date,
        "preview" => MailColumn.Preview,
        "account" => MailColumn.Account,
        _ => null,
    };

    public static string Label(this MailColumn column) => Strings.Get(Strings.Current, column switch
    {
        MailColumn.Subject => Str.Email_Col_Subject,
        MailColumn.Date => Str.Email_Col_Date,
        MailColumn.Preview => Str.Email_Col_Preview,
        MailColumn.Account => Str.Email_Col_Account,
        _ => Str.Email_Col_From,
    });

    /// <summary>
    /// A value this build does not know, read from a hand-edited or newer state file. Logged and
    /// replaced rather than thrown: a throw inside one widget's config would refuse the whole state
    /// file and blank every desktop.
    /// </summary>
    internal static void Unknown(string what, string? value, string fallback)
        => Log.Warn(Scope, $"unknown {what} '{value}' in a Mail widget's settings; using {fallback}");
}

/// <summary>
/// The JSON side of the closed sets. Lenient on the way in — an unknown name or a wrong token type
/// becomes the default and is logged — and exact on the way out.
/// </summary>
public abstract class LenientWireConverter<T> : JsonConverter<T>
    where T : struct, Enum
{
    protected abstract string What { get; }

    protected abstract T Fallback { get; }

    protected abstract T? Parse(string? wire);

    protected abstract string Write(T value);

    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var wire = reader.GetString();
            if (Parse(wire) is { } parsed)
            {
                return parsed;
            }

            MailNames.Unknown(What, wire, Write(Fallback));
            return Fallback;
        }

        // A number, an object, an array: skip the whole value so the reader stays in step.
        var token = reader.TokenType;
        reader.Skip();
        MailNames.Unknown(What, token.ToString(), Write(Fallback));
        return Fallback;
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStringValue(Write(value));
    }
}

public sealed class MailFolderConverter : LenientWireConverter<MailFolder>
{
    protected override string What => "folder";

    protected override MailFolder Fallback => MailFolder.Inbox;

    protected override MailFolder? Parse(string? wire) => MailNames.ParseFolder(wire);

    protected override string Write(MailFolder value) => value.Wire();
}

public sealed class MailSortOrderConverter : LenientWireConverter<MailSortOrder>
{
    protected override string What => "sort order";

    protected override MailSortOrder Fallback => MailSortOrder.NewestFirst;

    protected override MailSortOrder? Parse(string? wire) => MailNames.ParseSortOrder(wire);

    protected override string Write(MailSortOrder value) => value.Wire();
}

/// <summary>
/// A list of closed-set names. Unknown names are dropped and logged, a non-array becomes null (the
/// default), so neither can refuse the state file.
/// </summary>
public abstract class LenientWireListConverter<T> : JsonConverter<List<T>>
    where T : struct, Enum
{
    protected abstract string What { get; }

    protected abstract T? Parse(string? wire);

    protected abstract string Write(T value);

    public override bool HandleNull => true;

    public override List<T>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType != JsonTokenType.StartArray)
        {
            var token = reader.TokenType;
            reader.Skip();
            MailNames.Unknown(What, token.ToString(), "the default");
            return null;
        }

        var list = new List<T>();

        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (reader.TokenType == JsonTokenType.String && Parse(reader.GetString()) is { } value)
            {
                list.Add(value);
                continue;
            }

            var seen = reader.TokenType == JsonTokenType.String ? reader.GetString() : reader.TokenType.ToString();
            reader.Skip();
            MailNames.Unknown(What, seen, "nothing (dropped)");
        }

        return list;
    }

    public override void Write(Utf8JsonWriter writer, List<T>? value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);

        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartArray();
        foreach (var item in value)
        {
            writer.WriteStringValue(Write(item));
        }

        writer.WriteEndArray();
    }
}

public sealed class MailListFieldsConverter : LenientWireListConverter<MailListField>
{
    protected override string What => "list field";

    protected override MailListField? Parse(string? wire) => MailNames.ParseListField(wire);

    protected override string Write(MailListField value) => value.Wire();
}

public sealed class MailColumnsConverter : LenientWireListConverter<MailColumn>
{
    protected override string What => "column";

    protected override MailColumn? Parse(string? wire) => MailNames.ParseColumn(wire);

    protected override string Write(MailColumn value) => value.Wire();
}

/// <summary>
/// Account ids as a list of strings, tolerant of a wrong shape: a non-array or a non-string item is
/// dropped and logged rather than allowed to refuse the state file.
/// </summary>
public sealed class LenientStringListConverter : JsonConverter<List<string>>
{
    public override bool HandleNull => true;

    public override List<string>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType != JsonTokenType.StartArray)
        {
            var token = reader.TokenType;
            reader.Skip();
            MailNames.Unknown("account list", token.ToString(), "every account");
            return null;
        }

        var list = new List<string>();

        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (reader.TokenType == JsonTokenType.String && reader.GetString() is { } value)
            {
                list.Add(value);
                continue;
            }

            var token = reader.TokenType;
            reader.Skip();
            MailNames.Unknown("account id", token.ToString(), "nothing (dropped)");
        }

        return list;
    }

    public override void Write(Utf8JsonWriter writer, List<string>? value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);

        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartArray();
        foreach (var item in value)
        {
            writer.WriteStringValue(item);
        }

        writer.WriteEndArray();
    }
}
