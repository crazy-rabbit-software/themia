using Microsoft.UI.Xaml;
using Themia.Core.Links;
using Themia.Core.Office;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// [S-11924] [S-11927] The planner's place on its view's links: a day's head publishes its day under the
/// pointer (<see cref="LinkTopics.HoveredDate"/>) and takes the item layer's hover fill while that day is
/// pointed at anywhere on the view (a hover never moves the planner); the day selected there
/// (<see cref="LinkTopics.SelectedDate"/>, a calendar's click) is the planner's first day
/// (<see cref="WeekPlannerRules.Start"/>), so a selection pages it.
/// </summary>
internal sealed partial class WeekPlannerView
{
    private readonly WidgetLinks _links = new();

    /// <summary>[S-11924] The days' heads drawn now.</summary>
    private readonly LinkLights<DateLink> _heads;

    /// <summary>[S-11936] The event rows drawn now.</summary>
    private readonly LinkLights<EventLink> _eventRows;

    /// <summary>[S-11927] The day selected on the view, which the planner starts at; null: today.</summary>
    private DateOnly? _selected;

    /// <summary>The planner's first day now.</summary>
    private DateOnly Start(DateOnly today) => WeekPlannerRules.Start(today, _selected);

    private LinkLights<DateLink> FollowLinks()
    {
        _links.Follow(LinkTopics.SelectedDate, value =>
        {
            if (value?.Date == _selected)
            {
                return;
            }

            _selected = value?.Date;
            if (!_detached)
            {
                Render();
                _ = RefreshAsync(force: false);
            }
        });

        return new LinkLights<DateLink>(_links, LinkTopics.HoveredDate);
    }

    /// <summary>
    /// [S-11936] An event's row: the pointer on it publishes the event (<see cref="LinkTopics.HoveredEvent"/>,
    /// <see cref="CalendarShare.Key"/>), and it takes the hover fill while that event is pointed at anywhere on
    /// the view (a calendar's chip, the next meeting, up next's row).
    /// </summary>
    private void LinkEvent(FrameworkElement row, CalendarEvent item) => _eventRows.Row(row, CalendarShare.Key(item));

    /// <summary>[S-11924] A day's head: it publishes its day and takes the hover fill for it.</summary>
    private void LinkHead(FrameworkElement head, DateOnly day) => _heads.Row(head, new DateLink(day));
}
