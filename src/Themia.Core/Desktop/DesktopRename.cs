using Themia.Core.Files;
using Themia.Sdk;

namespace Themia.Core.Desktop;

/// <summary>[S-5543] What a rename box's text comes to.</summary>
public enum DesktopRenameVerdict
{
    /// <summary>A new name: rename the file to <see cref="DesktopRenameResult.FullName"/>.</summary>
    Rename,

    /// <summary>The same name (or the box left as it was): nothing to do, the box closes.</summary>
    Unchanged,

    /// <summary>Windows refuses the name (<see cref="DesktopRenameResult.Problem"/>): the box stays and says why.</summary>
    Invalid,

    /// <summary>Another item in the same folder has that name: the box stays and says so.</summary>
    Clash,
}

/// <summary>[S-5543] The verdict on a rename, the full name it renames to, and why it cannot when it cannot.</summary>
public readonly record struct DesktopRenameResult(DesktopRenameVerdict Verdict, string? FullName, FileNameProblem Problem);

/// <summary>
/// [S-5543] F2 on a desktop item, as Explorer's desktop renames: which items can be renamed in place,
/// what the box holds and selects, and what its text comes to, checked before the shell is asked
/// (the shell's own rename then runs with its own prompts). Pure.
/// </summary>
public static class DesktopRename
{
    /// <summary>A file, folder or shortcut on disk; never a shell item (This PC, the Recycle Bin), whose name is Windows'.</summary>
    public static bool CanRename(DesktopItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.Kind != DesktopItemKind.ShellItem && !DesktopItemKeys.IsShellItem(item.Key) && FileName(item.Key).Length > 0;
    }

    /// <summary>
    /// What the box shows and selects: the name as shown (no <c>.lnk</c>, the extension only when
    /// Explorer shows it), with the part before the extension selected when the extension shows (a
    /// folder's whole name, a name with no dot whole), as Explorer selects it.
    /// </summary>
    public static (string Text, int SelectLength) Edit(DesktopItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var text = item.Name;
        if (item.Kind == DesktopItemKind.Folder || !ShowsExtension(item))
        {
            return (text, text.Length);
        }

        var dot = text.LastIndexOf('.');
        return (text, dot > 0 ? dot : text.Length);
    }

    /// <summary>
    /// The verdict on <paramref name="edited"/>: the full file name it stands for (the hidden extension
    /// put back, as Explorer does, and a shortcut's <c>.lnk</c> always), checked against Windows' rules
    /// (<see cref="FileNames.Validate"/>) and against <paramref name="siblings"/>, the names of the other
    /// items in the same folder (a change of case alone is no clash).
    /// </summary>
    public static DesktopRenameResult Commit(DesktopItem item, string edited, IEnumerable<string> siblings)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(edited);
        ArgumentNullException.ThrowIfNull(siblings);

        var original = FileName(item.Key);
        var trimmed = edited.Trim();
        var full = item.Kind == DesktopItemKind.Folder || ShowsExtension(item)
            ? trimmed
            : trimmed + Extension(original);

        if (string.Equals(full, original, StringComparison.Ordinal) || string.Equals(trimmed, item.Name, StringComparison.Ordinal))
        {
            return new DesktopRenameResult(DesktopRenameVerdict.Unchanged, null, FileNameProblem.None);
        }

        var problem = FileNames.Validate(full);
        if (problem != FileNameProblem.None || trimmed.Length == 0)
        {
            return new DesktopRenameResult(DesktopRenameVerdict.Invalid, null, problem == FileNameProblem.None ? FileNameProblem.Empty : problem);
        }

        var clash = !string.Equals(full, original, StringComparison.OrdinalIgnoreCase)
            && siblings.Any(name => string.Equals(name, full, StringComparison.OrdinalIgnoreCase));
        return clash
            ? new DesktopRenameResult(DesktopRenameVerdict.Clash, full, FileNameProblem.None)
            : new DesktopRenameResult(DesktopRenameVerdict.Rename, full, FileNameProblem.None);
    }

    /// <summary>The names of the items in <paramref name="key"/>'s own folder, from the listing (its own name left out).</summary>
    public static IReadOnlyList<string> Siblings(string key, IEnumerable<DesktopItem> listing)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(listing);
        var folder = Folder(key);
        return [.. listing
            .Where(i => !DesktopItemKeys.IsShellItem(i.Key) && !DesktopItemKeys.Comparer.Equals(i.Key, key)
                && string.Equals(Folder(i.Key), folder, StringComparison.OrdinalIgnoreCase))
            .Select(i => FileName(i.Key))];
    }

    /// <summary>The key's last part: the file's own name with its extension.</summary>
    public static string FileName(string key)
    {
        var trimmed = key.TrimEnd('\\', '/');
        var cut = trimmed.LastIndexOfAny(['\\', '/']);
        return cut >= 0 ? trimmed[(cut + 1)..] : trimmed;
    }

    private static string Folder(string key)
    {
        var trimmed = key.TrimEnd('\\', '/');
        var cut = trimmed.LastIndexOfAny(['\\', '/']);
        return cut >= 0 ? trimmed[..cut] : string.Empty;
    }

    private static string Extension(string fileName)
    {
        var dot = fileName.LastIndexOf('.');
        return dot > 0 ? fileName[dot..] : string.Empty;
    }

    /// <summary>
    /// Whether the shown name carries the file's extension: Explorer's "File name extensions" setting,
    /// and a type that never shows its own (a shortcut's <c>.lnk</c>), both read off the name the shell gave.
    /// </summary>
    private static bool ShowsExtension(DesktopItem item)
    {
        var extension = Extension(FileName(item.Key));
        return extension.Length == 0 || item.Name.EndsWith(extension, StringComparison.OrdinalIgnoreCase);
    }
}
