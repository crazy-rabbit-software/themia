using Microsoft.UI.Xaml;
using Themia.Core.Links;
using Themia.Core.Tasks;
using Themia.Sdk.Kit;

namespace Themia.App.Widgets;

/// <summary>
/// [S-11925] Tasks' place on its view's links: a task is its due day (<see cref="TaskDue.DayLink"/>; a task
/// without one takes no part). The pointer on a row publishes that day (<see cref="LinkTopics.HoveredDate"/>);
/// while a day is pointed at anywhere on the view, the rows due that day take the item layer's hover fill.
/// </summary>
internal sealed partial class TasksView
{
    private readonly WidgetLinks _links = new();

    /// <summary>[S-11925] The rows drawn now, by their due day.</summary>
    private LinkLights<DateLink>? _dueDays;

    private LinkLights<DateLink> DueDays => _dueDays ??= new LinkLights<DateLink>(_links, LinkTopics.HoveredDate);

    /// <summary>[S-11925] A row the kit's list drew: its task's due day.</summary>
    private void LinkRow(KitItem item, FrameworkElement row)
    {
        if (TaskOf(item) is { } task)
        {
            DueDays.Row(row, TaskDue.DayLink(task.DueRaw, TimeZoneInfo.Local));
        }
    }
}
