using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Input;
using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Core.Files;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;

namespace Themia.Widget.Fence;

/// <summary>
/// [S-13023] A search fence: the files its rules match in a folder or drive, as Windows Search finds them
/// (<see cref="IDesktopFences.SearchAsync"/>), drawn in the fence's grid as its items are. They are views of the files, not
/// desktop items: they are selected in the fence alone (the SDK's <see cref="ItemSelection"/>: a press, Ctrl, Shift, the
/// arrows), a double-click or Enter opens them, a right-click is the shell's own menu for them, a drag takes them out as a
/// copy or a shortcut (never a move: the files stay where they are), F5 or the menu's Refresh searches again. Removing the
/// fence touches no file. The search runs when the fence is shown and again when its rules or query change.
/// </summary>
internal sealed partial class FenceView
{
    /// <summary>[S-13023] What the last search found, as shown.</summary>
    private IReadOnlyList<FoundFile> _found = [];

    /// <summary>[S-13023] The running search, cancelled by the next one and on detach.</summary>
    private CancellationTokenSource? _searchCts;

    /// <summary>[S-13023] What the last search was for (its location, its count): the same one is not run again.</summary>
    private string? _searchedFor;

    private bool _searching;

    /// <summary>[S-13023] The fence's own selection of found files (they are in no desktop selection).</summary>
    private readonly ItemSelection _picks = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>[S-13023] Whether the fence shows files a search finds.</summary>
    private bool Searches => _config.Source == FenceSource.Search;

    /// <summary>[S-13023] Search when shown on the desktop and the query is not the one last run (or a refresh asks); stop a search fence's search otherwise.</summary>
    private void SearchIfNeeded(bool again = false)
    {
        if (!Searches || !_attached || _fixture.On || _host.IsPreview || WidgetId is null)
        {
            CancelSearch();
            if (!Searches)
            {
                _found = [];
                _searchedFor = null;
            }

            return;
        }

        var key = FenceSearch.Aqs(_config.Rules, _config.Query, DateTime.UtcNow.Date) + "|" + _config.Query.Folder + "|" + _config.Query.Max;
        if (!again && string.Equals(key, _searchedFor, StringComparison.Ordinal))
        {
            return;
        }

        _searchedFor = key;
        _ = SearchAsync();
    }

    private void CancelSearch()
    {
        _searchCts?.Cancel();
        _searchCts?.Dispose();
        _searchCts = null;
        _searching = false;
    }

    private async Task SearchAsync()
    {
        CancelSearch();
        var cts = new CancellationTokenSource();
        _searchCts = cts;
        _searching = true;
        Render();
        try
        {
            var found = await Fences.SearchAsync(_config.Rules.Clone(), _config.Query.Clone(), cts.Token).ConfigureAwait(true);
            if (ReferenceEquals(cts, _searchCts) && !cts.IsCancellationRequested)
            {
                _found = found;
            }
        }
        catch (Exception ex)
        {
            Log.Warn(Scope, "a search fence's search failed", ex);
        }
        finally
        {
            if (ReferenceEquals(cts, _searchCts))
            {
                _searching = false;
                Render();
            }
        }
    }

    /// <summary>[S-13023] A found file as the cell draws it: a desktop item of its kind (the shell's icon by its path).</summary>
    private static DesktopItem AsItem(FoundFile file)
    {
        var extension = FenceRules.ExtensionOfKey(file.Path);
        var kind = file.IsFolder ? DesktopItemKind.Folder : extension is ".lnk" or ".url" ? DesktopItemKind.Shortcut : DesktopItemKind.File;
        return new DesktopItem(file.Path, file.Name, kind);
    }

    /// <summary>[S-13023] What the empty fence says: searching, no rule set yet, or no file found.</summary>
    private string SearchEmptyText()
        => _searching ? FenceStr.Fence_Search_Searching.Text()
            : FenceSearch.Aqs(_config.Rules, _config.Query, DateTime.UtcNow).Length == 0 ? FenceStr.Fence_Search_NoRule.Text()
            : FenceStr.Fence_Search_None.Text();

    /// <summary>The files the gesture is about: the selection when it holds <paramref name="key"/>, else that one alone.</summary>
    private IReadOnlyList<string> FoundTargets(string key) => _picks.IsSelected(key) ? _picks.Selected : [key];

    /// <summary>[S-13023] A left press on a found file: the fence's own selection (Ctrl toggles, Shift the range); it may become its drag.</summary>
    private void OnFoundPressed(Cell cell, PointerPoint point, bool ctrl, bool shift)
    {
        _picks.SetOrder(_shown);
        _picks.Press(_picks.IndexOf(cell.Key), ctrl, shift);
        _press = (cell.Key, point.Position, ctrl || shift);
        PointerMoved -= OnPressMoved;
        PointerMoved += OnPressMoved;
        PaintCells();
    }

    private void OnFoundReleased(Cell cell)
    {
        _picks.Release(_picks.IndexOf(cell.Key));
        PaintCells();
    }

    /// <summary>[S-13023] A double-click or Enter: the files open as Explorer opens them (at most 15 at once).</summary>
    private void OpenFound(string key)
    {
        foreach (var path in FoundTargets(key).Take(15))
        {
            _host.Launcher.Open(path);
        }
    }

    /// <summary>[S-13023] A right-click: the shell's own menu for the files (the press's file joins the selection first, as Explorer's does).</summary>
    private void FoundMenu(Cell cell, int x, int y, nint window)
    {
        _picks.SetOrder(_shown);
        _picks.Context(_picks.IndexOf(cell.Key));
        PaintCells();
        _host.ExplorerMenus.ShowForItems(_picks.Selected.Count > 0 ? _picks.Selected : [cell.Key], x, y, window);
    }

    /// <summary>[S-13023] A drag of found files: a copy or a shortcut wherever they are let go, never a move; Explorer's ghost.</summary>
    private async Task DragFoundAsync(Cell held, PointerPoint point)
    {
        try
        {
            var paths = FoundTargets(held.Key);
            var offer = new ItemDragOffer([.. paths], DropEffect.Copy | DropEffect.Link);
            var square = held.IconHost.TransformToVisual(this).TransformBounds(new Windows.Foundation.Rect(0, 0, held.IconHost.ActualWidth, held.IconHost.ActualHeight));
            var anchor = new Windows.Foundation.Point(Math.Clamp(point.Position.X - square.X, 0, square.Width), Math.Clamp(point.Position.Y - square.Y, 0, square.Height));
            var effect = await ItemDrag.StartAsync(this, point, offer, () => Ghost(held, square.Width, anchor, offer, _picks.IsSelected)).ConfigureAwait(true);
            Log.Debug(Scope, $"a drag from a search fence ended: {effect}");
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "a found file's drag failed", ex);
        }
    }

    /// <summary>[S-13023] The keys with the fence's lease: Enter opens, F5 searches again, the arrows, Space and Ctrl+A select.</summary>
    private bool FoundKey(WidgetKey key)
    {
        switch (key)
        {
            case WidgetKey.Enter when _picks.Focus is { } focus:
                OpenFound(focus);
                return true;
            case WidgetKey.Refresh:
                SearchIfNeeded(again: true);
                return true;
        }

        _picks.SetOrder(_shown);
        var columns = FenceGrid.Columns(Math.Max(0, ActualWidth), CellSize);
        if (!_picks.Key(key, SelectionInput.ShiftHeld(), columns, Math.Max(1, (int)(_scroll.ViewportHeight / Math.Max(1, CellSize.Height)))))
        {
            return false;
        }

        PaintCells();
        return true;
    }
}
