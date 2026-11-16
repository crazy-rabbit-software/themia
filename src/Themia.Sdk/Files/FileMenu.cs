using Themia.Core.Files;
using Themia.Core.Shell;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-6416] [S-5582] A command of a file menu, as Windows 11's File Explorer names them: the folder
/// widget's item and background menus and the desktop icons' menu are lines of these, worded and drawn
/// by one builder (<c>FileMenuEntries</c>).
/// </summary>
public enum FileCommand
{
    /// <summary>A divider between groups.</summary>
    Separator,

    Cut,
    Copy,
    Paste,
    Rename,
    Share,
    Delete,
    Open,

    /// <summary>[S-5582] Open, worded for a desktop icon that is an app layout.</summary>
    OpenLayout,

    /// <summary>A submenu of the apps that open the file, and "Choose another app".</summary>
    OpenWith,

    /// <summary>A folder in a new Explorer window (navigate mode, where Open stays in the widget).</summary>
    OpenInNewWindow,

    /// <summary>The item selected in Explorer, in the folder it is in; a link's target, as Explorer's line on a shortcut.</summary>
    OpenFileLocation,

    PinToQuickAccess,
    PinToStart,
    CompressToZip,
    CopyAsPath,

    /// <summary>[S-5582] A desktop icon's path, as it is: a line each, unquoted.</summary>
    CopyPath,

    /// <summary>[S-5582] A desktop icon's web address or Settings page.</summary>
    CopyUrl,

    /// <summary>[S-5582] The editor of a desktop icon's app layout.</summary>
    EditLayout,

    /// <summary>[S-5582] Empty the Recycle Bin (Windows asks first).</summary>
    EmptyRecycleBin,

    KeepOnDevice,
    FreeUpSpace,
    Properties,

    /// <summary>[S-5582] A desktop icon of Themia's own: its page, in edit mode.</summary>
    IconProperties,

    /// <summary>[S-65] "Edit mode" (unchecked here): edit mode on, from a desktop icon's menu.</summary>
    EditMode,

    /// <summary>The shell's full classic menu (<c>IContextMenu</c>).</summary>
    ShowMoreOptions,

    New,
    SelectAll,
    Up,
    Refresh,
    OpenInExplorer,
}

/// <summary>One line of a file menu: the command and whether it can be clicked now.</summary>
public readonly record struct FileMenuLine(FileCommand Command, bool Enabled = true)
{
    public static FileMenuLine Separator => new(FileCommand.Separator);

    public bool IsSeparator => Command == FileCommand.Separator;
}

/// <summary>Where a file menu opens: its keys and a few of its lines differ, as Explorer's own do between a folder window and the desktop.</summary>
public enum FileMenuPlace
{
    /// <summary>The folder widget.</summary>
    Folder,

    /// <summary>The desktop's icons.</summary>
    Desktop,
}

/// <summary>One selected file or folder, as far as the menu's rule reads it.</summary>
/// <param name="Name">Its file name with the extension.</param>
/// <param name="IsDirectory">A folder.</param>
/// <param name="Cloud">A cloud item (OneDrive's placeholder or pinned file): the cloud pins apply.</param>
public readonly record struct FileMenuItem(string Name, bool IsDirectory, bool Cloud = false)
{
    /// <summary>A shortcut file, whose "Open file location" is its target's.</summary>
    public bool IsLink => !IsDirectory && Name.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase);
}

/// <summary>What the item menu depends on besides the selection.</summary>
/// <param name="Place">The folder widget or the desktop.</param>
/// <param name="Navigates">The folder widget in navigate mode: Open stays in the widget, so "Open in new window" is offered.</param>
/// <param name="ClipboardHasFiles">Files are on the clipboard (Paste into a folder).</param>
public readonly record struct FileMenuFacts(FileMenuPlace Place, bool Navigates, bool ClipboardHasFiles);

/// <summary>
/// [S-6416] [S-6417] [S-5582] Which commands a menu for selected files and folders offers, in Windows
/// 11 File Explorer's order and groups: the one rule of the folder widget's item menu and the desktop
/// icons' menu. Pure; <c>FileMenuEntries</c> maps each command to its icon, label and key, and
/// <see cref="ContextMenuLayout"/> moves the row commands (<see cref="Quick"/>) into the icon row.
/// </summary>
public static class FileMenu
{
    /// <summary>
    /// [S-6416] The item menu for <paramref name="selected"/>: the row Cut, Copy, Paste (a single
    /// folder, the target), Rename (one item), Share (files only, as Explorer's), Delete; then Open,
    /// Open with (one file), Open in new window (one folder, navigate mode), Open file location (in the
    /// folder widget always: the item in Explorer; on the desktop a link's only, as Explorer's), Pin to
    /// Quick access (folders), Pin to Start (one folder, app or shortcut), Compress to ZIP file, Copy as
    /// path, Properties; the cloud pins when a cloud item is selected; Show more options last.
    /// </summary>
    public static IReadOnlyList<FileMenuLine> Item(IReadOnlyList<FileMenuItem> selected, FileMenuFacts facts)
    {
        ArgumentNullException.ThrowIfNull(selected);

        if (selected.Count == 0)
        {
            return [];
        }

        var single = selected.Count == 1 ? selected[0] : (FileMenuItem?)null;
        var allFolders = selected.All(e => e.IsDirectory);
        var anyFolder = selected.Any(e => e.IsDirectory);
        var anyCloud = selected.Any(e => e.Cloud);

        List<FileMenuLine> lines =
        [
            new(FileCommand.Cut),
            new(FileCommand.Copy),
        ];

        if (single is { IsDirectory: true })
        {
            lines.Add(new(FileCommand.Paste, facts.ClipboardHasFiles));
        }

        if (single is not null)
        {
            lines.Add(new(FileCommand.Rename));
        }

        if (!anyFolder)
        {
            lines.Add(new(FileCommand.Share));
        }

        lines.Add(new(FileCommand.Delete));
        lines.Add(new(FileCommand.Open));

        if (single is { IsDirectory: false })
        {
            lines.Add(new(FileCommand.OpenWith));
        }

        if (single is { IsDirectory: true } && facts.Navigates)
        {
            lines.Add(new(FileCommand.OpenInNewWindow));
        }

        if (facts.Place == FileMenuPlace.Folder || single is { IsLink: true })
        {
            lines.Add(new(FileCommand.OpenFileLocation));
        }

        if (allFolders)
        {
            lines.Add(new(FileCommand.PinToQuickAccess));
        }

        if (single is { } one && PinnableToStart(one.Name, one.IsDirectory))
        {
            lines.Add(new(FileCommand.PinToStart));
        }

        lines.Add(new(FileCommand.CompressToZip));
        lines.Add(new(FileCommand.CopyAsPath));
        lines.Add(new(FileCommand.Properties));

        if (anyCloud)
        {
            lines.Add(FileMenuLine.Separator);
            lines.Add(new(FileCommand.KeepOnDevice));
            lines.Add(new(FileCommand.FreeUpSpace));
        }

        lines.Add(FileMenuLine.Separator);
        lines.Add(new(FileCommand.ShowMoreOptions));
        return lines;
    }

    /// <summary>[S-1740] The icon-row command a file command is, if any.</summary>
    public static QuickCommand? Quick(FileCommand command) => command switch
    {
        FileCommand.Cut => QuickCommand.Cut,
        FileCommand.Copy => QuickCommand.Copy,
        FileCommand.Paste => QuickCommand.Paste,
        FileCommand.Rename => QuickCommand.Rename,
        FileCommand.Share => QuickCommand.Share,
        FileCommand.Delete => QuickCommand.Delete,
        _ => null,
    };

    /// <summary>
    /// [S-6417] [S-5582] The key shown beside a command, Explorer's own: only keys the place itself
    /// answers through the lease ([S-1327], [S-5545]). The folder widget also answers Ctrl+V, Ctrl+A
    /// and Ctrl+Shift+C; the desktop's Ctrl+V pastes onto the desktop, never into the folder a menu was
    /// opened on, so its Paste shows none. Shift+F10 opens the shell's menu, as in Explorer.
    /// </summary>
    public static KeyChord? Accelerator(FileCommand command, FileMenuPlace place = FileMenuPlace.Folder) => command switch
    {
        FileCommand.Open or FileCommand.OpenLayout => KeyChord.Plain(ChordKey.Enter),
        FileCommand.Cut => KeyChord.Ctrl(ChordKey.X),
        FileCommand.Copy => KeyChord.Ctrl(ChordKey.C),
        FileCommand.Paste when place == FileMenuPlace.Folder => KeyChord.Ctrl(ChordKey.V),
        FileCommand.SelectAll when place == FileMenuPlace.Folder => KeyChord.Ctrl(ChordKey.A),
        FileCommand.Rename => KeyChord.Plain(ChordKey.F2),
        FileCommand.Delete => KeyChord.Plain(ChordKey.Delete),
        FileCommand.CopyAsPath when place == FileMenuPlace.Folder => KeyChord.CtrlShift(ChordKey.C),
        FileCommand.Properties or FileCommand.IconProperties => KeyChord.Alt(ChordKey.Enter),
        FileCommand.ShowMoreOptions => KeyChord.Shift(ChordKey.F10),
        _ => null,
    };

    /// <summary>What Explorer pins to Start: a folder, a program or a shortcut.</summary>
    public static bool PinnableToStart(string name, bool isDirectory)
    {
        ArgumentNullException.ThrowIfNull(name);
        return isDirectory
            || name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// [S-6419] The ZIP file "Compress to ZIP file" makes for the item the menu was opened on: its name
    /// without the extension (a folder's whole name; a dot-file's whole name), ".zip", made unique
    /// against <paramref name="taken"/> as Explorer does ("a (2).zip").
    /// </summary>
    public static string ZipName(string name, bool isDirectory, IEnumerable<string> taken)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(taken);

        var trimmed = name.TrimEnd('.', ' ');
        var dot = trimmed.LastIndexOf('.');
        var stem = isDirectory || dot <= 0 ? trimmed : trimmed[..dot];

        if (stem.Length == 0)
        {
            stem = "Archive";
        }

        return FileNames.Unique(stem + ".zip", taken);
    }

    /// <summary>
    /// [S-6416] What "Copy as path" copies: each path quoted, a line each, as Explorer's.
    /// </summary>
    public static string QuotedPaths(IReadOnlyList<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        return string.Join("\r\n", paths.Select(p => $"\"{p}\""));
    }
}
