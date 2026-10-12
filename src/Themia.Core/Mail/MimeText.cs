using System.Globalization;
using System.Text;

namespace Themia.Core.Mail;

/// <summary>
/// [S-552] The byte-level decoders a mail preview needs: base64, quoted-printable bodies, charsets,
/// and HTML reduced to its words.
/// <para>
/// MIME text is handled as a "byte string" — each byte one <see cref="char"/> 0–255 (Latin-1) —
/// from the moment it leaves the socket until a part's own charset turns it into text. That keeps
/// the walk lossless: a body in 8-bit ISO-8859-1 survives the boundary split byte for byte, where
/// decoding the whole response as UTF-8 first (the classic app did, lossily) would have turned
/// every accented letter into U+FFFD before the charset was even read.
/// </para>
/// </summary>
public static class MimeText
{
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>Bytes as a byte string: one char per byte, lossless both ways.</summary>
    public static string ByteString(ReadOnlySpan<byte> bytes) => Encoding.Latin1.GetString(bytes);

    /// <summary>A byte string back to its bytes. Characters above 0xFF (not from the wire) become '?'.</summary>
    public static byte[] Bytes(string byteString) => Encoding.Latin1.GetBytes(byteString);

    /// <summary>
    /// Standard-alphabet base64, as mail writes it: line breaks and padding are noise and skipped,
    /// a trailing partial group is decoded as far as it goes (a truncated fetch ends mid-group), and
    /// any other character makes the whole thing invalid — null, never a half-decoded guess.
    /// </summary>
    public static byte[]? Base64(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var values = new List<int>(text.Length);
        foreach (var c in text)
        {
            if (c == '=' || char.IsWhiteSpace(c))
            {
                continue;
            }

            var v = c switch
            {
                >= 'A' and <= 'Z' => c - 'A',
                >= 'a' and <= 'z' => c - 'a' + 26,
                >= '0' and <= '9' => c - '0' + 52,
                '+' => 62,
                '/' => 63,
                _ => -1,
            };

            if (v < 0)
            {
                return null;
            }

            values.Add(v);
        }

        var output = new List<byte>(values.Count * 3 / 4);
        var i = 0;

        for (; i + 3 < values.Count; i += 4)
        {
            var group = (values[i] << 18) | (values[i + 1] << 12) | (values[i + 2] << 6) | values[i + 3];
            output.Add((byte)(group >> 16));
            output.Add((byte)(group >> 8));
            output.Add((byte)group);
        }

        var left = values.Count - i;
        if (left == 3)
        {
            output.Add((byte)((values[i] << 2) | (values[i + 1] >> 4)));
            output.Add((byte)((values[i + 1] << 4) | (values[i + 2] >> 2)));
        }
        else if (left == 2)
        {
            output.Add((byte)((values[i] << 2) | (values[i + 1] >> 4)));
        }

        return [.. output];
    }

    /// <summary>
    /// A quoted-printable <b>body</b> (RFC 2045 §6.7) to bytes: <c>=XX</c> escapes and soft line breaks
    /// (a trailing <c>=</c> joining wrapped lines). Unlike an RFC 2047 "Q" word, <c>_</c> is itself.
    /// </summary>
    public static byte[] QuotedPrintable(string byteString)
    {
        ArgumentNullException.ThrowIfNull(byteString);

        var s = byteString;
        var output = new List<byte>(s.Length);
        var i = 0;

        while (i < s.Length)
        {
            if (s[i] == '=')
            {
                if (i + 1 < s.Length && s[i + 1] == '\n')
                {
                    i += 2;
                    continue;
                }

                if (i + 2 < s.Length && s[i + 1] == '\r' && s[i + 2] == '\n')
                {
                    i += 3;
                    continue;
                }

                if (i + 2 < s.Length
                    && byte.TryParse(s.AsSpan(i + 1, 2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var b))
                {
                    output.Add(b);
                    i += 3;
                    continue;
                }

                // A bare '=' at the very end is a soft break whose newline a truncated fetch cut off.
                if (i + 1 >= s.Length)
                {
                    break;
                }
            }

            output.Add((byte)s[i]);
            i++;
        }

        return [.. output];
    }

    /// <summary>
    /// Bytes to text by their charset. Valid UTF-8 is read as UTF-8 whatever the label says (it
    /// covers us-ascii and every correctly labelled UTF-8 body); a Latin-1-family label goes through
    /// the Windows-1252 table, because mail labelled ISO-8859-1 that uses 0x80–0x9F is cp1252 in
    /// practice; UTF-16 and the legacy code pages (KOI8-R, windows-1251, GBK, Big5, Shift_JIS, ...) are
    /// honoured; anything else degrades to lossy UTF-8 rather than failing the preview.
    /// </summary>
    public static string Decode(byte[] bytes, string? charset)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        var label = (charset ?? string.Empty).Trim().Trim('"').ToLowerInvariant();

        if (label.StartsWith("utf-16", StringComparison.Ordinal) || label.StartsWith("utf-32", StringComparison.Ordinal))
        {
            try
            {
                return Encoding.GetEncoding(label).GetString(bytes);
            }
            catch (ArgumentException)
            {
                // ignore: an unknown UTF-16/32 variant label falls through to the generic paths.
            }
        }

        try
        {
            return StrictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            // Not UTF-8: the label decides below.
        }

        if (IsLatin1Family(label))
        {
            return Windows1252(bytes);
        }

        // The legacy code pages much of the world's mail still declares: KOI8-R and windows-1251
        // (Yandex, Mail.ru), GBK / GB2312 / Big5 (163.com, QQ), Shift_JIS, EUC-KR. Read from the
        // in-box code-page tables, without registering a process-wide provider (classic issue #147).
        if (label.Length > 0 && CodePagesEncodingProvider.Instance.GetEncoding(label) is { } legacy)
        {
            return legacy.GetString(bytes);
        }

        return Encoding.UTF8.GetString(bytes);
    }

    private static bool IsLatin1Family(string label)
        => label.StartsWith("iso-8859-1", StringComparison.Ordinal)
            || label.StartsWith("iso8859-1", StringComparison.Ordinal)
            || label.StartsWith("windows-1252", StringComparison.Ordinal)
            || label.StartsWith("cp1252", StringComparison.Ordinal)
            || label is "latin1" or "latin-1" or "us-ascii" or "ascii" or "iso-8859-15";

    /// <summary>Windows-1252 differs from Latin-1 only in 0x80–0x9F.</summary>
    private static readonly char[] HighCp1252 =
    [
        '€', '\u0081', '‚', 'ƒ', '„', '…', '†', '‡',
        'ˆ', '‰', 'Š', '‹', 'Œ', '\u008D', 'Ž', '\u008F',
        '\u0090', '‘', '’', '“', '”', '•', '–', '—',
        '˜', '™', 'š', '›', 'œ', '\u009D', 'ž', 'Ÿ',
    ];

    private static string Windows1252(byte[] bytes)
    {
        var chars = new char[bytes.Length];
        for (var i = 0; i < bytes.Length; i++)
        {
            var b = bytes[i];
            chars[i] = b is >= 0x80 and <= 0x9F ? HighCp1252[b - 0x80] : (char)b;
        }

        return new string(chars);
    }

    /// <summary>Raw header bytes as text: UTF-8 when they are, else the cp1252 reading of legacy 8-bit.</summary>
    public static string HeaderText(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        try
        {
            return StrictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return Windows1252(bytes);
        }
    }

    private static readonly HashSet<string> BlockTags = new(StringComparer.Ordinal)
    {
        "p", "br", "div", "tr", "td", "th", "li", "ul", "ol", "table", "h1", "h2", "h3", "h4", "h5", "h6", "blockquote", "hr",
    };

    /// <summary>
    /// An HTML body reduced to its readable words: <c>&lt;script&gt;</c> and <c>&lt;style&gt;</c> blocks
    /// dropped whole (their contents are code), tags removed — a block tag leaves a space so two
    /// paragraphs do not run together — and entities decoded.
    /// </summary>
    public static string StripHtml(string html)
    {
        ArgumentNullException.ThrowIfNull(html);

        var output = new StringBuilder(Math.Min(html.Length, 32 * 1024));
        var i = 0;

        while (i < html.Length)
        {
            if (html[i] != '<')
            {
                var next = html.IndexOf('<', i);
                var stop = next < 0 ? html.Length : next;
                output.Append(html, i, stop - i);
                i = stop;
                continue;
            }

            var nameStart = i + 1 < html.Length && html[i + 1] == '/' ? i + 2 : i + 1;
            var nameEnd = nameStart;
            while (nameEnd < html.Length && char.IsAsciiLetterOrDigit(html[nameEnd]))
            {
                nameEnd++;
            }

            var name = html[nameStart..nameEnd].ToLowerInvariant();
            var close = html.IndexOf('>', i);
            var tagEnd = close < 0 ? html.Length : close + 1;

            if (nameStart == i + 1 && name is "script" or "style")
            {
                var closer = html.IndexOf("</" + name, tagEnd, StringComparison.OrdinalIgnoreCase);
                if (closer < 0)
                {
                    break;
                }

                var closerEnd = html.IndexOf('>', closer);
                i = closerEnd < 0 ? html.Length : closerEnd + 1;
                continue;
            }

            if (BlockTags.Contains(name))
            {
                output.Append(' ');
            }

            i = tagEnd;
        }

        return DecodeEntities(output.ToString());
    }

    /// <summary>
    /// The named entities that actually show up in mail text, and numeric references. An unknown
    /// entity stays verbatim: a literal <c>&amp;foo;</c> beats a swallowed word.
    /// </summary>
    public static string DecodeEntities(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (!text.Contains('&', StringComparison.Ordinal))
        {
            return text;
        }

        var output = new StringBuilder(text.Length);
        var i = 0;

        while (i < text.Length)
        {
            var amp = text.IndexOf('&', i);
            if (amp < 0)
            {
                output.Append(text, i, text.Length - i);
                break;
            }

            output.Append(text, i, amp - i);

            // An entity is short; anything longer is a literal ampersand.
            var window = Math.Min(12, text.Length - amp);
            var semi = text.IndexOf(';', amp, window);
            if (semi < 0)
            {
                output.Append('&');
                i = amp + 1;
                continue;
            }

            var name = text[(amp + 1)..semi];
            var decoded = Entity(name);

            if (decoded is null)
            {
                output.Append(text, amp, semi - amp + 1);
            }
            else
            {
                output.Append(decoded);
            }

            i = semi + 1;
        }

        return output.ToString();
    }

    private static string? Entity(string name)
    {
        switch (name.ToLowerInvariant())
        {
            case "nbsp": return " ";
            case "amp": return "&";
            case "lt": return "<";
            case "gt": return ">";
            case "quot": return "\"";
            case "apos": return "'";
            case "zwnj" or "zwj": return string.Empty;
        }

        if (name.Length < 2 || name[0] != '#')
        {
            return null;
        }

        var hex = name[1] is 'x' or 'X';
        var digits = hex ? name[2..] : name[1..];
        var ok = hex
            ? int.TryParse(digits, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var code)
            : int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out code);

        if (!ok || code <= 0 || code > 0x10FFFF || code is >= 0xD800 and <= 0xDFFF)
        {
            return null;
        }

        // A zero-width "preheader" spacer (U+200C, U+034F, U+00AD) is padding, not text.
        if (code is 0x200C or 0x200B or 0x034F or 0x00AD or 0xFEFF)
        {
            return string.Empty;
        }

        return char.ConvertFromUtf32(code);
    }

    /// <summary>
    /// Collapse every run of white space to one space and cut to <paramref name="max"/> characters,
    /// never inside a surrogate pair.
    /// </summary>
    public static string OneLine(string text, int max)
    {
        ArgumentNullException.ThrowIfNull(text);

        var output = new StringBuilder(Math.Min(text.Length, max + 1));
        var pendingSpace = false;

        foreach (var c in text)
        {
            if (char.IsWhiteSpace(c) || char.IsControl(c))
            {
                pendingSpace = output.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                output.Append(' ');
                pendingSpace = false;
            }

            output.Append(c);

            if (output.Length >= max)
            {
                break;
            }
        }

        if (output.Length > max)
        {
            output.Length = max;
        }

        if (output.Length > 0 && char.IsHighSurrogate(output[^1]))
        {
            output.Length--;
        }

        return output.ToString();
    }
}
