using Themia.Core.Files;
using Themia.Core.Shell;

namespace Themia.Core.Widgets;

/// <summary>[S-6416] A command of the folder widget's item and background menus, as Windows 11's File Explorer names them.</summary>
public enum FolderCommand
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

    /// <summary>A submenu of the apps that open the file, and "Choose another app".</summary>
    OpenWith,

    /// <summary>A folder in a new Explorer window (navigate mode, where Open stays in the widget).</summary>
    OpenInNewWindow,

    /// <summary>The item selected in Explorer, in the folder it is in.</summary>
    OpenFileLocation,

    PinToQuickAccess,
    PinToStart,
    CompressToZip,
    CopyAsPath,
    KeepOnDevice,
    FreeUpSpace,
    Properties,

    /// <summary>The shell's full classic menu (<c>IContextMenu</c>).</summary>
    ShowMoreOptions,

    New,
    SelectAll,
    Up,
    Refresh,
    OpenInExplorer,
}

/// <summary>A key the widget answers, as a menu shows it beside the command that does the same ([S-6417]).</summary>
public enum FolderAccelerator
{
    None,
    Enter,
    CtrlX,
    CtrlC,
    CtrlV,
    CtrlA,
    F2,
    Delete,
    ShiftF10,
}

/// <summary>One line of a folder menu: the command and whether it can be clicked now.</summary>
public readonly record struct FolderMenuLine(FolderCommand Command, bool Enabled = true)
{
    public static FolderMenuLine Separator => new(FolderCommand.Separator);

    public bool IsSeparator => Command == FolderCommand.Separator;
}

/// <summary>What the background menu depends on.</summary>
/// <param name="HasPath">The widget shows a folder at all.</param>
/// <param name="Listed">That folder was read (it can take new items and a paste).</param>
/// <param name="HasItems">Something is shown (Select all).</param>
/// <param name="ClipboardHasFiles">Files are on the clipboard (Paste).</param>
/// <param name="CanGoUp">Navigate mode with a parent to go to.</param>
public readonly record struct FolderBackgroundFacts(bool HasPath, bool Listed, bool HasItems, bool ClipboardHasFiles, bool CanGoUp);

/// <summary>
/// [S-6416]..[S-6418] Which commands the folder widget's menus offer for a selection (files, folders,
/// both) and for the background, in Windows 11 File Explorer's order and groups. Pure; the view maps
/// each command to its icon, label and action, and <see cref="ContextMenuLayout"/> moves the row
/// commands (<see cref="Quick"/>) into the icon row.
/// </summary>
public static class FolderMenu
{
    /// <summary>
    /// [S-6416] The item menu for <paramref name="selected"/>: the row Cut, Copy, Paste (a single
    /// folder, the target), Rename (one item), Share (files only, as Explorer's), Delete; then Open,
    /// Open with (one file), Open in new window (one folder, navigate mode), Open file location, Pin to
    /// Quick access (folders), Pin to Start (one folder, app or shortcut), Compress to ZIP file, Copy as
    /// path, Properties; the cloud pins when a cloud item is selected; Show more options last.
    /// </summary>
    public static IReadOnlyList<FolderMenuLine> Item(IReadOnlyList<FolderEntry> selected, bool navigates, bool clipboardHasFiles)
    {
        ArgumentNullException.ThrowIfNull(selected);

        if (selected.Count == 0)
        {
            return [];
        }

        var single = selected.Count == 1 ? selected[0] : (FolderEntry?)null;
        var allFolders = selected.All(e => e.IsDirectory);
        var anyFolder = selected.Any(e => e.IsDirectory);
        var anyCloud = selected.Any(e => CloudBadges.State(e.Attributes) != CloudState.None);

        List<FolderMenuLine> lines =
        [
            new(FolderCommand.Cut),
            new(FolderCommand.Copy),
        ];

        if (single is { IsDirectory: true })
        {
            lines.Add(new(FolderCommand.Paste, clipboardHasFiles));
        }

        if (single is not null)
        {
            lines.Add(new(FolderCommand.Rename));
        }

        if (!anyFolder)
        {
            lines.Add(new(FolderCommand.Share));
        }

        lines.Add(new(FolderCommand.Delete));
        lines.Add(new(FolderCommand.Open));

        if (single is { IsDirectory: false })
        {
            lines.Add(new(FolderCommand.OpenWith));
        }

        if (single is { IsDirectory: true } && navigates)
        {
            lines.Add(new(FolderCommand.OpenInNewWindow));
        }

        lines.Add(new(FolderCommand.OpenFileLocation));

        if (allFolders)
        {
            lines.Add(new(FolderCommand.PinToQuickAccess));
        }

        if (single is { } one && PinnableToStart(one))
        {
            lines.Add(new(FolderCommand.PinToStart));
        }

        lines.Add(new(FolderCommand.CompressToZip));
        lines.Add(new(FolderCommand.CopyAsPath));
        lines.Add(new(FolderCommand.Properties));

        if (anyCloud)
        {
            lines.Add(FolderMenuLine.Separator);
            lines.Add(new(FolderCommand.KeepOnDevice));
            lines.Add(new(FolderCommand.FreeUpSpace));
        }

        lines.Add(FolderMenuLine.Separator);
        lines.Add(new(FolderCommand.ShowMoreOptions));
        return lines;
    }

    /// <summary>
    /// [S-6418] The background menu: Paste, Select all; Up (navigate mode), Refresh; New; Open in
    /// Explorer, Copy as path, Properties; Show more options. No View or Sort: how the widget looks is
    /// its settings' ([S-1746]).
    /// </summary>
    public static IReadOnlyList<FolderMenuLine> Background(FolderBackgroundFacts facts)
    {
        List<FolderMenuLine> lines =
        [
            new(FolderCommand.Paste, facts.Listed && facts.ClipboardHasFiles),
            new(FolderCommand.SelectAll, facts.HasItems),
            FolderMenuLine.Separator,
        ];

        if (facts.CanGoUp)
        {
            lines.Add(new(FolderCommand.Up));
        }

        lines.Add(new(FolderCommand.Refresh));
        lines.Add(FolderMenuLine.Separator);
        lines.Add(new(FolderCommand.New, facts.Listed));
        lines.Add(FolderMenuLine.Separator);
        lines.Add(new(FolderCommand.OpenInExplorer, facts.HasPath));
        lines.Add(new(FolderCommand.CopyAsPath, facts.HasPath));
        lines.Add(new(FolderCommand.Properties, facts.HasPath));
        lines.Add(FolderMenuLine.Separator);
        lines.Add(new(FolderCommand.ShowMoreOptions, facts.HasPath));
        return lines;
    }

    /// <summary>[S-1740] The icon-row command a folder command is, if any.</summary>
    public static QuickCommand? Quick(FolderCommand command) => command switch
    {
        FolderCommand.Cut => QuickCommand.Cut,
        FolderCommand.Copy => QuickCommand.Copy,
        FolderCommand.Paste => QuickCommand.Paste,
        FolderCommand.Rename => QuickCommand.Rename,
        FolderCommand.Share => QuickCommand.Share,
        FolderCommand.Delete => QuickCommand.Delete,
        _ => null,
    };

    /// <summary>
    /// [S-6417] The key shown beside a command: only keys the widget itself answers ([S-1327]), so
    /// Explorer's Ctrl+Shift+C and Alt+Enter, chords that never reach a widget, are not shown.
    /// Shift+F10 opens the shell's menu, as in Explorer.
    /// </summary>
    public static FolderAccelerator Accelerator(FolderCommand command) => command switch
    {
        FolderCommand.Open => FolderAccelerator.Enter,
        FolderCommand.Cut => FolderAccelerator.CtrlX,
        FolderCommand.Copy => FolderAccelerator.CtrlC,
        FolderCommand.Paste => FolderAccelerator.CtrlV,
        FolderCommand.SelectAll => FolderAccelerator.CtrlA,
        FolderCommand.Rename => FolderAccelerator.F2,
        FolderCommand.Delete => FolderAccelerator.Delete,
        FolderCommand.ShowMoreOptions => FolderAccelerator.ShiftF10,
        _ => FolderAccelerator.None,
    };

    /// <summary>What Explorer pins to Start: a folder, a program or a shortcut.</summary>
    public static bool PinnableToStart(FolderEntry entry)
        => entry.IsDirectory
            || entry.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            || entry.Name.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// [S-6419] The ZIP file "Compress to ZIP file" makes for <paramref name="named"/> (the item the
    /// menu was opened on): its name without the extension (a folder's whole name; a dot-file's whole
    /// name), ".zip", made unique against <paramref name="taken"/> as Explorer does ("a (2).zip").
    /// </summary>
    public static string ZipName(FolderEntry named, IEnumerable<string> taken)
    {
        ArgumentNullException.ThrowIfNull(taken);

        var name = named.Name.TrimEnd('.', ' ');
        var dot = name.LastIndexOf('.');
        var stem = named.IsDirectory || dot <= 0 ? name : name[..dot];

        if (stem.Length == 0)
        {
            stem = "Archive";
        }

        return FileNames.Unique(stem + ".zip", taken);
    }
}
