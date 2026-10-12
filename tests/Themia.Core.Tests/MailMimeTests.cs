using System.Text;
using Themia.Core.Mail;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// Header, date and MIME preview decoding, ported from the classic app's Rust tests (issue numbers
/// are the classic app's).
/// </summary>
public class MailMimeTests
{
    [Fact]
    public void S550_header_fields_unfold_and_decode()
    {
        const string raw = "From: Alice <alice@example.com>\r\nSubject: hello\r\n world\r\nDate: Mon, 02 Jun 2025 10:00:00 +0000\r\n";

        Assert.Equal("hello world", MailHeaders.Field(raw, "subject"));
        Assert.Equal(("Alice", "alice@example.com"), MailHeaders.ParseFrom(MailHeaders.Field(raw, "from")));
        Assert.Equal(string.Empty, MailHeaders.Field(raw, "cc"));
    }

    [Fact]
    public void S550_header_block_ends_at_the_blank_line()
    {
        const string raw = "Subject: real\r\n\r\nSubject: in the body\r\n";
        Assert.Equal("real", MailHeaders.Field(raw, "subject"));
    }

    [Fact]
    public void S550_encoded_words_b_q_and_plain()
    {
        Assert.Equal("Hello", MailHeaders.DecodeWords("=?UTF-8?B?SGVsbG8=?="));
        Assert.Equal("a b", MailHeaders.DecodeWords("=?UTF-8?Q?a_b?="));
        Assert.Equal("just plain", MailHeaders.DecodeWords("just plain"));
        Assert.Equal("Hi there", MailHeaders.DecodeWords("=?UTF-8?B?SGk=?= there"));

        // White space between two adjacent encoded words is dropped.
        Assert.Equal("HelloWorld", MailHeaders.DecodeWords("=?UTF-8?B?SGVsbG8=?= =?UTF-8?B?V29ybGQ=?="));

        // Malformed: no closing "?=" is emitted as written.
        Assert.Equal("=?UTF-8?B?SGk", MailHeaders.DecodeWords("=?UTF-8?B?SGk"));
        Assert.Equal("raw", MailHeaders.DecodeWord("=?UTF-8?X?raw?="));
        Assert.Equal("=??=", MailHeaders.DecodeWord("=??="));
    }

    [Fact]
    public void S550_q_word_whose_text_starts_with_an_escape_is_one_word()
    {
        // The classic decoder ended the word at the first "?=" — right after the "Q" here.
        Assert.Equal("été", MailHeaders.DecodeWords("=?utf-8?Q?=C3=A9t=C3=A9?="));
    }

    [Fact]
    public void S550_encoded_word_honours_its_charset()
    {
        // ISO-8859-1 "é" is 0xE9, not valid UTF-8 on its own.
        Assert.Equal("café", MailHeaders.DecodeWords("=?iso-8859-1?Q?caf=E9?="));
        Assert.Equal("café", MailHeaders.DecodeWords("=?utf-8?q?caf=C3=A9?="));
    }

    [Fact]
    public void S550_q_bytes_handle_escapes_and_garbage()
    {
        string Q(string s) => Encoding.UTF8.GetString(MailHeaders.QBytes(s));

        Assert.Equal("Hello World", Q("Hello_World"));
        Assert.Equal("a b", Q("a=20b"));
        Assert.Equal("100%", Q("100=25"));
        Assert.Equal("ab=", Q("ab="));
        Assert.Equal("=ZZ", Q("=ZZ"));
        Assert.Equal(string.Empty, Q(string.Empty));
    }

    [Fact]
    public void S550_base64_known_vectors_and_invalid_input()
    {
        string? B(string s) => MimeText.Base64(s) is { } b ? Encoding.UTF8.GetString(b) : null;

        Assert.Equal("hello", B("aGVsbG8="));
        Assert.Equal("hi", B("aGk="));
        Assert.Equal("fooba", B("Zm9vYmE="));
        Assert.Equal(string.Empty, B(string.Empty));
        Assert.Null(B("not valid!@#$"));
    }

    [Theory]
    [InlineData("Alice <alice@example.com>", "Alice", "alice@example.com")]
    [InlineData("\"Lee, Ann\" <ann@x.com>", "Lee, Ann", "ann@x.com")]
    [InlineData("<bare@x.com>", "", "bare@x.com")]
    [InlineData("bare@x.com", "", "bare@x.com")]
    [InlineData("bob@x.com (Bob Smith)", "Bob Smith", "bob@x.com")]
    [InlineData("Mailer Daemon", "Mailer Daemon", "")]
    [InlineData("", "", "")]
    public void S550_from_field_splits_name_and_address(string from, string name, string address)
        => Assert.Equal((name, address), MailHeaders.ParseFrom(from));

    [Fact]
    public void S551_dates_apply_their_zone()
    {
        DateTimeOffset At(int y, int mo, int d, int h, int mi, int s) => new(y, mo, d, h, mi, s, TimeSpan.Zero);

        Assert.Equal(At(2026, 4, 1, 12, 0, 0), MailHeaders.ParseDate("Mon, 01 Apr 2026 12:00:00 +0000"));
        Assert.Equal(At(2025, 6, 2, 10, 30, 5), MailHeaders.ParseDate("Tue, 02 Jun 2025 10:30:05 GMT"));

        // issue #45(d): a positive offset is subtracted, a negative one added.
        Assert.Equal(At(2024, 7, 3, 6, 9, 10), MailHeaders.ParseDate("Wed, 03 Jul 2024 08:09:10 +0200"));
        Assert.Equal(At(2026, 7, 2, 7, 51, 37), MailHeaders.ParseDate("Thu, 02 Jul 2026 00:51:37 -0700"));

        // The offset rolls the instant across a year.
        Assert.Equal(At(2024, 12, 31, 23, 0, 0), MailHeaders.ParseDate("Wed, 01 Jan 2025 01:00:00 +0200"));
        Assert.Equal(At(2024, 7, 3, 6, 9, 10), MailHeaders.ParseDate("Wed, 03 Jul 2024 08:09:10 +02:00"));
        Assert.Equal(At(1999, 12, 15, 23, 59, 59), MailHeaders.ParseDate("15 Dec 1999 23:59:59 +0000"));

        // A trailing comment is ignored.
        Assert.Equal(At(2026, 4, 1, 12, 0, 0), MailHeaders.ParseDate("Mon, 1 Apr 2026 12:00:00 +0000 (UTC)"));
    }

    [Fact]
    public void S551_named_zones_and_obsolete_forms()
    {
        // RFC 5322 §4.3 defines the US zones; the classic app treated them as unknown and kept the
        // wall-clock time, which put an EST message five hours off.
        Assert.Equal(new DateTimeOffset(2024, 7, 3, 13, 9, 10, TimeSpan.Zero), MailHeaders.ParseDate("Wed, 03 Jul 2024 08:09:10 EST"));
        Assert.Equal(new DateTimeOffset(2024, 7, 3, 15, 9, 10, TimeSpan.Zero), MailHeaders.ParseDate("Wed, 03 Jul 2024 08:09:10 PDT"));

        // An unknown name is "offset unknown", which the RFC reads as UTC.
        Assert.Equal(new DateTimeOffset(2024, 7, 3, 8, 9, 10, TimeSpan.Zero), MailHeaders.ParseDate("Wed, 03 Jul 2024 08:09:10 XYZ"));

        // Two-digit years, and a time without seconds.
        Assert.Equal(new DateTimeOffset(2003, 1, 5, 9, 15, 0, TimeSpan.Zero), MailHeaders.ParseDate("5 Jan 03 09:15 +0000"));
        Assert.Equal(new DateTimeOffset(1998, 1, 5, 9, 15, 0, TimeSpan.Zero), MailHeaders.ParseDate("5 Jan 98 09:15 +0000"));
    }

    [Theory]
    [InlineData("garbage")]
    [InlineData("")]
    [InlineData("31 Feb 2025 10:00:00 +0000")]
    [InlineData("01 Foo 2025 10:00:00 +0000")]
    [InlineData("01 Jan 2025 25:00:00 +0000")]
    [InlineData("01 Jan 2025 aa:bb +0000")]
    public void S551_unparseable_dates_are_null_not_guessed(string value)
        => Assert.Null(MailHeaders.ParseDate(value));

    [Fact]
    public void S552_parsers_never_throw_on_adversarial_input()
    {
        string[] inputs =
        [
            "", ":::::", "=?UTF-8?B?", "=?x?Q?=ZZ=\r\n", "héllo wörld 日本語 \0\u0001", "=?=?=?", "<<<>>>&&&;;",
            "Content-Type: multipart/mixed; boundary=\"\"\r\n\r\n--\r\n--", "&#xFFFFFFFF; &#55296; &#0;",
        ];

        foreach (var s in inputs)
        {
            _ = MailHeaders.DecodeWords(s);
            _ = MailHeaders.ParseDate(s);
            _ = MailHeaders.ParseFrom(s);
            _ = MimeText.StripHtml(s);
            _ = MimeText.DecodeEntities(s);
            _ = MimePreview.Build(s, s);
        }
    }

    [Fact]
    public void S552_issue_45b_preview_strips_tags_collapses_and_truncates()
    {
        Assert.Equal("Hello world, this is the body.", MimePreview.Build(string.Empty, "<p>Hello   <b>world</b>,\r\nthis  is the body.</p>"));
        Assert.Equal("a b c", MimePreview.Build(string.Empty, "  a\t\tb\r\n c  "));
        Assert.Equal(string.Empty, MimePreview.Build(string.Empty, string.Empty));
        Assert.Equal(200, MimePreview.Build(string.Empty, string.Concat(Enumerable.Repeat("x ", 300))).Length);
    }

    [Fact]
    public void S552_issue_114_multipart_preview_is_the_text_part_not_the_scaffolding()
    {
        const string headers = "From: a@b.com\r\nSubject: Hi\r\nContent-Type: multipart/alternative;\r\n boundary=\"------------1D9645AFA03EA13B508AAAAA\"\r\n\r\n";
        const string body = "This is a multi-part message in MIME format.\r\n"
            + "--------------1D9645AFA03EA13B508AAAAA\r\n"
            + "Content-Type: text/plain; charset=utf-8; format=flowed\r\n"
            + "Content-Transfer-Encoding: 7bit\r\n\r\n"
            + "This is the first line of the actual plain text message.\r\n\r\n"
            + "--------------1D9645AFA03EA13B508AAAAA\r\n"
            + "Content-Type: text/html; charset=utf-8\r\n"
            + "Content-Transfer-Encoding: 7bit\r\n\r\n"
            + "<html><body><p>HTML version</p></body></html>\r\n"
            + "--------------1D9645AFA03EA13B508AAAAA--\r\n";

        Assert.Equal("This is the first line of the actual plain text message.", MimePreview.Build(headers, body));
    }

    [Fact]
    public void S552_issue_114_quoted_printable_part_is_decoded()
    {
        const string headers = "Content-Type: multipart/mixed; boundary=\"----=_Part_3500521_102256291\"\r\n\r\n";
        const string body = "------=_Part_3500521_102256291\r\n"
            + "Content-Type: text/plain; charset=utf-8\r\n"
            + "Content-Transfer-Encoding: quoted-printable\r\n\r\n"
            + "\r\n =20\r\nText starts here and wraps a very long w=\r\nord.\r\n"
            + "------=_Part_3500521_102256291--\r\n";

        Assert.Equal("Text starts here and wraps a very long word.", MimePreview.Build(headers, body));
    }

    [Fact]
    public void S552_issue_114_html_only_preview_drops_style_and_script()
    {
        const string headers = "Content-Type: multipart/related; boundary=bnd\r\n\r\n";
        const string body = "--bnd\r\n"
            + "Content-Type: text/html; charset=utf-8\r\n\r\n"
            + "<html><head><style>.a { color: #fff; }</style><script>var x = 1;</script></head>"
            + "<body><p>Fish&nbsp;&amp;&nbsp;chips</p></body></html>\r\n"
            + "--bnd--\r\n";

        Assert.Equal("Fish & chips", MimePreview.Build(headers, body));
    }

    [Fact]
    public void S552_html_paragraphs_do_not_run_together()
        => Assert.Equal("One. Two.", MimePreview.Build("Content-Type: text/html\r\n\r\n", "<p>One.</p><p>Two.</p>"));

    [Fact]
    public void S552_issue_114_nested_parts_walked_and_attachments_skipped()
    {
        const string headers = "Content-Type: multipart/mixed; boundary=\"outer\"\r\n\r\n";
        const string body = "--outer\r\n"
            + "Content-Type: multipart/alternative; boundary=\"inner\"\r\n\r\n"
            + "--inner\r\n"
            + "Content-Type: text/plain; charset=us-ascii\r\n\r\n"
            + "Real body text.\r\n"
            + "--inner--\r\n"
            + "--outer\r\n"
            + "Content-Type: text/plain; charset=us-ascii\r\n"
            + "Content-Disposition: attachment; filename=\"notes.txt\"\r\n\r\n"
            + "Attachment contents.\r\n"
            + "--outer--\r\n";

        Assert.Equal("Real body text.", MimePreview.Build(headers, body));
    }

    [Fact]
    public void S552_issue_114_transfer_encodings_and_charsets()
    {
        const string headers = "Content-Type: multipart/mixed; boundary=b\r\n\r\n";

        Assert.Equal("Hello from base64.", MimePreview.Build(headers,
            "--b\r\nContent-Type: text/plain\r\nContent-Transfer-Encoding: base64\r\n\r\nSGVsbG8gZnJvbSBiYXNlNjQu\r\n--b--\r\n"));

        // Corrupt base64: the part is dropped, not previewed as base64.
        Assert.Equal(string.Empty, MimePreview.Build(headers,
            "--b\r\nContent-Type: text/plain\r\nContent-Transfer-Encoding: base64\r\n\r\n!!!not base64!!!\r\n--b--\r\n"));

        // Windows-1252 0x92 is a curly apostrophe, not U+FFFD.
        Assert.Equal("It’s here.", MimePreview.Build(headers,
            "--b\r\nContent-Type: text/plain; charset=windows-1252\r\nContent-Transfer-Encoding: quoted-printable\r\n\r\nIt=92s here.\r\n--b--\r\n"));
    }

    [Fact]
    public void S552_eight_bit_latin1_body_survives_the_walk()
    {
        // Raw 8-bit bytes, not QP: decoding the whole response as UTF-8 first would lose them.
        var head = Encoding.ASCII.GetBytes("--b\r\nContent-Type: text/plain; charset=iso-8859-1\r\nContent-Transfer-Encoding: 8bit\r\n\r\nGr");
        var body = head.Concat(new byte[] { 0xFC, 0xDF, 0x65 }).Concat(Encoding.ASCII.GetBytes("\r\n--b--\r\n")).ToArray();

        Assert.Equal("Grüße", MimePreview.Build("Content-Type: multipart/mixed; boundary=b\r\n\r\n", body));
    }

    [Fact]
    public void S552_issue_114_unusable_structures_preview_empty()
    {
        Assert.Equal(string.Empty, MimePreview.Build(
            "Content-Type: multipart/mixed; boundary=\"never-used\"\r\n\r\n",
            "This is a multi-part message in MIME format.\r\n--other\r\nstuff\r\n"));

        Assert.Equal(string.Empty, MimePreview.Build(
            "Content-Type: multipart/mixed; boundary=b\r\n\r\n",
            "--b\r\nContent-Type: image/png\r\nContent-Transfer-Encoding: base64\r\n\r\niVBORw0KGgo=\r\n--b--\r\n"));
    }

    [Fact]
    public void S552_truncated_fetch_keeps_the_part_that_arrived()
    {
        // A 32 KB partial fetch ends mid-part, with no closing delimiter.
        Assert.Equal("The start of a long letter", MimePreview.Build(
            "Content-Type: multipart/alternative; boundary=b\r\n\r\n",
            "--b\r\nContent-Type: text/plain\r\n\r\nThe start of a long letter"));
    }

    [Fact]
    public void S552_issue_114_single_part_body_decodes_and_keeps_angle_brackets()
    {
        const string headers = "Content-Type: text/plain; charset=utf-8\r\nContent-Transfer-Encoding: quoted-printable\r\n\r\n";
        Assert.Equal("See <https://example.com/x> for café.", MimePreview.Build(headers, "See <https://example.com/x> for caf=C3=A9.\r\n"));
    }

    [Fact]
    public void S552_issue_114_content_type_params_parse_without_word_decoding()
    {
        const string headers = "Content-Type: multipart/mixed; boundary=\"=?_not-a-word?=\"\r\nSubject: =?utf-8?q?caf=C3=A9?=\r\n\r\n";

        Assert.Equal("=?_not-a-word?=", MimePreview.Parameter(MailHeaders.FieldRaw(headers, "content-type"), "boundary"));
        Assert.Equal("café", MailHeaders.Field(headers, "subject"));

        const string folded = "Content-Type: text/plain;\r\n\tcharset=ISO-8859-1\r\n\r\n";
        Assert.Equal("ISO-8859-1", MimePreview.Parameter(MailHeaders.FieldRaw(folded, "Content-Type"), "CHARSET"));
    }

    [Fact]
    public void S552_entities_named_numeric_and_unknown()
    {
        Assert.Equal("it's <b> & \"q\" é", MimeText.DecodeEntities("it&#39;s &lt;b&gt; &amp; &quot;q&quot; &#xE9;"));
        Assert.Equal("&foo; & bar", MimeText.DecodeEntities("&foo; & bar"));

        // A zero-width "preheader" spacer is padding, not text.
        Assert.Equal("Hi", MimeText.DecodeEntities("Hi&#8204;&zwnj;"));
    }

    [Fact]
    public void S552_one_line_never_splits_a_surrogate_pair()
    {
        var text = new string('a', 199) + "\U0001F600";
        var line = MimeText.OneLine(text, 200);

        Assert.Equal(199, line.Length);
        Assert.False(char.IsHighSurrogate(line[^1]));
    }

    [Fact]
    public void S552_issue_164_chinese_text_around_an_entity_and_a_cut_mid_character_never_throws()
    {
        // Classic issue #164: the preview cut at a byte index inside "时" and panicked.
        const string headers = "Content-Type: text/html; charset=utf-8\r\nContent-Transfer-Encoding: base64\r\n\r\n";
        var html = Convert.ToBase64String(Encoding.UTF8.GetBytes("<p>&gt;\n发送时间: 2026</p>"));
        Assert.Equal("> 发送时间: 2026", MimePreview.Build(headers, html));

        // A part longer than the scan limit is cut there, which here falls inside a three-byte character.
        var body = Encoding.UTF8.GetBytes(new string('时', (MimePreview.PartScanLimit / 3) + 10));
        var preview = MimePreview.Build("Content-Type: text/plain; charset=utf-8\r\n\r\n", body);
        Assert.Equal(MimePreview.MaxLength, preview.Length);
        Assert.All(preview, c => Assert.Equal('时', c));
    }

    [Fact]
    public void S552_issue_147_legacy_cyrillic_and_chinese_charsets_are_honoured()
    {
        // Classic issue #147 (Yandex: letters from other senders read garbled) and 163.com mail: KOI8-R,
        // windows-1251 and GBK are not UTF-8 and used to degrade to replacement characters.
        Assert.Equal("Привет", MimeText.Decode([240, 210, 201, 215, 197, 212], "koi8-r"));
        Assert.Equal("发送时间", MimeText.Decode([183, 162, 203, 205, 202, 177, 188, 228], "gbk"));
        Assert.Equal("发送时间", MimeText.Decode([183, 162, 203, 205, 202, 177, 188, 228], "GB2312"));
        Assert.Equal("Счёт за май", MailHeaders.DecodeWords("=?windows-1251?B?0fe48iDn4CDs4Ok=?="));

        // An unknown label still degrades to lossy UTF-8 rather than throwing.
        Assert.Equal("ok", MimeText.Decode("ok"u8.ToArray(), "x-made-up"));
    }
}
