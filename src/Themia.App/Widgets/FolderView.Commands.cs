using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Themia.App.Services.Files;
using Themia.App.Shell;
using Themia.Core.Diagnostics;
using Themia.Core.Files;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Core.Shell;
using Themia.Sdk.WinUI;

namespace Themia.App.Widgets;

/// <summary>[S-1329]..[S-1331] Opening, the menus, the clipboard, delete, rename, new, search.</summary>
internal sealed partial class FolderView
{
    /// <summary>Opening more than this many at once asks nothing, it just stops: a stray Ctrl+A, Enter is not forty windows.</summary>
    private const int OpenAtMost = 15;

    private RenameSession? _renaming;

    /// <summary>A new folder or file to rename as soon as the reload shows it ([S-1331]).</summary>
    private string? _renameAfterLoad;

    private bool _closingSearch;

    private sealed record RenameSession(ItemVisual Visual, TextBox Box);

    /// <summary>Where the last menu opened, in screen pixels: "Show more options" opens the shell's menu there.</summary>
    private (int X, int Y) _menuPoint;

    private void RememberMenuPoint(UIElement element, Windows.Foundation.Point local)
    {
        var root = element.TransformToVisual(null).TransformPoint(local);
        var scale = XamlRoot?.RasterizationScale ?? 1.0;
        _menuPoint = _widgetHost.ExplorerMenus.ToScreen(Window(), (int)(root.X * scale), (int)(root.Y * scale));
    }

    /// <summary>[S-1330] Windows' own menu for the selection, or for the folder's background.</summary>
    private void ShowShellMenu(IReadOnlyList<string>? items)
    {
        var (x, y) = _menuPoint;

        if (items is { Count: > 0 })
        {
            _widgetHost.ExplorerMenus.ShowForItems(items, x, y, Window());
        }
        else if (CurrentPath.Length > 0)
        {
            _widgetHost.ExplorerMenus.ShowForBackground(CurrentPath, x, y, Window());
        }
    }

    // ---- opening --------------------------------------------------------------------------------

    /// <summary>
    /// [S-1327] Open an item: a folder inside the widget (navigate mode) or in Explorer; a shortcut to a
    /// folder likewise, resolved first; anything else as Explorer would.
    /// </summary>
    private void OpenEntry(FolderEntry entry)
    {
        if (entry.IsDirectory)
        {
            if (Navigates)
            {
                NavigateTo(entry.FullPath);
            }
            else
            {
                _widgetHost.FileOps.Open(entry.FullPath);
            }

            return;
        }

        if (Navigates && entry.Name.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase)
            && _widgetHost.FileOps.ResolveShortcut(entry.FullPath) is { } target && Directory.Exists(target))
        {
            NavigateTo(target);
            return;
        }

        _widgetHost.FileOps.Open(entry.FullPath);
    }

    private void OpenSelection()
    {
        var selected = SelectedEntries();

        if (selected.Count == 1)
        {
            OpenEntry(selected[0]);
            return;
        }

        foreach (var entry in selected.Take(OpenAtMost))
        {
            _widgetHost.FileOps.Open(entry.FullPath);
        }
    }

    private List<FolderEntry> SelectedEntries() => [.. _shown.Where(e => _selection.IsSelected(e.FullPath))];

    // ---- clipboard ------------------------------------------------------------------------------

    /// <summary>[S-1329] Ctrl+C / Ctrl+X: the selection onto the Windows clipboard, as Explorer puts it there.</summary>
    private void CopySelection(bool cut)
    {
        var paths = _selection.Selected;

        if (paths.Count > 0 && !_widgetHost.FileClipboard.SetFiles(paths, cut))
        {
            Log.Warn(Scope, "the selection could not be put on the clipboard");
        }
    }

    /// <summary>
    /// [S-1329] Ctrl+V: files copied or cut anywhere (Explorer, another folder widget) into
    /// <paramref name="destination"/>; a cut moves and then empties the clipboard; virtual files (an
    /// Outlook attachment) are saved.
    /// </summary>
    private async Task PasteAsync(string destination)
    {
        try
        {
            if (destination.Length == 0 || _widgetHost.FileClipboard.Read() is not { } content)
            {
                return;
            }

            if (content.Paths.Count > 0)
            {
                if (content.Effect == DropEffect.Move)
                {
                    if (await _widgetHost.FileOps.MoveAsync(content.Paths, destination).ConfigureAwait(true) == FileOpOutcome.Done)
                    {
                        _widgetHost.FileClipboard.CompleteCut();
                    }
                }
                else
                {
                    await _widgetHost.FileOps.CopyAsync(content.Paths, destination).ConfigureAwait(true);
                }
            }
            else if (content.VirtualFiles > 0)
            {
                _widgetHost.FileClipboard.SaveVirtualFiles(destination);
                Reload(force: false);
            }
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "paste failed", ex);
        }
    }

    /// <summary>Copy path: Explorer's "Copy as path", quoted, one per line.</summary>
    private static void CopyPaths(IReadOnlyList<string> paths)
    {
        if (paths.Count == 0)
        {
            return;
        }

        try
        {
            var package = new Windows.ApplicationModel.DataTransfer.DataPackage();
            package.SetText(string.Join("\r\n", paths.Select(p => $"\"{p}\"")));
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);
            Windows.ApplicationModel.DataTransfer.Clipboard.Flush();
        }
        catch (Exception ex)
        {
            Log.Warn(Scope, "could not copy the path", ex);
        }
    }

    // ---- delete ---------------------------------------------------------------------------------

    /// <summary>[S-1330] Delete: asks first, then the Recycle Bin through the shell. Never permanent.</summary>
    private async Task DeleteSelectionAsync()
    {
        try
        {
            var paths = _selection.Selected;

            if (paths.Count == 0)
            {
                return;
            }

            var question = paths.Count == 1
                ? Str.WidgetsB_Folder_RecycleOne.Format(("name", Path.GetFileName(paths[0])))
                : Str.WidgetsB_Folder_RecycleMany.Plural(paths.Count);

            if (!await ConfirmFlyout.AskAsync(this, question, Str.Common_Delete.Text()).ConfigureAwait(true))
            {
                return;
            }

            var outcome = await _widgetHost.FileOps.RecycleAsync(paths).ConfigureAwait(true);

            if (outcome == FileOpOutcome.Failed)
            {
                Log.Warn(Scope, $"{paths.Count} item(s) could not be moved to the Recycle Bin");
            }
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "delete failed", ex);
        }
    }

    // ---- rename ---------------------------------------------------------------------------------

    private void RenameSelection()
    {
        if (_selection.Count != 1 || _visuals.FirstOrDefault(v => _selection.IsSelected(v.Entry.FullPath)) is not { } visual)
        {
            return;
        }

        BeginRename(visual);
    }

    /// <summary>
    /// [S-1331] Rename in place: the name becomes a text box holding the name as shown (without the
    /// extension when extensions are hidden, as Explorer does); Enter or a click elsewhere commits,
    /// Escape cancels.
    /// </summary>
    private void BeginRename(ItemVisual visual)
    {
        CancelRename();

        var shown = FileNames.Display(visual.Entry.Name, visual.Entry.IsDirectory, _config.Extensions);
        var box = new TextBox
        {
            Text = shown,
            Padding = new Thickness(4, 0, 4, 0),
            MinHeight = 0,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.NoWrap,
        };

        _renaming = new RenameSession(visual, box);
        visual.Name.Visibility = Visibility.Collapsed;
        visual.NameCell.Children.Add(box);

        box.GotFocus += (_, _) => _surface?.RequestTextInput(true);
        box.LostFocus += (_, _) =>
        {
            _surface?.RequestTextInput(false);
            CommitRename();
        };
        box.KeyDown += (_, args) =>
        {
            if (args.Key == Windows.System.VirtualKey.Enter)
            {
                args.Handled = true;
                CommitRename();
            }
            else if (args.Key == Windows.System.VirtualKey.Escape)
            {
                args.Handled = true;
                CancelRename();
            }
        };

        box.Loaded += (_, _) =>
        {
            box.Focus(FocusState.Programmatic);

            // The stem selected, the extension left alone, as Explorer selects it.
            var dot = visual.Entry.IsDirectory ? -1 : shown.LastIndexOf('.');
            box.Select(0, dot > 0 ? dot : shown.Length);
        };
    }

    private void CommitRename()
    {
        if (_renaming is not { } session)
        {
            return;
        }

        _renaming = null;
        var entry = session.Visual.Entry;
        var name = FileNames.FromEdited(entry.Name, session.Box.Text, entry.IsDirectory, _config.Extensions);
        EndRename(session);

        if (string.Equals(name, entry.Name, StringComparison.Ordinal))
        {
            return;
        }

        var problem = FileNames.Validate(name);

        if (problem != FileNameProblem.None)
        {
            ShowNote(session.Visual.Root, FileNames.Message(problem));
            return;
        }

        _ = RenameAsync(entry.FullPath, name);
    }

    private async Task RenameAsync(string path, string name)
    {
        try
        {
            if (await _widgetHost.FileOps.RenameAsync(path, name).ConfigureAwait(true) == FileOpOutcome.Done)
            {
                // Keep the renamed item selected once the reload draws it under its new name.
                var renamed = Path.Combine(Path.GetDirectoryName(path) ?? string.Empty, name);
                _selection.SetOrder([.. _shown.Select(e => e.FullPath), renamed]);
                _selection.SelectOnly(renamed);
            }
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "rename failed", ex);
        }
    }

    private void CancelRename()
    {
        if (_renaming is { } session)
        {
            _renaming = null;
            EndRename(session);
        }
    }

    private static void EndRename(RenameSession session)
    {
        session.Visual.NameCell.Children.Remove(session.Box);
        session.Visual.Name.Visibility = Visibility.Visible;
    }

    private static void ShowNote(FrameworkElement at, string message)
    {
        var text = Fluent.Text(FluentType.Body);
        text.Text = message;
        text.TextWrapping = TextWrapping.Wrap;
        text.MaxWidth = 240;

        try
        {
            // [S-1500] Its own popup: inside the surface's window the region clips it to the card.
            new Flyout { Content = text, ShouldConstrainToRootBounds = false }.ShowAt(at);
        }
        catch (Exception ex)
        {
            Log.Warn(Scope, $"could not show: {message}", ex);
        }
    }

    // ---- new ------------------------------------------------------------------------------------

    /// <summary>[S-1331] New folder: created, then renamed in place as soon as the reload shows it.</summary>
    private void NewFolder()
    {
        if (CurrentPath.Length > 0 && _widgetHost.FileOps.NewFolder(CurrentPath) is { } created)
        {
            _renameAfterLoad = created;
            Reload(force: false);
        }
    }

    private void NewFile(ShellNewTemplate template)
    {
        if (CurrentPath.Length > 0 && _widgetHost.FileOps.NewFile(CurrentPath, template) is { } created)
        {
            _renameAfterLoad = created;
            Reload(force: false);
        }
    }

    // ---- menus ----------------------------------------------------------------------------------

    private void OnBackgroundRightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        try
        {
            CommitRename();

            RememberMenuPoint(_scrollContent, e.GetPosition(_scrollContent));

            if (VisualAt(e.OriginalSource) is { } visual)
            {
                _selection.Context(visual.Index);
                RefreshSelectionVisuals();
                ItemMenu.Show(visual.Root, e, ItemEntries());
            }
            else
            {
                ItemMenu.Show(_scrollContent, e, BackgroundEntries());
            }
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "the menu could not be opened", ex);
        }
    }

    /// <summary>[S-6417] Shift+F10: the shell's full menu for the selection (or the folder), at the focused item.</summary>
    private void ShowShellMenuFromKeyboard()
    {
        var focus = _selection.FocusIndex;
        FrameworkElement anchor = focus >= 0 && focus < _visuals.Count ? _visuals[focus].Root : this;
        RememberMenuPoint(anchor, new Windows.Foundation.Point(anchor.ActualWidth / 2, anchor.ActualHeight / 2));
        ShowShellMenu(_selection.IsEmpty ? null : _selection.Selected);
    }

    private void ShowMenuFromKeyboard()
    {
        var focus = _selection.FocusIndex;
        var entries = focus >= 0 && focus < _visuals.Count && !_selection.IsEmpty ? ItemEntries() : BackgroundEntries();

        FrameworkElement anchor = focus >= 0 && focus < _visuals.Count ? _visuals[focus].Root : this;
        RememberMenuPoint(anchor, new Windows.Foundation.Point(anchor.ActualWidth / 2, anchor.ActualHeight / 2));
        ItemMenu.Open(anchor, entries);
    }

    /// <summary>
    /// [S-6416] The item menu, as Windows 11's File Explorer draws it: which commands, in which
    /// order, is <see cref="FolderMenu.Item"/>'s (tested in Core); this maps each to its icon, label,
    /// key and action, and <see cref="ItemMenu"/> puts the row commands into the icon row.
    /// </summary>
    private List<ItemMenuEntry> ItemEntries()
    {
        var selected = SelectedEntries();
        var paths = selected.Select(e => e.FullPath).ToList();
        var single = selected.Count == 1 ? selected[0] : (FolderEntry?)null;

        return [.. FolderMenu.Item(selected, Navigates, _widgetHost.FileClipboard.HasFiles).Select(line => ItemEntry(line, selected, paths, single))];
    }

    private ItemMenuEntry ItemEntry(FolderMenuLine line, List<FolderEntry> selected, List<string> paths, FolderEntry? single)
    {
        var count = selected.Count;
        var several = count > 1;
        var disabled = !line.Enabled;
        var keys = Accelerator(line.Command);
        var quick = FolderMenu.Quick(line.Command);

        return line.Command switch
        {
            FolderCommand.Separator => ItemMenuEntry.Separator,
            FolderCommand.Cut => new(Glyphs.Cut, several ? Str.WidgetsB_Folder_CutCount.Format(("count", count)) : Str.CtxMenu_Cut.Text(), () => CopySelection(cut: true), Quick: quick, Shortcut: keys),
            FolderCommand.Copy => new(Glyphs.Copy, several ? Str.WidgetsB_Folder_CopyCount.Format(("count", count)) : Str.CtxMenu_Copy.Text(), () => CopySelection(cut: false), Quick: quick, Shortcut: keys),

            // Into the folder that was right-clicked, as Explorer's Paste on a folder does.
            FolderCommand.Paste => new(Glyphs.Paste, Str.CtxMenu_Paste.Text(), () => _ = PasteAsync(single?.FullPath ?? CurrentPath), Disabled: disabled, Quick: quick, Shortcut: keys),
            FolderCommand.Rename => new(Glyphs.Rename, Str.WidgetsB_Folder_Rename.Text(), RenameSelection, Quick: quick, Shortcut: keys),
            FolderCommand.Share => new(Glyphs.Share, Str.FolderMenu_Share.Text(), () => RunShellVerb(paths, "Windows.ModernShare"), Quick: quick),
            FolderCommand.Delete => new(Glyphs.Delete, several ? Str.WidgetsB_Folder_DeleteCount.Format(("count", count)) : Str.Common_Delete.Text(), () => _ = DeleteSelectionAsync(), Danger: true, Quick: quick, Shortcut: keys),
            FolderCommand.Open => new(single is { IsDirectory: true } ? Glyphs.FolderOpen : Glyphs.Open, Str.CtxMenu_Open.Text(), OpenSelection, Shortcut: keys),
            FolderCommand.OpenWith => new(Glyphs.OpenWith, Str.FolderMenu_OpenWith.Text(), null, Children: OpenWithEntries(single!.Value.FullPath)),
            FolderCommand.OpenInNewWindow => new(Glyphs.NewWindow, Str.FolderMenu_OpenInNewWindow.Text(), () => _widgetHost.FileOps.Open(single!.Value.FullPath)),
            FolderCommand.OpenFileLocation => new(Glyphs.Folder, Str.FolderMenu_OpenFileLocation.Text(), () => _widgetHost.FileOps.ShowInExplorer(paths)),
            FolderCommand.PinToQuickAccess => new(Glyphs.Pin, Str.FolderMenu_PinToQuickAccess.Text(), () => RunShellVerb(paths, "pintohome")),
            FolderCommand.PinToStart => new(Glyphs.Pin, Str.FolderMenu_PinToStart.Text(), () => RunShellVerb(paths, "pintostartscreen")),
            FolderCommand.CompressToZip => new(Glyphs.ZipFolder, Str.FolderMenu_CompressToZip.Text(), () => _ = CompressAsync(selected)),
            FolderCommand.CopyAsPath => new(Glyphs.CopyAsPath, Str.FolderMenu_CopyAsPath.Text(), () => CopyPaths(paths), Shortcut: keys),
            FolderCommand.KeepOnDevice => new(Glyphs.Pin, Str.WidgetsB_Folder_KeepOnDevice.Text(), () => _widgetHost.FileOps.SetCloudPin(paths, CloudPin.Pin)),
            FolderCommand.FreeUpSpace => new(Glyphs.Unpin, Str.WidgetsB_Folder_FreeUpSpace.Text(), () => _widgetHost.FileOps.SetCloudPin(paths, CloudPin.Unpin)),
            FolderCommand.Properties => new(Glyphs.Properties, Str.Folder_Properties.Text(), () => _widgetHost.FileOps.Properties(paths), Shortcut: keys),
            FolderCommand.ShowMoreOptions => new(Glyphs.OpenExternal, Str.WidgetsB_Folder_MoreOptions.Text(), () => ShowShellMenu(paths), Shortcut: keys),
            _ => ItemMenuEntry.Separator,
        };
    }

    /// <summary>
    /// [S-6416] "Open with": the apps Windows recommends for the file, with their icons, then
    /// "Choose another app" (Windows' own dialog), as Explorer's submenu.
    /// </summary>
    private List<ItemMenuEntry> OpenWithEntries(string path)
    {
        var entries = _widgetHost.ExplorerMenus.OpenWithApps(path)
            .Select(app => new ItemMenuEntry(Glyphs.Open, app.Label, () => _widgetHost.ExplorerMenus.OpenWith(path, app.Name), IconFile: app.IconFile))
            .ToList();

        entries.Add(ItemMenuEntry.Separator);
        entries.Add(new(Glyphs.OpenWith, Str.FolderMenu_ChooseAnotherApp.Text(), () => _widgetHost.FileOps.OpenWith(path)));
        return entries;
    }

    /// <summary>[S-6418] The background menu: <see cref="FolderMenu.Background"/>'s commands, drawn as the item menu's.</summary>
    private List<ItemMenuEntry> BackgroundEntries()
    {
        var here = CurrentPath;
        var facts = new FolderBackgroundFacts(
            HasPath: here.Length > 0,
            Listed: here.Length > 0 && _snapshot.Status is FolderListingStatus.Ok or FolderListingStatus.Empty,
            HasItems: _shown.Count > 0,
            ClipboardHasFiles: _widgetHost.FileClipboard.HasFiles,
            CanGoUp: Navigates && _history.CanGoUp);

        return [.. FolderMenu.Background(facts).Select(line => BackgroundEntry(line, here))];
    }

    private ItemMenuEntry BackgroundEntry(FolderMenuLine line, string here)
    {
        var disabled = !line.Enabled;
        var keys = Accelerator(line.Command);

        return line.Command switch
        {
            FolderCommand.Paste => new(Glyphs.Paste, Str.CtxMenu_Paste.Text(), () => _ = PasteAsync(here), Disabled: disabled, Quick: QuickCommand.Paste, Shortcut: keys),
            FolderCommand.SelectAll => new(Glyphs.SelectAll, Str.WidgetsB_Folder_SelectAll.Text(), () =>
            {
                _selection.SelectAll();
                RefreshSelectionVisuals();
            }, Disabled: disabled, Shortcut: keys),
            FolderCommand.Up => new(Glyphs.MoveUp, Str.WidgetsB_Folder_Up.Text(), GoUp),
            FolderCommand.Refresh => new(Glyphs.Refresh, Str.Common_Refresh.Text(), () => Reload(force: false)),
            FolderCommand.New => new(Glyphs.Add, Str.WidgetsB_Folder_New.Text(), null, Disabled: disabled, Children: NewEntries()),
            FolderCommand.OpenInExplorer => new(Glyphs.FolderOpen, Str.Folder_OpenInExplorer.Text(), () => _widgetHost.FileOps.Open(here), Disabled: disabled),
            FolderCommand.CopyAsPath => new(Glyphs.CopyAsPath, Str.FolderMenu_CopyAsPath.Text(), () => CopyPaths([here]), Disabled: disabled),
            FolderCommand.Properties => new(Glyphs.Properties, Str.Folder_Properties.Text(), () => _widgetHost.FileOps.Properties([here]), Disabled: disabled),
            FolderCommand.ShowMoreOptions => new(Glyphs.OpenExternal, Str.WidgetsB_Folder_MoreOptions.Text(), () => ShowShellMenu(null), Disabled: disabled, Shortcut: keys),
            _ => ItemMenuEntry.Separator,
        };
    }

    /// <summary>[S-1331] New: Folder, then the ShellNew templates.</summary>
    private List<ItemMenuEntry> NewEntries()
    {
        var templates = _widgetHost.Folders.NewFileTemplatesAsync() is { IsCompletedSuccessfully: true } done ? done.Result : [];
        var entries = new List<ItemMenuEntry> { new(Glyphs.NewFolder, Str.CtxMenu_Folder.Text(), NewFolder) };

        if (templates.Count > 0)
        {
            entries.Add(ItemMenuEntry.Separator);
            entries.AddRange(templates.Select(t => new ItemMenuEntry(Glyphs.File, t.Label, () => NewFile(t))));
        }

        return entries;
    }

    /// <summary>[S-6417] The key a menu line shows: only those the widget answers.</summary>
    private static KeyChord? Accelerator(FolderCommand command) => FolderMenu.Accelerator(command);

    /// <summary>[S-6417] Alt+Enter: the selection's properties, or the shown folder's when nothing is selected.</summary>
    private void ShowProperties()
    {
        var paths = _selection.Selected;
        if (paths.Count > 0)
        {
            _widgetHost.FileOps.Properties(paths);
        }
        else if (CurrentPath.Length > 0)
        {
            _widgetHost.FileOps.Properties([CurrentPath]);
        }
    }

    /// <summary>[S-6416] One of the shell's own commands (Share, the pins); said at the widget when Windows has none here.</summary>
    private void RunShellVerb(IReadOnlyList<string> paths, string verb)
    {
        if (!_widgetHost.ExplorerMenus.InvokeVerb(paths, verb, Window()))
        {
            ShowNote(this, Str.FolderMenu_ShellFailed.Text());
        }
    }

    /// <summary>
    /// [S-6419] Compress to ZIP file: one new ZIP beside the items, named after the one the menu was
    /// opened on, then renamed in place as Explorer does.
    /// </summary>
    private async Task CompressAsync(List<FolderEntry> selected)
    {
        try
        {
            var here = CurrentPath;

            if (selected.Count == 0 || here.Length == 0)
            {
                return;
            }

            var named = _selection.Focus is { } focus && selected.FirstOrDefault(e => string.Equals(e.FullPath, focus, StringComparison.OrdinalIgnoreCase)) is { Name.Length: > 0 } focused
                ? focused
                : selected[0];
            var name = FolderMenu.ZipName(named, _snapshot.Entries.Select(e => e.Name));
            var zip = await _widgetHost.Folders.CompressAsync([.. selected.Select(e => e.FullPath)], Path.Combine(here, name)).ConfigureAwait(true);

            if (zip is null)
            {
                ShowNote(this, Str.FolderMenu_ZipFailed.Text());
                return;
            }

            if (!_detached && string.Equals(here, CurrentPath, StringComparison.OrdinalIgnoreCase))
            {
                _renameAfterLoad = zip;
                Reload(force: false);
            }
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "compressing failed", ex);
        }
    }

    /// <summary>
    /// [S-1330] The card's own menu (right-click on the header): open the folder in Explorer, refresh.
    /// [S-1746] The view, the size, the columns, hidden items and extensions are how the widget looks:
    /// they are in its settings only.
    /// </summary>
    public IReadOnlyList<WidgetAction> ContextActions =>
    [
        new(Glyphs.OpenExternal, Str.Folder_OpenInExplorer.Text(), () => _widgetHost.FileOps.Open(CurrentPath)),
        new(Glyphs.Refresh, Str.Common_Refresh.Text(), () => Reload(force: false)),
    ];

    // ---- search ---------------------------------------------------------------------------------

    /// <summary>[S-1326] The header's search: a box above the list that filters as you type.</summary>
    private void ToggleSearch()
    {
        if (_searchOpen)
        {
            CloseSearch();
            return;
        }

        _searchOpen = true;
        Lay();
        _searchBox.Visibility = Visibility.Visible;
        _searchBox.Focus(FocusState.Programmatic);
    }

    private void CloseSearch()
    {
        if (!_searchOpen && _search.Length == 0)
        {
            return;
        }

        _searchOpen = false;
        _closingSearch = true;
        _searchBox.Text = string.Empty;
        _closingSearch = false;
        _search = string.Empty;
        Lay();
        Render();
    }

    private void OnSearchKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Escape)
        {
            e.Handled = true;
            CloseSearch();
        }
        else if (e.Key == Windows.System.VirtualKey.Enter && _shown.Count > 0)
        {
            e.Handled = true;
            OpenEntry(_shown[0]);
        }
    }
}
