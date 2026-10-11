using System.Text.RegularExpressions;
using Themia.Core.Accounts;
using Themia.Core.Storage;

namespace Themia.Core.Sync;

/// <summary>
/// [S-2665] [S-2666] The start-up sweep: shelves left behind by an account that is no longer signed in
/// (a crash between the sign-out's save and its purge, [S-2607]). The owners are hashed into the file
/// names ([S-2604]), so the sweep works the other way round: it hashes every owner that exists, and
/// what the folder holds beyond those names is an orphan.
/// </summary>
public static partial class ShelfSweep
{
    /// <summary>Account data kinds: every one of them is kept for every account that exists.</summary>
    private static readonly SyncKind[] AccountKinds = [SyncKind.Mail, SyncKind.Calendar, SyncKind.Contacts, SyncKind.Tasks];

    /// <summary>
    /// The web caches' shelves are shared by every widget of their kind (one owner each, entries
    /// bounded and aged out on load, [S-1300], [S-729]), so they are never orphans.
    /// </summary>
    private static readonly string[] WebOwners = [WebShelf.NetworkOwner, WebShelf.WeatherOwner];

    /// <summary>
    /// [S-2666] Whether the account list can be trusted to say who does not exist. Only a file that
    /// loaded whole: no file (perhaps unreachable, not gone), a file set aside as unreadable, or one
    /// repaired on load (an account dropped as damaged is not an account signed out) says nothing
    /// about which accounts are gone, and synced data is never deleted on a recoverable error.
    /// </summary>
    public static bool MayRun(AccountLoad load)
    {
        ArgumentNullException.ThrowIfNull(load);
        return load.Outcome == LoadOutcome.Loaded && load.Detail is null && load.Writable;
    }

    /// <summary>
    /// [S-2665] The files in the sync folder that are shelves (or set-aside shelves) of no owner in
    /// <paramref name="owners"/>. Anything that is not named like a shelf (the key, a temp file,
    /// something else) is never touched.
    /// </summary>
    public static IReadOnlyList<string> Orphans(IEnumerable<string> fileNames, IEnumerable<string> owners)
    {
        ArgumentNullException.ThrowIfNull(fileNames);
        ArgumentNullException.ThrowIfNull(owners);

        var kept = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var owner in owners)
        {
            foreach (var kind in AccountKinds)
            {
                kept.Add(SyncVault.ShelfName(kind, owner));
            }
        }

        foreach (var owner in WebOwners)
        {
            kept.Add(SyncVault.ShelfName(SyncKind.Web, owner));
        }

        var orphans = new List<string>();
        foreach (var name in fileNames)
        {
            var match = ShelfFile().Match(name);
            if (match.Success && !kept.Contains(match.Groups["shelf"].Value))
            {
                orphans.Add(name);
            }
        }

        return orphans;
    }

    /// <summary>The kind part of a shelf file name ("mail"), for a log line that names no owner.</summary>
    public static string KindOf(string fileName)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        var dash = fileName.IndexOf('-', StringComparison.Ordinal);
        return dash > 0 ? fileName[..dash] : fileName;
    }

    [GeneratedRegex(@"^(?<shelf>(mail|calendar|contacts|tasks|web)-[0-9a-f]{32}\.shelf)(\.corrupt)?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ShelfFile();
}
