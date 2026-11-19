using Themia.App.Services.Files;
using Themia.App.Shell;
using Themia.App.Widgets;
using Themia.Core.Desktop;
using Themia.Core.Diagnostics;
using Themia.Core.Files;
using Themia.Core.Geometry;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Windows.Foundation;

namespace Themia.App.Services;

/// <summary>
/// [S-13000]..[S-13014] The fences on the desktop, the host's side: what a fence widget asks (<see cref="IDesktopFences"/>)
/// carried out on the state through the one write path, by Core's rules (<see cref="DesktopFences"/>); the fences' items in
/// the one selection ([S-13011]: the boxes each fence lays its items in); making a fence on the locked desktop, from a
/// rectangle drawn with the right button ([S-13007]) or round the selected icons ([S-13008]). Never a file: a fence only
/// changes where an item's icon stands. UI thread.
/// </summary>
internal sealed partial class AppHost : IDesktopFences
{
    /// <summary>[S-13011] Where each fence (by its widget id) draws the items it shows now, in its own DIPs.</summary>
    private readonly Dictionary<string, IReadOnlyList<FenceItemBox>> _fenceBoxes = new(StringComparer.Ordinal);

    /// <summary>[S-13000] The fence kind: the one widget whose config holds desktop items, or null in a build without it.</summary>
    private static WidgetDescriptor? FenceKind
        => ModuleCatalog.Widgets.FirstOrDefault(d => typeof(IHoldsDesktopItems).IsAssignableFrom(d.ConfigType));

    /// <summary>[S-13000] What a fence asks of the desktop: this host.</summary>
    public IDesktopFences Fences => this;

    /// <summary>[S-13011] The item whose rename box shows, by its key: a fence draws it on its own item.</summary>
    public string? RenamingKey => _renamingKey;

    /// <inheritdoc />
    public string? DesktopFolder => DesktopFolderPath();

    /// <inheritdoc />
    public void Laid(string fenceId, IReadOnlyList<FenceItemBox> boxes)
    {
        ArgumentNullException.ThrowIfNull(boxes);
        _fenceBoxes[fenceId] = boxes;
    }

    /// <summary>
    /// [S-13012] Roll a fence up to its title bar (its height kept to unroll to) or unroll it. Not an undo step: a look,
    /// as Fences' own roll-up; the fence's rect follows in the same change (the kind's sizing pins a rolled-up fence's
    /// height to its title bar).
    /// </summary>
    public void RollUp(string fenceId, bool rolledUp)
    {
        if (DesktopFences.Find(State, fenceId) is not { } at || at.Fence.Fence.RolledUp == rolledUp)
        {
            return;
        }

        var screen = ScreenOfView(at.View.Id) ?? DeskPrimaryScreen;
        Mutate(_ =>
        {
            var (widget, fence) = (at.Fence.Widget, at.Fence.Fence);
            if (rolledUp)
            {
                fence.OpenHeight = widget.H;
                fence.RolledUp = true;
                if (screen.IsUsable)
                {
                    widget.H = Math.Min(widget.H, FenceGrid.TitleHeight / screen.Height);
                }
            }
            else
            {
                fence.RolledUp = false;
                if (fence.OpenHeight > 0)
                {
                    widget.H = fence.OpenHeight;
                }

                fence.OpenHeight = 0;
            }
        }, undo: null);
    }

    /// <summary>[S-13013] A fence's standing sort: its items in the order of <paramref name="by"/>, by Core's one sort (<see cref="DesktopSort.Order"/>).</summary>
    public IReadOnlyList<string> Order(IReadOnlyList<string> keys, DesktopSortBy by)
    {
        ArgumentNullException.ThrowIfNull(keys);
        if (!Enum.IsDefined(by))
        {
            return keys;
        }

        var items = keys.Select(key => HostDesktopItems.Instance.Find(key) ?? new DesktopItem(key, DesktopItemKeys.FallbackName(key), DesktopItemKind.File)).ToList();
        return [.. DesktopSort.Order(items, by, item => SortFacts(item, [])).Select(item => item.Key)];
    }

    /// <inheritdoc />
    public void Rename(string fenceId, string name)
    {
        if (DesktopFences.Find(State, fenceId) is not { } at)
        {
            return;
        }

        var clean = new string([.. (name ?? string.Empty).Where(c => !char.IsControl(c))]).Trim();
        if (!string.Equals(clean, at.Fence.Fence.Name, StringComparison.Ordinal))
        {
            Mutate(_ => at.Fence.Fence.Name = clean, undo: UndoAction.Change);
        }
    }

    /// <summary>
    /// [S-13009] A fence goes, never a file: with <paramref name="putBack"/> its items arrive on its view's free desktop
    /// where the fence stood (the free spots nearest its corner, [S-11876]); else they stay off the view. One undo step
    /// whose undo brings the fence back with its items (its config, holding them, comes back with it, [S-13010]).
    /// </summary>
    public void Remove(string fenceId, bool putBack)
    {
        if (DesktopFences.Find(State, fenceId) is not { } at || ItemKind is not { } kind)
        {
            return;
        }

        var (view, widget, fence) = (at.View, at.Fence.Widget, at.Fence.Fence);
        var count = fence.Items.Count;
        Mutate(_ =>
        {
            view.Widgets.Remove(widget);
            if (putBack)
            {
                PutBackFenceItems(view, widget);
            }
        }, undo: UndoAction.Delete);
        _fenceBoxes.Remove(fenceId);
        OfferUndo(putBack && count > 0 ? Str.Desktop_Fence_Removed.Plural(count) : Str.Shell_Undo_DeletedWidgets.Plural(1));
    }

    /// <summary>
    /// [S-13009] The items of <paramref name="fence"/>, just taken off <paramref name="view"/>, arrive on its free desktop
    /// where the fence stood: the free spots nearest its corner, one by one ([S-11876]); one already free there keeps its
    /// icon. Inside the caller's change.
    /// </summary>
    private void PutBackFenceItems(ViewState view, WidgetInstance fence)
    {
        if (ItemKind is not { } kind || fence.Config is not IHoldsDesktopItems held || held.Items.Count == 0)
        {
            return;
        }

        var screen = ScreenOfView(view.Id) ?? DeskPrimaryScreen;
        var free = DesktopViews.Shown(view);
        var items = held.Items.Where(key => !free.Contains(key)).Select(key => HostDesktopItems.Instance.Find(key)).OfType<DesktopItem>().ToList();
        var (w, h) = DeskIconSize(screen);
        var left = DesktopFences.LayFree(view, items, fence.X + (w / 2), fence.Y + (h / 2), screen, (DeskCellDip.Width, DeskCellDip.Height),
            (item, rect) => NewItemWidget(kind.Id, item, rect));
        if (left.Count > 0)
        {
            Log.Info(Scope, $"{left.Count} item(s) of a removed fence found no free spot; Add widget lists them");
        }
    }

    /// <summary>
    /// [S-13005] The desktop's own drag is let go over a fence: its desktop items move in before <paramref name="index"/>,
    /// leaving the free desktop and any other fence of the fence's view (<see cref="DesktopFences.MoveIn"/>), in the
    /// selection's order. An own icon or a card in the drag stays where it was. One undo step.
    /// </summary>
    public DropEffect DropDeskDrag(string fenceId, int index)
    {
        if (_deskDrag is not { } drag || DesktopFences.Find(State, fenceId) is not { } at)
        {
            return DropEffect.None;
        }

        drag.Dropped = true;
        var keys = _desk.Selected.Where(key => drag.Keys.Contains(key) && HostDesktopItems.Instance.Find(key) is not null).ToList();
        if (keys.Count == 0)
        {
            return DropEffect.None;
        }

        Mutate(_ => DesktopFences.MoveIn(at.View, fenceId, keys, index), undo: UndoAction.Move);
        return DesktopCommands.MoveEffect(drag.Offer.Effects);
    }

    /// <inheritdoc />
    public DropEffect DropFiles(string fenceId, Themia.App.Widgets.IWidgetDrag drag)
    {
        ArgumentNullException.ThrowIfNull(drag);
        if (DesktopFences.Find(State, fenceId) is not { } at || DesktopFolderPath() is not { } desktop)
        {
            return DropEffect.None;
        }

        ExpectLanding(new DesktopLandingHint(at.View.Id, null, null, DropNames(drag), Fence: fenceId));
        var effect = drag.ForwardDrop(desktop, DropEffect.None);
        NoteDropUndo(effect, drag.Paths);
        return effect;
    }

    /// <inheritdoc />
    public void Paste(string fenceId) => _ = PasteAsync(fence: fenceId);

    /// <summary>[S-13005] New › Folder from a fence: made in the desktop folder, landing in the fence with its rename box open ([S-5552]).</summary>
    public void NewFolder(string fenceId)
    {
        if (DesktopFences.Find(State, fenceId) is not { } at || DesktopFolderPath() is not { } desktop)
        {
            return;
        }

        if (HostFileOps.Instance.NewFolder(desktop) is not { } created || Path.GetFileName(created) is not { Length: > 0 } name)
        {
            Log.Warn(Scope, "a new folder for a fence was not made");
            return;
        }

        ExpectLanding(new DesktopLandingHint(at.View.Id, null, null, [name], Renames: true, Fence: fenceId) { Undo = DesktopFileUndoKind.NewFolder });
    }

    // ---- making a fence ([S-13007], [S-13008]) ----------------------------------------------------

    /// <summary>
    /// [S-13007] The rectangle drawn with the right button on empty desktop of <paramref name="surface"/> (its corners in the
    /// surface's DIPs) was let go: Fences' small menu, "Create fence here", opens at the release; its line makes a fence
    /// on that rectangle, snapped to the grid. Outside edit mode only: the locked desktop is where fences are made.
    /// </summary>
    public void FenceRectDrawn(DesktopWindow surface, Point start, Point end)
    {
        if (FenceKind is null || EditMode)
        {
            return;
        }

        var metrics = surface.Metrics;
        if (!metrics.IsUsable)
        {
            return;
        }

        var drawn = new FracRect(
            Math.Min(start.X, end.X) / metrics.Width,
            Math.Min(start.Y, end.Y) / metrics.Height,
            Math.Abs(end.X - start.X) / metrics.Width,
            Math.Abs(end.Y - start.Y) / metrics.Height);
        if (!surface.OpenMenuAt(end, FenceCreateEntries(() => CreateFence(surface, drawn, []))))
        {
            Log.Warn(Scope, "the menu to create a fence did not open");
        }
    }

    /// <summary>[S-13007] Fences' small menu after a drawn rectangle: its one line. Static: the menu render draws it too.</summary>
    internal static List<ItemMenuEntry> FenceCreateEntries(Action create) => [new(Glyphs.Group, Str.Desktop_Fence_CreateHere.Text(), create)];

    /// <summary>
    /// [S-13008] "Create fence from selection": a fence made round the selected desktop items of the view the menu opened
    /// on (their free icons' box with the title bar above, at least what shows them all), the items moved into it in the
    /// selection's order.
    /// </summary>
    private void CreateFenceFromSelection()
    {
        var keys = _desk.Selected.Where(key => HostDesktopItems.Instance.Find(key) is not null).ToList();
        var surface = _windows.FirstOrDefault(w => w.IsAlive && w.Hwnd == _deskMenuAt.Owner) ?? KeyboardSurface ?? DesktopKeysSurface;
        if (keys.Count == 0 || surface is null || !surface.Metrics.IsUsable)
        {
            return;
        }

        var view = State.ViewForMonitor(surface.Monitor.Id);
        var set = new HashSet<string>(keys, DesktopItemKeys.Comparer);
        var rects = view.Widgets.Where(w => w.Config is IDesktopItemConfig { Item.Length: > 0 } c && set.Contains(c.Item)).Select(w => w.Rect).ToList();
        if (rects.Count == 0)
        {
            // Every one is in a fence already: the new one stands at the menu's place.
            var (x, y) = MenuPointIn(surface);
            rects = [new FracRect(x, y, 0, 0)];
        }

        if (DesktopFences.AroundIcons(rects, keys.Count, DeskCellDip, surface.Metrics) is { } rect)
        {
            CreateFence(surface, rect, keys);
        }
    }

    /// <summary>Where the last desktop menu opened, as fractions of <paramref name="surface"/>.</summary>
    private (double X, double Y) MenuPointIn(DesktopWindow surface)
    {
        var bounds = surface.Monitor.Bounds;
        return bounds.Width > 0 && bounds.Height > 0
            ? (Math.Clamp((_deskMenuAt.X - bounds.Left) / (double)bounds.Width, 0, 1), Math.Clamp((_deskMenuAt.Y - bounds.Top) / (double)bounds.Height, 0, 1))
            : (0, 0);
    }

    /// <summary>[S-13007] [S-13008] A new fence on <paramref name="surface"/>'s view at <paramref name="rect"/> (snapped), holding <paramref name="keys"/>. One undo step.</summary>
    private void CreateFence(DesktopWindow surface, FracRect rect, IReadOnlyList<string> keys)
    {
        if (FenceKind is not { } kind || !surface.Metrics.IsUsable)
        {
            return;
        }

        var sizing = kind.Sizing(kind.CreateDefault());
        if (DesktopFences.FromRect(rect, sizing.Min, surface.Metrics) is not { } snapped)
        {
            return;
        }

        var view = State.ViewForMonitor(surface.Monitor.Id);
        var widget = WidgetInstance.Create(kind.Id, snapped);
        Mutate(_ =>
        {
            view.Widgets.Add(widget);
            if (keys.Count > 0)
            {
                DesktopFences.MoveIn(view, widget.Id, keys, 0);
            }
        }, undo: UndoAction.Add);
        Log.Info(Scope, $"a fence was made with {keys.Count} item(s)");
    }

    /// <summary>
    /// [S-13006] The desktop's drag let go on empty desktop of <paramref name="target"/>'s view at <paramref name="dip"/>:
    /// the dragged items that stood in a fence of the drag's own view leave it and arrive on the target view's free
    /// desktop at the drop point (the free spots nearest it, [S-11876]); one already free there keeps its icon. Inside
    /// the caller's change.
    /// </summary>
    private void FenceItemsOut(ViewState sourceView, ViewState targetView, ScreenMetrics to, Point dip, HashSet<string> keys)
    {
        if (ItemKind is not { } kind)
        {
            return;
        }

        var held = DesktopFences.Held(sourceView);
        var leaving = _desk.Selected.Where(key => keys.Contains(key) && held.Contains(key)).ToList();
        if (leaving.Count == 0)
        {
            return;
        }

        DesktopFences.TakeOut(sourceView, leaving);
        if (!ReferenceEquals(sourceView, targetView))
        {
            DesktopFences.TakeOut(targetView, leaving);
        }

        var free = DesktopViews.Free(targetView);
        var items = leaving.Where(key => !free.Contains(key)).Select(key => HostDesktopItems.Instance.Find(key)).OfType<DesktopItem>().ToList();
        var left = DesktopFences.LayFree(targetView, items, dip.X / to.Width, dip.Y / to.Height, to, (DeskCellDip.Width, DeskCellDip.Height),
            (item, rect) => NewItemWidget(kind.Id, item, rect));
        if (left.Count > 0)
        {
            Log.Info(Scope, $"{left.Count} item(s) dragged out of a fence found no free spot; Add widget lists them");
        }
    }

    /// <summary>[S-13011] The boxes the fences of <paramref name="view"/> on <paramref name="surface"/> lay their items in, in physical screen pixels.</summary>
    private IEnumerable<(string Key, WidgetInstance Fence, DesktopBox Box)> FenceItemBoxes(DesktopWindow surface, ViewState view, int display)
    {
        var bounds = surface.Monitor.Bounds;
        var scale = surface.BandScale;
        foreach (var on in DesktopFences.On(view))
        {
            if (on.Fence.RolledUp || !surface.Draws(on.Widget.Id) || !_fenceBoxes.TryGetValue(on.Widget.Id, out var boxes))
            {
                continue;
            }

            var r = on.Widget.Rect;
            var left = bounds.Left + (r.X * bounds.Width);
            var top = bounds.Top + (r.Y * bounds.Height);
            foreach (var box in boxes)
            {
                if (on.Fence.Items.Contains(box.Key, DesktopItemKeys.Comparer) && HostDesktopItems.Instance.Find(box.Key) is not null)
                {
                    yield return (box.Key, on.Widget, new DesktopBox(box.Key, display, left + (box.X * scale), top + (box.Y * scale), box.Width * scale, box.Height * scale));
                }
            }
        }
    }

    /// <summary>[S-13011] A fence's item as the desktop's commands see it: a desktop item by its key.</summary>
    private static DeskIcon FenceIcon(string key)
        => new(key, DesktopItemKeys.IsShellItem(key) ? DeskIconKind.ShellItem : DeskIconKind.Item, key, string.Empty, Opens: true, PathExists: true, ItemClickMode.Double);
}
