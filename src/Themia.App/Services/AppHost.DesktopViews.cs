using Themia.App.Services.Files;
using Themia.Core.Desktop;
using Themia.Core.Diagnostics;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Sdk;

namespace Themia.App.Services;

/// <summary>
/// [S-5525]..[S-5529] Which desktop icons each view shows, carried out: the Views card's choice and list, and the icon
/// menu's Hide from this view. The rules are Core's (<see cref="DesktopViews"/>); each change is one undo step.
/// </summary>
internal sealed partial class AppHost
{
    /// <summary>The primary display's metrics (else the first live display's): where a view no display shows is laid out.</summary>
    private ScreenMetrics DeskPrimaryScreen
        => (_windows.FirstOrDefault(w => w.IsAlive && w.Monitor.IsPrimary) ?? _windows.FirstOrDefault(w => w.IsAlive))?.Metrics ?? default;

    /// <summary>The metrics of the display showing <paramref name="viewId"/>, else the primary display's.</summary>
    private ScreenMetrics DeskScreenOf(string viewId)
        => ScreenOfView(viewId) is { IsUsable: true } shown ? shown : DeskPrimaryScreen;

    /// <summary>
    /// [S-5528] Set which desktop icons a view shows, one undo step: to None its item widgets go; to All every listed item
    /// it lacks is laid in at the first free spots ([S-5521]); to Chosen what it shows now is its chosen set.
    /// </summary>
    public void SetViewDesktopIcons(string viewId, ViewDesktopIcons value)
    {
        if (State.FindView(viewId) is not { } current || current.DesktopIcons == value)
        {
            return;
        }

        var screen = DeskScreenOf(viewId);
        IReadOnlyList<DesktopItem> unplaced = [];
        Mutate(state =>
        {
            if (state.FindView(viewId) is not { } view || !DesktopViews.Set(view, value))
            {
                return;
            }

            if (value == ViewDesktopIcons.All && ItemKind is { } kind && HostDesktopItems.Instance.HasListing)
            {
                unplaced = DesktopViews.Fill(view, HostDesktopItems.Instance.Items, screen, (DeskCellDip.Width, DeskCellDip.Height),
                    (item, rect) => NewItemWidget(kind.Id, item, rect));
            }
        });

        Log.Info(Scope, $"a view shows {value} desktop icons now");
        if (unplaced.Count > 0)
        {
            Log.Info(Scope, $"{unplaced.Count} desktop item(s) found no free cell on the view; Add widget lists them");
        }
    }

    /// <summary>[S-5527] Show one desktop item on a view at its first free spot, or hide it from that view; one undo step.</summary>
    public void ShowDesktopItemOn(string viewId, string key, bool shown)
    {
        if (State.FindView(viewId) is not { } current || ItemKind is not { } kind)
        {
            return;
        }

        if (!shown)
        {
            if (DesktopViews.Shown(current).Contains(key))
            {
                Mutate(state => _ = state.FindView(viewId) is { } view ? DesktopViews.Hide(view, [key]) : 0, undo: UndoAction.Delete);
            }

            return;
        }

        if (HostDesktopItems.Instance.Find(key) is not { } item || DesktopViews.Shown(current).Contains(key) || !DesktopViews.Holds(current))
        {
            return;
        }

        var screen = DeskScreenOf(viewId);
        IReadOnlyList<DesktopItem> unplaced = [];
        Mutate(state =>
        {
            if (state.FindView(viewId) is { } view)
            {
                unplaced = DesktopViews.Fill(view, [item], screen, (DeskCellDip.Width, DeskCellDip.Height), (it, rect) => NewItemWidget(kind.Id, it, rect));
            }
        }, undo: UndoAction.Add);

        if (unplaced.Count > 0)
        {
            Log.Info(Scope, "the desktop item found no free cell on the view; Add widget lists it");
        }
    }

    /// <summary>[S-5527] The icon menu's Hide from this view: each desktop item's icon off the view it stands on, one undo step.</summary>
    private void HideDesktopIcons(IReadOnlyList<DeskIcon> icons)
    {
        var byView = icons
            .Where(icon => !icon.Own && _deskWidgets.ContainsKey(icon.Key))
            .GroupBy(icon => _deskWidgets[icon.Key].Surface.CurrentViewId, StringComparer.Ordinal)
            .ToList();
        if (byView.Count == 0)
        {
            return;
        }

        Mutate(state =>
        {
            foreach (var group in byView)
            {
                if (state.FindView(group.Key) is { } view)
                {
                    DesktopViews.Hide(view, group.Select(icon => icon.Value));
                }
            }
        }, undo: UndoAction.Delete);
    }
}
