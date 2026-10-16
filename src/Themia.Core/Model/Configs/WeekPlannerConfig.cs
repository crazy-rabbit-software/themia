using System.Text.Json.Serialization;

namespace Themia.Core.Model;

/// <summary>
/// [S-5320] The week planner: the days ahead with their events, and under today the tasks due.
/// <para>
/// A calendar widget first ([S-561]): its accounts, calendars, colours and birthdays are every
/// calendar's, so it shares their settings builder, their data seam and their sync interest. The
/// tasks are the Tasks widget's Today view ([S-596]) of one account ([S-5324]), read through the
/// shared task service, never a second fetch.
/// </para>
/// </summary>
public sealed class WeekPlannerConfig : CalendarConfig
{
    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.WeekPlanner;

    /// <summary>Days side by side, the classic week view's shape; never read to pick a range ([S-5321]).</summary>
    [JsonIgnore]
    public override CalendarStyle Layout => CalendarStyle.Week;

    /// <summary>[S-5324] List the tasks due today (and overdue) under today's events.</summary>
    public bool ShowTasks { get; set; } = true;

    /// <summary>[S-5324] The task account; empty for the first account with tasks, as the Tasks widget's [S-590].</summary>
    public string TaskAccountId { get; set; } = string.Empty;

    protected override void RepairOwn(List<string> repairs)
    {
        // System.Text.Json writes a JSON null straight through a non-nullable string property.
        if (TaskAccountId is null)
        {
            TaskAccountId = string.Empty;
            repairs.Add("week planner had no task account; the first account with tasks");
        }
    }

    public override WidgetConfig Clone()
    {
        var copy = CopySharedTo(new WeekPlannerConfig());
        copy.ShowTasks = ShowTasks;
        copy.TaskAccountId = TaskAccountId;
        return copy;
    }
}
