using Themia.Core.Links;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// [S-11930] [S-11932] The mixer's place on its view's links: a row is its session's app
/// (<see cref="MixerRules.AppKey"/>; the system sounds and a session whose process could not be read take
/// no part). The pointer or the keyboard cursor on a row publishes the app
/// (<see cref="LinkTopics.HoveredApp"/>); a row lights for the app pointed at anywhere on the view (the item
/// layer's hover fill) and for the app picked there (<see cref="LinkTopics.SelectedApp"/>: the selected fill
/// and the accent pill), in place. A click keeps its meaning: the mixer never picks.
/// </summary>
internal sealed partial class MixerView
{
    private readonly WidgetLinks _links = new();
    private LinkLights<AppLink>? _apps;

    private LinkLights<AppLink> Apps => _apps ??= new LinkLights<AppLink>(_links, LinkTopics.HoveredApp, LinkTopics.SelectedApp);

    /// <summary>The rows as they are shown: each visible row is its app, a hidden one nothing.</summary>
    private void LinkRows()
    {
        for (var i = 0; i < _rows.Count; i++)
        {
            if (i < _shown.Count)
            {
                Apps.Row(_rows[i].Host, AppOf(_shown[i]));
            }
            else
            {
                Apps.Remove(_rows[i].Host);
            }
        }

        Apps.Apply();
    }

    /// <summary>[S-3307] The keyboard cursor publishes its row's app as the hover; none when it lets go.</summary>
    private void PointCursor(int index)
        => Apps.Point(index >= 0 && index < _shown.Count ? AppOf(_shown[index]) : null);

    private static AppLink? AppOf(MixerRow row) => MixerRules.AppKey(row) is { } key ? new AppLink(key) : null;
}
