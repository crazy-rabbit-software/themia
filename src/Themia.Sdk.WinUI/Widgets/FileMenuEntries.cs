using Themia.App.Services.Files;
using Themia.App.Shell;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Shell;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;

namespace Themia.App.Widgets;

/// <summary>
/// [S-6416] [S-5582] Who a file menu is drawn for: where it opens, what each of its commands does and
/// what its submenus hold. The folder widget and the desktop each give one; the lines' icons, words
/// and keys are <see cref="FileMenuEntries"/>' alone.
/// </summary>
/// <param name="Place">The folder widget or the desktop: which keys the lines show.</param>
/// <param name="Run">Carries out a command on the selection the menu was opened for.</param>
/// <param name="Children">The lines of a submenu command (<see cref="FileCommand.OpenWith"/>, <see cref="FileCommand.New"/>).</param>
/// <param name="OpensFolder">Open opens a folder: it is drawn with the open folder.</param>
public sealed record FileMenuTarget(
    FileMenuPlace Place,
    Action<FileCommand> Run,
    Func<FileCommand, IReadOnlyList<ItemMenuEntry>> Children,
    bool OpensFolder = false);

/// <summary>
/// [S-6416] [S-5582] The one place a file command becomes a menu line: its icon (Explorer's picture
/// where it has one, [S-1754]), its words, its key ([S-6417]) and its place in the icon row
/// ([S-1740]). The folder widget's item and background menus and the desktop icons' menu are all drawn
/// from here, so the same command reads the same wherever it stands.
/// </summary>
public static class FileMenuEntries
{
    private const string Scope = "file-menu";

    /// <summary>The lines of <paramref name="lines"/>, in their order, for <see cref="ItemMenu"/>.</summary>
    public static List<ItemMenuEntry> Of(IReadOnlyList<FileMenuLine> lines, FileMenuTarget target)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(target);
        return [.. lines.Select(line => Entry(line, target))];
    }

    /// <summary>One line. A command with no line here (the folder widget's own Up, Select all and Open in Explorer) is the caller's to word.</summary>
    public static ItemMenuEntry Entry(FileMenuLine line, FileMenuTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        var command = line.Command;
        if (command == FileCommand.Separator)
        {
            return ItemMenuEntry.Separator;
        }

        var (glyph, text) = Look(command, target.OpensFolder);
        var submenu = command is FileCommand.OpenWith or FileCommand.New;
        return new ItemMenuEntry(
            glyph,
            text,
            submenu ? null : () => target.Run(command),
            Danger: command == FileCommand.Delete,
            Disabled: !line.Enabled,
            Children: submenu ? target.Children(command) : null,
            Quick: FileMenu.Quick(command),
            Shortcut: FileMenu.Accelerator(command, target.Place));
    }

    private static (string Glyph, string Text) Look(FileCommand command, bool opensFolder) => command switch
    {
        FileCommand.Cut => (Glyphs.Cut, Str.CtxMenu_Cut.Text()),
        FileCommand.Copy => (Glyphs.Copy, Str.CtxMenu_Copy.Text()),
        FileCommand.Paste => (Glyphs.Paste, Str.CtxMenu_Paste.Text()),
        FileCommand.Rename => (Glyphs.Rename, Str.Common_Rename.Text()),
        FileCommand.Share => (Glyphs.Share, Str.FolderMenu_Share.Text()),
        FileCommand.Delete => (Glyphs.Delete, Str.Common_Delete.Text()),
        FileCommand.Open => (opensFolder ? Glyphs.FolderOpen : Glyphs.Open, Str.CtxMenu_Open.Text()),
        FileCommand.OpenLayout => (Glyphs.Open, Str.WidgetsC_Shortcuts_Menu_OpenLayout.Text()),
        FileCommand.OpenWith => (Glyphs.OpenWith, Str.FolderMenu_OpenWith.Text()),
        FileCommand.OpenInNewWindow => (Glyphs.NewWindow, Str.FolderMenu_OpenInNewWindow.Text()),
        FileCommand.OpenFileLocation => (Glyphs.Folder, Str.Shortcut_OpenFileLocation.Text()),
        FileCommand.PinToQuickAccess => (Glyphs.Pin, Str.FolderMenu_PinToQuickAccess.Text()),
        FileCommand.PinToStart => (Glyphs.PinToStart, Str.FolderMenu_PinToStart.Text()),
        FileCommand.CompressToZip => (Glyphs.ZipFolder, Str.FolderMenu_CompressToZip.Text()),
        FileCommand.CopyAsPath => (Glyphs.CopyAsPath, Str.FolderMenu_CopyAsPath.Text()),
        FileCommand.CopyPath => (Glyphs.CopyAsPath, Str.Shortcut_CopyPath.Text()),
        FileCommand.CopyUrl => (Glyphs.Link, Str.Shortcut_CopyUrl.Text()),
        FileCommand.EditLayout => (Glyphs.Edit, Str.Shortcut_EditLayoutMenu.Text()),
        FileCommand.EmptyRecycleBin => (Glyphs.EmptyBin, Str.RecycleBin_EmptyEllipsis.Text()),
        FileCommand.KeepOnDevice => (Glyphs.Pin, Str.WidgetsB_Folder_KeepOnDevice.Text()),
        FileCommand.FreeUpSpace => (Glyphs.Unpin, Str.WidgetsB_Folder_FreeUpSpace.Text()),
        FileCommand.Properties => (Glyphs.Properties, Str.Folder_Properties.Text()),
        FileCommand.IconProperties => (Glyphs.Properties, Str.Desktop_Menu_Properties.Text()),
        FileCommand.UnlockWidgets => (Glyphs.Unlock, Str.Shell_Menu_UnlockWidgets.Text()),
        FileCommand.ShowMoreOptions => (Glyphs.OpenExternal, Str.WidgetsB_Folder_MoreOptions.Text()),
        FileCommand.New => (Glyphs.Add, Str.WidgetsB_Folder_New.Text()),
        FileCommand.Refresh => (Glyphs.Refresh, Str.Common_Refresh.Text()),
        _ => throw new ArgumentOutOfRangeException(nameof(command), command, "this command is worded by the menu that offers it"),
    };

    /// <summary>
    /// [S-6416] "Open with": the apps Windows recommends for the file, with their icons, then
    /// "Choose another app" (Windows' own dialog), as Explorer's submenu.
    /// </summary>
    public static List<ItemMenuEntry> OpenWith(IExplorerMenus menus, IShellFileOps ops, string path)
    {
        ArgumentNullException.ThrowIfNull(menus);
        ArgumentNullException.ThrowIfNull(ops);
        var entries = menus.OpenWithApps(path)
            .Select(app => new ItemMenuEntry(Glyphs.Open, app.Label, () => menus.OpenWith(path, app.Name), IconFile: app.IconFile))
            .ToList();

        entries.Add(ItemMenuEntry.Separator);
        entries.Add(new(Glyphs.OpenWith, Str.FolderMenu_ChooseAnotherApp.Text(), () => ops.OpenWith(path)));
        return entries;
    }

    /// <summary>
    /// [S-1331] [S-5548] New ›: Folder, Shortcut where the place offers one (the desktop), then the
    /// shell's New file types.
    /// </summary>
    public static List<ItemMenuEntry> New(IReadOnlyList<ShellNewTemplate> templates, Action newFolder, Action? newShortcut, Action<ShellNewTemplate> newFile)
    {
        ArgumentNullException.ThrowIfNull(templates);
        ArgumentNullException.ThrowIfNull(newFile);
        // [S-5559] Each line under the icon of what it makes, as Explorer's New ›: the folder's, the shortcut's, a type's.
        var entries = new List<ItemMenuEntry> { new(Glyphs.NewFolder, Str.CtxMenu_Folder.Text(), newFolder, IconFile: TypeIconName.Folder) };
        if (newShortcut is not null)
        {
            entries.Add(new(Glyphs.Link, Str.Desktop_Menu_NewShortcut.Text(), newShortcut, IconFile: TypeIconName.Shortcut));
        }

        if (templates.Count > 0)
        {
            entries.Add(ItemMenuEntry.Separator);
            entries.AddRange(templates.Select(t => new ItemMenuEntry(Glyphs.File, t.Label, () => newFile(t), IconFile: TypeIconName.Of(t.Extension))));
        }

        return entries;
    }

    /// <summary>[S-6416] Copy as path: Explorer's own, each path quoted, a line each.</summary>
    public static void CopyAsPath(IReadOnlyList<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        if (paths.Count == 0)
        {
            return;
        }

        try
        {
            var package = new Windows.ApplicationModel.DataTransfer.DataPackage();
            package.SetText(FileMenu.QuotedPaths(paths));
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);
            Windows.ApplicationModel.DataTransfer.Clipboard.Flush();
        }
        catch (Exception ex)
        {
            Log.Warn(Scope, "could not copy the path", ex);
        }
    }

    /// <summary>
    /// [S-6416] Open file location: Explorer at the items' folder with them selected; for one shortcut
    /// whose target the shell resolves, at the target, as Explorer's line on a shortcut.
    /// </summary>
    public static void OpenFileLocation(IShellFileOps ops, IReadOnlyList<string> paths)
    {
        ArgumentNullException.ThrowIfNull(ops);
        ArgumentNullException.ThrowIfNull(paths);
        if (paths.Count == 1 && paths[0].EndsWith(".lnk", StringComparison.OrdinalIgnoreCase)
            && ops.ResolveShortcut(paths[0]) is { Length: > 0 } target)
        {
            ops.ShowInExplorer([target]);
            return;
        }

        ops.ShowInExplorer(paths);
    }
}
