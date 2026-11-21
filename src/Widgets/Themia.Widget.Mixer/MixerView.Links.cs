using Themia.Core.Links;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// [S-11930] [S-11932] [S-11965] The mixer's place on its view's links: grouped by app, a row is its app
/// (<see cref="MixerRules.AppKey"/>; the system sounds and a session whose process could not be read take
/// no part); ungrouped, a row is one session and takes no part. The pointer or the keyboard cursor on a row publishes the app
/// (<see cref="LinkTopics.HoveredApp"/>); a row lights for the app pointed at anywhere on the view (the item
/// layer's hover fill) and for the app picked there (<see cref="LinkTopics.SelectedApp"/>: the selected fill
/// and the accent pill), in place. A click keeps its meaning: the mixer never picks.
/// </summary>
internal sealed partial class MixerView
{
    private readonly WidgetLinks _links = new();
    private LinkLights<AppLink>? _apps;

    private LinkLights<AppLink> Apps => _apps ??= new LinkLights<AppLink>(_links, LinkTopics.HoveredApp, LinkTopics.SelectedApp);

    /// <summary>A row the kit's list drew: its app.</summary>
    private void LinkRow(Themia.Sdk.Kit.KitItem item, Microsoft.UI.Xaml.FrameworkElement row)
        => Apps.Row(row, RowOf(item.Id) is { } shown ? AppOf(shown.Row) : null);

    /// <summary>The kit's list draws its rows again: the old ones are forgotten.</summary>
    private void ForgetRows() => Apps.Clear();

    /// <summary>[S-3307] The keyboard cursor publishes its row's app as the hover; none when it lets go.</summary>
    private void PointCursor(Themia.Sdk.Kit.KitItem? item)
        => Apps.Point(item is not null && RowOf(item.Id) is { } shown ? AppOf(shown.Row) : null);

    private AppLink? AppOf(MixerRow row) => MixerRules.AppKey(row, _config.GroupByApp) is { } key ? new AppLink(key) : null;
}
