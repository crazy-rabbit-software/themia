using System.Text.Json.Serialization;
using Themia.Core.Localization;

namespace Themia.Core.Tasks;

/// <summary>
/// [S-590] Where a Tasks widget's list comes from: a Microsoft or Google account, or a list kept on
/// this PC. A closed set, written by name, so a config can never name a third source by accident.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<TaskSource>))]
public enum TaskSource
{
    /// <summary>Microsoft To Do (Graph) or Google Tasks, through one of Themia's own accounts.</summary>
    Account = 0,

    /// <summary>A named list stored in <c>tasks.json</c> beside the app state. No account, no network.</summary>
    Local = 1,
}

/// <summary>[S-602] How a row lays out its due date: under the title, or beside it on one line.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<TaskViewMode>))]
public enum TaskViewMode
{
    List = 0,
    Compact = 1,
}

/// <summary>
/// One task, as both providers and the local store are reduced to it.
/// <para>
/// <see cref="DueRaw"/> stays the provider's own string. Which calendar <i>day</i> it means depends on
/// the viewer's zone for one of the two encodings in the wild ([S-593]), so it is resolved at render
/// time from one clock reading, never frozen at fetch time.
/// </para>
/// </summary>
/// <param name="Id">The provider's id, a local id, or a temporary one for a task still being created.</param>
/// <param name="ListId">
/// The list the task lives in. Under Today the rows come from many lists, so every write is addressed
/// to this rather than to the widget's selection ([S-597]).
/// </param>
/// <param name="Important">Graph's <c>importance: high</c>. Google and local tasks have no such field.</param>
/// <param name="CompletedOn">
/// [S-612] When a finished task was finished (Graph <c>completedDateTime</c>, Google <c>completed</c>),
/// parsed at the boundary; null for an open task or one the provider gave no date for.
/// </param>
public sealed record TaskItem(
    string Id,
    string ListId,
    string Title,
    bool Completed,
    bool Important = false,
    string? DueRaw = null,
    TaskStamp? CompletedOn = null)
{
    /// <summary>What the row shows: an untitled task is named rather than drawn as a gap.</summary>
    public string DisplayTitle => string.IsNullOrWhiteSpace(Title) ? Str.WidgetsC_Tasks_UntitledTask.Text() : Title;
}

/// <summary>A task list on an account.</summary>
/// <param name="IsDefault">
/// Graph's <c>wellknownListName: defaultList</c>; for Google the first list, which is its
/// <c>@default</c>. A widget with no list chosen shows this one.
/// </param>
/// <param name="BuiltIn">
/// [S-614] One of To Do's own lists (Graph <c>wellknownListName</c> other than <c>none</c>: Tasks,
/// Flagged email), which To Do does not let anyone rename. Google has none.
/// </param>
public sealed record TaskListInfo(string Id, string Name, bool IsDefault, bool BuiltIn = false);

/// <summary>[S-611] Which due dates a widget's list keeps. A closed set, written by name.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<TaskDueFilter>))]
public enum TaskDueFilter
{
    /// <summary>Every task, dated or not.</summary>
    Any = 0,

    /// <summary>Open tasks due before today (a finished task is never overdue, [S-594]).</summary>
    Overdue = 1,

    /// <summary>Due today.</summary>
    Today = 2,

    /// <summary>Due in the calendar week that holds today, by the regional week start ([S-564]).</summary>
    ThisWeek = 3,

    /// <summary>No due date.</summary>
    NoDate = 4,
}

/// <summary>
/// [S-595] [S-611] [S-612] What a widget's list keeps of the tasks it has: finished ones or not, and
/// the user's filters. One value, read by the one rule <see cref="TaskRules.Visible"/>.
/// </summary>
/// <param name="FinishedTodayOnly">[S-612] Finished tasks only when they were finished today (Today's view).</param>
public sealed record TaskFilter(
    bool ShowCompleted,
    TaskDueFilter Due = TaskDueFilter.Any,
    bool ImportantOnly = false,
    string? Text = null,
    bool FinishedTodayOnly = false)
{
    /// <summary>The trimmed text filter, or null when there is none.</summary>
    public string? Words => string.IsNullOrWhiteSpace(Text) ? null : Text.Trim();

    /// <summary>Whether a filter narrows the list beyond hiding finished tasks.</summary>
    public bool Narrows => Due != TaskDueFilter.Any || ImportantOnly || Words is not null;
}
