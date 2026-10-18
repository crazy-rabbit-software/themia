namespace Themia.Core.Widgets;

/// <summary>[S-1304] A site's own icon, as the feed rows and the shortcuts' web tiles both ask for it.</summary>
public static class SiteIcons
{
    /// <summary>
    /// [S-1304] Where a site's own icon lives: <c>/favicon.ico</c> at the root of the link's origin
    /// (scheme, host and port, nothing else), or null for a link that has none. No third-party icon
    /// service: asking one would tell it every site the user reads.
    /// </summary>
    public static Uri? FaviconOf(string? link)
    {
        if (string.IsNullOrEmpty(link) || !Uri.TryCreate(link, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || string.IsNullOrEmpty(uri.Host) || !string.IsNullOrEmpty(uri.UserInfo))
        {
            return null;
        }

        return new UriBuilder(uri.Scheme, uri.Host, uri.Port, "/favicon.ico").Uri;
    }

    /// <summary>
    /// [S-1304] Whether bytes served as a favicon are a bitmap the image decoder should be handed:
    /// ICO, PNG, GIF, JPEG or BMP by their magic numbers. An HTML "not found" page served with a 200,
    /// or an SVG (scriptable), is not.
    /// </summary>
    public static bool LooksLikeIcon(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 6)
        {
            return false;
        }

        return (bytes[0] == 0x00 && bytes[1] == 0x00 && bytes[2] == 0x01 && bytes[3] == 0x00)        // ICO
            || (bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)        // PNG
            || (bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x38)        // GIF8
            || (bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)                            // JPEG
            || (bytes[0] == 0x42 && bytes[1] == 0x4D);                                                // BMP
    }
}
