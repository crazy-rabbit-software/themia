using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml;
using Themia.App.Services.Files;
using Themia.App.Shell;
using Themia.Core.Diagnostics;
using Themia.Core.Files;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Shell;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;
using Themia.Widget.Folder;

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
                ? FolderStr.WidgetsB_Folder_RecycleOne.Format(("name", Path.GetFileName(paths[0])))
                : FolderStr.WidgetsB_Folder_RecycleMany.Plural(paths.Count);

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
            Padding = new Thickness(FamilySpace.S1, 0, FamilySpace.S1, 0),
            MinHeight = 0,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.NoWrap,
        };

        _renaming = new RenameSession(visual, box);
        visual.Name.Visibility = Visibility.Collapsed;
        Grid.SetColumn(box, visual.NameColumn);
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
        var text = Family.Text(FamilyType.Body, ColourRole.TextPrimary, message);
        text.TextWrapping = TextWrapping.Wrap;

        try
        {
            // [S-1500] Its own popup, kept in the display's work area by the framework wherever the card sits.
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
    /// order, is <see cref="FolderMenu.Item"/>'s (the SDK's one file menu rule, tested); each line's
    /// icon, words and key are <see cref="FileMenuEntries"/>' (the desktop icons' too), and
    /// <see cref="ItemMenu"/> puts the row commands into the icon row. This says what each does here.
    /// </summary>
    private List<ItemMenuEntry> ItemEntries()
    {
        var selected = SelectedEntries();
        var paths = selected.Select(e => e.FullPath).ToList();
        var single = selected.Count == 1 ? selected[0] : (FolderEntry?)null;
        var target = new FileMenuTarget(
            FileMenuPlace.Folder,
            command => RunItemCommand(command, selected, paths, single),
            command => command == FileCommand.OpenWith && single is { } file ? OpenWithEntries(file.FullPath) : [],
            OpensFolder: single is { IsDirectory: true });

        return FileMenuEntries.Of(FolderMenu.Item(selected, Navigates, _widgetHost.FileClipboard.HasFiles), target);
    }

    /// <summary>[S-6416] "Open with" for one file: the shared submenu.</summary>
    private List<ItemMenuEntry> OpenWithEntries(string path) => FileMenuEntries.OpenWith(_widgetHost.ExplorerMenus, _widgetHost.FileOps, path);

    private void RunItemCommand(FileCommand command, List<FolderEntry> selected, List<string> paths, FolderEntry? single)
    {
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
                _ = PasteAsync(single?.FullPath ?? CurrentPath);
                break;
            case FileCommand.Rename:
                RenameSelection();
                break;
            case FileCommand.Share:
                RunShellVerb(paths, "Windows.ModernShare");
                break;
            case FileCommand.Delete:
                _ = DeleteSelectionAsync();
                break;
            case FileCommand.Open:
                OpenSelection();
                break;
            case FileCommand.OpenInNewWindow when single is { } folder:
                _widgetHost.FileOps.Open(folder.FullPath);
                break;
            case FileCommand.OpenFileLocation:
                FileMenuEntries.OpenFileLocation(_widgetHost.FileOps, paths);
                break;
            case FileCommand.PinToQuickAccess:
                RunShellVerb(paths, "pintohome");
                break;
            case FileCommand.PinToStart:
                RunShellVerb(paths, "pintostartscreen");
                break;
            case FileCommand.CompressToZip:
                _ = CompressAsync(selected);
                break;
            case FileCommand.CopyAsPath:
                FileMenuEntries.CopyAsPath(paths);
                break;
            case FileCommand.KeepOnDevice:
                _widgetHost.FileOps.SetCloudPin(paths, CloudPin.Pin);
                break;
            case FileCommand.FreeUpSpace:
                _widgetHost.FileOps.SetCloudPin(paths, CloudPin.Unpin);
                break;
            case FileCommand.Properties:
                _widgetHost.FileOps.Properties(paths);
                break;
            case FileCommand.ShowMoreOptions:
                ShowShellMenu(paths);
                break;
        }
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
        var target = new FileMenuTarget(
            FileMenuPlace.Folder,
            command => RunBackgroundCommand(command, here),
            command => command == FileCommand.New ? NewEntries() : []);

        return [.. FolderMenu.Background(facts).Select(line => BackgroundEntry(line, here, target))];
    }

    /// <summary>The widget's own three lines (Select all, Up, Open in Explorer); every other is the shared file menu's.</summary>
    private ItemMenuEntry BackgroundEntry(FileMenuLine line, string here, FileMenuTarget target)
    {
        var disabled = !line.Enabled;

        return line.Command switch
        {
            FileCommand.SelectAll => new(Glyphs.SelectAll, FolderStr.WidgetsB_Folder_SelectAll.Text(), () =>
            {
                _selection.SelectAll();
                RefreshSelectionVisuals();
            }, Disabled: disabled, Shortcut: FileMenu.Accelerator(line.Command)),
            FileCommand.Up => new(Glyphs.MoveUp, FolderStr.WidgetsB_Folder_Up.Text(), GoUp),
            FileCommand.OpenInExplorer => new(Glyphs.FolderOpen, Str.Folder_OpenInExplorer.Text(), () => _widgetHost.FileOps.Open(here), Disabled: disabled),

            // Ctrl+Shift+C copies the selection's paths ([S-6417]); with nothing selected it copies nothing, so this line shows no key.
            FileCommand.CopyAsPath => FileMenuEntries.Entry(line, target) with { Shortcut = null },
            _ => FileMenuEntries.Entry(line, target),
        };
    }

    private void RunBackgroundCommand(FileCommand command, string here)
    {
        switch (command)
        {
            case FileCommand.Paste:
                _ = PasteAsync(here);
                break;
            case FileCommand.Refresh:
                Reload(force: false);
                break;
            case FileCommand.CopyAsPath:
                FileMenuEntries.CopyAsPath([here]);
                break;
            case FileCommand.Properties:
                _widgetHost.FileOps.Properties([here]);
                break;
            case FileCommand.ShowMoreOptions:
                ShowShellMenu(null);
                break;
        }
    }

    /// <summary>[S-1331] New: Folder, then the ShellNew templates.</summary>
    private List<ItemMenuEntry> NewEntries()
    {
        var templates = _widgetHost.Folders.NewFileTemplatesAsync() is { IsCompletedSuccessfully: true } done ? done.Result : [];
        return FileMenuEntries.New(templates, NewFolder, newShortcut: null, NewFile);
    }

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
            ShowNote(this, FolderStr.FolderMenu_ShellFailed.Text());
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
            var name = FileMenu.ZipName(named.Name, named.IsDirectory, _snapshot.Entries.Select(e => e.Name));
            var zip = await _widgetHost.Folders.CompressAsync([.. selected.Select(e => e.FullPath)], Path.Combine(here, name)).ConfigureAwait(true);

            if (zip is null)
            {
                ShowNote(this, FolderStr.FolderMenu_ZipFailed.Text());
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
    public IReadOnlyList<WidgetAction> ContextActions
    {
        get
        {
            // [S-1326] What the header has no room for ([S-6710]: two buttons at most): Search in
            // navigate mode (whose header holds Back and Up), Forward and Home where they can act.
            List<WidgetAction> actions = [];
            if (Navigates)
            {
                actions.Add(new(Glyphs.Search, FolderStr.WidgetsB_Folder_SearchTip.Text(), ToggleSearch));
                if (_history.CanGoForward)
                {
                    actions.Add(new(Glyphs.Forward, FolderStr.Folder_Forward.Text(), GoForward));
                }

                if (!_history.AtHome)
                {
                    actions.Add(new(Glyphs.Home, FolderStr.WidgetsB_Folder_HomeTip.Text(), GoHome));
                }
            }

            actions.Add(new(Glyphs.OpenExternal, Str.Folder_OpenInExplorer.Text(), () => _widgetHost.FileOps.Open(CurrentPath)));
            actions.Add(new(Glyphs.Refresh, Str.Common_Refresh.Text(), () => Reload(force: false)));
            return actions;
        }
    }

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
        _searchBox.FocusEnd();
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

}
