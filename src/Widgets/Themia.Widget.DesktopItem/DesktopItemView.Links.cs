using Themia.App.Widgets;
using Themia.Core.Links;
using Themia.Sdk;

namespace Themia.Widget.DesktopItem;

/// <summary>
/// [S-11930] [S-11934] [S-11945] [S-11949] The desktop icon's place on its view's links. A desktop item
/// is its file, folder or link by its path (<see cref="LinkKeys.LocalFileLink"/>; This PC, the Recycle
/// Bin and every other shell item are none). An own icon whose target is an executable is that app
/// (<see cref="OwnIconRules.AppKey"/>); one whose target is a file or a folder is that file
/// (<see cref="OwnIconRules.FileKey"/>, its stored path); a site, a location, a Settings page and a
/// layout take no part. The pointer on the icon publishes what it is (<see cref="LinkTopics.HoveredApp"/>,
/// <see cref="LinkTopics.HoveredFile"/>). An icon that is the app or file pointed at anywhere on the
/// view, or the app picked there (<see cref="LinkTopics.SelectedApp"/>), is drawn as under the pointer
/// (the desktop's hover, the one selection chrome), over what its own selection draws. A click keeps
/// its meaning (select, open): a desktop icon never picks.
/// </summary>
internal sealed partial class DesktopItemView
{
    private readonly WidgetLinks _links = new();

    /// <summary>The app another widget of the view points at, the app picked there, and the file pointed at; null for none.</summary>
    private AppLink? _pointedApp;
    private AppLink? _pickedApp;
    private FileLink? _pointedFile;

    /// <summary>Follow the apps and files of the view (once, from the constructor; heard again on every join).</summary>
    private void FollowLinks()
    {
        _links.Follow(LinkTopics.HoveredApp, app =>
        {
            _pointedApp = app;
            PaintLinked();
        });
        _links.Follow(LinkTopics.SelectedApp, app =>
        {
            _pickedApp = app;
            PaintLinked();
        });
        _links.Follow(LinkTopics.HoveredFile, file =>
        {
            _pointedFile = file;
            PaintLinked();
        });
    }

    private void PaintLinked()
    {
        if (_attached)
        {
            Paint();
        }
    }

    private AppLink? App => Own && !_config.IsLayout && OwnIconRules.AppKey(_target) is { } key ? new AppLink(key) : null;

    private FileLink? LinkedFile => Own
        ? _config.IsLayout ? null : OwnIconRules.FileKey(_target)
        : Item is { Kind: not DesktopItemKind.ShellItem } item ? LinkKeys.LocalFileLink(item.Key) : null;

    /// <summary>Whether the icon is the app pointed at or picked on the view, or the file pointed at.</summary>
    private bool LinkedElsewhere
        => (App is { } app && (app.Equals(_pointedApp) || app.Equals(_pickedApp)))
            || (LinkedFile is { } file && file.Equals(_pointedFile));

    /// <summary>The pointer came onto the icon (<paramref name="on"/>) or left it: its app or its file is the view's hover while it is there.</summary>
    private void PointLink(bool on)
    {
        if (App is { } app)
        {
            _links.Set(LinkTopics.HoveredApp, on ? app : null);
        }
        else if (LinkedFile is { } file)
        {
            _links.Set(LinkTopics.HoveredFile, on ? file : null);
        }

        if (!on)
        {
            // [S-11964] What the board holds now, not what was heard last: a hover this icon took over from another
            // widget (the same app or file set again) and let go is cleared without this icon hearing it.
            _pointedApp = _links.Get(LinkTopics.HoveredApp);
            _pointedFile = _links.Get(LinkTopics.HoveredFile);
        }
    }
}
