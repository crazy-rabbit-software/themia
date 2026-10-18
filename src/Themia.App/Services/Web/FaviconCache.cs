using Themia.Core.Diagnostics;
using Themia.Core.Widgets;

namespace Themia.App.Services.Web;

/// <summary>
/// [S-1304] Site icons for feed items, fetched from the site itself: <c>https://host/favicon.ico</c>,
/// never a third-party icon service (asking one would tell it every site the user reads).
/// <para>
/// The bytes are shared app-wide, one request per origin per session however many rows and widgets
/// show that site; a view decodes them into its own image. The address comes from a stranger's feed,
/// so the request goes through the network guard ([S-1301]), is capped at 64 KB, and the reply must
/// look like an icon or bitmap (not an HTML error page, not an SVG). A site with no icon is
/// remembered as such for six hours, so a row does not ask again on every redraw.
/// </para>
/// </summary>
internal static class FaviconCache
{
    private const string Scope = "favicon";

    private const int MaxBytes = 64 * 1024;

    /// <summary>How many origins to remember. A desktop's feeds name a few dozen sites.</summary>
    private const int MaxOrigins = 256;

    private static readonly TimeSpan RetryMissingAfter = TimeSpan.FromHours(6);

    private static readonly Lock Gate = new();
    private static readonly Dictionary<string, Entry> Entries = [];

    private sealed record Entry(Task<byte[]?> Bytes, DateTimeOffset Started);

    /// <summary>The icon for the site <paramref name="link"/> belongs to, or null when it has none.</summary>
    public static Task<byte[]?> GetAsync(string link)
    {
        if (SiteIcons.FaviconOf(link) is not { } icon)
        {
            return Task.FromResult<byte[]?>(null);
        }

        var origin = icon.GetLeftPart(UriPartial.Authority);
        var now = DateTimeOffset.Now;

        lock (Gate)
        {
            if (Entries.TryGetValue(origin, out var held)
                && !(held.Bytes.IsCompletedSuccessfully && held.Bytes.Result is null && now - held.Started > RetryMissingAfter))
            {
                return held.Bytes;
            }

            if (Entries.Count >= MaxOrigins)
            {
                var oldest = Entries.MinBy(pair => pair.Value.Started).Key;
                Entries.Remove(oldest);
            }

            // Deliberately not cancellable by a caller: the answer is shared by every row on the site.
            var task = Task.Run(() => FetchAsync(icon));
            Entries[origin] = new Entry(task, now);
            return task;
        }
    }

    private static async Task<byte[]?> FetchAsync(Uri icon)
    {
        try
        {
            var (status, body, detail) = await WebFetch
                .GetBytesAsync(icon, "image/x-icon, image/png, image/*;q=0.8", MaxBytes, CancellationToken.None)
                .ConfigureAwait(false);

            if (status != FetchStatus.Ok || body is null)
            {
                Log.Debug(Scope, $"no icon at {icon.Host}: {FetchState.Message(status)} {detail}".TrimEnd());
                return null;
            }

            if (!FeedAggregate.LooksLikeIcon(body))
            {
                Log.Debug(Scope, $"{icon.Host}/favicon.ico is not an icon");
                return null;
            }

            return body;
        }
        catch (Exception ex)
        {
            // WebFetch reports network failures as statuses; anything thrown here is a bug, logged
            // rather than left to fault a task every row on the site awaits.
            Log.Error(Scope, "fetching a site icon threw", ex);
            return null;
        }
    }
}
