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
}
