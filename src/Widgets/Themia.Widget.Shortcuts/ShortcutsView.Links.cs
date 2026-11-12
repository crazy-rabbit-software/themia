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
/// <para>
/// [S-11945] [S-11949] An item whose target is a file or a folder (no executable) is that file
/// (<see cref="ShortcutRules.FileKey"/>, its stored path): it publishes it (<see cref="LinkTopics.HoveredFile"/>)
/// the same way and looks as under the pointer while it is pointed at elsewhere.
/// </para>
/// </summary>
internal sealed partial class ShortcutsView
{
    private readonly WidgetLinks _links = new();
    private LinkLights<AppLink>? _apps;
    private LinkLights<FileLink>? _files;

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

    private LinkLights<FileLink> Files
    {
        get
        {
            if (_files is null)
            {
                _files = new LinkLights<FileLink>(_links, LinkTopics.HoveredFile);
                _files.Changed += OnLinksChanged;
            }

            return _files;
        }
    }

    private void OnLinksChanged()
    {
        if (!_detached)
        {
            PaintAll();
        }
    }

    /// <summary>How <paramref name="cell"/> stands against the app pointed at and picked on the view, and the file pointed at.</summary>
    private LinkLight LinkLightOf(Cell cell)
    {
        var apps = Apps;
        var shown = _cells.Select(AppOf).OfType<AppLink>();
        var app = LinkEmphasis.Light(AppOf(cell), LinkEmphasis.Shown(apps.Pointed, shown), LinkEmphasis.Shown(apps.Picked, shown));
        return app != LinkLight.None ? app : LinkEmphasis.Light(FileOf(cell), Files.Pointed, null);
    }

    /// <summary>The pointer came onto a cell (<paramref name="on"/>) or left it: its app or its file is the view's hover while it is there.</summary>
    private void PointCell(Cell cell, bool on)
    {
        if (AppOf(cell) is { } app)
        {
            Apps.Point(on ? app : null);
        }

        if (FileOf(cell) is { } file && (on || file.Equals(Files.Pointed)))
        {
            Files.Point(on ? file : null);
        }
    }

    /// <summary>[S-3307] The keyboard cursor publishes its item's app or file as the hover; none when it lets go.</summary>
    private void PointCursor()
    {
        var cell = _active ? CursorCell() : null;
        Apps.Point(cell is null ? null : AppOf(cell));
        Files.Point(cell is null ? null : FileOf(cell));
    }

    private static AppLink? AppOf(Cell cell) => ShortcutRules.AppKey(cell.Target) is { } key ? new AppLink(key) : null;

    private static FileLink? FileOf(Cell cell) => ShortcutRules.FileKey(cell.Target);
}
