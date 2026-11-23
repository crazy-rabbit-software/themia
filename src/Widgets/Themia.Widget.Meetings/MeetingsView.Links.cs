using Microsoft.UI.Xaml;
using Themia.App.Widgets;
using Themia.Core.Links;
using Themia.Core.Office;
using Themia.Sdk.Kit;

namespace Themia.Widget.Meetings;

/// <summary>
/// [S-11935] [S-11965] The Meetings' place on its view's links. An item is its meeting, and only its meeting: the pointer on
/// it publishes the meeting (<see cref="LinkTopics.HoveredEvent"/>, <see cref="CalendarShare.Key"/>) and lights that item
/// alone; the item takes the item layer's hover fill while that same meeting is pointed at in another widget of the view.
/// </summary>
internal sealed partial class MeetingsView
{
    private readonly WidgetLinks _links = new();
    private LinkLights<EventLink>? _meetings;

    private LinkLights<EventLink> Meetings => _meetings ??= new LinkLights<EventLink>(_links, LinkTopics.HoveredEvent);

    /// <summary>An item the list drew: its meeting.</summary>
    private void LinkRow(KitItem item, FrameworkElement row)
        => Meetings.Row(row, _source.MeetingOf(item.Id) is { } meeting ? CalendarShare.Key(meeting) : null);

    /// <summary>The list draws its items again: the old ones are forgotten.</summary>
    private void ForgetRows() => Meetings.Clear();
}
