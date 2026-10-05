namespace Themia.Core.Widgets;

/// <summary>One application the Start menu knows about.</summary>
/// <param name="Name">What to call it: the shortcut's own name, without the extension.</param>
/// <param name="Category">The Start-menu folder it sits in, or empty at the top level.</param>
/// <param name="Target">The shortcut file. Handed to the shell unchanged, after being validated.</param>
public readonly record struct InstalledApp(string Name, string Category, string Target);

/// <summary>
/// The rules the app list follows, as pure functions of what was found on disk.
/// <para>
/// Kept out of the enumeration itself so the decisions that shape the list — what counts as an
/// application, which duplicate wins — can be tested without a Start
/// menu to point at. The enumeration is then only "walk these folders and call these".
/// </para>
/// </summary>
public static class InstalledAppRules
{
    /// <summary>The most entries the list will hold, whatever the Start menu contains.</summary>
    public const int MaxEntries = 1000;

    /// <summary>
    /// Start-menu folders that are not a category of applications.
    /// <para>
    /// <c>Startup</c> is the one that matters: its contents are things Windows runs at sign-in, and
    /// listing them as launchable apps is both wrong and confusing.
    /// </para>
    /// </summary>
    public static bool IsSkippedFolder(string? folderName)
        => folderName is not null && folderName.Equals("Startup", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Whether a shortcut is a launcher entry rather than one of the pieces of housekeeping that
    /// ship beside it. An "Uninstall Foo" tile one mis-click away on the desktop is a hazard, not a
    /// feature.
    /// </summary>
    public static bool IsLauncherShortcut(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        foreach (var excluded in Excluded)
        {
            if (name.Contains(excluded, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static readonly string[] Excluded = ["uninstall", "uninst", "remove "];

    /// <summary>
    /// Put the scan in order: one entry per name, sorted, capped.
    /// <para>
    /// The first of a duplicate name wins, and the caller scans the user's Start menu before the
    /// machine's, so a per-user install shadows the all-users one — which is the one the user
    /// actually has.
    /// </para>
    /// </summary>
    public static List<InstalledApp> Arrange(IEnumerable<InstalledApp> scanned, int maxCount)
    {
        ArgumentNullException.ThrowIfNull(scanned);

        var limit = Math.Clamp(maxCount, 1, MaxEntries);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var kept = new List<InstalledApp>();

        foreach (var app in scanned)
        {
            if (app.Name.Length == 0 || !seen.Add(app.Name))
            {
                continue;
            }

            kept.Add(app);
        }

        kept.Sort(static (a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

        if (kept.Count > limit)
        {
            kept.RemoveRange(limit, kept.Count - limit);
        }

        return kept;
    }
}
