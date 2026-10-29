using Themia.Sdk;

namespace Themia.Core.Desktop;

/// <summary>
/// [S-5510] Which of the desktop root's children Explorer's desktop shows. The root's enumeration also
/// lists what the navigation pane shows (Home, Gallery, Libraries, OneDrive, the user's folder), and
/// lists This PC, Network and the Recycle Bin whatever Windows' desktop-icon settings say; Explorer's
/// desktop filters them, and so does this, by the same two facts: where a file lives, and the
/// desktop-icon settings (<c>Explorer\HideDesktopIcons\NewStartPanel</c>, one value per class id: 0
/// shown, 1 hidden).
/// </summary>
public static class DesktopShown
{
    /// <summary>The Recycle Bin's class id: the one shell item on the desktop when its setting is absent.</summary>
    public const string RecycleBin = "{645FF040-5081-101B-9F08-00AA002F954E}";

    /// <summary>The user's files (the profile folder): a desktop icon setting of its own, though its key is a path.</summary>
    public const string UsersFiles = "{59031a47-3f72-44a7-89c5-5595fe6b30ee}";

    /// <summary>
    /// Whether Explorer's desktop shows <paramref name="item"/>: a file or folder that lives in the
    /// user's or the Public desktop folder (<paramref name="desktopFolders"/>); the profile folder when
    /// its setting says shown; a shell item at the desktop's top level (<c>::{CLSID}</c>) when its
    /// setting says shown, absent meaning shown for the Recycle Bin alone (Windows' default). Anything
    /// else (a navigation-pane item, a nested shell name) is not on the desktop.
    /// </summary>
    /// <param name="setting">A class id's desktop-icon setting, or null when it has none.</param>
    public static bool Shows(DesktopItem item, IReadOnlyCollection<string> desktopFolders, string? profileFolder, Func<string, int?> setting)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(desktopFolders);
        ArgumentNullException.ThrowIfNull(setting);

        if (DesktopItemKeys.IsShellItem(item.Key))
        {
            var clsid = item.Key[2..];
            if (clsid.Contains('\\', StringComparison.Ordinal) || !clsid.StartsWith('{') || !clsid.EndsWith('}'))
            {
                return false;
            }

            return setting(clsid) is int value ? value == 0 : string.Equals(clsid, RecycleBin, StringComparison.OrdinalIgnoreCase);
        }

        var key = Trim(item.Key);
        if (profileFolder is not null && string.Equals(key, Trim(profileFolder), StringComparison.OrdinalIgnoreCase))
        {
            return setting(UsersFiles) == 0;
        }

        var cut = key.LastIndexOf('\\');
        if (cut <= 0)
        {
            return false;
        }

        var parent = key[..cut];
        foreach (var folder in desktopFolders)
        {
            if (!string.IsNullOrWhiteSpace(folder) && string.Equals(parent, Trim(folder), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string Trim(string path) => path.TrimEnd('\\', '/');
}
