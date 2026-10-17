namespace Themia.Core.Files;

/// <summary>
/// [S-1332] Where dropped or pasted files may go and what a drop does, ported from the original's
/// <c>dropTarget.ts</c> and its drop target's effect choice. Pure: shared by the folder widget and
/// (later) the Shortcuts widget and the desktop surface.
/// </summary>
public static class FileDrops
{
    /// <summary>
    /// A Windows path for comparison: forward slashes made back, trailing separators dropped,
    /// lower-cased (Windows paths are case-insensitive), so <c>D:/Docs/</c> equals <c>D:\Docs</c>.
    /// </summary>
    public static string Normalize(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        var slashed = path.Replace('/', '\\');
        var trimmed = slashed.TrimEnd('\\');

        // "C:\" trims to "C:", which names the drive's current directory, not its root. Keep the
        // root's separator so a drop onto a drive root compares as that root.
        if (trimmed.Length == 2 && trimmed[1] == ':')
        {
            trimmed += "\\";
        }

        return trimmed.ToLowerInvariant();
    }

    /// <summary>The normalized parent directory of a path.</summary>
    public static string Parent(string path)
    {
        var normal = Normalize(path);
        var cut = normal.TrimEnd('\\').LastIndexOf('\\');

        if (cut < 0)
        {
            return string.Empty;
        }

        // The parent of "c:\x" is the root "c:\", which keeps its separator.
        return cut == 2 && normal[1] == ':' ? normal[..3] : normal[..cut];
    }

    public static bool SamePath(string a, string b) => Normalize(a) == Normalize(b);

    /// <summary>Whether <paramref name="path"/> is <paramref name="folder"/> or somewhere beneath it.</summary>
    public static bool IsWithin(string path, string folder)
    {
        var p = Normalize(path);
        var f = Normalize(folder);
        return p == f || p.StartsWith(f.EndsWith('\\') ? f : f + "\\", StringComparison.Ordinal);
    }

    /// <summary>
    /// The sources that may actually be pasted into <paramref name="destination"/>. Drops the no-ops
    /// that the shell's move engine can turn into damage: an item already in that folder (a same-folder
    /// drop, unless it is a copy, which the shell names "- Copy"), and a folder dropped onto itself or
    /// into its own subtree.
    /// </summary>
    public static IReadOnlyList<string> Pastable(IEnumerable<string> sources, string destination, DropEffect effect)
    {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(destination);

        return
        [
            .. sources.Where(source =>
                !string.IsNullOrWhiteSpace(source)
                && !IsWithin(destination, source)
                && (effect == DropEffect.Copy || !SamePath(Parent(source), destination))),
        ];
    }

    /// <summary>
    /// The effect for the modifier keys held, out of what the source <paramref name="allowed"/>:
    /// Ctrl+Shift links, Ctrl copies, otherwise a move when the source allows one, else a copy.
    /// A drop from a different volume moves as well; the original did, and plain-drop-moves is what
    /// the widget promises ([S-1332]).
    /// </summary>
    public static DropEffect Choose(bool ctrl, bool shift, DropEffect allowed)
    {
        if (ctrl && shift && allowed.HasFlag(DropEffect.Link))
        {
            return DropEffect.Link;
        }

        if (ctrl && allowed.HasFlag(DropEffect.Copy))
        {
            return DropEffect.Copy;
        }

        if (allowed.HasFlag(DropEffect.Move))
        {
            return DropEffect.Move;
        }

        return allowed.HasFlag(DropEffect.Copy) ? DropEffect.Copy : DropEffect.None;
    }
}
