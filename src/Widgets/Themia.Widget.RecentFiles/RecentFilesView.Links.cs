using Themia.Core.Links;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// [S-11945] [S-11948] Recent files' place on its view's links: a row is its file
/// (<see cref="LinkKeys.FileLink"/> of its full path). The pointer on it, or the keyboard cursor, publishes it
/// (<see cref="LinkTopics.HoveredFile"/>); a row that is the file pointed at anywhere on the view is drawn as
/// under the pointer (the desktop's hover, <see cref="SelectionChrome"/>), the focused row keeping its own
/// mark. Painted in place: never rebuilt, never scrolled.
/// </summary>
internal sealed partial class RecentFilesView
{
    private readonly WidgetLinks _links = new();
    private LinkLights<FileLink>? _fileLights;

    private LinkLights<FileLink> FileLights
    {
        get
        {
            if (_fileLights is null)
            {
                _fileLights = new LinkLights<FileLink>(_links, LinkTopics.HoveredFile);
                _fileLights.Changed += OnFileLinks;
            }

            return _fileLights;
        }
    }

    private void OnFileLinks()
    {
        if (!_detached)
        {
            ShowCursor(bringIntoView: false);
        }
    }

    /// <summary>[S-11945] Whether row <paramref name="index"/> is the file pointed at on the view.</summary>
    private bool PointedElsewhere(int index)
        => LinkEmphasis.Light(FileAt(index), FileLights.Pointed, null) == LinkLight.Pointed;

    /// <summary>The pointer came onto row <paramref name="index"/> (<paramref name="on"/>) or left it: its file is the view's hover while it is there.</summary>
    private void PointRow(int index, bool on)
    {
        var file = FileAt(index);
        if (on)
        {
            FileLights.Point(file);
        }
        else if (file is not null && file.Equals(FileLights.Pointed))
        {
            FileLights.Point(null);
        }
    }

    /// <summary>[S-3307] The keyboard cursor publishes its row's file as the hover; none when it lets go.</summary>
    private void PointCursor() => FileLights.Point(FileAt(_cursor));

    private FileLink? FileAt(int index) => index >= 0 && index < _files.Count ? LinkKeys.FileLink(_files[index].Path) : null;
}
