namespace Themia.Sdk.WinUI;

/// <summary>
/// [S-1304] A site's own icon (<see cref="IWidgetHost.Favicons"/>): the host's one cache, shared by the
/// feeds and the shortcuts' web tiles, fetched once per site and never from a third-party service.
/// </summary>
public interface IFavicons
{
    /// <summary>The icon's bytes for the site <paramref name="link"/> belongs to (an image by its magic number), or null when it has none.</summary>
    Task<byte[]?> GetAsync(string link);
}
