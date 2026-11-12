using Themia.Core.Links;

namespace Themia.App.Widgets;

/// <summary>
/// [S-11950] The drive card's place on its view's links: the card is its drive (<see cref="LinkKeys.DriveLink"/>
/// of its root). The pointer on it publishes the drive (<see cref="LinkTopics.HoveredDrive"/>), and while the
/// drive is pointed at elsewhere on the view the whole card takes the item layer's hover fill ([S-11903]).
/// </summary>
internal sealed partial class DriveView
{
    private readonly WidgetLinks _links = new();
    private LinkCard<DriveLink>? _card;

    private LinkCard<DriveLink> Card => _card ??= new LinkCard<DriveLink>(_links, LinkTopics.HoveredDrive, this);

    private void JoinLinks()
    {
        _links.Join(_surface?.Links);
        Card.Publishes(this);
    }

    private void LeaveLinks()
    {
        Card.Release();
        _links.Leave();
    }

    /// <summary>The drive shown now is what the card is.</summary>
    private void LinkDrive() => Card.Key = LinkKeys.DriveLink(_drive?.Root);
}
