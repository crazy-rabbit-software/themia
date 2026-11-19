using Microsoft.UI.Xaml;
using Themia.Core.Links;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// [S-11924] [S-11965] The tracker's place on its view's links: a cell of the list's one heatmap is its day
/// (<see cref="HabitPeriods.Day"/>, under a daily cadence; an hour, a week or a month is not one day and takes no part).
/// The pointer on one publishes that day (<see cref="LinkTopics.HoveredDate"/>) and keeps that cell whole, the others at
/// the links' 0.6; while a day is pointed at elsewhere on the view, its cell stays whole the same way. A habit's dot or
/// a cell of a habit's own grid is that habit's day, one of several on the same day, and takes no part.
/// </summary>
internal sealed partial class HabitTrackerView
{
    private readonly WidgetLinks _links = new();

    /// <summary>[S-11924] The list's heatmap cells drawn now, as marks of their day.</summary>
    private LinkLights<DateLink>? _dayMarks;

    private LinkLights<DateLink> DayMarks => _dayMarks ??= new LinkLights<DateLink>(_links, LinkTopics.HoveredDate);

    /// <summary>
    /// [S-11924] [S-11965] A heatmap cell of <paramref name="period"/>: a mark of its day when the period is one and the
    /// grid is the list's (<paramref name="perHabit"/>: a habit's own grid, whose cells take no part).
    /// </summary>
    private void MarkDay(UIElement mark, HabitBucket period, HabitPeriod cadence, bool perHabit)
    {
        if (perHabit)
        {
            ForgetDay(mark);
            return;
        }

        DayMarks.Mark(mark, HabitPeriods.Day(period, cadence) is { } day ? new DateLink(day) : null);
    }

    /// <summary>A cell taken down.</summary>
    private void ForgetDay(UIElement mark) => DayMarks.Remove(mark);
}
