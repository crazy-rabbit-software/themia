namespace Themia.Sdk;

/// <summary>[S-5510] What a desktop item is, as Explorer's desktop tells them apart.</summary>
public enum DesktopItemKind
{
    /// <summary>A file (a ZIP folder too: the shell lists it as a file you can open).</summary>
    File = 0,

    /// <summary>A folder on disk.</summary>
    Folder = 1,

    /// <summary>A shortcut (<c>.lnk</c>, <c>.url</c>): drawn with the shell's arrow.</summary>
    Shortcut = 2,

    /// <summary>A shell item that is no file: This PC, the Recycle Bin, Network, Control Panel, the user's folder.</summary>
    ShellItem = 3,
}

/// <summary>
/// [S-5510] One item of the desktop's namespace, as much as a widget draws and the reconcile keys on.
/// </summary>
/// <param name="Key">
/// The shell's parsing name relative to the desktop (<see cref="DesktopItemKeys"/>): a full path for a
/// file or folder, <c>::{CLSID}</c> for a shell item. What an item widget's config stores.
/// </param>
/// <param name="Name">Its name as Explorer's desktop shows it (no <c>.lnk</c>; extensions as Explorer's setting says).</param>
/// <param name="Kind">File, folder, shortcut or shell item.</param>
public sealed record DesktopItem(string Key, string Name, DesktopItemKind Kind);

/// <summary>[S-5510] [S-5530] Desktop item keys: what one is, how two compare, and what opening one launches.</summary>
public static class DesktopItemKeys
{
    /// <summary>[S-5522] The longest key: the shell's own limit on a path (<c>UNICODE_STRING</c>'s, 32 767).</summary>
    public const int MaxLength = 32_767;

    /// <summary>Two keys are one item when they differ only in case, as the file system and the shell's names do.</summary>
    public static StringComparer Comparer => StringComparer.OrdinalIgnoreCase;

    /// <summary>A shell item's key: a class id under the desktop (<c>::{20D04FE0-…}</c> is This PC).</summary>
    public static bool IsShellItem(string key) => key.StartsWith("::", StringComparison.Ordinal);

    /// <summary>[S-5522] A key worth keeping: not blank, not past <see cref="MaxLength"/>, no control character.</summary>
    public static bool IsValid(string? key)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > MaxLength)
        {
            return false;
        }

        foreach (var c in key)
        {
            if (char.IsControl(c))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// [S-5530] What the launcher opens for <paramref name="key"/>: a path as it is, a shell item through
    /// its shell name (<c>shell:::{CLSID}</c>), which the launcher accepts as a namespace name.
    /// </summary>
    public static string LaunchTarget(string key) => IsShellItem(key) ? "shell:" + key : key;

    /// <summary>
    /// [S-5603] A name for an item the listing does not hold (yet, or any more): a path's last part
    /// with a shortcut's extension dropped, a shell item's key as it is.
    /// </summary>
    public static string FallbackName(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (IsShellItem(key))
        {
            return key;
        }

        var trimmed = key.TrimEnd('\\', '/');
        var cut = trimmed.LastIndexOfAny(['\\', '/']);
        var name = cut >= 0 ? trimmed[(cut + 1)..] : trimmed;
        return name.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) && name.Length > 4 ? name[..^4] : name;
    }
}

/// <summary>
/// [S-5521] [S-5523] The order desktop items are laid out and listed in: Explorer's Sort by name, so
/// shell items first, then folders, then files and shortcuts, each by name ignoring case (the key
/// breaks a tie, so the order is total).
/// </summary>
public static class DesktopItemOrder
{
    /// <summary>The items, ordered.</summary>
    public static IReadOnlyList<DesktopItem> Sort(IEnumerable<DesktopItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        return [.. items.Order(Comparer.Instance)];
    }

    private static int Rank(DesktopItemKind kind) => kind switch
    {
        DesktopItemKind.ShellItem => 0,
        DesktopItemKind.Folder => 1,
        _ => 2,
    };

    private sealed class Comparer : IComparer<DesktopItem>
    {
        public static readonly Comparer Instance = new();

        public int Compare(DesktopItem? x, DesktopItem? y)
        {
            if (x is null || y is null)
            {
                return x is null ? (y is null ? 0 : -1) : 1;
            }

            var rank = Rank(x.Kind).CompareTo(Rank(y.Kind));
            if (rank != 0)
            {
                return rank;
            }

            var name = StringComparer.OrdinalIgnoreCase.Compare(x.Name, y.Name);
            return name != 0 ? name : StringComparer.OrdinalIgnoreCase.Compare(x.Key, y.Key);
        }
    }
}
