using Themia.Core.Links;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// [S-11930] [S-11934] Shortcuts' place on its view's links: an item whose target is an executable is that
/// app (<see cref="ShortcutRules.AppKey"/>; a folder, a document, a site, a layout takes no part). The
/// pointer on it, or the keyboard cursor, publishes the app (<see cref="LinkTopics.HoveredApp"/>). An item
/// that is the app pointed at anywhere on the view looks as it does under the pointer (the desktop's hover,
/// <see cref="SelectionChrome"/>); one that is the app picked there (<see cref="LinkTopics.SelectedApp"/>) takes
/// the item layer's selected fill (<see cref="LinkLook.Row"/>). Painted in place; a click keeps its meaning (select,
/// open): shortcuts never pick.
/// </summary>
internal sealed partial class ShortcutsView
{
    private readonly WidgetLinks _links = new();
    private LinkLights<AppLink>? _apps;

    private LinkLights<AppLink> Apps
    {
        get
        {
            if (_apps is null)
            {
                _apps = new LinkLights<AppLink>(_links, LinkTopics.HoveredApp, LinkTopics.SelectedApp);
                _apps.Changed += OnLinksChanged;
            }

            return _apps;
        }
    }

    private void OnLinksChanged()
    {
        if (!_detached)
        {
            PaintAll();
        }
    }

    /// <summary>How <paramref name="cell"/> stands against the app pointed at and picked on the view.</summary>
    private LinkLight LinkLightOf(Cell cell)
    {
        var apps = Apps;
        var shown = _cells.Select(AppOf).OfType<AppLink>();
        return LinkEmphasis.Light(AppOf(cell), LinkEmphasis.Shown(apps.Pointed, shown), LinkEmphasis.Shown(apps.Picked, shown));
    }

    /// <summary>The pointer came onto a cell (<paramref name="on"/>) or left it: its app is the view's hover while it is there.</summary>
    private void PointCell(Cell cell, bool on)
    {
        if (AppOf(cell) is { } app)
        {
            Apps.Point(on ? app : null);
        }
    }

    /// <summary>[S-3307] The keyboard cursor publishes its item's app as the hover; none when it lets go.</summary>
    private void PointCursor()
        => Apps.Point(CursorCell() is { } cell && _active ? AppOf(cell) : null);

    private static AppLink? AppOf(Cell cell) => ShortcutRules.AppKey(cell.Target) is { } key ? new AppLink(key) : null;
}
