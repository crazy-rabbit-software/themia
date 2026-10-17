using System.Buffers.Binary;
using System.Text;
using Themia.Core.Localization;

namespace Themia.Core.Files;

/// <summary>What a drag over the desktop surface is over.</summary>
public enum SurfaceHit
{
    /// <summary>Bare surface: only hit-testable while editing ([S-851]); otherwise that spot is Explorer's desktop.</summary>
    Empty,

    /// <summary>The surface's own controls: the edit toolbar, the view bar, a notice, the placement hint.</summary>
    Chrome,

    /// <summary>A widget card.</summary>
    Widget,
}

/// <summary>Who takes a drop on the surface.</summary>
public enum DropRoute
{
    /// <summary>Nobody: the drag shows the no-drop cursor and a release does nothing.</summary>
    Refuse,

    /// <summary>The widget under the pointer, which picks the effect itself.</summary>
    Widget,

    /// <summary>The user's Desktop folder, through the shell's own drop target for it.</summary>
    Desktop,
}

/// <summary>The image the shell draws beside a drag's thumbnail (<c>DROPIMAGETYPE</c>).</summary>
public enum DropImage
{
    /// <summary>No description: the source's default cursor feedback.</summary>
    Invalid = -1,
    None = 0,
    Copy = 1,
    Move = 2,
    Link = 4,
    Label = 6,
    Warning = 7,
    NoImage = 8,
}

/// <summary>
/// What a drag shows while it is over a target: the effect (the cursor) and the shell's caption beside
/// the drag image — <paramref name="Message"/> with <c>%1</c> standing for <paramref name="Insert"/>,
/// drawn in the accent, as Explorer's "Move to <b>Documents</b>".
/// </summary>
public readonly record struct DropFeedback(DropEffect Effect, string Message, string Insert)
{
    /// <summary>Refused: no effect, no caption.</summary>
    public static DropFeedback Refused => new(DropEffect.None, string.Empty, string.Empty);

    /// <summary>The caption the shell draws for this feedback.</summary>
    public DropImage Image => Effect switch
    {
        DropEffect.Move => DropImage.Move,
        DropEffect.Copy => DropImage.Copy,
        DropEffect.Link => DropImage.Link,
        _ => DropImage.Invalid,
    };
}

/// <summary>
/// [S-1020]..[S-1026] The desktop surface as a drop target: who takes a drop (<see cref="Route"/>),
/// the effect a forced choice asks the shell for (<see cref="KeysFor"/>), the caption beside the drag
/// image (<see cref="Feedback"/>, <see cref="EncodeDescription"/>), and the paths a Chromium app
/// offers as text (<see cref="FileUrls"/>). Pure, so the rules are tested rather than dragged.
/// </summary>
public static class SurfaceDrops
{
    /// <summary><c>MK_LBUTTON</c>.</summary>
    public const uint KeyLeftButton = 0x01;

    /// <summary><c>MK_RBUTTON</c>.</summary>
    public const uint KeyRightButton = 0x02;

    /// <summary><c>MK_SHIFT</c>.</summary>
    public const uint KeyShift = 0x04;

    /// <summary><c>MK_CONTROL</c>.</summary>
    public const uint KeyControl = 0x08;

    /// <summary><c>MK_MBUTTON</c>.</summary>
    public const uint KeyMiddleButton = 0x10;

    /// <summary><c>MK_ALT</c>.</summary>
    public const uint KeyAlt = 0x20;

    /// <summary><c>MAX_PATH</c>: each string of a <c>DROPDESCRIPTION</c> is a fixed WCHAR[260].</summary>
    public const int DescriptionChars = 260;

    /// <summary><c>sizeof(DROPDESCRIPTION)</c>: the type, then two WCHAR[260].</summary>
    public const int DescriptionSize = 4 + (DescriptionChars * 2 * 2);

    /// <summary>
    /// [S-1020] Who takes a drop. A widget takes it only outside edit mode and only when its view
    /// accepts drops at all (Folder, Shortcuts); over any other widget, or over the surface's own
    /// controls, a drag is refused rather than passed to whatever lies behind — the user is pointing
    /// at the widget, and a file landing on the Desktop behind a clock would be a surprise. Bare
    /// surface (reachable only while editing) is the desktop, so it goes where the desktop would put it:
    /// the Desktop folder. A drag that carries nothing droppable is refused everywhere.
    /// </summary>
    public static DropRoute Route(SurfaceHit hit, bool editMode, bool widgetTakesDrops, DropContent content)
    {
        if (content == DropContent.None)
        {
            return DropRoute.Refuse;
        }

        return hit switch
        {
            SurfaceHit.Widget => !editMode && widgetTakesDrops ? DropRoute.Widget : DropRoute.Refuse,
            SurfaceHit.Empty => DropRoute.Desktop,
            _ => DropRoute.Refuse,
        };
    }

    /// <summary>
    /// [S-1023] The key state that makes the shell's drop target do <paramref name="forced"/>: Shift
    /// moves, Ctrl copies, Ctrl+Shift links — Explorer's own modifiers, so the shell chooses what our
    /// rule chose. The mouse buttons are kept (a right-button drag still gets the shell's
    /// "Move here / Copy here" menu); Alt is dropped (it would mean link). <see cref="DropEffect.None"/>
    /// passes the user's keys through unchanged.
    /// </summary>
    public static uint KeysFor(DropEffect forced, uint keys)
    {
        var buttons = keys & ~(KeyShift | KeyControl | KeyAlt);

        return forced switch
        {
            DropEffect.Move => buttons | KeyShift,
            DropEffect.Copy => buttons | KeyControl,
            DropEffect.Link => buttons | KeyControl | KeyShift,
            _ => keys,
        };
    }

    /// <summary>
    /// [S-1024] The caption for <paramref name="effect"/> into <paramref name="place"/>, in the shell's
    /// own words ("Move to %1", "Copy to %1", "Create link in %1").
    /// </summary>
    public static DropFeedback Feedback(DropEffect effect, string place) => Feedback(Strings.Current, effect, place);

    /// <summary><see cref="Feedback(DropEffect, string)"/> in <paramref name="locale"/> (%1 is the shell's own insert mark).</summary>
    public static DropFeedback Feedback(Locale locale, DropEffect effect, string place)
    {
        ArgumentNullException.ThrowIfNull(place);

        return effect switch
        {
            DropEffect.Move => new(DropEffect.Move, Strings.Get(locale, Str.Shell_Drop_MoveTo), place),
            DropEffect.Copy => new(DropEffect.Copy, Strings.Get(locale, Str.Shell_Drop_CopyTo), place),
            DropEffect.Link => new(DropEffect.Link, Strings.Get(locale, Str.Shell_Drop_LinkIn), place),
            _ => DropFeedback.Refused,
        };
    }

    /// <summary>
    /// [S-1024] The one effect out of what the target answered: a target may answer a set (Copy|Move),
    /// and the drag shows the strongest the user asked for — Move, then Copy, then Link.
    /// </summary>
    public static DropEffect Single(DropEffect effect)
        => effect.HasFlag(DropEffect.Move) ? DropEffect.Move
            : effect.HasFlag(DropEffect.Copy) ? DropEffect.Copy
            : effect.HasFlag(DropEffect.Link) ? DropEffect.Link
            : DropEffect.None;

    /// <summary>
    /// [S-1024] A <c>DROPDESCRIPTION</c>: <c>DROPIMAGETYPE</c> then the message and the insert, each a
    /// NUL-terminated WCHAR[260]. Longer strings are cut to fit (the shell would refuse the whole
    /// description otherwise), never across a surrogate pair.
    /// </summary>
    public static byte[] EncodeDescription(DropImage image, string message, string insert)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(insert);

        var bytes = new byte[DescriptionSize];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, (int)image);
        Write(bytes.AsSpan(4, DescriptionChars * 2), message);
        Write(bytes.AsSpan(4 + (DescriptionChars * 2), DescriptionChars * 2), insert);
        return bytes;
    }

    private static void Write(Span<byte> field, string text)
    {
        var fit = Fit(text, DescriptionChars - 1);
        Encoding.Unicode.GetBytes(fit, field);
    }

    private static string Fit(string text, int chars)
    {
        if (text.Length <= chars)
        {
            return text;
        }

        var cut = char.IsHighSurrogate(text[chars - 1]) ? chars - 1 : chars;
        return text[..cut];
    }

    /// <summary>
    /// [S-1025] The file paths in dropped text: the <c>file:///</c> lines Chromium-based apps (VS Code)
    /// offer when their <c>CF_HDROP</c> cannot be read. Percent escapes are decoded as UTF-8 bytes
    /// (<c>M%C3%BCller</c> is <c>Müller</c>, not Latin-1 mojibake), slashes turned round, and a line
    /// that does not end up as an absolute local or UNC path is skipped. Untrusted text: never throws.
    /// </summary>
    public static IReadOnlyList<string> FileUrls(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        var paths = new List<string>();

        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();

            if (!line.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var rest = line["file://".Length..];

            // file:///C:/x is a local path; file://server/share/x is a UNC path.
            var path = rest.StartsWith('/')
                ? PercentDecode(rest[1..]).Replace('/', '\\')
                : @"\\" + PercentDecode(rest).Replace('/', '\\');

            if (IsAbsoluteWindowsPath(path) && !path.Contains('\0', StringComparison.Ordinal) && !paths.Contains(path, StringComparer.OrdinalIgnoreCase))
            {
                paths.Add(path);
            }
        }

        return paths;
    }

    /// <summary>
    /// [S-1025] A wide text payload (<c>CF_UNICODETEXT</c>, <c>UniformResourceLocatorW</c>) as a string:
    /// up to its first NUL, or the whole of an unterminated one; an odd trailing byte is ignored.
    /// </summary>
    public static string DecodeText(ReadOnlySpan<byte> bytes)
    {
        var even = bytes[..(bytes.Length & ~1)];

        for (var i = 0; i + 1 < even.Length; i += 2)
        {
            if (even[i] == 0 && even[i + 1] == 0)
            {
                return Encoding.Unicode.GetString(even[..i]);
            }
        }

        return Encoding.Unicode.GetString(even);
    }

    private static bool IsAbsoluteWindowsPath(string path)
    {
        if (path.Length >= 3 && char.IsAsciiLetter(path[0]) && path[1] == ':' && path[2] == '\\')
        {
            return true;
        }

        // \\server\share: a server name and something after it.
        return path.StartsWith(@"\\", StringComparison.Ordinal) && path.Length > 2 && path[2] != '\\' && path.IndexOf('\\', 2) > 2;
    }

    /// <summary>
    /// Percent-decoding as bytes: each well-formed <c>%XX</c> contributes its byte, every other
    /// character its own UTF-8, and the whole buffer is read as UTF-8 once. A malformed escape is kept
    /// as written.
    /// </summary>
    public static string PercentDecode(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (!text.Contains('%', StringComparison.Ordinal))
        {
            return text;
        }

        var bytes = new List<byte>(text.Length);
        Span<byte> one = stackalloc byte[4];

        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '%' && i + 2 < text.Length && IsHex(text[i + 1]) && IsHex(text[i + 2]))
            {
                bytes.Add((byte)((Hex(text[i + 1]) << 4) | Hex(text[i + 2])));
                i += 2;
                continue;
            }

            var pair = char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]);
            var length = Encoding.UTF8.GetBytes(text.AsSpan(i, pair ? 2 : 1), one);

            if (pair)
            {
                i++;
            }

            for (var b = 0; b < length; b++)
            {
                bytes.Add(one[b]);
            }
        }

        return Encoding.UTF8.GetString([.. bytes]);
    }

    private static bool IsHex(char c) => char.IsAsciiHexDigit(c);

    private static int Hex(char c) => c <= '9' ? c - '0' : (char.ToLowerInvariant(c) - 'a') + 10;
}
