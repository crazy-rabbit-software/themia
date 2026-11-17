using Themia.Core.Files;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.Core.Desktop;

/// <summary>[S-5580] What one desktop icon is to the desktop's commands.</summary>
public enum DeskIconKind
{
    /// <summary>A desktop item that is a file, a folder or a link on the desktop.</summary>
    Item,

    /// <summary>A desktop item that is no file: This PC, the Recycle Bin, Network.</summary>
    ShellItem,

    /// <summary>An own icon that opens nothing yet.</summary>
    OwnWaiting,

    /// <summary>An own icon that is an app layout.</summary>
    OwnLayout,

    /// <summary>An own icon whose target is a web address.</summary>
    OwnWeb,

    /// <summary>An own icon whose target is a Windows Settings page.</summary>
    OwnSettings,

    /// <summary>An own icon whose target is a file or folder path.</summary>
    OwnPath,

    /// <summary>An own icon whose target is a Windows location (<c>shell:</c>) other than the Recycle Bin.</summary>
    OwnLocation,

    /// <summary>An own icon whose target is the Recycle Bin.</summary>
    OwnBin,

    /// <summary>An own icon whose target the launch rule refuses.</summary>
    OwnRefused,
}

/// <summary>
/// [S-5580] One desktop icon as the desktop's commands see it, whichever its source.
/// </summary>
/// <param name="Key">Its selection key (<see cref="DesktopItemKeys.Of"/>).</param>
/// <param name="Kind">What it is.</param>
/// <param name="Value">A desktop item's key; an own icon's target as classified (a full path, an address, a shell name); empty for none.</param>
/// <param name="Target">An own icon's target as stored, which the launcher classifies again when it opens; empty otherwise.</param>
/// <param name="Opens">Whether opening it does anything: a desktop item, a valid target, a layout with an assigned pane.</param>
/// <param name="PathExists">An own icon's path target is a local file or folder that is there now (never probed on a network path).</param>
/// <param name="Click">How an own icon opens.</param>
public sealed record DeskIcon(string Key, DeskIconKind Kind, string Value, string Target, bool Opens, bool PathExists, ItemClickMode Click)
{
    /// <summary>Whether it is an own icon, not a desktop item.</summary>
    public bool Own => Kind is not (DeskIconKind.Item or DeskIconKind.ShellItem);

    /// <summary>
    /// The file-system path it stands for in a file operation (the clipboard, a drag): a desktop item's
    /// own path, an own icon's path target while it is there; null for everything else.
    /// </summary>
    public string? FilePath => Kind switch
    {
        DeskIconKind.Item => Value,
        DeskIconKind.OwnPath when PathExists => Value,
        _ => null,
    };

    /// <summary>What "Copy path" copies for it, or null when it has no path to say.</summary>
    public string? PathText => Kind switch
    {
        DeskIconKind.Item or DeskIconKind.OwnPath or DeskIconKind.OwnLocation or DeskIconKind.OwnBin => Value,
        _ => null,
    };

    /// <summary>
    /// The icon of <paramref name="config"/>. <paramref name="exists"/> answers whether a local path is
    /// there; it is asked only for a path that may be looked at (<see cref="LaunchTargets.MayProbe"/>).
    /// </summary>
    public static DeskIcon Of(IDesktopItemConfig config, Func<string, bool> exists)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(exists);
        var key = DesktopItemKeys.Of(config);

        if (config.Item.Length > 0)
        {
            var kind = DesktopItemKeys.IsShellItem(config.Item) ? DeskIconKind.ShellItem : DeskIconKind.Item;
            return new DeskIcon(key, kind, config.Item, string.Empty, Opens: true, PathExists: true, ItemClickMode.Double);
        }

        if (config.Layout is { } layout)
        {
            return new DeskIcon(key, DeskIconKind.OwnLayout, string.Empty, string.Empty, LayoutTree.AssignedCount(layout) > 0, false, config.ClickMode);
        }

        if (string.IsNullOrWhiteSpace(config.Target))
        {
            return new DeskIcon(key, DeskIconKind.OwnWaiting, string.Empty, string.Empty, false, false, config.ClickMode);
        }

        var target = LaunchTargets.Classify(config.Target);
        if (!target.IsValid)
        {
            return new DeskIcon(key, DeskIconKind.OwnRefused, string.Empty, config.Target, false, false, config.ClickMode);
        }

        var own = target.Kind switch
        {
            LaunchKind.Web => DeskIconKind.OwnWeb,
            LaunchKind.Settings => DeskIconKind.OwnSettings,
            LaunchKind.LocalPath => DeskIconKind.OwnPath,
            _ => LaunchTargets.SameTarget(config.Target, RecycleBinState.Target) ? DeskIconKind.OwnBin : DeskIconKind.OwnLocation,
        };
        var there = own == DeskIconKind.OwnPath && LaunchTargets.MayProbe(target) && exists(target.Value);
        return new DeskIcon(key, own, target.Value, config.Target, Opens: true, there, config.ClickMode);
    }
}

/// <summary>[S-5581] One icon to open: a target for the launcher, or the app layout of the icon with <paramref name="Key"/>.</summary>
public readonly record struct DeskOpen(string Key, string? Launch, bool Layout);

/// <summary>[S-5582] What the menu for a selection depends on besides the selection.</summary>
/// <param name="CanEmptyBin">The Recycle Bin holds something and is not being emptied.</param>
/// <param name="CanEditLayout">The app layout editor can open.</param>
/// <param name="ClipboardHasFiles">Files are on the clipboard (Paste into a folder icon).</param>
public readonly record struct DeskMenuFacts(bool CanEmptyBin, bool CanEditLayout, bool ClipboardHasFiles, bool WidgetsLocked = false);

/// <summary>
/// [S-5582] The menu for a selection of desktop icons: its lines (the icon row's commands among them,
/// <see cref="FileMenu.Quick"/>), and what the shell's classic menu opens for (<see cref="Classic"/>:
/// desktop item keys or an own icon's path; empty: there is no classic menu, and no Show more options).
/// </summary>
public sealed record DeskMenu(IReadOnlyList<FileMenuLine> Lines, IReadOnlyList<string> Classic);

/// <summary>[S-5583] What Delete does to a selection: the files handed to the shell's delete, and the own icons whose widgets go.</summary>
public sealed record DeskDelete(IReadOnlyList<string> Files, IReadOnlyList<string> OwnKeys);

/// <summary>[S-5584] How F2 renames the focused icon.</summary>
public enum DeskRename
{
    /// <summary>Not renamable (a shell item, nothing focused).</summary>
    None,

    /// <summary>A desktop item: the shell's rename of the file.</summary>
    Shell,

    /// <summary>An own icon: its label.</summary>
    Label,
}

/// <summary>[S-5585] What Ctrl+C or Ctrl+X puts on the clipboard: the paths, and whether as a cut.</summary>
public sealed record DeskClipboard(IReadOnlyList<string> Paths, bool Cut);

/// <summary>[S-5586] What a drag of desktop icons carries to another target: the files and folders, and the effects it allows (None: nothing for anyone else).</summary>
public sealed record DeskDrag(IReadOnlyList<string> Paths, DropEffect Allowed);

/// <summary>
/// [S-5580]..[S-5587] The desktop's commands on a selection that may mix both sources of desktop icon,
/// as pure rules: what opens, which menu, what Delete, F2 and the clipboard do, what a drag carries and
/// what a drop onto an icon is forwarded to. The host carries them out; nothing here touches a file.
/// </summary>
public static class DesktopCommands
{
    /// <summary>[S-5545] Opening opens at most this many icons at once.</summary>
    public const int OpenAtMost = 15;

    /// <summary>The Recycle Bin as the shell's drop target is asked for: its item under the desktop.</summary>
    public const string RecycleBinItem = "::{645FF040-5081-101B-9F08-00AA002F954E}";

    /// <summary>
    /// [S-5581] Open (a double-click, Enter): every selected icon that opens anything, a desktop item
    /// through the launcher (a shell item by its shell name), an own icon its target or its layout; at
    /// most <see cref="OpenAtMost"/>, and how many more were left.
    /// </summary>
    public static (IReadOnlyList<DeskOpen> Open, int Skipped) Open(IReadOnlyList<DeskIcon> selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        var all = selection.Where(icon => icon.Opens).Select(icon => icon.Kind switch
        {
            DeskIconKind.Item or DeskIconKind.ShellItem => new DeskOpen(icon.Key, DesktopItemKeys.LaunchTarget(icon.Value), false),
            DeskIconKind.OwnLayout => new DeskOpen(icon.Key, null, true),
            _ => new DeskOpen(icon.Key, icon.Target, false),
        }).ToList();
        return ([.. all.Take(OpenAtMost)], Math.Max(0, all.Count - OpenAtMost));
    }

    /// <summary>
    /// [S-5582] The menu for <paramref name="selection"/>, Windows 11's shape for every source.
    /// <list type="bullet">
    /// <item>Desktop items that are all files, folders or links: Explorer's file menu
    /// (<see cref="FileMenu.Item"/> on the desktop; <paramref name="isFolder"/> says which keys are folders).</item>
    /// <item>Desktop items with a shell item among them (This PC, the Recycle Bin): Open, Empty Recycle
    /// Bin… (the bin alone; off unless it can be emptied), Properties. Never Cut, Rename or Delete.</item>
    /// <item>One own icon: Rename and Delete (its label, its widget) in the row; Open (off while it
    /// opens nothing), what its kind of target offers (a layout its editor; a web address or Settings
    /// page its address to copy; a path its location and its path; a Windows location its path, the
    /// Recycle Bin also Empty Recycle Bin…), Properties… (its page).</item>
    /// <item>Several own icons: Open, Copy path where every one has a path, Delete.</item>
    /// <item>Both sources: Open and, where every one has a path, Copy path.</item>
    /// </list>
    /// Every menu then holds Hide from this view while every icon is a desktop item's ([S-5527]), Edit mode ([S-65]), Lock widgets ([S-12951]) and, where the shell has a classic menu for it
    /// (desktop items; one own icon whose path is there), Show more options last.
    /// </summary>
    public static DeskMenu Menu(IReadOnlyList<DeskIcon> selection, DeskMenuFacts facts, Func<string, bool> isFolder)
    {
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(isFolder);
        if (selection.Count == 0)
        {
            return new DeskMenu([], []);
        }

        var lines = new List<FileMenuLine>();
        IReadOnlyList<string> classic = [];
        if (selection.All(icon => icon.Kind == DeskIconKind.Item))
        {
            classic = [.. selection.Select(icon => icon.Value)];
            var items = selection.Select(icon => new FileMenuItem(FileName(icon.Value), isFolder(icon.Value))).ToList();
            lines.AddRange(FileMenu.Item(items, new FileMenuFacts(FileMenuPlace.Desktop, Navigates: false, facts.ClipboardHasFiles))
                .Where(line => line.Command != FileCommand.ShowMoreOptions));
        }
        else if (selection.All(icon => !icon.Own))
        {
            classic = [.. selection.Select(icon => icon.Value)];
            lines.Add(new(FileCommand.Open));
            if (selection.Count == 1 && DesktopItemKeys.Comparer.Equals(selection[0].Value, RecycleBinItem))
            {
                lines.Add(new(FileCommand.EmptyRecycleBin, facts.CanEmptyBin));
            }

            lines.Add(new(FileCommand.Properties));
        }
        else if (selection.Count == 1)
        {
            var icon = selection[0];
            lines.Add(new(FileCommand.Rename));
            lines.Add(new(FileCommand.Delete));
            lines.Add(new(icon.Kind == DeskIconKind.OwnLayout ? FileCommand.OpenLayout : FileCommand.Open, icon.Opens));
            switch (icon.Kind)
            {
                case DeskIconKind.OwnLayout:
                    lines.Add(new(FileCommand.EditLayout, facts.CanEditLayout));
                    break;
                case DeskIconKind.OwnWeb or DeskIconKind.OwnSettings:
                    lines.Add(new(FileCommand.CopyUrl));
                    break;
                case DeskIconKind.OwnPath:
                    lines.Add(new(FileCommand.OpenFileLocation));
                    lines.Add(new(FileCommand.CopyPath));
                    break;
                case DeskIconKind.OwnBin:
                    lines.Add(new(FileCommand.EmptyRecycleBin, facts.CanEmptyBin));
                    lines.Add(new(FileCommand.CopyPath));
                    break;
                case DeskIconKind.OwnLocation:
                    lines.Add(new(FileCommand.CopyPath));
                    break;
            }

            lines.Add(new(FileCommand.IconProperties));
            if (icon is { Kind: DeskIconKind.OwnPath, PathExists: true })
            {
                classic = [icon.Value];
            }
        }
        else
        {
            lines.Add(new(FileCommand.Open, selection.Any(icon => icon.Opens)));
            if (selection.All(icon => icon.PathText is not null))
            {
                lines.Add(new(FileCommand.CopyPath));
            }

            if (selection.All(icon => icon.Own))
            {
                lines.Add(FileMenuLine.Separator);
                lines.Add(new(FileCommand.Delete));
            }
        }

        lines.Add(FileMenuLine.Separator);

        // [S-5527] Desktop items alone (never an own icon, which is the view's placed widget): off this view.
        if (selection.All(icon => !icon.Own))
        {
            lines.Add(new(FileCommand.HideFromView));
        }

        lines.Add(new(FileCommand.EditMode, Checked: false));
        lines.Add(new(FileCommand.LockWidgets, Checked: facts.WidgetsLocked));
        if (classic.Count > 0)
        {
            lines.Add(FileMenuLine.Separator);
            lines.Add(new(FileCommand.ShowMoreOptions));
        }

        return new DeskMenu(lines, classic);
    }

    /// <summary>A desktop item key's file name: its last part (a drive root, which no desktop item is, keeps the whole).</summary>
    private static string FileName(string key)
    {
        var at = key.TrimEnd('\\', '/').LastIndexOfAny(['\\', '/']);
        return at >= 0 ? key[(at + 1)..].TrimEnd('\\', '/') : key;
    }

    /// <summary>[S-5582] What Copy path (or Copy URL) copies for <paramref name="selection"/>: each one's path or address, a line each.</summary>
    public static string CopyText(IReadOnlyList<DeskIcon> selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        return string.Join(Environment.NewLine, selection.Select(icon => icon.PathText ?? icon.Value).Where(text => text.Length > 0));
    }

    /// <summary>
    /// [S-5583] Delete: the selected desktop items' files go to the shell's delete (a shell item is no
    /// file), the selected own icons' widgets are removed and nothing of their targets is touched; a
    /// mixed selection does both.
    /// </summary>
    public static DeskDelete Delete(IReadOnlyList<DeskIcon> selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        return new DeskDelete(
            [.. selection.Where(icon => icon.Kind == DeskIconKind.Item).Select(icon => icon.Value)],
            [.. selection.Where(icon => icon.Own).Select(icon => icon.Key)]);
    }

    /// <summary>[S-5584] F2 on <paramref name="focus"/>: a desktop item's file through the shell, an own icon's label, a shell item not at all.</summary>
    public static DeskRename Rename(DeskIcon? focus) => focus switch
    {
        null or { Kind: DeskIconKind.ShellItem } => DeskRename.None,
        { Own: true } => DeskRename.Label,
        _ => DeskRename.Shell,
    };

    /// <summary>
    /// [S-5585] Ctrl+C: the selected desktop items' files, and each own icon's path target that is
    /// there. Ctrl+X cuts the desktop items' files only: a cut would move an own icon's target, and
    /// the clipboard holds one effect for all it carries, so an own icon adds nothing to a cut.
    /// </summary>
    public static DeskClipboard Clipboard(IReadOnlyList<DeskIcon> selection, bool cut)
    {
        ArgumentNullException.ThrowIfNull(selection);
        var paths = selection
            .Where(icon => !cut || !icon.Own)
            .Select(icon => icon.FilePath)
            .OfType<string>()
            .Distinct(DesktopItemKeys.Comparer)
            .ToList();
        return new DeskClipboard(paths, cut);
    }

    /// <summary>
    /// [S-5586] What a drag of <paramref name="dragged"/> hands another target (a folder, an app, the
    /// Recycle Bin, Explorer): the desktop items that are files, folders or links, and each own icon's
    /// path target that is there. A shell item (This PC, the Recycle Bin) is no file and is handed to
    /// nobody; it still moves on the desktop with the rest. With an own icon's path among them the drag
    /// never allows a move (its target is copied or linked, never moved away); with no file at all it
    /// carries nothing and allows nothing, so everything but the desktop itself refuses it.
    /// </summary>
    public static DeskDrag Drag(IReadOnlyList<DeskIcon> dragged)
    {
        ArgumentNullException.ThrowIfNull(dragged);
        var paths = dragged
            .Select(icon => icon.FilePath)
            .OfType<string>()
            .Distinct(DesktopItemKeys.Comparer)
            .ToList();
        var allowed = paths.Count == 0 ? DropEffect.None
            : dragged.Any(icon => icon.Own && icon.FilePath is not null) ? DropEffect.Copy | DropEffect.Link
            : DropEffect.Copy | DropEffect.Move | DropEffect.Link;
        return new DeskDrag(paths, allowed);
    }

    /// <summary>
    /// [S-5586] The effects the desktop's own drag is started with: what it allows others, and a move
    /// when it allows them nothing, so the desktop itself can still take it.
    /// </summary>
    public static DropEffect DragEffects(DeskDrag drag)
    {
        ArgumentNullException.ThrowIfNull(drag);
        return drag.Allowed == DropEffect.None ? DropEffect.Move : drag.Allowed;
    }

    /// <summary>
    /// [S-5546] The effect the desktop answers its own drag with over the place its icons would move
    /// to: a move where the drag allows one, else the first effect it does allow (OLE drops only on an
    /// allowed effect). It is never a file operation whichever it is.
    /// </summary>
    public static DropEffect MoveEffect(DropEffect allowed)
        => allowed.HasFlag(DropEffect.Move) ? DropEffect.Move
            : allowed.HasFlag(DropEffect.Link) ? DropEffect.Link
            : allowed.HasFlag(DropEffect.Copy) ? DropEffect.Copy
            : DropEffect.None;

    /// <summary>
    /// [S-5546] Whether the desktop's own drag, let go here, moves its icons: over empty desktop, and
    /// over one of the dragged icons themselves (a short move lands on where one of them still is),
    /// outside edit mode. Over any other widget the widget decides.
    /// </summary>
    public static bool MovesIcons(bool deskDrag, bool editMode, SurfaceHit hit, bool overDragged)
        => deskDrag && !editMode && (hit == SurfaceHit.Empty || (hit == SurfaceHit.Widget && overDragged));

    /// <summary>
    /// [S-5587] What a drop onto <paramref name="icon"/> is forwarded to, the target's own shell drop
    /// target: a desktop item itself (a folder takes the files, a program opens them, the Recycle Bin
    /// deletes them), an own icon's path target that may be looked at, an own Recycle Bin; null for
    /// everything else (a web address, a Settings page, a Windows location, a layout).
    /// </summary>
    public static string? DropTarget(DeskIcon icon)
    {
        ArgumentNullException.ThrowIfNull(icon);
        return icon.Kind switch
        {
            DeskIconKind.Item or DeskIconKind.ShellItem => icon.Value,
            DeskIconKind.OwnPath when LaunchTargets.MayProbe(LaunchTargets.Classify(icon.Value)) => icon.Value,
            DeskIconKind.OwnBin => RecycleBinItem,
            _ => null,
        };
    }
}
