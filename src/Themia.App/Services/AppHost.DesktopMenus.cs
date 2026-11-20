using Themia.App.Services.Files;
using Themia.App.Shell;
using Themia.App.Widgets;
using Themia.Core.Desktop;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Shell;
using Themia.Core.Widgets;
using Themia.Sdk;
using Windows.Foundation;

namespace Themia.App.Services;

/// <summary>
/// [S-5548] [S-5551] [S-5582] The desktop's menus outside edit mode, as Windows 11's desktop has them:
/// a right-click on an icon or on empty desktop (and the Menu key) opens the Windows 11 menu, drawn by
/// the one <see cref="ItemMenu"/> every Themia menu is; its Show more options (and Shift+F10, and a
/// right-click with Shift held) opens the shell's classic menu. Which lines a menu holds is a pure rule
/// (<see cref="DesktopCommands.Menu"/>, <see cref="DesktopBackground.Menu"/>); a file's lines are worded
/// by the builder the folder widget's menu uses (<see cref="FileMenuEntries"/>); every command runs
/// what its key runs ([S-5545]), never a second implementation.
/// </summary>
internal sealed partial class AppHost
{
    /// <summary>Where the last desktop menu opened, in screen pixels, and over which surface: Show more options opens the classic menu there.</summary>
    private (int X, int Y, nint Owner) _deskMenuAt;

    public void Context(string key, int screenX, int screenY, nint owner, bool classic)
    {
        CloseRenameFor(null);
        GestureOn(_windows.FirstOrDefault(w => w.IsAlive && w.Hwnd == owner));
        LayoutDesk();
        _desk.Context(key);
        _deskActive = true;
        RepaintDesk();
        ShowDeskMenu(screenX, screenY, owner, classic);
    }

    /// <summary>[S-5582] What the selection's menu depends on now.</summary>
    private DeskMenuFacts DeskFacts()
    {
        var bin = HostRecycleBin.Instance;
        return new DeskMenuFacts(RecycleBinState.CanEmpty(bin.Current, bin.Emptying), HostAppLayouts.Instance.CanEdit, HostFileClipboard.Instance.HasFiles, State.Preferences.LockWidgets);
    }

    /// <summary>Whether the desktop item <paramref name="key"/> is a folder, as the listing has it.</summary>
    private static bool IsDeskFolder(string key) => HostDesktopItems.Instance.Find(key)?.Kind == DesktopItemKind.Folder;

    /// <summary>
    /// [S-5582] The menu for the selection, by the desktop's one rule (<see cref="DesktopCommands.Menu"/>),
    /// at a screen point over <paramref name="owner"/>: the Windows 11 menu, or with
    /// <paramref name="classic"/> (Shift+F10, Shift held) the shell's classic menu at once where the
    /// selection has one.
    /// </summary>
    private void ShowDeskMenu(int screenX, int screenY, nint owner, bool classic)
    {
        var icons = DeskIcons(_desk.Selected);
        var menu = DesktopCommands.Menu(icons, DeskFacts(), IsDeskFolder);
        if (menu.Lines.Count == 0)
        {
            return;
        }

        _deskMenuAt = (screenX, screenY, owner);
        if (classic && menu.Classic.Count > 0)
        {
            ShowClassicItemMenu(menu, icons);
            return;
        }

        try
        {
            var surface = _windows.FirstOrDefault(w => w.IsAlive && w.Hwnd == owner) ?? KeyboardSurface ?? DesktopKeysSurface;
            var single = icons.Count == 1 ? icons[0] : null;
            var entries = DeskMenuEntries(
                menu,
                opensFolder: single is { Kind: DeskIconKind.Item } && IsDeskFolder(single.Value),
                command => RunDeskCommand(command, icons, menu),
                () => single is { Kind: DeskIconKind.Item }
                    ? FileMenuEntries.OpenWith(HostExplorerMenus.Instance, HostFileOps.Instance, single.Value)
                    : []);
            if (surface is null || !surface.OpenMenuAtScreen(screenX, screenY, entries))
            {
                Log.Warn(Scope, "the desktop icon menu did not open");
            }
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "the desktop icon menu failed", ex);
        }

        RepaintDesk();
    }

    /// <summary>
    /// [S-5582] A desktop icon menu's lines for <see cref="ItemMenu"/>: the rule's lines through the one
    /// file menu builder, on the desktop. Static: the menu render draws them too.
    /// </summary>
    internal static List<ItemMenuEntry> DeskMenuEntries(DeskMenu menu, bool opensFolder, Action<FileCommand> run, Func<IReadOnlyList<ItemMenuEntry>> openWith)
        => FileMenuEntries.Of(menu.Lines, new FileMenuTarget(
            FileMenuPlace.Desktop,
            run,
            command => command == FileCommand.OpenWith ? openWith() : [],
            opensFolder));

    /// <summary>
    /// [S-5582] One line of the desktop icon menu, carried out on <paramref name="icons"/> (the selection
    /// the menu was opened for) by what its key runs: the selection's own cut, copy, delete, rename, open
    /// and properties ([S-5545], [S-5583]..[S-5585]).
    /// </summary>
    private void RunDeskCommand(FileCommand command, IReadOnlyList<DeskIcon> icons, DeskMenu menu)
    {
        if (icons.Count == 0)
        {
            return;
        }

        var first = icons[0];
        var files = icons.Where(icon => icon.Kind == DeskIconKind.Item).Select(icon => icon.Value).ToList();
        switch (command)
        {
            case FileCommand.Cut:
                CopySelection(cut: true);
                break;
            case FileCommand.Copy:
                CopySelection(cut: false);
                break;
            case FileCommand.Paste:
                // Into the folder that was right-clicked, as Explorer's Paste on a folder does.
                _ = PasteAsync(into: first.Value);
                break;
            case FileCommand.Rename:
                BeginRename(first.Key, null);
                break;
            case FileCommand.Delete:
                DeleteSelection(permanent: false);
                break;
            case FileCommand.Open or FileCommand.OpenLayout:
                OpenIcons(icons);
                break;
            case FileCommand.Share:
                RunShellVerb(files, "Windows.ModernShare");
                break;
            case FileCommand.PinToQuickAccess:
                RunShellVerb(files, "pintohome");
                break;
            case FileCommand.PinToStart:
                RunShellVerb(files, "pintostartscreen");
                break;
            case FileCommand.OpenFileLocation when first.Own:
                if (HostLauncher.Instance.OpenContainingFolder(first.Target) is var outcome and not LaunchOutcome.Opened)
                {
                    Log.Warn(Scope, $"a desktop icon's file location did not open ({outcome})");
                }

                break;
            case FileCommand.OpenFileLocation:
                FileMenuEntries.OpenFileLocation(HostFileOps.Instance, files);
                break;
            case FileCommand.CompressToZip:
                _ = CompressAsync(files, first.Value);
                break;
            case FileCommand.CopyAsPath:
                FileMenuEntries.CopyAsPath(files);
                break;
            case FileCommand.CopyPath or FileCommand.CopyUrl:
                if (!ClipboardWatcher.SetText(DesktopCommands.CopyText(icons)))
                {
                    Log.Warn(Scope, "could not copy a desktop icon's path to the clipboard");
                }

                break;
            case FileCommand.EditLayout:
                if (DeskPlace(first.Key) is { } held && held.Widget.Config is IDesktopItemConfig layout)
                {
                    HostAppLayouts.Instance.Edit(layout.ItemId);
                }

                break;
            case FileCommand.EmptyRecycleBin:
                _ = HostRecycleBin.Instance.EmptyAsync();
                break;
            case FileCommand.Properties:
                ShowSelectionProperties();
                break;
            case FileCommand.IconProperties:
                ShowIconProperties(first.Key);
                break;
            case FileCommand.EditMode:
                SetEditMode(true);
                break;
            case FileCommand.LockWidgets:
                SetLockWidgets(!State.Preferences.LockWidgets);
                break;
            case FileCommand.HideFromView:
                HideDesktopIcons(icons);
                break;
            case FileCommand.CreateFence:
                // [S-13008] A fence round the selected desktop items, which move into it.
                CreateFenceFromSelection();
                break;
            case FileCommand.ShowMoreOptions:
                // After the click's own menu has closed: the classic menu is tracked in a loop of its own.
                if (!_dispatcher.TryEnqueue(() => ShowClassicItemMenu(menu, icons)))
                {
                    Log.Warn(Scope, "could not queue the shell's menu for the desktop icons");
                }

                break;
        }
    }

    /// <summary>[S-6416] One of the shell's own commands on desktop files (Share, the pins), logged when Windows has none here.</summary>
    private void RunShellVerb(IReadOnlyList<string> files, string verb)
    {
        try
        {
            if (files.Count > 0 && !ShellContextMenu.InvokeVerb(files, verb, DeskOwner))
            {
                Log.Info(Scope, $"the shell offers no '{verb}' for the selected desktop item(s)");
            }
        }
        catch (Exception ex)
        {
            Log.Error(Scope, $"the shell's '{verb}' failed for desktop items", ex);
        }
    }

    /// <summary>
    /// [S-5545] [S-5582] Alt+Enter and the menu's Properties: with an own icon focused, that icon's page;
    /// else the shell's Properties of the selected desktop items.
    /// </summary>
    private void ShowSelectionProperties()
    {
        if (DeskIconOf(_desk.Focus) is { Own: true } own)
        {
            ShowIconProperties(own.Key);
        }
        else if (DeskIcons(_desk.Selected).Where(icon => !icon.Own).Select(icon => icon.Value).ToList() is { Count: > 0 } items)
        {
            HostFileOps.Instance.Properties(items);
        }
    }

    /// <summary>
    /// [S-6419] Compress to ZIP file on the desktop: one new ZIP in the user's desktop folder, named
    /// after the icon the menu was opened on, landing in the next free cell with its rename box open
    /// ([S-5552]), as Explorer does.
    /// </summary>
    private async Task CompressAsync(IReadOnlyList<string> files, string named)
    {
        try
        {
            if (files.Count == 0 || DesktopFolderPath() is not { } desktop)
            {
                return;
            }

            var name = FileMenu.ZipName(Path.GetFileName(named), IsDeskFolder(named), Directory.EnumerateFileSystemEntries(desktop).Select(Path.GetFileName).OfType<string>());
            var surface = _windows.FirstOrDefault(w => w.IsAlive && w.Hwnd == _deskMenuAt.Owner) ?? KeyboardSurface ?? DesktopKeysSurface;
            if (surface is not null)
            {
                ExpectLanding(new DesktopLandingHint(surface.CurrentViewId, null, null, [name], Renames: true));
            }

            if (await HostFolders.Instance.CompressAsync(files, Path.Combine(desktop, name)).ConfigureAwait(true) is null)
            {
                Log.Warn(Scope, "the desktop selection could not be compressed to a ZIP file");
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            Log.Error(Scope, "compressing desktop items failed", ex);
        }
    }

    /// <summary>
    /// [S-5548] The shell's classic menu for what <paramref name="menu"/> says it opens for
    /// (<see cref="DeskMenu.Classic"/>): the selected desktop items, every shell extension's lines with
    /// it (Send to, Open with, an archiver's), its Rename the desktop's own box (the shell has no view of
    /// the desktop's here to rename in); or an own icon's target path, as the shell has it.
    /// </summary>
    private void ShowClassicItemMenu(DeskMenu menu, IReadOnlyList<DeskIcon> icons)
    {
        if (menu.Classic.Count == 0)
        {
            return;
        }

        var (x, y, owner) = _deskMenuAt;
        var ownTarget = icons.Any(icon => icon.Own);
        try
        {
            ShellContextMenu.ShowForItems(menu.Classic, x, y, owner, ownTarget ? null : verb =>
            {
                if (!string.Equals(verb, "rename", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                BeginRename(_desk.Focus, null);
                return true;
            });
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "the shell's menu for desktop icons failed", ex);
        }

        RepaintDesk();
    }

    // ---- the background menu ----------------------------------------------------------------------

    /// <summary>
    /// [S-5548] A right-click on empty desktop: the Windows 11 background menu
    /// (<see cref="DesktopBackground.Menu"/>), or with <paramref name="classic"/> (Shift+F10, Shift held)
    /// the shell's classic one at once. What a command brings (a new folder, a paste) lands where the
    /// click was ([S-5591]).
    /// </summary>
    public void DesktopBackgroundMenu(DesktopWindow surface, Point dip, bool classic)
    {
        var metrics = surface.Metrics;
        if (!metrics.IsUsable)
        {
            return;
        }

        var scale = surface.Monitor.Bounds.Width / metrics.Width;
        var (x, y) = ShellContextMenu.ToScreen(surface.Hwnd, (int)Math.Round(dip.X * scale), (int)Math.Round(dip.Y * scale));
        _deskMenuAt = (x, y, surface.Hwnd);
        if (classic)
        {
            ShowClassicDesktopMenu(surface, dip);
            return;
        }

        try
        {
            var facts = DesktopBackgroundFacts.Of(State, HostFileClipboard.Instance.HasFiles, ShellFileOps.TerminalPath() is not null, DesktopUndoOffered());
            var templates = ShellNewTemplates.GetAsync() is { IsCompletedSuccessfully: true } read ? read.Result : [];
            var entries = DesktopBackgroundEntries(
                DesktopBackground.Menu(facts),
                templates,
                command => RunDesktopMenuCommand(surface, dip, command),
                template => NewOnDesktop(surface, dip, desktop => HostFileOps.Instance.NewFile(desktop, template)),
                facts.Undo);
            if (!surface.OpenMenuAt(dip, entries))
            {
                Log.Warn(Scope, "the desktop's background menu did not open");
            }
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "the desktop's background menu failed", ex);
        }
    }

    /// <summary>
    /// [S-5548] The background menu's lines for <see cref="ItemMenu"/>, worded; New › is the file menus'
    /// own submenu (<see cref="FileMenuEntries.New"/>) with the shell's file types after Folder and
    /// Shortcut. Static: the menu render and <c>--desktop-menu-check</c> read them too.
    /// </summary>
    private const string SizeGroup = "desktop-icon-size";
    private const string LayoutGroup = "desktop-icon-layout";
    private const string SortGroup = "desktop-sort";

    internal static List<ItemMenuEntry> DesktopBackgroundEntries(
        IReadOnlyList<DesktopMenuEntry> lines, IReadOnlyList<ShellNewTemplate> templates, Action<DesktopMenuCommand> run, Action<ShellNewTemplate> newFile,
        DesktopUndoName? undo = null)
    {
        ItemMenuEntry Entry(DesktopMenuEntry line)
        {
            if (line.IsSeparator)
            {
                return ItemMenuEntry.Separator;
            }

            if (line.Group is { } group)
            {
                return group switch
                {
                    DesktopMenuGroup.View => new(Glyphs.View, Str.Desktop_Menu_View.Text(), null, Children: [.. line.Children.Select(Entry)]),
                    DesktopMenuGroup.SortBy => new(Glyphs.Sort, Str.Desktop_Menu_SortBy.Text(), null, Children: [.. line.Children.Select(Entry)]),
                    _ => new(Glyphs.Add, Str.WidgetsB_Folder_New.Text(), null, Children: FileMenuEntries.New(
                        templates, () => run(DesktopMenuCommand.NewFolder), () => run(DesktopMenuCommand.NewShortcut), newFile)),
                };
            }

            var command = line.Command!.Value;
            void Run() => run(command);
            return command switch
            {
                // [S-5593] [S-5596] One of a set: Windows draws the dot on the one that holds.
                DesktopMenuCommand.LargeIcons => new(null, Str.Desktop_Icons_Large.Text(), Run, Checked: line.Chosen, Group: SizeGroup),
                DesktopMenuCommand.MediumIcons => new(null, Str.Desktop_Icons_Medium.Text(), Run, Checked: line.Chosen, Group: SizeGroup),
                DesktopMenuCommand.SmallIcons => new(null, Str.Desktop_Icons_Small.Text(), Run, Checked: line.Chosen, Group: SizeGroup),

                // [S-13970] The layouts, a set of their own.
                DesktopMenuCommand.TextBelowIcons => new(null, Str.Desktop_Icons_TextBelow.Text(), Run, Checked: line.Chosen, Group: LayoutGroup),
                DesktopMenuCommand.TextBesideIcons => new(null, Str.Desktop_Icons_TextBeside.Text(), Run, Checked: line.Chosen, Group: LayoutGroup),
                DesktopMenuCommand.IconsOnly => new(null, Str.Desktop_Icons_IconOnly.Text(), Run, Checked: line.Chosen, Group: LayoutGroup),

                // [S-5594] A one-shot: it arranges now, no check mark.
                DesktopMenuCommand.AutoArrangeIcons => new(null, Str.Desktop_Icons_AutoArrange.Text(), Run),
                DesktopMenuCommand.SortByName => new(null, Str.Desktop_Menu_Sort_Name.Text(), Run, Checked: line.Chosen, Group: SortGroup),
                DesktopMenuCommand.SortBySize => new(null, Str.Desktop_Menu_Sort_Size.Text(), Run, Checked: line.Chosen, Group: SortGroup),
                DesktopMenuCommand.SortByItemType => new(null, Str.Desktop_Menu_Sort_Type.Text(), Run, Checked: line.Chosen, Group: SortGroup),
                DesktopMenuCommand.SortByDateModified => new(null, Str.Desktop_Menu_Sort_Date.Text(), Run, Checked: line.Chosen, Group: SortGroup),
                DesktopMenuCommand.Refresh => new(Glyphs.Refresh, Str.Desktop_Menu_Refresh.Text(), Run),
                DesktopMenuCommand.Paste => new(Glyphs.Paste, Str.CtxMenu_Paste.Text(), Run, Quick: QuickCommand.Paste, Shortcut: KeyChord.Ctrl(ChordKey.V)),
                DesktopMenuCommand.PasteShortcut => new(Glyphs.PasteShortcut, Str.Desktop_Menu_PasteShortcut.Text(), Run),
                DesktopMenuCommand.NewFolder => new(Glyphs.NewFolder, Str.CtxMenu_Folder.Text(), Run),
                DesktopMenuCommand.NewShortcut => new(Glyphs.Link, Str.Desktop_Menu_NewShortcut.Text(), Run),
                DesktopMenuCommand.DisplaySettings => new(Glyphs.Display, Str.Desktop_Menu_DisplaySettings.Text(), Run),
                DesktopMenuCommand.Personalise => new(Glyphs.Personalize, Str.Desktop_Menu_Personalise.Text(), Run),
                // Windows Terminal's own icon, as Explorer's line has it; the glyph until it is read, or for a Terminal the shell does not list.
                DesktopMenuCommand.OpenInTerminal => new(Glyphs.Terminal, Str.Desktop_Menu_OpenInTerminal.Text(), Run, IconFile: ShellFileOps.TerminalAppItem),
                // [S-65] Edit mode, a toggle line as Windows' View toggles: unchecked, the desktop being locked.
                DesktopMenuCommand.EditMode => new(Glyphs.Edit, Str.Shell_Menu_EditMode.Text(), Run, Checked: false),
                // [S-12951] Lock widgets, checked while on.
                DesktopMenuCommand.LockWidgets => new(Glyphs.Lock, Str.Shell_Menu_LockWidgets.Text(), Run, Checked: line.Checked),
                DesktopMenuCommand.AddWidget => new(Glyphs.Apps, Str.Shell_Toolbar_AddWidget.Text(), Run),
                DesktopMenuCommand.ShowMoreOptions => new(Glyphs.OpenExternal, Str.WidgetsB_Folder_MoreOptions.Text(), Run, Shortcut: KeyChord.Shift(ChordKey.F10)),

                // [S-13103] Explorer's own line: "Undo Delete", Ctrl+Z.
                DesktopMenuCommand.Undo => new(Glyphs.Undo, Str.Desktop_Undo_Line.Format(("action", UndoName(undo))), Run, Shortcut: KeyChord.Ctrl(ChordKey.Z)),
                _ => throw new ArgumentOutOfRangeException(nameof(lines), command, "a background menu command with no line"),
            };
        }

        return [.. lines.Select(Entry)];
    }

    /// <summary>[S-13103] The word the Undo line names its step by, as Explorer's ("Undo Rename").</summary>
    private static string UndoName(DesktopUndoName? name) => name switch
    {
        DesktopUndoName.Copy => Str.Desktop_Undo_Copy.Text(),
        DesktopUndoName.Move => Str.Desktop_Undo_Move.Text(),
        DesktopUndoName.NewFolder => Str.Desktop_Undo_NewFolder.Text(),
        DesktopUndoName.Shortcut => Str.Desktop_Undo_Shortcut.Text(),
        DesktopUndoName.Rename => Str.Desktop_Undo_Rename.Text(),
        DesktopUndoName.NewFile => Str.Desktop_Undo_New.Text(),
        _ => Str.Desktop_Undo_Delete.Text(),
    };

    /// <summary>
    /// [S-5548] [S-5551] [S-5554] [S-5557] One line of the background menu, opened on
    /// <paramref name="surface"/> at <paramref name="dip"/>: what it brings lands there ([S-5591]).
    /// </summary>
    private void RunDesktopMenuCommand(DesktopWindow surface, Point dip, DesktopMenuCommand command)
    {
        var metrics = surface.Metrics;
        if (!metrics.IsUsable)
        {
            return;
        }

        if (DesktopBackground.SortOf(command) is { } by)
        {
            // [S-5554] A Sort by line arranges this display's items.
            SortDesk(surface, by);
            return;
        }

        if (DesktopBackground.SizeOf(command) is { } size)
        {
            // [S-5593] A size line sets the one icon size, for every display.
            SetDesktopIconSize(size);
            return;
        }

        if (DesktopBackground.LayoutOf(command) is { } layout)
        {
            // [S-13970] A layout line sets the one icon layout, for every display and every fence.
            SetDesktopIconLayout(layout);
            return;
        }

        var hint = new DesktopLandingHint(surface.CurrentViewId, dip.X / metrics.Width, dip.Y / metrics.Height, []);
        switch (command)
        {
            case DesktopMenuCommand.AutoArrangeIcons:
                // [S-5594] Packs the icons once, now: one undo step, no setting.
                AutoArrangeDesktopIcons();
                break;
            case DesktopMenuCommand.Refresh:
                // [S-5554] Reads the desktop again, as F5.
                HostDesktopItems.Instance.Refresh();
                break;
            case DesktopMenuCommand.Undo:
                // [S-13103] Ctrl+Z's own step.
                Undo();
                break;

            case DesktopMenuCommand.Paste:
                _ = PasteAsync(at: (surface, hint.X!.Value, hint.Y!.Value));
                break;
            case DesktopMenuCommand.PasteShortcut:
                // [S-13102] The shortcuts it makes are one step: their undo recycles them.
                ExpectLanding(hint with { Undo = DesktopFileUndoKind.Shortcut });
                RunDesktopVerb(surface, "pastelink");
                break;
            case DesktopMenuCommand.NewFolder:
                NewOnDesktop(surface, dip, HostFileOps.Instance.NewFolder);
                break;
            case DesktopMenuCommand.NewShortcut:
                // Windows' own Create Shortcut wizard, which names what it makes.
                ExpectLanding(hint);
                RunDesktopVerb(surface, "NewLink");
                break;
            case DesktopMenuCommand.DisplaySettings:
                OpenSettingsPage("ms-settings:display");
                break;
            case DesktopMenuCommand.Personalise:
                OpenSettingsPage("ms-settings:personalization");
                break;
            case DesktopMenuCommand.OpenInTerminal:
                if (DesktopFolderPath() is { } desktop)
                {
                    ShellFileOps.OpenTerminal(desktop);
                }

                break;
            case DesktopMenuCommand.EditMode:
                SetEditMode(true);
                break;
            case DesktopMenuCommand.LockWidgets:
                SetLockWidgets(!State.Preferences.LockWidgets);
                break;
            case DesktopMenuCommand.AddWidget:
                // [S-5551] Edit mode on, then the add page under that display's bar ([S-11707]).
                SetEditMode(true);
                if (!_dispatcher.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, surface.OpenAddPage))
                {
                    Log.Warn(Scope, "could not queue Add widget from the desktop's menu; edit mode is on, the bar's Add widget opens it");
                }

                break;
            case DesktopMenuCommand.ShowMoreOptions:
                // After the click's own menu has closed: the classic menu is tracked in a loop of its own.
                if (!_dispatcher.TryEnqueue(() => ShowClassicDesktopMenu(surface, dip)))
                {
                    Log.Warn(Scope, "could not queue the shell's background menu");
                }

                break;
        }
    }

    private static void OpenSettingsPage(string page)
    {
        if (HostLauncher.Instance.OpenWindowsSettings(new Uri(page)) is var outcome and not LaunchOutcome.Opened)
        {
            Log.Warn(Scope, $"a Windows Settings page did not open from the desktop's menu ({outcome})");
        }
    }

    /// <summary>One of the desktop background's own shell commands by its verb ("pastelink", "NewLink"), as its line in the classic menu.</summary>
    private static void RunDesktopVerb(DesktopWindow surface, string verb)
    {
        try
        {
            if (!ShellContextMenu.InvokeDesktopVerb(verb, surface.Hwnd))
            {
                Log.Warn(Scope, $"the shell did not run '{verb}' on the desktop");
            }
        }
        catch (Exception ex)
        {
            Log.Error(Scope, $"the desktop's '{verb}' failed", ex);
        }
    }

    /// <summary>
    /// [S-5552] New › made something in the user's desktop folder (<paramref name="make"/>: its path, or
    /// null): it lands at the click and opens its rename box, as Explorer's New › does.
    /// </summary>
    private void NewOnDesktop(DesktopWindow surface, Point dip, Func<string, string?> make)
    {
        var metrics = surface.Metrics;
        if (!metrics.IsUsable || DesktopFolderPath() is not { } desktop)
        {
            return;
        }

        if (make(desktop) is not { } created || Path.GetFileName(created) is not { Length: > 0 } name)
        {
            Log.Warn(Scope, "New › made nothing on the desktop");
            return;
        }

        // [S-13102] What New › made is one step of the undo log (its undo recycles it), as Explorer's Undo New.
        var undo = Directory.Exists(created) ? DesktopFileUndoKind.NewFolder : DesktopFileUndoKind.NewFile;
        ExpectLanding(new DesktopLandingHint(surface.CurrentViewId, dip.X / metrics.Width, dip.Y / metrics.Height, [name], Renames: true) { Undo = undo });
    }

    /// <summary>
    /// [S-5548] The shell's classic background menu (Show more options, Shift+F10): the desktop root's own
    /// lines, every extension's with them and nothing of Themia's. What a command there brings lands at
    /// the click, and what its New › made opens its rename box ([S-5552]).
    /// </summary>
    private void ShowClassicDesktopMenu(DesktopWindow surface, Point dip)
    {
        var metrics = surface.Metrics;
        if (!metrics.IsUsable)
        {
            return;
        }

        try
        {
            var (x, y, _) = _deskMenuAt;
            var choice = ShellContextMenu.ShowForDesktop(x, y, surface.Hwnd);
            if (choice.Ran)
            {
                ExpectLanding(new DesktopLandingHint(surface.CurrentViewId, dip.X / metrics.Width, dip.Y / metrics.Height, [], Renames: choice.New));
            }
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "the shell's background menu failed", ex);
        }
    }

    /// <summary>
    /// [S-5545] The Menu key: the Windows 11 menu for the selection at the focused icon, else the
    /// background menu; Shift+F10 (<paramref name="classic"/>) the shell's classic one, as Explorer.
    /// </summary>
    private void MenuFromKeys(bool classic)
    {
        var surface = KeyboardSurface ?? DesktopKeysSurface;
        if (surface is null)
        {
            return;
        }

        // [S-5540] The menu stands at the focused icon where the user is: its box and its surface on the display of the last gesture.
        if (_desk.Selected.Count > 0 && _desk.PlaceOf(_desk.Focus) is { } box)
        {
            var on = DeskPlace(box.Key)?.Surface ?? surface;
            ShowDeskMenu((int)box.CenterX, (int)box.CenterY, on.Hwnd, classic);
            return;
        }

        var metrics = surface.Metrics;
        DesktopBackgroundMenu(surface, new Point(metrics.Width / 2, metrics.Height / 2), classic);
    }
}
