namespace Themia.Sdk.Services;

/// <summary>[S-9906] How a query's next page is found (per query: one service mixes them).</summary>
public enum PagingKind
{
    /// <summary>One answer, no pages.</summary>
    None,

    /// <summary>RFC 8288 <c>Link: &lt;…&gt;; rel="next"</c>, followed only on the first page's https origin.</summary>
    LinkHeader,

    /// <summary>A cursor the map returns (Relay's <c>endCursor</c> while <c>hasNextPage</c>, a <c>next_cursor</c> until null).</summary>
    BodyCursor,

    /// <summary>As <see cref="BodyCursor"/>, the cursor named a page token by the query.</summary>
    PageToken,

    /// <summary><c>offset += limit</c> until a short page.</summary>
    OffsetLimit,
}

/// <summary>[S-9906] The <c>Link</c> header's next page.</summary>
public static class LinkPaging
{
    /// <summary>
    /// The <c>rel="next"</c> address of a <c>Link</c> header, or null: an absolute https address on the
    /// same origin as <paramref name="requested"/> (the token goes with it), anything else refused.
    /// </summary>
    public static Uri? Next(string? link, Uri requested)
    {
        ArgumentNullException.ThrowIfNull(requested);

        if (string.IsNullOrWhiteSpace(link))
        {
            return null;
        }

        foreach (var part in SplitLinks(link))
        {
            var open = part.IndexOf('<', StringComparison.Ordinal);
            var close = part.IndexOf('>', StringComparison.Ordinal);
            if (open < 0 || close <= open)
            {
                continue;
            }

            var parameters = part[(close + 1)..].Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var isNext = parameters.Any(p =>
            {
                var equals = p.IndexOf('=', StringComparison.Ordinal);
                return equals > 0
                    && p[..equals].Trim().Equals("rel", StringComparison.OrdinalIgnoreCase)
                    && p[(equals + 1)..].Trim().Trim('"').Split(' ', StringSplitOptions.RemoveEmptyEntries).Contains("next", StringComparer.OrdinalIgnoreCase);
            });

            if (!isNext || !Uri.TryCreate(part[(open + 1)..close].Trim(), UriKind.Absolute, out var next))
            {
                continue;
            }

            return SameHttpsOrigin(requested, next) ? next : null;
        }

        return null;
    }

    /// <summary>Whether a page address may carry the first page's token: https and the same origin.</summary>
    public static bool SameHttpsOrigin(Uri first, Uri next)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(next);
        return next.IsAbsoluteUri && next.Scheme == Uri.UriSchemeHttps && first.Scheme == Uri.UriSchemeHttps
            && string.Equals(first.IdnHost, next.IdnHost, StringComparison.OrdinalIgnoreCase) && first.Port == next.Port
            && string.IsNullOrEmpty(next.UserInfo);
    }

    // Commas separate links, but an address may hold one too: split only outside <...>.
    private static IEnumerable<string> SplitLinks(string header)
    {
        var depth = 0;
        var start = 0;
        for (var i = 0; i < header.Length; i++)
        {
            switch (header[i])
            {
                case '<': depth++; break;
                case '>': depth = Math.Max(0, depth - 1); break;
                case ',' when depth == 0:
                    yield return header[start..i];
                    start = i + 1;
                    break;
            }
        }

        yield return header[start..];
    }
}

/// <summary>Why paging stopped.</summary>
public enum PageStop
{
    /// <summary>Not stopped: there is a next page to ask.</summary>
    More,

    /// <summary>The service said there is no next page.</summary>
    End,

    /// <summary>The widget has what it asked for.</summary>
    Enough,

    /// <summary>The descriptor's page or item bound.</summary>
    Bound,

    /// <summary>An empty page.</summary>
    EmptyPage,

    /// <summary>A cursor or address seen before: the service loops, a bad response, never an endless fetch.</summary>
    Loop,
}

/// <summary>
/// [S-9906] The one paging loop of every kit query: it stops at the widget's count, at the
/// descriptor's <c>MaxPages</c> and <c>ItemBound</c>, on an empty page, on the service's own end, and
/// on a cursor that repeats (a loop is a bad response). One instance per fetch.
/// </summary>
public sealed class ServicePager
{
    private readonly HashSet<string> _seen = new(StringComparer.Ordinal);

    public ServicePager(PagingKind paging, int wanted, int maxPages, int itemBound, int pageSize)
    {
        Paging = paging;
        Wanted = Math.Max(1, wanted);
        MaxPages = Math.Max(1, maxPages);
        ItemBound = Math.Max(1, itemBound);
        PageSize = Math.Max(1, pageSize);
    }

    public PagingKind Paging { get; }

    public int Wanted { get; }

    public int MaxPages { get; }

    public int ItemBound { get; }

    public int PageSize { get; }

    /// <summary>Pages taken so far.</summary>
    public int Pages { get; private set; }

    /// <summary>Items taken so far.</summary>
    public int Items { get; private set; }

    /// <summary>Why the last <see cref="Next"/> stopped, or <see cref="PageStop.More"/>.</summary>
    public PageStop Stop { get; private set; } = PageStop.More;

    /// <summary>
    /// After a page: where the next one starts, or null with <see cref="Stop"/> saying why not.
    /// </summary>
    /// <param name="requested">The page's own address (the Link header's next is checked against it).</param>
    /// <param name="link">The reply's <c>Link</c> header.</param>
    /// <param name="chunk">The page as the rules mapped it.</param>
    public PageAt? Next(Uri requested, string? link, KitChunk chunk)
    {
        ArgumentNullException.ThrowIfNull(requested);
        ArgumentNullException.ThrowIfNull(chunk);

        Pages++;
        Items += chunk.Items.Count;

        if (Paging == PagingKind.None)
        {
            return Stopped(PageStop.End);
        }

        if (chunk.Items.Count == 0)
        {
            return Stopped(PageStop.EmptyPage);
        }

        if (Items >= Wanted)
        {
            return Stopped(PageStop.Enough);
        }

        if (Pages >= MaxPages || Items >= ItemBound)
        {
            return Stopped(PageStop.Bound);
        }

        PageAt? next = Paging switch
        {
            PagingKind.LinkHeader => LinkPaging.Next(link, requested) is { } address ? new PageAt.Address(address) : null,
            PagingKind.BodyCursor or PagingKind.PageToken => chunk.NextCursor is { Length: > 0 } cursor ? new PageAt.Cursor(cursor) : null,
            PagingKind.OffsetLimit => chunk.Items.Count < PageSize ? null : new PageAt.Offset(Items),
            _ => null,
        };

        if (next is null)
        {
            return Stopped(PageStop.End);
        }

        var mark = next switch
        {
            PageAt.Address a => "a:" + a.Next.AbsoluteUri,
            PageAt.Cursor c => "c:" + c.Value,
            PageAt.Offset o => "o:" + o.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
            _ => string.Empty,
        };

        if (!_seen.Add(mark) || (next is PageAt.Address again && again.Next == requested))
        {
            return Stopped(PageStop.Loop);
        }

        Stop = PageStop.More;
        return next;
    }

    private PageAt? Stopped(PageStop why)
    {
        Stop = why;
        return null;
    }
}

/// <summary>[S-9908] When an entry is next asked.</summary>
public static class ServicePolling
{
    /// <summary>
    /// The next ask after a fetch at <paramref name="fetchedAt"/>: the user's cadence, never below the
    /// service's idle floor nor (for anonymous reads) its anonymous floor, and never before a block on
    /// the entry's bucket ends.
    /// </summary>
    public static DateTimeOffset NextPoll(DateTimeOffset fetchedAt, TimeSpan cadence, TimeSpan idleFloor, DateTimeOffset? blockedUntil, TimeSpan? anonymousFloor = null)
    {
        var wait = cadence;
        if (wait < idleFloor)
        {
            wait = idleFloor;
        }

        if (anonymousFloor is { } floor && wait < floor)
        {
            wait = floor;
        }

        var next = fetchedAt + wait;
        return blockedUntil is { } until && until > next ? until : next;
    }
}
