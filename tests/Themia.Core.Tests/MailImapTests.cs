using System.Text;
using Themia.Core.Accounts;
using Themia.Core.Mail;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// IMAP framing, response parsing, folder resolution and message handles. The response bytes are
/// shaped like real servers' (Dovecot, Gmail, Outlook) output.
/// </summary>
public class MailImapTests
{
    private static ImapResponse Parse(string response) => ImapParser.Parse(Encoding.Latin1.GetBytes(response));

    [Theory]
    [InlineData("* 1 FETCH (RFC822.HEADER {342}", 342)]
    [InlineData("* 1 FETCH (BODY[TEXT]<0> {0}", 0)]
    [InlineData("a1 LOGIN {5+}", 5)]
    [InlineData("* OK ready", null)]
    [InlineData("* OK {abc}", null)]
    [InlineData("}", null)]
    [InlineData("* x {1234567890}", null)]
    public void S553_literal_length_is_read_from_the_line_end(string line, int? expected)
        => Assert.Equal(expected, ImapParser.LiteralLength(Encoding.ASCII.GetBytes(line)));

    [Fact]
    public void S553_fetch_with_literals_yields_exact_octets()
    {
        const string header = "From: Ann <ann@x.com>\r\nSubject: Hi\r\n\r\n";
        const string text = "Hello (with a paren) and \"quotes\"\r\n";
        var response = $"* 12 FETCH (UID 345 FLAGS (\\Seen \\Answered) RFC822.HEADER {{{header.Length}}}\r\n{header} BODY[TEXT]<0> {{{text.Length}}}\r\n{text})\r\n";

        var fetched = ImapData.Fetch(Parse(response));

        Assert.NotNull(fetched);
        Assert.Equal(12, fetched.Sequence);
        Assert.Equal(345u, fetched.Uid);
        Assert.True(fetched.Seen);
        Assert.Equal(header, Encoding.ASCII.GetString(fetched.Header!));
        Assert.Equal(text, Encoding.ASCII.GetString(fetched.Text!));
    }

    [Fact]
    public void S553_fetch_items_in_any_order_and_nil_text()
    {
        var fetched = ImapData.Fetch(Parse("* 3 FETCH (FLAGS () BODY[TEXT]<0> NIL UID 9 RFC822.HEADER {2}\r\nX:)\r\n"));

        Assert.NotNull(fetched);
        Assert.Equal(9u, fetched.Uid);
        Assert.False(fetched.Seen);
        Assert.Null(fetched.Text);
        Assert.Equal("X:", Encoding.ASCII.GetString(fetched.Header!));
    }

    [Fact]
    public void S553_literal_length_is_honoured_not_searched()
    {
        // The literal itself contains ")\r\n* 4 FETCH" — only the announced length may end it.
        const string evil = ")\r\n* 4 FETCH (UID 1)";
        var fetched = ImapData.Fetch(Parse($"* 3 FETCH (UID 7 RFC822.HEADER {{{evil.Length}}}\r\n{evil})\r\n"));

        Assert.Equal(7u, fetched!.Uid);
        Assert.Equal(evil, Encoding.ASCII.GetString(fetched.Header!));
    }

    [Fact]
    public void S553_issue_187_qq_mail_puts_no_space_after_a_literal()
    {
        // QQ Mail runs the next item straight on after a literal's octets (no SP before FLAGS).
        const string header = "From: Ann <ann@qq.com>\r\nSubject: Hi\r\n\r\n";
        const string text = "Hello\r\n";
        var response = $"* 3061 FETCH (UID 3523 RFC822.HEADER {{{header.Length}}}\r\n{header}FLAGS () BODY[TEXT] {{{text.Length}}}\r\n{text})\r\n";

        var fetched = ImapData.Fetch(Parse(response));

        Assert.NotNull(fetched);
        Assert.Equal(3523u, fetched.Uid);
        Assert.False(fetched.Seen);
        Assert.Equal(header, Encoding.ASCII.GetString(fetched.Header!));
        Assert.Equal(text, Encoding.ASCII.GetString(fetched.Text!));
    }

    [Fact]
    public void S553_select_data_exists_and_uidvalidity()
    {
        Assert.Equal(172, ImapData.Exists(Parse("* 172 EXISTS\r\n")));
        Assert.Null(ImapData.Exists(Parse("* 172 RECENT\r\n")));
        Assert.Equal(3857529045u, ImapData.UidValidity(Parse("* OK [UIDVALIDITY 3857529045] UIDs valid\r\n")));
        Assert.Null(ImapData.UidValidity(Parse("* OK [UIDNEXT 4392] Predicted next UID\r\n")));
        Assert.Null(ImapData.UidValidity(Parse("* OK [PERMANENTFLAGS (\\Deleted \\Seen \\*)] Limited\r\n")));
    }

    [Fact]
    public void S553_list_reads_attributes_and_quoted_or_literal_names()
    {
        var sent = ImapData.List(Parse("* LIST (\\HasNoChildren \\Sent) \"/\" \"[Gmail]/Sent Mail\"\r\n"));
        Assert.Equal("[Gmail]/Sent Mail", sent!.Name);
        Assert.Contains("\\Sent", sent.Attributes);

        var inbox = ImapData.List(Parse("* LIST () \".\" INBOX\r\n"));
        Assert.Equal("INBOX", inbox!.Name);

        var literal = ImapData.List(Parse("* LIST (\\Trash) NIL {11}\r\nDeleted Box\r\n"));
        Assert.Equal("Deleted Box", literal!.Name);

        var escaped = ImapData.List(Parse("* LIST () \"/\" \"Say \\\"hi\\\"\"\r\n"));
        Assert.Equal("Say \"hi\"", escaped!.Name);
    }

    [Fact]
    public void S553_status_reads_messages_and_unseen()
    {
        Assert.Equal(new MailFolderStatus(231, 3), ImapData.Status(Parse("* STATUS \"INBOX\" (MESSAGES 231 UNSEEN 3)\r\n")));
        Assert.Equal(new MailFolderStatus(5, 0), ImapData.Status(Parse("* STATUS Sent (UNSEEN 0 MESSAGES 5)\r\n")));
        Assert.Null(ImapData.Status(Parse("* STATUS INBOX (MESSAGES 5)\r\n")));
    }

    [Fact]
    public void S553_parser_survives_sloppy_and_hostile_lines()
    {
        string[] lines = ["", "*", "* OK (unbalanced", "* OK ) stray [ bracket", "* 1 FETCH (UID", "* 1 FETCH (RFC822.HEADER {999}\r\nshort", "\"\\", "{", "* LIST", "a1 NO [AUTHENTICATIONFAILED] nope"];

        foreach (var line in lines)
        {
            var response = Parse(line);
            _ = ImapData.Fetch(response);
            _ = ImapData.List(response);
            _ = ImapData.Status(response);
            _ = ImapData.Exists(response);
            _ = ImapData.UidValidity(response);
        }
    }

    [Fact]
    public void S553_issue_45_recent_range_returns_the_last_n()
    {
        Assert.Equal("4991:5000", ImapRules.RecentRange(5000, 10));
        Assert.Equal("1:3", ImapRules.RecentRange(3, 10));
        Assert.Equal("1:10", ImapRules.RecentRange(10, 10));
        Assert.Equal("1", ImapRules.RecentRange(1, 10));
        Assert.Equal("5000", ImapRules.RecentRange(5000, 1));
        Assert.Null(ImapRules.RecentRange(0, 10));
        Assert.Null(ImapRules.RecentRange(5000, 0));
    }

    [Fact]
    public void S553_issue_45_fetch_items_are_parenthesised_and_peek()
    {
        Assert.StartsWith("(", ImapRules.FetchItems, StringComparison.Ordinal);
        Assert.EndsWith(")", ImapRules.FetchItems, StringComparison.Ordinal);
        Assert.Contains("BODY.PEEK[TEXT]", ImapRules.FetchItems, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("[AUTHENTICATIONFAILED] Invalid credentials", AccountError.SignInRequired)]
    [InlineData("LOGIN failed.", AccountError.SignInRequired)]
    [InlineData("[UNAVAILABLE] Try again later", AccountError.ServerError)]
    [InlineData("[INUSE] Mailbox locked", AccountError.ServerError)]
    public void S553_a_refused_login_is_only_a_sign_in_problem_when_it_says_so(string text, AccountError expected)
        => Assert.Equal(expected, ImapRules.LoginRefusal(text));

    [Fact]
    public void S554_folder_special_use_and_candidates()
    {
        Assert.Equal(@"\Sent", MailFolder.Sent.SpecialUse());
        Assert.Equal(@"\Drafts", MailFolder.Drafts.SpecialUse());
        Assert.Equal(@"\Trash", MailFolder.Deleted.SpecialUse());
        Assert.Equal(@"\Junk", MailFolder.Junk.SpecialUse());
        Assert.Equal(@"\Archive", MailFolder.Archive.SpecialUse());
        Assert.Null(MailFolder.Inbox.SpecialUse());

        Assert.Contains("[Gmail]/Sent Mail", MailFolder.Sent.Candidates());
        Assert.Contains("[Gmail]/Spam", MailFolder.Junk.Candidates());
        Assert.Contains("[Gmail]/All Mail", MailFolder.Archive.Candidates());
        Assert.Empty(MailFolder.Inbox.Candidates());
    }

    [Fact]
    public void S554_resolution_prefers_special_use_then_names_then_a_guess()
    {
        ImapMailbox Box(string name, params string[] attributes) => new(name, attributes);

        var gmail = new[] { Box("INBOX"), Box("[Gmail]", @"\Noselect"), Box("[Gmail]/Sent Mail", @"\Sent"), Box("[Gmail]/Trash", @"\Trash") };
        Assert.Equal("[Gmail]/Sent Mail", MailFolder.Sent.ResolveImap(gmail));
        Assert.Equal("[Gmail]/Trash", MailFolder.Deleted.ResolveImap(gmail));
        Assert.Equal("INBOX", MailFolder.Inbox.ResolveImap(gmail));

        // No SPECIAL-USE: the first candidate that exists, matched case-insensitively.
        var outlook = new[] { Box("Inbox"), Box("Sent Items"), Box("Deleted Items"), Box("Junk Email") };
        Assert.Equal("Sent Items", MailFolder.Sent.ResolveImap(outlook));
        Assert.Equal("Deleted Items", MailFolder.Deleted.ResolveImap(outlook));
        Assert.Equal("Junk Email", MailFolder.Junk.ResolveImap(outlook));

        // Nothing matches: the first candidate.
        Assert.Equal("Drafts", MailFolder.Drafts.ResolveImap([]));
    }

    [Fact]
    public void S554_gmail_labels_and_graph_names()
    {
        Assert.Equal("INBOX", MailFolder.Inbox.GmailLabel());
        Assert.Equal("SENT", MailFolder.Sent.GmailLabel());
        Assert.Equal("DRAFT", MailFolder.Drafts.GmailLabel());
        Assert.Equal("TRASH", MailFolder.Deleted.GmailLabel());
        Assert.Equal("SPAM", MailFolder.Junk.GmailLabel());

        Assert.Equal("inbox", MailFolder.Inbox.GraphName());
        Assert.Equal("sentitems", MailFolder.Sent.GraphName());
        Assert.Equal("deleteditems", MailFolder.Deleted.GraphName());
        Assert.Equal("junkemail", MailFolder.Junk.GraphName());
        Assert.Equal("archive", MailFolder.Archive.GraphName());
    }

    [Fact]
    public void S555_message_ids_round_trip()
    {
        Assert.Equal("inbox|12|1", new ImapMessageId(MailFolder.Inbox, 12, 1).ToString());
        Assert.Equal(new ImapMessageId(MailFolder.Inbox, 12, 1), ImapMessageId.Parse("inbox|12|1"));
        Assert.Equal(new ImapMessageId(MailFolder.Sent, 42, 7788), ImapMessageId.Parse("sentitems|42|7788"));
        Assert.Equal(new ImapMessageId(MailFolder.Deleted, 7, null), ImapMessageId.Parse("deleteditems|7"));

        Assert.Null(ImapMessageId.Parse("99"));
        Assert.Null(ImapMessageId.Parse("sentitems|notanumber"));
        Assert.Null(ImapMessageId.Parse("notanumber"));
        Assert.Null(ImapMessageId.Parse("inbox|12|notanumber"));
        Assert.Null(ImapMessageId.Parse("nowhere|12|1"));
        Assert.Null(ImapMessageId.Parse(string.Empty));
    }

    [Fact]
    public void S555_uidvalidity_guard_only_refuses_a_known_mismatch()
    {
        Assert.True(new ImapMessageId(MailFolder.Inbox, 1, 1).StillValidIn(1));
        Assert.False(new ImapMessageId(MailFolder.Inbox, 1, 1).StillValidIn(2));
        Assert.True(new ImapMessageId(MailFolder.Inbox, 1, null).StillValidIn(9));
        Assert.True(new ImapMessageId(MailFolder.Inbox, 1, 1).StillValidIn(null));
    }

    [Fact]
    public void S545_imap_fetch_becomes_a_message()
    {
        var header = Encoding.Latin1.GetBytes("From: =?utf-8?q?Ren=C3=A9?= <rene@x.com>\r\nSubject: Café menu\r\nDate: Wed, 03 Jul 2024 08:09:10 +0200\r\nContent-Type: text/plain\r\n\r\n");
        var fetched = new ImapFetched(3, 77, [@"\Seen"], header, Encoding.ASCII.GetBytes("Lunch   is\r\nready."));

        var message = MailWire.FromImap(fetched, "imap:rene@x.com", MailFolder.Inbox, 5);

        Assert.NotNull(message);
        Assert.Equal("inbox|77|5", message.RawId);
        Assert.Equal("René", message.FromName);
        Assert.Equal("rene@x.com", message.FromAddress);
        Assert.Equal("Café menu", message.Subject);
        Assert.Equal(new DateTimeOffset(2024, 7, 3, 6, 9, 10, TimeSpan.Zero), message.Received);
        Assert.True(message.IsRead);
        Assert.Equal("Lunch is ready.", message.Preview);
        Assert.Equal(string.Empty, message.WebLink);

        Assert.Null(MailWire.FromImap(fetched with { Uid = null }, "a", MailFolder.Inbox, 5));
    }
}
