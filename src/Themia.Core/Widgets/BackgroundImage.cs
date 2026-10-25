using Themia.Core.Model;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-6590]..[S-6593] A widget's own background image: a picture file on this PC that covers the
/// card's body behind its content. The path is the user's data: a file that is gone is drawn as no
/// image and logged, never forgotten, so a drive plugged back in brings it back.
/// </summary>
public static class BackgroundImage
{
    /// <summary>[S-6591] The longest path kept; a longer one is no file Windows opens.</summary>
    public const int MaxPathLength = 1024;

    /// <summary>[S-6591] The picture types the card can draw (Windows' own decoders), lower case.</summary>
    public static IReadOnlyList<string> Extensions { get; } = [".png", ".jpg", ".jpeg", ".jfif", ".bmp", ".gif", ".tif", ".tiff", ".webp"];

    /// <summary>
    /// [S-6591] Whether <paramref name="path"/> can name a background image: a full local or network
    /// path (never relative: what it is relative to is not stable), no longer than
    /// <see cref="MaxPathLength"/>, without characters a path cannot hold, of a type in
    /// <see cref="Extensions"/>. Whether the file exists is not asked here.
    /// </summary>
    public static bool IsImagePath(string? path)
    {
        var trimmed = path?.Trim() ?? string.Empty;
        return trimmed.Length > 0
            && trimmed.Length <= MaxPathLength
            && trimmed.IndexOfAny(Path.GetInvalidPathChars()) < 0
            && Path.IsPathFullyQualified(trimmed)
            && Extensions.Contains(Path.GetExtension(trimmed).ToLowerInvariant());
    }

    /// <summary>
    /// [S-6591] A loaded path brought inside its rule: empty stays empty (no image), one that cannot
    /// name a picture becomes empty, loudly. Returns the path to keep and the repair, if any.
    /// </summary>
    public static (string Path, string? Repair) Repair(string? path)
    {
        var trimmed = path?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            return (string.Empty, null);
        }

        return IsImagePath(trimmed)
            ? (trimmed, null)
            : (string.Empty, $"background image '{Shorten(trimmed)}' is not a picture file's full path; no image");
    }

    /// <summary>
    /// [S-6592] What the card's body fill paints over an image: a colour of the widget's own lies over
    /// it at its opacity, a tint that keeps text readable; Default and Transparent paint nothing, so
    /// the picture is the card. Without an image the fill is drawn as it is.
    /// </summary>
    public static CardFill Over(CardFill fill, bool hasImage)
        => hasImage && fill.Kind != FillKind.Colour ? new CardFill(FillKind.Transparent, null, 0) : fill;

    /// <summary>The file's name, for the menu line that shows the image ([S-6593]); empty for none.</summary>
    public static string Name(string? path)
    {
        var trimmed = path?.Trim() ?? string.Empty;
        return trimmed.Length == 0 ? string.Empty : Path.GetFileName(trimmed);
    }

    private static string Shorten(string value) => value.Length <= 80 ? value : value[..77] + "...";
}
