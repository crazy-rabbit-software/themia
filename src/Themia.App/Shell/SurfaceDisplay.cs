using Themia.App.Services;
using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.Core.Geometry;
using Themia.Core.Widgets;

namespace Themia.App.Shell;

/// <summary>
/// One display's surface, as a widget may see it: the views, the one shown here, and a way to show
/// another. [S-487]
/// <para>
/// Read live on every call rather than captured, because the views can be renamed, added and
/// deleted from the settings window while the widget is on screen, and the monitor behind a surface
/// can be re-resolved after a display change.
/// </para>
/// </summary>
internal sealed class SurfaceDisplay(AppHost host, DesktopWindow surface) : IWidgetDisplay
{
    public IReadOnlyList<ViewSummary> Views
        => [.. host.State.Views.Select(v => new ViewSummary(v.Id, v.Name, v.Icon, v.Widgets.Count))];

    public string ShownViewId => surface.CurrentViewId;

    /// <summary>
    /// [S-19] This display only. Through the host, so it is the same mutation the toolbar's View
    /// menu makes and the same save.
    /// </summary>
    public void ShowView(string viewId) => host.ShowView(surface.Monitor.Id, viewId);

    /// <inheritdoc />
    public IReadOnlyList<FracRect> Tiles(string viewId)
        => host.State.FindView(viewId) is { } view
            ? [.. view.Widgets.Where(w => w.ParentId is null).Select(w => w.Rect)]
            : [];

    /// <summary>The whole monitor's shape: widget rects are fractions of it, taskbar included ([S-840]).</summary>
    public double Aspect
        => surface.Monitor.Bounds is { Width: > 0, Height: > 0 } bounds ? (double)bounds.Width / bounds.Height : 0;

    /// <inheritdoc />
    public void DuplicateView(string viewId) => host.DuplicateView(viewId, surface.Monitor.Id);

    /// <inheritdoc />
    public void MoveView(string viewId, int delta) => host.MoveView(viewId, delta);

    /// <inheritdoc />
    public void DeleteView(string viewId) => host.DeleteView(viewId);

    /// <inheritdoc />
    public void OpenViewSettings(string viewId) => host.ShowConfig(ConfigTarget.ForView(viewId));
}
