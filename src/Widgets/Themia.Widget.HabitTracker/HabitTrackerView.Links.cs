using Microsoft.UI.Xaml;
using Themia.Core.Links;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// [S-11924] The tracker's place on its view's links: a daily habit's history dot or heatmap cell is its
/// day (<see cref="HabitPeriods.Day"/>; an hour, a week or a month is not one day and takes no part). The
/// pointer on one publishes that day (<see cref="LinkTopics.HoveredDate"/>); while a day is pointed at
/// anywhere on the view, its dots and cells stay whole and the others go to the links' 0.6.
/// </summary>
internal sealed partial class HabitTrackerView
{
    private readonly WidgetLinks _links = new();

    /// <summary>[S-11924] Every history dot and heatmap cell drawn now, as marks of their day.</summary>
    private LinkLights<DateLink>? _dayMarks;

    private LinkLights<DateLink> DayMarks => _dayMarks ??= new LinkLights<DateLink>(_links, LinkTopics.HoveredDate);

    /// <summary>[S-11924] A dot or a cell of <paramref name="period"/>: a mark of its day when the period is one.</summary>
    private void MarkDay(UIElement mark, HabitBucket period, HabitPeriod cadence)
        => DayMarks.Mark(mark, HabitPeriods.Day(period, cadence) is { } day ? new DateLink(day) : null);

    /// <summary>A dot or a cell taken down.</summary>
    private void ForgetDay(UIElement mark) => DayMarks.Remove(mark);
}
