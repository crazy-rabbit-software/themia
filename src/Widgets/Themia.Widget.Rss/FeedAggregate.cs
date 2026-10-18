using Themia.Core.Localization;
using Themia.Sdk.Kit;
namespace Themia.Core.Widgets;

/// <summary>One feed's part of a refresh: what it returned, or why it did not.</summary>
/// <param name="Url">The checked address.</param>
/// <param name="Document">The parsed feed, or null when this feed failed and nothing was held for it.</param>
/// <param name="Status">How the last attempt for this feed went.</param>
/// <param name="Detail">The failure, for the tooltip ("HTTP 403 Forbidden"), or null.</param>
/// <param name="HeldAt">When the held document was fetched, or null when none is held.</param>
public sealed record FeedPart(string Url, FeedDocument? Document, FetchStatus Status, string? Detail, DateTimeOffset? HeldAt = null);

/// <summary>The merged list a feed widget draws.</summary>
/// <param name="Title">The header title the feeds themselves suggest, or empty for the catalogue name.</param>
/// <param name="Entries">Every item, newest first, undated last, capped.</param>
public sealed record MergedFeed(string Title, IReadOnlyList<FeedEntry> Entries);

/// <summary>
/// [S-1302] Several feeds into one list, as the original's <c>aggregateFeeds</c> (S-30/S-33/S-34).
/// <para>
/// Newest first across all feeds; an undated item sinks below every dated one (and undated items
/// keep their feeds' own order); the combined list is capped. A feed that failed while others
/// answered is a <i>partial</i> refresh: its outcome (<see cref="Outcomes"/>) is named after the
/// feed, so the header can say which one is broken instead of presenting a short list as complete.
/// </para>
/// </summary>
public static class FeedAggregate
{
    public static MergedFeed Merge(IReadOnlyList<FeedPart> parts, int cap)
    {
        ArgumentNullException.ThrowIfNull(parts);

        var merged = new List<(FeedEntry Entry, int Order)>();
        var order = 0;

        foreach (var part in parts)
        {
            foreach (var entry in part.Document?.Entries ?? [])
            {
                merged.Add((entry, order++));
            }
        }

        // Stable: equal dates and undated items keep the order they arrived in.
        merged.Sort(static (a, b) =>
        {
            var byDate = (a.Entry.Published, b.Entry.Published) switch
            {
                ({ } x, { } y) => y.CompareTo(x),
                (null, { }) => 1,
                ({ }, null) => -1,
                _ => 0,
            };

            return byDate != 0 ? byDate : a.Order.CompareTo(b.Order);
        });

        var entries = merged.Take(Math.Max(1, cap)).Select(pair => pair.Entry).ToList();
        return new MergedFeed(Title(parts), entries);
    }

    /// <summary>[S-1302] [S-7040] How each feed last went, named after it, in the order the user listed them.</summary>
    public static IReadOnlyList<KitOutcome> Outcomes(IReadOnlyList<FeedPart> parts)
    {
        ArgumentNullException.ThrowIfNull(parts);
        return [.. parts.Select(part => new KitOutcome(Name(part), part.Status, part.Detail, part.Document is null ? null : part.HeldAt))];
    }

    /// <summary>
    /// The title the feeds suggest (S-04): one feed's own <c>&lt;title&gt;</c>; several feeds'
    /// names joined with ", " so selecting two never collapses the header; empty when there is no
    /// feed or nothing named.
    /// </summary>
    public static string Title(IReadOnlyList<FeedPart> parts)
    {
        ArgumentNullException.ThrowIfNull(parts);

        if (parts.Count == 1)
        {
            return parts[0].Document?.Title.Trim() ?? string.Empty;
        }

        return string.Join(", ", parts.Select(Name).Where(name => name.Length > 0));
    }

    /// <summary>A feed's name: its own title, else its host without "www.".</summary>
    public static string Name(FeedPart part)
    {
        ArgumentNullException.ThrowIfNull(part);

        var title = part.Document?.Title.Trim();
        return !string.IsNullOrEmpty(title) ? title : SiteHost(part.Url);
    }

    /// <summary>
    /// The site a link belongs to, as a bare host name ("bbc.co.uk"), or empty. Parsed, never
    /// pattern-matched, so what reaches the screen is <see cref="Uri.Host"/> and nothing a feed
    /// author wrote by hand.
    /// </summary>
    public static string SiteHost(string? link)
    {
        if (string.IsNullOrEmpty(link) || !Uri.TryCreate(link, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return string.Empty;
        }

        var host = uri.Host;
        return host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? host[4..] : host;
    }
}
