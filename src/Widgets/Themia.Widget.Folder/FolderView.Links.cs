using Themia.Core.Links;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI.Kit;

namespace Themia.App.Widgets;

/// <summary>
/// [S-11945] [S-11948] The folder's place on its view's links: a row or a cell is its entry's file or folder
/// (<see cref="LinkKeys.FileLink"/> of its full path). The pointer on it, or the keyboard cursor, publishes it
/// (<see cref="LinkTopics.HoveredFile"/>); an item that is the file pointed at anywhere on the view is drawn as
/// under the pointer (the desktop's hover, <see cref="SelectionChrome"/>), over whatever its own selection
/// draws, and is drawn by that selection alone again when it goes. Painted in place: never rebuilt, never scrolled.
/// </summary>
internal sealed partial class FolderView
{
    private readonly WidgetLinks _links = new();
    private LinkLights<FileLink>? _files;

    /// <summary>The file lit last, so a change repaints only the items it was and is.</summary>
    private FileLink? _litFile;

    private LinkLights<FileLink> Files
    {
        get
        {
            if (_files is null)
            {
                _files = new LinkLights<FileLink>(_links, LinkTopics.HoveredFile);
                _files.Changed += OnFileLinks;
            }

            return _files;
        }
    }

    /// <summary>Another widget (or this one's pointer) points at a file, or let go: the items that were it and are it are painted again.</summary>
    private void OnFileLinks()
    {
        var lit = Files.Pointed;
        if (_detached || Equals(lit, _litFile))
        {
            return;
        }

        var was = _litFile;
        _litFile = lit;
        foreach (var visual in _visuals)
        {
            var file = FileOf(visual);
            if (file is not null && (file.Equals(was) || file.Equals(lit)))
            {
                Paint(visual);
            }
        }
    }

    /// <summary>[S-11945] Whether <paramref name="visual"/> is the file pointed at on the view.</summary>
    private bool PointedElsewhere(ItemVisual visual)
        => LinkEmphasis.Light(FileOf(visual), Files.Pointed, null) == LinkLight.Pointed;

    /// <summary>The pointer came onto an item (<paramref name="on"/>) or left it: its file is the view's hover while it is there.</summary>
    private void PointItem(ItemVisual visual, bool on)
    {
        var file = FileOf(visual);
        if (on)
        {
            Files.Point(file);
        }
        else if (file is not null && file.Equals(Files.Pointed))
        {
            Files.Point(null);
        }
    }

    /// <summary>[S-3307] The keyboard cursor publishes its item as the hover; none when the widget lets go of the keys.</summary>
    private void PointCursor()
        => Files.Point(_active && _selection.Focus is { } focus ? LinkKeys.FileLink(focus) : null);

    private static FileLink? FileOf(ItemVisual visual) => LinkKeys.FileLink(visual.Entry.FullPath);

    // ---- the drive it is on ([S-11954]) ----------------------------------------------------------

    private LinkCard<DriveLink>? _driveCard;

    /// <summary>[S-11954] The card as the drive its folder is on: its header and its path publish it, the whole card lights for it.</summary>
    private LinkCard<DriveLink> DriveCard => _driveCard ??= new LinkCard<DriveLink>(_links, LinkTopics.HoveredDrive, this);

    /// <summary>
    /// [S-11954] The pointer on the header (the card's row the view's own parts stand in) or on the path crumb
    /// publishes the folder's drive; on its entries it is the entry's file that is pointed at, not the drive.
    /// </summary>
    private void JoinDriveLinks()
    {
        DriveCard.Publishes(_crumbHost);
        if (_header.Parent is HeaderBlock header)
        {
            DriveCard.Publishes(header);
        }
    }

    /// <summary>[S-11954] The drive of the folder on screen now (a share's none).</summary>
    private void LinkDrive() => DriveCard.Key = _resolved.IsValid ? LinkKeys.DriveLink(CurrentPath) : null;
}
