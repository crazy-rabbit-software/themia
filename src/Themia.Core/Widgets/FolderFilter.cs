namespace Themia.Core.Widgets;

/// <summary>
/// [S-1321] The header search (typed at run time), ported from the
/// original's <c>fileFilter.ts</c>: comma-separated keywords, OR semantics, case-insensitive
/// substring match. Pure, so the matching is tested without a disk.
/// </summary>
public static class FolderFilter
{
    /// <summary>
    /// The keywords in a raw filter string: split on commas, trimmed, lower-cased, empties (a trailing
    /// comma, a lone space) dropped. No keywords means no filter.
    /// </summary>
    public static IReadOnlyList<string> Keywords(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        return
        [
            .. raw.Split(',')
                .Select(token => token.Trim().ToLowerInvariant())
                .Where(token => token.Length > 0),
        ];
    }

    /// <summary>Whether <paramref name="name"/> contains at least one keyword; everything matches no keywords.</summary>
    public static bool Matches(string name, IReadOnlyList<string> keywords)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(keywords);

        if (keywords.Count == 0)
        {
            return true;
        }

        foreach (var keyword in keywords)
        {
            if (name.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The entries the header search keeps. [S-4121] The search is the only filter: the stored
    /// keyword filter setting is gone.
    /// </summary>
    public static IReadOnlyList<FolderEntry> Apply(IReadOnlyList<FolderEntry> entries, string? search)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var typed = Keywords(search);

        if (typed.Count == 0)
        {
            return entries;
        }

        return [.. entries.Where(e => Matches(e.Name, typed))];
    }
}
