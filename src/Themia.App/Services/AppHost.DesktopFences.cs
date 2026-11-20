using Themia.App.Services.Files;
using Themia.App.Shell;
using Microsoft.UI.Xaml.Input;
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
/// [S-13000]..[S-13014] [S-13040]..[S-13044] The fences on the desktop, the host's side: what a fence widget asks
/// (<see cref="IDesktopFences"/>) carried out on the state through the one write path, by Core's rules
/// (<see cref="DesktopFences"/>); every fence laid on its display's desktop icon grid in every change (its items are desktop
/// icons on cells of its body, <see cref="LayFences"/>); a move of icons let go landing them in or out of a fence by the one
/// drop computation (<see cref="LandMoved"/>); making a fence on the locked desktop, from a rectangle drawn with the right
/// button ([S-13007]) or round the selected icons ([S-13008]). Never a file: a fence only changes where an item's icon
/// stands. UI thread.
/// </summary>
internal sealed partial class AppHost : IDesktopFences
{
    /// <summary>[S-13000] The fence kind: the one widget whose config holds desktop items, or null in a build without it.</summary>
    private static WidgetDescriptor? FenceKind
        => ModuleCatalog.Widgets.FirstOrDefault(d => typeof(IHoldsDesktopItems).IsAssignableFrom(d.ConfigType));

    /// <summary>[S-13000] What a fence asks of the desktop: this host.</summary>
    public IDesktopFences Fences => this;

    /// <inheritdoc />
    public string? DesktopFolder => DesktopFolderPath();

    /// <summary>[S-13040] A desktop icon's box on <paramref name="screen"/>'s widget grid, as fractions: what a fence's cell is.</summary>
    internal (double W, double H) DeskIconBox(ScreenMetrics screen) => DeskIconSize(screen);

    /// <summary>[S-13042] A new icon for the desktop item <paramref name="key"/>, at the corner of nothing yet: the fence lays it.</summary>
    private WidgetInstance? FenceIcon(string key)
        => ItemKind is { } kind
            ? NewItemWidget(kind.Id, HostDesktopItems.Instance.Find(key) ?? new DesktopItem(key, DesktopItemKeys.FallbackName(key), DesktopItemKind.File), new FracRect(0, 0, 0, 0))
            : null;

    /// <summary>
    /// [S-13040] [S-13042] [S-13013] Every fence of every view laid on the desktop icon grid of the display that shows the
    /// view (Core's <see cref="DesktopFences.Lay"/>), in the change being made (<see cref="Mutate"/>): a fence with a standing
    /// sort holds its items in that order first. A view no display shows keeps its icons' rects till it is shown; its
    /// fences still let go of an item whose icon left it.
    /// </summary>
    private void LayFences(AppState state)
    {
        foreach (var view in state.Views)
        {
            if (!view.Widgets.Exists(w => w.Config is IHoldsDesktopItems))
            {
                continue;
            }

            foreach (var on in DesktopFences.On(view))
            {
                if (on.Fence.Sort is { } by && on.Fence.Items.Count > 1 && Order(on.Fence.Items, by) is var ordered && !ordered.SequenceEqual(on.Fence.Items, DesktopItemKeys.Comparer))
                {
                    on.Fence.Items = [.. ordered];
                }
            }

            var screen = ScreenOfView(view.Id) ?? new ScreenMetrics(0, 0);
            DesktopFences.Lay(view, screen, screen.IsUsable ? DeskIconSize(screen) : (0, 0));
        }
    }

    /// <summary>[S-13013] A fence's standing sort: its items in the order of <paramref name="by"/>, by Core's one sort (<see cref="DesktopSort.Order"/>).</summary>
    private static IReadOnlyList<string> Order(IReadOnlyList<string> keys, DesktopSortBy by)
    {
        if (!Enum.IsDefined(by))
        {
            return keys;
        }

        var items = keys.Select(key => HostDesktopItems.Instance.Find(key) ?? new DesktopItem(key, DesktopItemKeys.FallbackName(key), DesktopItemKind.File)).ToList();
        return [.. DesktopSort.Order(items, by, item => SortFacts(item, [])).Select(item => item.Key)];
    }

    /// <summary>
    /// [S-13041] A move of widgets on <paramref name="surface"/> let go (their rects already where they landed): the icons
    /// among them land in a fence or out of one by where they stand (<see cref="DesktopFences.Land"/>), and the fences are
    /// laid again, all in the move's one undo step. A view with no fence records the move as it always did.
    /// </summary>
    internal void LandMoved(DesktopWindow surface, IReadOnlyCollection<string> moved, UndoAction action)
    {
        var view = State.ViewForMonitor(surface.Monitor.Id);
        if (!view.Widgets.Exists(w => w.Config is IHoldsDesktopItems))
        {
            CommitGeometry(action);
            return;
        }

        var metrics = surface.Metrics;
        Mutate(_ => DesktopFences.Land(view, moved, metrics, DeskIconSize(metrics)), undo: action);
    }

    /// <summary>
    /// [S-13041] The one drop computation as a move on <paramref name="surface"/> asks it: where the held icon lands, on the
    /// free desktop or a fence's cell (<see cref="DesktopFences.DropCell"/>); null when the move is not one of icons alone
    /// (a fence or a card among <paramref name="moving"/> lands on the grid as any widget).
    /// </summary>
    internal Func<FracRect, FracRect>? DropCellFor(DesktopWindow surface, IReadOnlyCollection<WidgetInstance> moving)
    {
        var view = State.ViewForMonitor(surface.Monitor.Id);
        if (!view.Widgets.Exists(w => w.Config is IHoldsDesktopItems) || moving.Any(w => w.Config is not IDesktopItemConfig))
        {
            return null;
        }

        var metrics = surface.Metrics;
        var box = DeskIconSize(metrics);
        var ids = new HashSet<string>(moving.Select(w => w.Id), StringComparer.Ordinal);
        return landing => DesktopFences.DropCell(view, landing, metrics, box, ids);
    }

    /// <summary>[S-13042] Which widgets of <paramref name="view"/> the surface draws no card for now: the icons its fences do not show.</summary>
    internal HashSet<string> FenceHidden(ViewState view, ScreenMetrics metrics)
        => view.Widgets.Exists(w => w.Config is IHoldsDesktopItems) ? DesktopFences.Hidden(view, metrics, DeskIconSize(metrics)) : [];

    /// <summary>[S-13042] The widget ids of <paramref name="view"/>'s fences and the icons they hold: a move of icons alone may land on them.</summary>
    internal static HashSet<string> FencesAndTheirIcons(ViewState view)
        => [.. DesktopFences.On(view).Select(on => on.Widget.Id), .. DesktopFences.Members(view).Keys];

    /// <summary>[S-13042] The icons <paramref name="fenceId"/> of <paramref name="view"/> holds, by widget id: they move with it.</summary>
    internal static IEnumerable<string> FenceIconsOf(ViewState view, string fenceId)
        => DesktopFences.Members(view).Where(m => string.Equals(m.Value.Fence.Widget.Id, fenceId, StringComparison.Ordinal)).Select(m => m.Key);

    /// <summary>
    /// [S-13041] [S-13047] Whether a shell drag of the desktop's icons over <paramref name="widgetId"/> of <paramref name="view"/> is
    /// the desktop's drop: a fence, or an icon a fence holds (<see cref="DesktopFences.FenceAt"/>: the fence takes the icons by its
    /// cells, as the move does), unless it is a search's.
    /// </summary>
    internal static bool FenceTakesDeskDrag(ViewState view, string widgetId)
        => DesktopFences.FenceAt(view, widgetId) is { } fence && fence.Fence.Source != FenceSource.Search;

    /// <summary>
    /// [S-13012] Roll a fence up to its title bar (its height kept to unroll to) or unroll it. Not an undo step: a look,
    /// as Fences' own roll-up; the fence's rect follows in the same change (its title bar's grid rows), and its icons go
    /// from the surface while it is rolled up.
    /// </summary>
    public void RollUp(string fenceId, bool rolledUp)
    {
        if (DesktopFences.Find(State, fenceId) is not { } at || at.Fence.Fence.RolledUp == rolledUp)
        {
            return;
        }

        var screen = ScreenOfView(at.View.Id) ?? DeskPrimaryScreen;
        var before = at.Fence.Widget.Rect;
        Mutate(_ =>
        {
            var (widget, fence) = (at.Fence.Widget, at.Fence.Fence);
            if (rolledUp)
            {
                fence.OpenHeight = widget.H;
                fence.RolledUp = true;
                if (screen.IsUsable)
                {
                    widget.H = Math.Min(widget.H, FenceCells.TitleHeight(screen));
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

        // [S-13028] The roll glides on every display that shows the fence's view.
        foreach (var surface in _windows.Where(w => w.IsAlive && string.Equals(w.CurrentViewId, at.View.Id, StringComparison.Ordinal)))
        {
            surface.GlideCard(at.Fence.Widget.Id, before);
        }
    }

    /// <summary>[S-13043] The fence's body by whole rows: its icons move by a row's boxes, on the grid. Not an undo step: a look.</summary>
    public void Scroll(string fenceId, int rows)
    {
        if (rows == 0 || DesktopFences.Find(State, fenceId) is not { } at || ScreenOfView(at.View.Id) is not { IsUsable: true } screen)
        {
            return;
        }

        var fence = at.Fence.Fence;
        var layout = FenceCells.Of(at.Fence.Widget.Rect, fence, screen, DeskIconSize(screen));
        var to = (layout with { Scroll = fence.Scroll + rows }).Clamped(fence.Items.Count).Scroll;
        if (to != fence.Scroll)
        {
            Mutate(_ => fence.Scroll = to, undo: null);
        }
    }

    /// <summary>
    /// [S-13043] The mouse wheel over a fence (its body, or one of its icons) on <paramref name="surface"/> at
    /// <paramref name="dip"/>: its body scrolls a row each notch. False where no fence is under the point.
    /// </summary>
    internal bool FenceWheel(DesktopWindow surface, Point dip, int delta)
    {
        var metrics = surface.Metrics;
        if (delta == 0 || !metrics.IsUsable)
        {
            return false;
        }

        var view = State.ViewForMonitor(surface.Monitor.Id);
        var (x, y) = (dip.X / metrics.Width, dip.Y / metrics.Height);
        foreach (var on in DesktopFences.On(view).Reverse())
        {
            var r = on.Widget.Rect;
            if (!on.Fence.RolledUp && on.Fence.Source != FenceSource.Search && x >= r.X && x < r.X + r.W && y >= r.Y && y < r.Y + r.H)
            {
                Scroll(on.Widget.Id, delta > 0 ? -1 : 1);
                return true;
            }
        }

        return false;
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
    /// [S-13009] A fence goes, never a file: with <paramref name="putBack"/> its items stay on its view's free desktop where
    /// it showed them, the ones it did not show at the free spots nearest its corner ([S-11876]); else their icons leave the
    /// view. One undo step whose undo brings the fence back with its items (its config, holding them, comes back with it,
    /// [S-13010]).
    /// </summary>
    public void Remove(string fenceId, bool putBack)
    {
        if (DesktopFences.Find(State, fenceId) is not { } at)
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
            else
            {
                DesktopViews.Hide(view, fence.Items);
            }
        }, undo: UndoAction.Delete);
        OfferUndo(putBack && count > 0 ? Str.Desktop_Fence_Removed.Plural(count) : Str.Shell_Undo_DeletedWidgets.Plural(1));
    }

    /// <summary>
    /// [S-13009] The items of <paramref name="fence"/>, just taken off <paramref name="view"/>, back on its free desktop where
    /// it stood (<see cref="DesktopFences.PutBack"/>). Inside the caller's change.
    /// </summary>
    private void PutBackFenceItems(ViewState view, WidgetInstance fence)
    {
        if (ItemKind is not { } kind)
        {
            return;
        }

        var screen = ScreenOfView(view.Id) ?? DeskPrimaryScreen;
        var left = DesktopFences.PutBack(view, fence, screen, DeskIconSize(screen), (DeskCellDip.Width, DeskCellDip.Height),
            key => HostDesktopItems.Instance.Find(key), (item, rect) => NewItemWidget(kind.Id, item, rect));
        if (left.Count > 0)
        {
            Log.Info(Scope, $"{left.Count} item(s) of a removed fence found no free spot; Add widget lists them");
        }
    }

    /// <summary>
    /// [S-13042] Put desktop items into a fence, at its end (a fence with a standing sort takes them into its order): each
    /// leaves the free desktop and any other fence of the fence's view; one with no icon on that view gets one. The one way
    /// to place an icon into a fence from elsewhere (the unplaced items' page, a command). One undo step; false when the fence
    /// is gone or takes nothing (a search fence).
    /// </summary>
    public bool PlaceInFence(string fenceId, IReadOnlyList<string> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        if (DesktopFences.Find(State, fenceId) is not { } at || at.Fence.Fence.Source == FenceSource.Search)
        {
            return false;
        }

        var listed = keys.Where(key => HostDesktopItems.Instance.Find(key) is not null).ToList();
        if (listed.Count == 0)
        {
            return false;
        }

        Mutate(_ => DesktopFences.MoveIn(at.View, fenceId, listed, at.Fence.Fence.Items.Count, FenceIcon), undo: UndoAction.Move);
        return true;
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
    public bool FenceRectDrawn(DesktopWindow surface, Point start, Point end, Action closed)
    {
        if (FenceKind is null || EditMode)
        {
            return false;
        }

        var metrics = surface.Metrics;
        if (!metrics.IsUsable)
        {
            return false;
        }

        var drawn = new FracRect(
            Math.Min(start.X, end.X) / metrics.Width,
            Math.Min(start.Y, end.Y) / metrics.Height,
            Math.Abs(end.X - start.X) / metrics.Width,
            Math.Abs(end.Y - start.Y) / metrics.Height);
        if (!surface.OpenMenuAt(end, FenceCreateEntries(() => CreateFence(surface, drawn, [])), closed))
        {
            Log.Warn(Scope, "the menu to create a fence did not open");
            return false;
        }

        return true;
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
        var free = DesktopViews.Free(view);
        var rects = view.Widgets.Where(w => w.Config is IDesktopItemConfig { Item.Length: > 0 } c && set.Contains(c.Item) && free.Contains(c.Item)).Select(w => w.Rect).ToList();
        if (rects.Count == 0)
        {
            // Every one is in a fence already: the new one stands at the menu's place.
            var (x, y) = MenuPointIn(surface);
            rects = [new FracRect(x, y, 0, 0)];
        }

        if (DesktopFences.AroundIcons(rects, keys.Count, DeskItemBox(surface.Metrics), surface.Metrics) is { } rect)
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
                DesktopFences.MoveIn(view, widget.Id, keys, 0, FenceIcon);
            }
        }, undo: UndoAction.Add);
        Log.Info(Scope, $"a fence was made with {keys.Count} item(s)");
    }

    /// <inheritdoc />
    public FenceCellsDip Cells(string fenceId)
        => DesktopFences.Find(State, fenceId) is { } at && ScreenOfView(at.View.Id) is { IsUsable: true } screen
            ? new FenceCellsDip(FenceCells.TitleHeight(screen) * screen.Height, DeskItemBox(screen))
            : new FenceCellsDip(FenceGrid.TitleDip, DeskCellDip);

    /// <summary>[S-13019] A desktop icon's box on the widget grid of <paramref name="screen"/>, in DIPs: what a fence's cell is.</summary>
    private SizeDip DeskItemBox(ScreenMetrics screen)
    {
        var (w, h) = DeskIconSize(screen);
        return new SizeDip(w * screen.Width, h * screen.Height);
    }

    /// <inheritdoc />
    public bool Band(string fenceId, PointerRoutedEventArgs press)
    {
        ArgumentNullException.ThrowIfNull(press);
        if (EditMode || (SurfaceUnderCursor() ?? _windows.FirstOrDefault(w => w.IsAlive && w.Draws(fenceId))) is not { } surface)
        {
            return false;
        }

        _bandExcept = DesktopItemKeys.Widget(fenceId);
        if (surface.BeginDesktopBandFrom(press))
        {
            return true;
        }

        _bandExcept = null;
        return false;
    }

    /// <summary>[S-13020] The fence a band began in: never in its own band's hits.</summary>
    private string? _bandExcept;

    /// <inheritdoc />
    public Task<IReadOnlyList<FoundFile>> SearchAsync(FenceRuleSet rules, FenceQuery query, CancellationToken token)
        => FileSearch.FindAsync(rules, query, token);

    /// <summary>
    /// [S-13022] [S-13023] Every fence shown as its source says, in the change being made (<see cref="Mutate"/>): collecting
    /// fences take in what their rules match, a search fence holds nothing; what they let go lands on its view's free
    /// desktop at the first free spots (<see cref="DesktopFences.LetGo"/>). Nothing before the desktop is read.
    /// </summary>
    private void CollectFences(AppState state)
    {
        if (ItemKind is not { } kind || !HostDesktopItems.Instance.HasListing || !state.Views.Exists(v => DesktopFences.On(v).Any(on => on.Fence.Source != FenceSource.Held)))
        {
            return;
        }

        foreach (var (view, keys) in DesktopFences.Collect(state, HostDesktopItems.Instance.Items, FenceIcon))
        {
            var items = keys.Select(key => HostDesktopItems.Instance.Find(key)).OfType<DesktopItem>().ToList();
            var left = DesktopFences.LetGo(view, items, null, null, ScreenOfView(view.Id) ?? DeskPrimaryScreen, (DeskCellDip.Width, DeskCellDip.Height),
                (item, rect) => NewItemWidget(kind.Id, item, rect));
            if (left.Count > 0)
            {
                Log.Info(Scope, $"{left.Count} item(s) a fence let go found no free spot; Add widget lists them");
            }
        }
    }
}
