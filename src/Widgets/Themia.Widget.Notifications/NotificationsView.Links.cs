using Microsoft.UI.Xaml.Controls;
using Themia.Core.Links;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI.Kit;

namespace Themia.App.Widgets;

/// <summary>
/// [S-11930] [S-11933] [S-11965] Notifications' place on its view's links: an app's heading (grouped, [S-4402]) is that
/// app where its id says which (<see cref="NotificationRules.AppKey"/>; a sender it cannot name, Windows' own toasts
/// among them, takes no part). The pointer on a heading publishes the app (<see cref="LinkTopics.HoveredApp"/>); a
/// heading lights for the app pointed at anywhere on the view (the item layer's hover fill) and for the app picked
/// there (<see cref="LinkTopics.SelectedApp"/>: the selected fill and the accent pill), in place. A notification is
/// not its app: its row publishes nothing and lights for nothing, so pointing at an app never lights every
/// notification it sent. The list never picks.
/// </summary>
internal sealed partial class NotificationsView
{
    private readonly WidgetLinks _links = new();
    private LinkLights<AppLink>? _apps;

    private LinkLights<AppLink> Apps => _apps ??= new LinkLights<AppLink>(_links, LinkTopics.HoveredApp, LinkTopics.SelectedApp);

    /// <summary>An app's heading: that app. The whole heading takes the pointer, not only its icon and name.</summary>
    private void LinkHeading(Grid heading, NotificationGroup group)
    {
        heading.Background ??= Family.Brush(ColourRole.Clear);
        Apps.Row(heading, NotificationRules.AppKey(group.AppId) is { } key ? new AppLink(key) : null);
    }
}
