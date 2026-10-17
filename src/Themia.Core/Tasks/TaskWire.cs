using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Themia.Core.Accounts;
using Themia.Sdk;

namespace Themia.Core.Tasks;

/// <summary>Graph <c>todoTaskList</c>, just what the widget reads.</summary>
public sealed class GraphTodoList
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("wellknownListName")]
    public string? WellKnownListName { get; set; }
}

/// <summary>Graph <c>dateTimeTimeZone</c>.</summary>
public sealed class GraphDateTimeZone
{
    [JsonPropertyName("dateTime")]
    public string? DateTime { get; set; }

    [JsonPropertyName("timeZone")]
    public string? TimeZone { get; set; }
}

/// <summary>Graph <c>todoTask</c>.</summary>
public sealed class GraphTodoTask
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    /// <summary><c>notStarted</c>, <c>inProgress</c>, <c>completed</c>, <c>waitingOnOthers</c>, <c>deferred</c>.</summary>
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    /// <summary><c>low</c>, <c>normal</c>, <c>high</c>.</summary>
    [JsonPropertyName("importance")]
    public string? Importance { get; set; }

    [JsonPropertyName("dueDateTime")]
    public GraphDateTimeZone? DueDateTime { get; set; }

    /// <summary>[S-612] When it was finished; To Do may write only the day (midnight).</summary>
    [JsonPropertyName("completedDateTime")]
    public GraphDateTimeZone? CompletedDateTime { get; set; }
}

/// <summary>Google Tasks <c>TaskList</c>.</summary>
public sealed class GoogleTaskList
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }
}

/// <summary>Google Tasks <c>Task</c>.</summary>
public sealed class GoogleTask
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    /// <summary><c>needsAction</c> or <c>completed</c>.</summary>
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    /// <summary>RFC 3339; documented as date-only (the time part is meaningless).</summary>
    [JsonPropertyName("due")]
    public string? Due { get; set; }

    /// <summary>[S-612] RFC 3339: when it was finished.</summary>
    [JsonPropertyName("completed")]
    public string? CompletedAt { get; set; }

    /// <summary>A task hidden by "clear completed" in Google's own apps.</summary>
    [JsonPropertyName("hidden")]
    public bool? Hidden { get; set; }

    /// <summary>A task deleted but still listed when deleted tasks are asked for.</summary>
    [JsonPropertyName("deleted")]
    public bool? Deleted { get; set; }
}

/// <summary>Source-generated contracts for the task endpoints. Native AOT has no reflection serializer.</summary>
[JsonSourceGenerationOptions(NumberHandling = JsonNumberHandling.AllowReadingFromString)]
[JsonSerializable(typeof(GraphTodoList))]
[JsonSerializable(typeof(GraphTodoTask))]
[JsonSerializable(typeof(GoogleTaskList))]
[JsonSerializable(typeof(GoogleTask))]
public sealed partial class TaskWireContext : JsonSerializerContext;

/// <summary>
/// [S-591] Microsoft To Do (Graph) and Google Tasks, reduced to one shape, and every request the
/// widget makes of either. Pure: addresses, bodies and the mapping are tested here; the account
/// service makes the calls.
/// <para>
/// Ids go into the path escaped. Both providers hand out opaque base64-ish strings, and an id is
/// untrusted provider data like any other: one carrying a <c>/</c> must not become a second path
/// segment.
/// </para>
/// <para>
/// Bodies are written with <see cref="Utf8JsonWriter"/>, never by string interpolation: a title is
/// typed by the user and may hold a quote.
/// </para>
/// </summary>
public static class TaskWire
{
    public static readonly Uri GraphRoot = new("https://graph.microsoft.com/v1.0/");

    public static readonly Uri GoogleRoot = new("https://tasks.googleapis.com/tasks/v1/");

    /// <summary>Most tasks one list is read to; past this the list is long enough.</summary>
    public const int MaxTasksPerList = 400;

    /// <summary>Whether this provider holds tasks at all.</summary>
    public static bool Supports(ProviderId provider) => provider == HostProviders.Microsoft || provider == HostProviders.Google;

    // ---- addresses -----------------------------------------------------------------------------

    public static Uri Lists(ProviderId provider) => provider == HostProviders.Google
        ? new Uri(GoogleRoot, "users/@me/lists?maxResults=100")
        : new Uri(GraphRoot, "me/todo/lists?$top=100");

    /// <summary>
    /// A list's tasks. Google is asked to leave completed tasks out when they will not be shown;
    /// Graph is filtered here instead ([S-591]), as the classic app did, so both behave the same.
    /// </summary>
    public static Uri Tasks(ProviderId provider, string listId, bool showCompleted) => provider == HostProviders.Google
        ? new Uri(GoogleRoot, $"lists/{Escape(listId)}/tasks?maxResults=100&showCompleted={(showCompleted ? "true" : "false")}&showHidden=false")
        : new Uri(GraphRoot, $"me/todo/lists/{Escape(listId)}/tasks?$top=100");

    /// <summary>
    /// [S-2686] The start of a Graph list's delta round: every task of the list, finished or not
    /// (the round cannot be filtered), filtered when merged. Later rounds follow Graph's delta link.
    /// </summary>
    public static Uri GraphTasksDelta(string listId)
        => new(GraphRoot, $"me/todo/lists/{Escape(listId)}/tasks/delta?$select=id,title,status,importance,dueDateTime,completedDateTime");

    /// <summary>
    /// [S-2686] Google Tasks has no sync token: the tasks of a list changed since an instant, with the
    /// finished, hidden and deleted ones, so a task that left the list says so.
    /// </summary>
    public static Uri GoogleTasksSince(string listId, DateTimeOffset since)
        => new(GoogleRoot, $"lists/{Escape(listId)}/tasks?maxResults=100&showCompleted=true&showHidden=true&showDeleted=true&updatedMin="
            + Uri.EscapeDataString(since.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", System.Globalization.CultureInfo.InvariantCulture)));

    /// <summary>One task, for PATCH and DELETE.</summary>
    public static Uri Task(ProviderId provider, string listId, string taskId) => provider == HostProviders.Google
        ? new Uri(GoogleRoot, $"lists/{Escape(listId)}/tasks/{Escape(taskId)}")
        : new Uri(GraphRoot, $"me/todo/lists/{Escape(listId)}/tasks/{Escape(taskId)}");

    /// <summary>[S-614] One list, for a rename (PATCH).</summary>
    public static Uri List(ProviderId provider, string listId) => provider == HostProviders.Google
        ? new Uri(GoogleRoot, $"users/@me/lists/{Escape(listId)}")
        : new Uri(GraphRoot, $"me/todo/lists/{Escape(listId)}");

    /// <summary>Where a new task is POSTed.</summary>
    public static Uri Create(ProviderId provider, string listId) => provider == HostProviders.Google
        ? new Uri(GoogleRoot, $"lists/{Escape(listId)}/tasks")
        : new Uri(GraphRoot, $"me/todo/lists/{Escape(listId)}/tasks");

    // ---- reading -------------------------------------------------------------------------------

    /// <summary>A Graph list, or null when it has no id.</summary>
    public static TaskListInfo? FromGraph(GraphTodoList list)
    {
        ArgumentNullException.ThrowIfNull(list);
        return string.IsNullOrEmpty(list.Id)
            ? null
            : new TaskListInfo(
                list.Id,
                list.DisplayName ?? string.Empty,
                list.WellKnownListName == "defaultList",
                BuiltIn: list.WellKnownListName is { Length: > 0 } known && known != "none");
    }

    /// <summary>Google's lists: the first is its <c>@default</c>, so it is the default here.</summary>
    public static IReadOnlyList<TaskListInfo> FromGoogle(IEnumerable<GoogleTaskList> lists)
    {
        ArgumentNullException.ThrowIfNull(lists);

        var result = new List<TaskListInfo>();
        foreach (var list in lists)
        {
            if (!string.IsNullOrEmpty(list.Id))
            {
                result.Add(new TaskListInfo(list.Id, list.Title ?? string.Empty, result.Count == 0));
            }
        }

        return result;
    }

    /// <summary>
    /// A Graph task, or null when it has no id. Every status but <c>completed</c> is open work —
    /// "in progress" and "waiting on others" are not done.
    /// </summary>
    public static TaskItem? FromGraph(GraphTodoTask task, string listId)
    {
        ArgumentNullException.ThrowIfNull(task);

        return string.IsNullOrEmpty(task.Id)
            ? null
            : new TaskItem(
                task.Id,
                listId,
                task.Title ?? string.Empty,
                string.Equals(task.Status, "completed", StringComparison.OrdinalIgnoreCase),
                string.Equals(task.Importance, "high", StringComparison.OrdinalIgnoreCase),
                task.DueDateTime?.DateTime,
                Finished(string.Equals(task.Status, "completed", StringComparison.OrdinalIgnoreCase), task.CompletedDateTime?.DateTime));
    }

    /// <summary>A Google task, or null when it has no id or Google calls it hidden or deleted.</summary>
    public static TaskItem? FromGoogle(GoogleTask task, string listId)
    {
        ArgumentNullException.ThrowIfNull(task);

        return string.IsNullOrEmpty(task.Id) || task.Hidden == true || task.Deleted == true
            ? null
            : new TaskItem(
                task.Id,
                listId,
                task.Title ?? string.Empty,
                string.Equals(task.Status, "completed", StringComparison.OrdinalIgnoreCase),
                false,
                task.Due,
                Finished(string.Equals(task.Status, "completed", StringComparison.OrdinalIgnoreCase), task.CompletedAt));
    }

    /// <summary>[S-612] A finished task's completion, parsed here at the boundary; an open task has none, whatever was sent.</summary>
    private static TaskStamp? Finished(bool completed, string? raw) => completed ? TaskStamp.Parse(raw) : null;

    // ---- writing -------------------------------------------------------------------------------

    /// <summary>
    /// [S-597] Complete or reopen. Google also needs the completion stamp cleared, or it keeps the
    /// task completed.
    /// </summary>
    public static string CompleteBody(ProviderId provider, bool completed) => Json(writer =>
    {
        if (provider == HostProviders.Google)
        {
            writer.WriteString("status", completed ? "completed" : "needsAction");
            if (!completed)
            {
                writer.WriteNull("completed");
            }
        }
        else
        {
            writer.WriteString("status", completed ? "completed" : "notStarted");
        }
    });

    /// <summary>[S-614] A list rename: the name alone (Graph <c>displayName</c>, Google <c>title</c>).</summary>
    public static string RenameListBody(ProviderId provider, string name)
        => Json(writer => writer.WriteString(provider == HostProviders.Google ? "title" : "displayName", name));

    /// <summary>[S-600] A rename: the title alone, so it can never clear another field by omitting it.</summary>
    public static string RenameBody(string title) => Json(writer => writer.WriteString("title", title));

    /// <summary>
    /// [S-596] A new task, optionally due on a day. Midnight of the day is what both providers write
    /// for a date-only due, and what <see cref="TaskDue.Day"/> reads back verbatim ([S-593]).
    /// </summary>
    public static string CreateBody(ProviderId provider, string title, DateOnly? due) => Json(writer =>
    {
        writer.WriteString("title", title);

        if (due is not { } day)
        {
            return;
        }

        if (provider == HostProviders.Google)
        {
            writer.WriteString("due", $"{TaskDue.Wire(day)}T00:00:00.000Z");
        }
        else
        {
            writer.WriteStartObject("dueDateTime");
            writer.WriteString("dateTime", $"{TaskDue.Wire(day)}T00:00:00.0000000");
            writer.WriteString("timeZone", "UTC");
            writer.WriteEndObject();
        }
    });

    /// <summary>Read the task a create answered with, through the provider's contract. Null when it is not one.</summary>
    public static TaskItem? ParseCreated(ProviderId provider, string body, string listId)
    {
        try
        {
            return provider == HostProviders.Google
                ? JsonSerializer.Deserialize(body, TaskWireContext.Default.GoogleTask) is { } google ? FromGoogle(google, listId) : null
                : JsonSerializer.Deserialize(body, TaskWireContext.Default.GraphTodoTask) is { } graph ? FromGraph(graph, listId) : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string Escape(string id) => Uri.EscapeDataString(id);

    private static string Json(Action<Utf8JsonWriter> body)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            body(writer);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }
}
