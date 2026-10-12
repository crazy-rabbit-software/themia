using Themia.Core.Accounts;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>The IMAP login handshake's pure parts, and the server presets.</summary>
public class ImapProtocolTests
{
    [Theory]
    [InlineData("* OK [CAPABILITY IMAP4rev1] Dovecot ready.", ImapGreeting.Ok)]
    [InlineData("* PREAUTH ready", ImapGreeting.PreAuth)]
    [InlineData("* BYE too many connections", ImapGreeting.Bye)]
    [InlineData("220 smtp.example ESMTP", ImapGreeting.NotImap)]
    [InlineData(null, ImapGreeting.NotImap)]
    public void S521_greetings_are_recognised(string? line, ImapGreeting expected)
        => Assert.Equal(expected, ImapProtocol.ParseGreeting(line));

    [Fact]
    public void S521_the_tagged_answer_is_told_from_untagged_lines()
    {
        Assert.Null(ImapProtocol.ParseTagged("* CAPABILITY IMAP4rev1", "a1"));
        Assert.Null(ImapProtocol.ParseTagged("a10 OK done", "a1"));
        Assert.Equal(new ImapTagged(ImapStatus.Ok, "[CAPABILITY X] Logged in"), ImapProtocol.ParseTagged("a1 OK [CAPABILITY X] Logged in", "a1"));
        Assert.Equal(new ImapTagged(ImapStatus.No, "[AUTHENTICATIONFAILED] Invalid credentials"), ImapProtocol.ParseTagged("a1 NO [AUTHENTICATIONFAILED] Invalid credentials", "a1"));
        Assert.Equal(ImapStatus.Bad, ImapProtocol.ParseTagged("a1 bad syntax", "a1")?.Status);
    }

    [Theory]
    [InlineData("bob", "\"bob\"")]
    [InlineData("p\"w\\d", "\"p\\\"w\\\\d\"")]
    [InlineData("with space", "\"with space\"")]
    public void S521_values_are_quoted_so_nothing_ends_the_command_early(string value, string expected)
        => Assert.Equal(expected, ImapProtocol.Quote(value));

    [Theory]
    [InlineData("pässword")]
    [InlineData("line\r\nbreak")]
    [InlineData("nul\0")]
    public void S521_values_a_quoted_string_cannot_carry_need_a_literal(string value)
        => Assert.Null(ImapProtocol.Quote(value));

    [Fact]
    public void S521_continuation_and_literal_prefix()
    {
        Assert.True(ImapProtocol.IsContinuation("+ go ahead"));
        Assert.False(ImapProtocol.IsContinuation("* OK"));
        Assert.Equal("{9}", ImapProtocol.LiteralPrefix(9));
    }

    [Theory]
    [InlineData("me@gmail.com", "imap.gmail.com")]
    [InlineData("me@EU.ICLOUD.COM", "imap.mail.me.com")]
    [InlineData("me@notgmail.com", null)]
    [InlineData("me@outlook.com", null)]
    [InlineData("no-at-sign", null)]
    [InlineData("trailing@", null)]
    public void S522_imap_presets_match_the_domain_or_a_subdomain_only(string address, string? host)
        => Assert.Equal(host, ImapPresets.ForAddress(address)?.Host);

    [Fact]
    public void S522_dav_presets_follow_the_kind()
    {
        Assert.Equal("https://caldav.icloud.com", DavPresets.ForAddress("me@icloud.com", DavKind.Calendar));
        Assert.Equal("https://contacts.icloud.com", DavPresets.ForAddress("me@icloud.com", DavKind.Contacts));
        Assert.Null(DavPresets.ForAddress("me@example.org", DavKind.Calendar));
    }

    [Fact]
    public void S553_issue_169_the_client_names_itself_with_an_rfc_2971_id()
    {
        // Classic issue #169: 163.com (NetEase) refuses SELECT with "Unsafe Login" until the client
        // identifies itself after LOGIN. The ID is a parenthesised list of quoted field/value pairs.
        Assert.Equal("ID (\"name\" \"Themia\" \"vendor\" \"Lometics GmbH\")", ImapProtocol.IdCommand);

        // Sent only to a server that advertises ID; one that does not is never asked.
        var id = Themia.Core.Mail.ImapParser.Parse("* CAPABILITY IMAP4rev1 ID IDLE"u8.ToArray());
        var none = Themia.Core.Mail.ImapParser.Parse("* CAPABILITY IMAP4rev1 IDLE"u8.ToArray());
        Assert.True(Themia.Core.Mail.ImapDelta.HasCapability(id, "ID"));
        Assert.False(Themia.Core.Mail.ImapDelta.HasCapability(none, "ID"));
    }
}
