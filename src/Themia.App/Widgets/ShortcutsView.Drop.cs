using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Themia.App.Services;
using Themia.App.Services.Files;
using Themia.App.Shell;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Files;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Windows.Foundation;

namespace Themia.App.Widgets;

/// <summary>
/// The Shortcuts widget's drag and drop: things dropped onto it from Explorer, a browser or the
/// Start menu become items ([S-1400]..[S-1402]); items drag to a new place in the list, the list
/// moving under the pointer as they go ([S-1410]); and files and folders drag out of it to other
/// apps and other Shortcuts widgets ([S-1415]).
/// <para>
/// <b>One drop path.</b> Nothing here registers an OLE target or sets XAML's <c>AllowDrop</c>: the
/// desktop surface owns the app's only drop target and forwards a drop that lands on this widget's
/// content through <see cref="IWidgetDropTarget"/>. A reorder inside the widget is not a drop at
/// all — it is a pointer gesture over its own cells — and only becomes an OLE drag when the pointer
/// leaves the widget carrying files.
/// </para>
/// </summary>
internal sealed partial class ShortcutsView
{
    /// <summary>How far an item must be dragged before it is a move, not a click (original S-38: 5 px).</summary>
    private const double DragThreshold = 5;

    /// <summary>
    /// The items being dragged out of a Shortcuts widget right now, so a drop back onto one (this or
    /// another) reorders or moves them instead of adding the files again. UI thread only; set for
    /// the length of OLE's modal drag loop.
    /// </summary>
    private static DragOrigin? _dragOut;

    private sealed record DragOrigin(ShortcutsView Source, IReadOnlyList<string> Ids);

    private Cell? _dragCell;
    private Point _dragStart;
    private bool _reordering;
    private List<string> _dragIds = [];
    private string? _previewBefore;
    private int _dragGeneration;
    private bool _dropHighlight;

    // ---- reorder by dragging ([S-1410]) -----------------------------------------------------------------

    /// <summary>A left press on an item arms a drag; nothing moves until the threshold is crossed.</summary>
    private void ArmDrag(Cell cell, PointerRoutedEventArgs args)
    {
        _dragCell = _renaming == cell || _surfaceHost?.IsPreview == true ? null : cell;
        _dragStart = args.GetCurrentPoint(this).Position;
    }

    /// <summary>The pointer moved: start, or carry on, a drag. True when the move was the drag's.</summary>
    private bool MoveDrag(PointerRoutedEventArgs args)
    {
        if (_dragCell is not { } cell || _detached)
        {
            return false;
        }

        if (!args.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            EndReorder(commit: false);
            return false;
        }

        var point = args.GetCurrentPoint(this).Position;

        if (!_reordering)
        {
            if (Math.Abs(point.X - _dragStart.X) < DragThreshold && Math.Abs(point.Y - _dragStart.Y) < DragThreshold)
            {
                return false;
            }

            if (!CapturePointer(args.Pointer))
            {
                _dragCell = null;
                return false;
            }

            // The selection moves together when the press was on it; otherwise just this item.
            var selected = Selected();
            _dragIds = selected.Contains(cell) && selected.Count > 1
                ? selected.Select(c => c.Item.Id).ToList()
                : [cell.Item.Id];
            _reordering = true;
            _previewBefore = null;
            DimDragged(true);
        }

        args.Handled = true;

        if (point.X < 0 || point.Y < 0 || point.X > ActualWidth || point.Y > ActualHeight)
        {
            // [S-1415] Out of the widget: files and folders go on as an OLE drag to whatever is
            // under the pointer; anything else simply stops at the edge.
            var paths = DraggedPaths();

            if (paths.Count > 0)
            {
                var ids = _dragIds.ToList();
                EndReorder(commit: false);
                StartDragOut(paths, ids);
            }

            return true;
        }

        Preview(BeforeAt(args.GetCurrentPoint(_panel).Position));
        return true;
    }

    /// <summary>The button came up: a drag drops where its preview shows. True when there was one.</summary>
    private bool ReleaseDrag()
    {
        if (!_reordering)
        {
            _dragCell = null;
            return false;
        }

        EndReorder(commit: true);
        return true;
    }

    /// <summary>
    /// End a reorder on every way out. <paramref name="commit"/> writes the previewed order (what was
    /// seen is what is saved — original S-65); otherwise the cells go back where they were.
    /// </summary>
    private void EndReorder(bool commit)
    {
        var was = _reordering;
        _dragCell = null;

        if (!was)
        {
            return;
        }

        _reordering = false;
        DimDragged(false);
        ReleasePointerCaptures();

        var ids = _dragIds;
        var before = _previewBefore;
        _dragIds = [];
        _previewBefore = null;

        if (commit && !_detached && ShortcutDrop.Reorder(_config.Items, ids, before))
        {
            Save();
            Changed(rebuild: true);
            return;
        }

        if (!_detached)
        {
            ApplyOrder(Order());
        }
    }

    /// <summary>[S-1410] Where a drag over <paramref name="point"/> (in the panel) would land: before an item, or at the end.</summary>
    private string? BeforeAt(Point point)
    {
        var order = Order();
        var grid = _config.Style == ShortcutsStyle.Grid;

        for (var i = 0; i < _cells.Count; i++)
        {
            var root = _cells[i].Root;
            var bounds = root.TransformToVisual(_panel).TransformBounds(new Rect(0, 0, root.ActualWidth, root.ActualHeight));

            if (bounds.Contains(point))
            {
                var fraction = grid
                    ? (point.X - bounds.X) / Math.Max(1, bounds.Width)
                    : (point.Y - bounds.Y) / Math.Max(1, bounds.Height);

                // The cells are drawn in preview order, but the rule reads the committed order.
                var index = order.IndexOf(PreviewOrder()[ToPreviewIndex(_cells[i])]);
                return ShortcutDrop.InsertBefore(order, index, fraction);
            }
        }

        return null;
    }

    /// <summary>Show the list as it would be after a drop before <paramref name="before"/>.</summary>
    private void Preview(string? before)
    {
        if (before == _previewBefore && _previewBefore is not null)
        {
            return;
        }

        _previewBefore = before;
        ApplyOrder(PreviewOrder());
    }

    /// <summary>The visible order with the dragged items moved to the preview's place.</summary>
    private List<string> PreviewOrder()
    {
        var items = Order().Select(id => new ShortcutItem { Id = id }).ToList();
        ShortcutDrop.Reorder(items, _dragIds, _previewBefore);
        return items.Select(item => item.Id).ToList();
    }

    /// <summary>The slot a cell is drawn in now.</summary>
    private int ToPreviewIndex(Cell cell)
    {
        var columns = Math.Max(1, Columns());
        return (GetRow(cell.Root) * columns) + GetColumn(cell.Root);
    }

    /// <summary>Lay the cells out in <paramref name="order"/> without rebuilding them: each moves to its slot.</summary>
    private void ApplyOrder(IReadOnlyList<string> order)
    {
        var columns = Math.Max(1, Columns());

        foreach (var cell in _cells)
        {
            var index = Math.Max(0, IndexOf(order, cell.Item.Id));
            SetRow(cell.Root, index / columns);
            SetColumn(cell.Root, index % columns);
        }
    }

    private static int IndexOf(IReadOnlyList<string> order, string id)
    {
        for (var i = 0; i < order.Count; i++)
        {
            if (order[i] == id)
            {
                return i;
            }
        }

        return -1;
    }

    private void DimDragged(bool dim)
    {
        foreach (var cell in _cells)
        {
            if (_dragIds.Contains(cell.Item.Id))
            {
                cell.Root.Opacity = dim ? 0.5 : 1;
            }
        }
    }

    // ---- drag out ([S-1415]) ------------------------------------------------------------------------

    /// <summary>The local files and folders among the dragged items (web, Windows-location and layout items have none).</summary>
    private List<string> DraggedPaths()
        => _config.Items
            .Where(item => _dragIds.Contains(item.Id) && !item.IsLayout)
            .Select(item => LaunchTargets.Classify(item.Target))
            .Where(target => target.Kind == LaunchKind.LocalPath && LaunchTargets.MayProbe(target))
            .Select(target => target.Value)
            .ToList();

    /// <summary>
    /// Hand the files to an OLE drag. Posted, not run in the pointer handler (OLE's modal loop must
    /// not nest in XAML's input dispatch, as in the folder widget). Copy or link only — never move:
    /// dragging a shortcut out must not move the user's real file.
    /// </summary>
    private void StartDragOut(IReadOnlyList<string> paths, IReadOnlyList<string> ids)
    {
        if (DispatcherQueue is not { } queue)
        {
            return;
        }

        var generation = ++_dragGeneration;

        queue.TryEnqueue(() =>
        {
            if (_detached || generation != _dragGeneration)
            {
                return;
            }

            _dragOut = new DragOrigin(this, ids);

            try
            {
                DragDropInterop.DragOut(paths, DropEffect.Copy | DropEffect.Link, Window());
            }
            catch (Exception ex)
            {
                Log.Error(Scope, "the drag out of the shortcuts failed", ex);
            }
            finally
            {
                _dragOut = null;
            }
        });
    }

    /// <summary>This widget's window, which owns its drags and places its layouts.</summary>
    private nint Window()
    {
        if (XamlRoot?.ContentIslandEnvironment is { } environment)
        {
            return Microsoft.UI.Win32Interop.GetWindowFromWindowId(environment.AppWindowId);
        }

        return 0;
    }

    /// <summary>Items another Shortcuts widget took by a move leave this one.</summary>
    private void RemoveMoved(IReadOnlyCollection<string> ids)
    {
        if (_detached || _config.Items.RemoveAll(item => ids.Contains(item.Id)) == 0)
        {
            return;
        }

        Save();
        Changed(rebuild: true);
    }

    // ---- drop in ([S-1400]) --------------------------------------------------------------------------

    DropEffect IWidgetDropTarget.DragOver(WidgetDrag drag, Point local) => OverDrop(drag, local);

    void IWidgetDropTarget.DragLeave() => LeaveDrop();

    DropEffect IWidgetDropTarget.Drop(WidgetDrag drag, Point local) => TakeDrop(drag, local);

    private DropEffect OverDrop(WidgetDrag drag, Point local)
    {
        try
        {
            if (_detached || _surfaceHost?.IsPreview == true)
            {
                return DropEffect.None;
            }

            var effect = _dragOut is not null
                ? Pick(drag.Allowed, DropEffect.Link, DropEffect.Copy)
                : drag.Paths.Count > 0 || drag.Urls.Count > 0 || drag.HasShellItems
                    ? Pick(drag.Allowed, DropEffect.Link, DropEffect.Copy)
                    : DropEffect.None;

            if (_dragOut is { } origin && origin.Source == this && effect != DropEffect.None)
            {
                // Back over its own widget: the list previews the new order, as a drag inside it does.
                _dragIds = origin.Ids.ToList();
                Preview(BeforeAt(TransformToVisual(_panel).TransformPoint(local)));
            }

            ShowDropHighlight(effect != DropEffect.None);

            if (effect != DropEffect.None)
            {
                // [S-1400] The caption beside the drag image.
                drag.Place = _dragOut is { } from && from.Source == this
                    ? Str.WidgetsC_Shortcuts_Drop_MoveHere.Text()
                    : Str.WidgetsC_Shortcuts_Drop_Add.Text();
            }

            return effect;
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "a drag over the shortcuts failed", ex);
            return DropEffect.None;
        }
    }

    private void LeaveDrop()
    {
        ShowDropHighlight(false);

        if (_dragOut is { } origin && origin.Source == this && !_reordering)
        {
            _previewBefore = null;
            _dragIds = [];

            if (!_detached)
            {
                ApplyOrder(Order());
            }
        }
    }

    private DropEffect TakeDrop(WidgetDrag drag, Point local)
    {
        try
        {
            var effect = OverDrop(drag, local);
            var before = _previewBefore;
            LeaveDrop();

            if (effect == DropEffect.None)
            {
                return DropEffect.None;
            }

            if (_dragOut is { } origin)
            {
                DropFromShortcuts(origin, before, copy: drag.Ctrl);
                return effect;
            }

            DropFromOutside(drag);
            return effect;
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "a drop onto the shortcuts failed", ex);
            return DropEffect.None;
        }
    }

    private static DropEffect Pick(DropEffect allowed, DropEffect first, DropEffect second)
        => allowed.HasFlag(first) ? first : allowed.HasFlag(second) ? second : DropEffect.None;

    /// <summary>
    /// [S-1410]/[S-1415] Items dragged out of a Shortcuts widget and dropped on one. On itself: a
    /// reorder. On another: the items move (the receiver adds, then the source removes — original
    /// S-35, so a drop can never lose them), or are copied with Ctrl.
    /// </summary>
    private void DropFromShortcuts(DragOrigin origin, string? before, bool copy)
    {
        if (origin.Source == this)
        {
            if (ShortcutDrop.Reorder(_config.Items, origin.Ids, before))
            {
                Save();
                Changed(rebuild: true);
            }

            return;
        }

        var items = origin.Source._config.Items
            .Where(item => origin.Ids.Contains(item.Id))
            .Select(item => item.Clone())
            .Where(item => item.IsLayout || !_config.Items.Any(held => LaunchTargets.SameTarget(held.Target, item.Target)))
            .ToList();

        AddItems(items);

        if (!copy)
        {
            origin.Source.RemoveMoved(origin.Ids);
        }
    }

    /// <summary>
    /// [S-1400]..[S-1402] Files, folders, links and web addresses from another app: each becomes an
    /// item at the end, by reference. A Start menu app (shell items, no paths) is found by name among
    /// the Start menu's shortcuts, read on the thread pool.
    /// </summary>
    private void DropFromOutside(WidgetDrag drag)
    {
        var result = ShortcutDrop.FromDrop(drag.Paths, drag.Urls, _config.Items, ResolveLink, ReadUrlFile);

        if (result.Items.Count > 0 || drag.Paths.Count > 0 || drag.Urls.Count > 0)
        {
            AddDropped(result);
            return;
        }

        if (drag.Data == 0)
        {
            return;
        }

        // Read now: the data object is only valid during the drop.
        var shellItems = DragDropInterop.ReadShellItems(drag.Data);
        var paths = shellItems.Where(item => item.FileSystemPath is not null).Select(item => item.FileSystemPath!).ToList();
        var names = shellItems.Where(item => item.FileSystemPath is null && item.DisplayName.Length > 0).Select(item => item.DisplayName).ToList();

        if (paths.Count > 0)
        {
            AddDropped(ShortcutDrop.FromDrop(paths, [], _config.Items, ResolveLink, ReadUrlFile));
        }

        if (names.Count > 0)
        {
            _ = AddStartMenuAppsAsync(names);
        }
    }

    private void AddDropped(ShortcutDropResult result)
    {
        if (result.Skipped > 0)
        {
            Log.Info(Scope, $"a drop left out {result.Skipped} item(s): not allowed, or already here");
        }

        AddItems(result.Items);
    }

    /// <summary>Start menu apps dropped by name: each becomes the Start menu's own shortcut for it.</summary>
    private async Task AddStartMenuAppsAsync(IReadOnlyList<string> names)
    {
        try
        {
            var apps = await Task.Run(() => InstalledApps.Read(2000, CancellationToken.None)).ConfigureAwait(true);
            var paths = new List<string>();

            foreach (var name in names)
            {
                if (apps.FirstOrDefault(app => string.Equals(app.Name, name, StringComparison.OrdinalIgnoreCase)) is { Target.Length: > 0 } app)
                {
                    paths.Add(app.Target);
                }
                else
                {
                    Log.Info(Scope, "a dropped Start menu app has no Start menu shortcut to keep (a Store-only app?)");
                }
            }

            if (_detached || paths.Count == 0)
            {
                return;
            }

            AddDropped(ShortcutDrop.FromDrop(paths, [], _config.Items, ResolveLink, ReadUrlFile));
        }
        catch (Exception ex)
        {
            Log.Warn(Scope, "could not add the dropped Start menu apps", ex);
        }
    }

    /// <summary>A dropped link's target, or null to keep the link (unreadable, or it passes arguments).</summary>
    private static string? ResolveLink(string path)
        => LaunchTargets.MayProbe(LaunchTargets.Classify(path)) ? ShellFileOps.ResolveShortcut(path, withoutArguments: true) : null;

    /// <summary>A dropped <c>.url</c> file's text: local only, and only as long as such a file can be.</summary>
    private static string? ReadUrlFile(string path)
    {
        if (!LaunchTargets.MayProbe(LaunchTargets.Classify(path)))
        {
            return null;
        }

        try
        {
            var info = new FileInfo(path);

            if (!info.Exists || info.Length > ShortcutDrop.MaxUrlFileLength)
            {
                return null;
            }

            return File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            Log.Debug(Scope, $"could not read a dropped .url file: {ex.Message}");
            return null;
        }
    }

    /// <summary>[S-1400] The widget says it will take the drop: an accent edge round its items (original S-32).</summary>
    private void ShowDropHighlight(bool on)
    {
        if (_dropHighlight == on)
        {
            return;
        }

        _dropHighlight = on;
        _scroll.BorderThickness = new Thickness(on ? Fluent.Hairline * 2 : 0);
        _scroll.BorderBrush = on
            ? ThemeBrush.Of("AccentFillColorDefaultBrush", Colors.DodgerBlue)
            : null;
        _scroll.CornerRadius = new CornerRadius(Fluent.ControlRadius);
    }
}
