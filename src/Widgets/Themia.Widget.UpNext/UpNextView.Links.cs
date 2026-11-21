using Microsoft.UI.Xaml;
using Themia.App.Widgets;
using Themia.Core.Links;
using Themia.Core.Office;
using Themia.Sdk.Kit;

namespace Themia.Widget.UpNext;

/// <summary>
/// [S-11935] [S-11965] Up next's place on its view's links. A row is its meeting, and only its meeting: the pointer on
/// it publishes the meeting (<see cref="LinkTopics.HoveredEvent"/>, <see cref="CalendarShare.Key"/>) and lights that row
/// alone; the row takes the item layer's hover fill while that same meeting is pointed at in another widget of the view
/// (a calendar's chip, the next meeting, a planner's row). Neither the day a meeting is on nor its organiser lights it:
/// several meetings share those. The rows are lit in place on the rows the kit's list drew.
/// </summary>
internal sealed partial class UpNextView
{
    private readonly WidgetLinks _links = new();
    private LinkLights<EventLink>? _meetings;

    private LinkLights<EventLink> Meetings => _meetings ??= new LinkLights<EventLink>(_links, LinkTopics.HoveredEvent);

    /// <summary>A row the kit's list drew: its meeting.</summary>
    private void LinkRow(KitItem item, FrameworkElement row)
        => Meetings.Row(row, _source.MeetingOf(item.Id) is { } meeting ? CalendarShare.Key(meeting) : null);

    /// <summary>The kit's list draws its rows again: the old ones are forgotten.</summary>
    private void ForgetRows() => Meetings.Clear();
}
