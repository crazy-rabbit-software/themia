using System.Text.Json.Serialization;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.Core.Model;

/// <summary>[S-6074] The planner's rows that depend on another setting.</summary>
public enum WeekPlannerSetting
{
    /// <summary>The task account: only while the tasks show.</summary>
    TaskAccount,
}

/// <summary>
/// [S-5320] The week planner: the days ahead with their events, and under today the tasks due.
/// <para>
/// A calendar widget first ([S-561]): its accounts, calendars, colours and birthdays are every
/// calendar's, so it shares their settings builder, their data seam and their sync interest. The
/// tasks are the Tasks widget's Today view ([S-596]) of one account ([S-5324]), read through the
/// shared task service, never a second fetch.
/// </para>
/// </summary>
public sealed class WeekPlannerConfig : CalendarConfig, IShownSettings<WeekPlannerSetting>
{
    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.WeekPlanner;

    /// <summary>[S-6902] This widget's descriptor: what the host knows of it without its UI.</summary>
    public static WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("weekPlanner"),
        Kind = WidgetKind.WeekPlanner,
        ConfigType = typeof(WeekPlannerConfig),
        CreateDefault = () => new WeekPlannerConfig(),
        Json = WidgetConfigJsonContext.Default,
        Keywords = ["planner", "week", "schedule", "agenda", "todo", "to do", "tasks", "events", "calendar", "plan"],
        ProviderNames = ["Microsoft", "Google", "CalDAV"],
        LabelId = Str.Planner_Label,
        DescriptionId = Str.Planner_Desc,
        Glyph = Glyphs.WeekPlanner,
        DefaultWidth = 480,
        DefaultHeight = 280,
        Traits = WidgetTraits.HasItems,
    };

    /// <summary>Days side by side, the classic week view's shape; never read to pick a range ([S-5321]).</summary>
    [JsonIgnore]
    public override CalendarStyle Layout => CalendarStyle.Week;

    /// <summary>[S-5324] List the tasks due today (and overdue) under today's events.</summary>
    public bool ShowTasks { get; set; } = true;

    /// <summary>[S-5324] The task account; empty for the first account with tasks, as the Tasks widget's [S-590].</summary>
    public string TaskAccountId { get; set; } = string.Empty;

    /// <summary>[S-6074] Which account the tasks come from matters only while they show.</summary>
    [JsonIgnore]
    public IReadOnlyList<WeekPlannerSetting> ShownSettings => ShowTasks ? [WeekPlannerSetting.TaskAccount] : [];

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
