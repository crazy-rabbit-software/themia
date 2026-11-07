using System.Globalization;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.Widget.Shortcuts;

/// <summary>[S-1418] Where a shortcut's drawn icon comes from, best first.</summary>
public enum ShortcutIconSource
{
    /// <summary>The user's own picture (<see cref="ShortcutItem.IconPicture"/>), while its file is there.</summary>
    Picture,

    /// <summary>The user's own emoji (<see cref="ShortcutItem.IconEmoji"/>).</summary>
    Emoji,

    /// <summary>The icon the dropped <c>.url</c> named (<see cref="ShortcutItem.IconPath"/>, [S-1401]).</summary>
    Named,

    /// <summary>
    /// The target's own icon as Explorer draws it: a <c>.lnk</c>'s Change Icon… choice, a folder's
    /// <c>desktop.ini</c> icon and an app's icon included, a web address's site icon ([S-1383]).
    /// </summary>
    Automatic,
}

/// <summary>
/// [S-1417] [S-1418] [S-1419] A shortcut's own icon (issue 157): a picture file or one emoji the user
/// chose, over the icon Windows gives the target. Both come from the properties menu and from a JSON
/// file anything running as the user can rewrite, so both are checked here, on the way in and on load.
/// </summary>
public static class ShortcutIcons
{
    /// <summary>
    /// The picture files a cell draws as a picture: the bitmaps XAML decodes, <c>.ico</c> (through the
    /// shell, which picks the icon's best size) and <c>.svg</c>. Anything else is refused, never guessed at.
    /// </summary>
    public static IReadOnlyList<string> PictureExtensions { get; } = [".png", ".jpg", ".jpeg", ".bmp", ".gif", ".ico", ".svg"];

    /// <summary>A picture past this is not read: an icon is drawn at 96 pixels at most.</summary>
    public const long MaxPictureBytes = 16L * 1024 * 1024;

    /// <summary>
    /// UTF-16 units one emoji may take: the longest standard sequences (a family or a kiss with skin
    /// tones, a subdivision flag) are under 16; anything past this is text, not an icon.
    /// </summary>
    public const int MaxEmojiLength = 32;

    /// <summary>Whether <paramref name="path"/> may be a shortcut's picture: a local, probe-able file of a picture type.</summary>
    public static bool IsPicture(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var target = LaunchTargets.Classify(path);
        if (target.Kind != LaunchKind.LocalPath || !LaunchTargets.MayProbe(target))
        {
            return false;
        }

        var extension = Path.GetExtension(target.Value);
        return PictureExtensions.Any(allowed => string.Equals(allowed, extension, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Whether the picture is drawn through the shell's icon (an <c>.ico</c>) rather than decoded as a picture.</summary>
    public static bool IsIconFile(string path) => string.Equals(Path.GetExtension(path), ".ico", StringComparison.OrdinalIgnoreCase);

    /// <summary>Whether the picture is a vector drawing (<c>.svg</c>).</summary>
    public static bool IsSvg(string path) => string.Equals(Path.GetExtension(path), ".svg", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The typed emoji as it is kept, or null when it is not one: surrounding spaces go, then exactly one
    /// grapheme (an emoji with its skin tone, a ZWJ family, a flag, or one character) must be left, with
    /// no control or format character standing alone and no more than <see cref="MaxEmojiLength"/> units.
    /// </summary>
    public static string? Emoji(string? typed)
    {
        var trimmed = typed?.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.Length > MaxEmojiLength)
        {
            return null;
        }

        if (new StringInfo(trimmed).LengthInTextElements != 1)
        {
            return null;
        }

        // A lone control, format (a bare ZWJ, a direction mark), separator or unpaired surrogate is no picture.
        var first = CharUnicodeInfo.GetUnicodeCategory(trimmed, 0);
        if (first is UnicodeCategory.Control or UnicodeCategory.Format or UnicodeCategory.Surrogate
            or UnicodeCategory.SpaceSeparator or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator
            or UnicodeCategory.OtherNotAssigned or UnicodeCategory.PrivateUse)
        {
            return null;
        }

        return trimmed.Any(c => char.IsControl(c)) ? null : trimmed;
    }

    /// <summary>
    /// [S-1418] Which icon the cell draws: the user's picture while <paramref name="pictureReadable"/>
    /// (its file is there and decodes), else the user's emoji, else the icon a <c>.url</c> named, else
    /// the target's own (Windows' choice for a link or a folder).
    /// </summary>
    public static ShortcutIconSource Choose(ShortcutItem item, bool pictureReadable)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (item.IconPicture is not null && pictureReadable)
        {
            return ShortcutIconSource.Picture;
        }

        if (item.IconEmoji is not null)
        {
            return ShortcutIconSource.Emoji;
        }

        return item.IconPath is not null ? ShortcutIconSource.Named : ShortcutIconSource.Automatic;
    }

    /// <summary>Whether the user chose an icon of their own (so Reset has something to undo).</summary>
    public static bool HasOwn(ShortcutItem item) => item.IconPicture is not null || item.IconEmoji is not null;

    /// <summary>[S-1417] The user's picture, replacing an emoji. False (nothing changed) when it is refused or already set.</summary>
    public static bool SetPicture(ShortcutItem item, string path)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (!IsPicture(path) || (item.IconPicture == path && item.IconEmoji is null))
        {
            return false;
        }

        item.IconPicture = path;
        item.IconEmoji = null;
        return true;
    }

    /// <summary>[S-1417] The user's emoji, replacing a picture. False when it is not one emoji or already set.</summary>
    public static bool SetEmoji(ShortcutItem item, string typed)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (Emoji(typed) is not { } emoji || (item.IconEmoji == emoji && item.IconPicture is null))
        {
            return false;
        }

        item.IconEmoji = emoji;
        item.IconPicture = null;
        return true;
    }

    /// <summary>[S-1417] Back to the automatic icon. The <c>.url</c>'s own icon is Windows', so it stays. False when there was nothing to reset.</summary>
    public static bool Reset(ShortcutItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (!HasOwn(item))
        {
            return false;
        }

        item.IconPicture = null;
        item.IconEmoji = null;
        return true;
    }

    /// <summary>[S-1419] Load repair of one item's own icon: a refused picture or emoji goes, and of both the picture stays.</summary>
    public static void Repair(ShortcutItem item, List<string> repairs)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(repairs);

        if (item.IconPicture is { } picture && !IsPicture(picture))
        {
            item.IconPicture = null;
            repairs.Add("Shortcuts widget had a custom icon that is not a local picture file");
        }

        if (item.IconEmoji is { } emoji && Emoji(emoji) != emoji)
        {
            item.IconEmoji = null;
            repairs.Add("Shortcuts widget had a custom icon that is not one emoji");
        }

        if (item.IconPicture is not null && item.IconEmoji is not null)
        {
            item.IconEmoji = null;
            repairs.Add("Shortcuts widget had both a picture and an emoji as an icon");
        }
    }
}
