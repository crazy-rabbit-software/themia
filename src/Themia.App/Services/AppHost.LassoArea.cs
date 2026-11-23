using Themia.App.Shell;
using Themia.App.Widgets;
using Themia.Core.Desktop;
using Themia.Core.Diagnostics;
using Themia.Core.Geometry;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Windows.Foundation;

namespace Themia.App.Services;

/// <summary>
/// [S-13018] A lasso let go over empty desktop (locked or in edit mode): a menu of two lines at the release, Add widget
/// here (the add page, the widget picked laid into the area) and Create fence here (a fence on the area taking in the
/// free icons inside it). The rules are Core's (<see cref="LassoArea"/>).
/// </summary>
internal sealed partial class AppHost
{
    /// <summary>
    /// [S-13018] The lasso from <paramref name="start"/> to <paramref name="end"/> (DIPs of <paramref name="surface"/>) was
    /// let go having touched <paramref name="caught"/> widgets: an area offers its menu at the release. True when the menu
    /// opened; <paramref name="closed"/> runs once as it closes (the band stays drawn until then).
    /// </summary>
    public bool LassoReleased(DesktopWindow surface, Point start, Point end, int caught, Action closed)
    {
        var metrics = surface.Metrics;
        if (!metrics.IsUsable)
        {
            return false;
        }

        var band = new FracRect(
            Math.Min(start.X, end.X) / metrics.Width,
            Math.Min(start.Y, end.Y) / metrics.Height,
            Math.Abs(end.X - start.X) / metrics.Width,
            Math.Abs(end.Y - start.Y) / metrics.Height);
        if (LassoArea.Offered(band, caught, metrics) is not { } area)
        {
            return false;
        }

        var viewId = surface.CurrentViewId;
        Action? fence = FenceKind is null ? null : () =>
        {
            if (State.FindView(viewId) is { } view)
            {
                CreateFence(surface, area, LassoArea.IconsInside(view, area));
            }
        };

        if (!surface.OpenMenuAt(end, LassoEntries(() => surface.OpenAddPageFor(area), fence), closed))
        {
            Log.Warn(Scope, "the menu for a lassoed area did not open");
            return false;
        }

        return true;
    }

    /// <summary>[S-13018] The lassoed area's menu: Add widget here, then Create fence here (when the build has fences). Static: the menu render draws it too.</summary>
    internal static List<ItemMenuEntry> LassoEntries(Action addWidget, Action? createFence)
    {
        var entries = new List<ItemMenuEntry> { new(Glyphs.Add, Str.Desktop_Area_AddWidget.Text(), addWidget) };
        if (createFence is not null)
        {
            entries.AddRange(FenceCreateEntries(createFence));
        }

        return entries;
    }

    /// <summary>
    /// [S-13018] A widget picked on the add page opened for a lassoed area: made, laid into <paramref name="area"/> of
    /// <paramref name="surface"/>'s view (<see cref="LassoArea.Fill"/>: the box its kind allows, on the grid), selected; one
    /// undo step. False when the licence refuses it or the display is not measured.
    /// </summary>
    public bool PlaceWidgetIn(DesktopWindow surface, WidgetPick kind, FracRect area)
    {
        if (!surface.Metrics.IsUsable || !AllowCreate(1))
        {
            return false;
        }

        var widget = WidgetInstance.Create(kind, area);
        LassoArea.Fill(widget, area, surface.Metrics, surface.ContentScaleOf(widget));
        var viewId = surface.CurrentViewId;
        Mutate(state => (state.FindView(viewId) ?? state.ActiveView()).Widgets.Add(widget), undo: UndoAction.Add);
        SelectMany(viewId, [widget.Id]);
        Log.Info(Scope, $"a {kind} widget was laid into a lassoed area");
        return true;
    }
}
