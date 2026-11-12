using Themia.Core.Links;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI.Kit;

namespace Themia.App.Widgets;

/// <summary>
/// [S-11930] [S-11933] Notifications' place on its view's links: a row is its sender's app where its id says
/// which (<see cref="NotificationRules.AppKey"/>; a sender it cannot name, Windows' own toasts among them,
/// takes no part). The pointer or the keyboard cursor on a row publishes the app
/// (<see cref="LinkTopics.HoveredApp"/>); a row lights for the app pointed at anywhere on the view (the item
/// layer's hover fill) and for the app picked there (<see cref="LinkTopics.SelectedApp"/>: the selected fill
/// and the accent pill), in place. A click keeps its meaning (the ring; a double-click opens the sender):
/// the list never picks.
/// </summary>
internal sealed partial class NotificationsView
{
    private readonly WidgetLinks _links = new();
    private LinkLights<AppLink>? _apps;

    private LinkLights<AppLink> Apps => _apps ??= new LinkLights<AppLink>(_links, LinkTopics.HoveredApp, LinkTopics.SelectedApp);

    private void LinkRow(RowBlock row, NotificationItem item) => Apps.Row(row, AppOf(item));

    /// <summary>[S-3307] The keyboard cursor publishes its row's sender as the hover; none when it lets go.</summary>
    private void PointCursor()
        => Apps.Point(_cursor is { } at && at < _rows.Count ? AppOf(_rows[at].Item) : null);

    private static AppLink? AppOf(NotificationItem item) => NotificationRules.AppKey(item.AppId) is { } key ? new AppLink(key) : null;
}
