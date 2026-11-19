using System.Text.Json.Serialization;

namespace Themia.Sdk;

/// <summary>
/// [S-13022] What a fence shows (the owner, 2026-11-19: "also allow to create fences that show items of some kind, also give
/// the option to show items of some kind from anywhere from the drive. (query style)").
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<FenceSource>))]
public enum FenceSource
{
    /// <summary>The desktop items put into it (a drop, its rules for new items, a fence round a selection): Fences' own.</summary>
    Held,

    /// <summary>[S-13022] Every desktop item its rules match, now and later: the fence collects them from the free desktop.</summary>
    Desktop,

    /// <summary>[S-13023] Files its rules match anywhere in a folder or drive, found by Windows Search: views of the files, it holds none.</summary>
    Search,
}

/// <summary>[S-13023] How recently a found file was changed.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<FenceRecency>))]
public enum FenceRecency
{
    Any,
    Day,
    Week,
    Month,
    Year,
}

/// <summary>
/// [S-13023] A search fence's query beside its rules (<see cref="FenceRuleSet"/>: the kinds of file and a name's text): where
/// to look (a folder or a drive; empty for the user's own folder, where Windows indexes their files), how recent, and at
/// most how many. Stored in the fence's config.
/// </summary>
public sealed class FenceQuery
{
    /// <summary>The folder or drive searched (and every folder under it); empty for the user's own folder.</summary>
    public string Folder { get; set; } = string.Empty;

    /// <summary>How recently a file was changed to be shown.</summary>
    public FenceRecency Modified { get; set; } = FenceRecency.Any;

    /// <summary>At most this many files, the newest first.</summary>
    public int Max { get; set; } = FenceSearch.DefaultMax;

    public FenceQuery Clone() => new() { Folder = Folder, Modified = Modified, Max = Max };
}

/// <summary>[S-13023] One file a search fence found: its path, the name it shows, whether it is a folder, when it was changed (UTC).</summary>
public sealed record FoundFile(string Path, string Name, bool IsFolder, DateTime? ModifiedUtc);

/// <summary>
/// [S-13023] A search fence's query as pure functions: the Windows Search query its rules and query make (Advanced Query
/// Syntax, which the shell's search folder answers from the system index where the place is indexed), the same rules
/// checked on what came back (the index's answer is never trusted to be exact), and a stored query made whole.
/// </summary>
public static class FenceSearch
{
    /// <summary>How many files a new search fence shows.</summary>
    public const int DefaultMax = 60;

    /// <summary>The most a search fence shows: more is a folder's job, not a fence's.</summary>
    public const int MostResults = 500;

    /// <summary>The longest folder path kept.</summary>
    public const int MaxFolder = 260;

    /// <summary>The oldest change a file may have under <paramref name="recency"/> (UTC), or null for any.</summary>
    public static DateTime? Since(FenceRecency recency, DateTime nowUtc) => recency switch
    {
        FenceRecency.Day => nowUtc.AddDays(-1),
        FenceRecency.Week => nowUtc.AddDays(-7),
        FenceRecency.Month => nowUtc.AddMonths(-1),
        FenceRecency.Year => nowUtc.AddYears(-1),
        _ => null,
    };

    /// <summary>
    /// The Advanced Query Syntax for <paramref name="rules"/> and <paramref name="query"/>: any of the kinds (their
    /// extensions, <c>.lnk</c>/<c>.url</c> for shortcuts, <c>kind:folder</c>) or the name's text, and, when set, the
    /// change no older than the recency. Empty when no rule is set (a fence that would show every file shows none).
    /// </summary>
    public static string Aqs(FenceRuleSet rules, FenceQuery query, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(query);
        var any = new List<string>();
        foreach (var type in rules.Types)
        {
            switch (type)
            {
                case FenceFileType.Folders:
                    any.Add("kind:folder");
                    break;
                case FenceFileType.Shortcuts:
                    any.Add("ext:.lnk");
                    any.Add("ext:.url");
                    break;
                default:
                    any.AddRange(FenceRules.ExtensionsOf(type).Select(e => "ext:" + e));
                    break;
            }
        }

        any.AddRange(rules.Extensions.Select(e => "ext:" + e));
        if (rules.NameContains.Length > 0)
        {
            any.Add("System.FileName:~~\"" + rules.NameContains.Replace("\"", string.Empty, StringComparison.Ordinal) + "\"");
        }

        any = [.. any.Distinct(StringComparer.OrdinalIgnoreCase)];
        if (any.Count == 0)
        {
            return string.Empty;
        }

        var aqs = any.Count == 1 ? any[0] : "(" + string.Join(" OR ", any) + ")";
        if (Since(query.Modified, nowUtc) is { } since)
        {
            aqs += " System.DateModified:>=" + since.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        }

        return aqs;
    }

    /// <summary>
    /// Whether a found <paramref name="file"/> is one the fence shows: its rules match it (<see cref="FenceRules.Matches"/>,
    /// as a desktop item of its kind) and it was changed within the recency.
    /// </summary>
    public static bool Matches(FenceRuleSet rules, FenceQuery query, FoundFile file, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(file);
        var extension = FenceRules.ExtensionOfKey(file.Path);
        var kind = file.IsFolder ? DesktopItemKind.Folder
            : extension is ".lnk" or ".url" ? DesktopItemKind.Shortcut
            : DesktopItemKind.File;
        if (!FenceRules.Matches(rules, new DesktopItem(file.Path, file.Name, kind)))
        {
            return false;
        }

        return Since(query.Modified, nowUtc) is not { } since || (file.ModifiedUtc is { } changed && changed >= since);
    }

    /// <summary>
    /// What a search shows of <paramref name="found"/>: the ones that match, each path once, the newest first (no date
    /// last), at most the query's <see cref="FenceQuery.Max"/>.
    /// </summary>
    public static IReadOnlyList<FoundFile> Shown(FenceRuleSet rules, FenceQuery query, IEnumerable<FoundFile> found, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(found);
        return [.. found
            .Where(f => Matches(rules, query, f, nowUtc))
            .DistinctBy(f => f.Path, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(f => f.ModifiedUtc ?? DateTime.MinValue)
            .ThenBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Clamp(query.Max, 1, MostResults))];
    }

    /// <summary>
    /// A stored query made whole: a folder that is no absolute path of this PC (or too long) is empty (every indexed
    /// place), an unknown recency is Any, the count is kept within 1..<see cref="MostResults"/>. Each repair is said.
    /// </summary>
    public static IReadOnlyList<string> Repair(FenceQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        var repairs = new List<string>();
        var folder = (query.Folder ?? string.Empty).Trim();
        if (folder.Length > 0 && (folder.Length > MaxFolder || !Path.IsPathFullyQualified(folder) || folder.IndexOfAny(Path.GetInvalidPathChars()) >= 0))
        {
            repairs.Add("a search fence's folder was not a full path; it searches every indexed place");
            folder = string.Empty;
        }

        query.Folder = folder;
        if (!Enum.IsDefined(query.Modified))
        {
            query.Modified = FenceRecency.Any;
            repairs.Add("a search fence had an unknown recency; any");
        }

        if (query.Max is < 1 or > MostResults)
        {
            query.Max = Math.Clamp(query.Max, 1, MostResults);
            repairs.Add("a search fence's count was out of range; clamped");
        }

        return repairs;
    }
}
