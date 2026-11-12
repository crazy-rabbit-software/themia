using System.Globalization;
using System.Text;
using System.Text.Json;
using Themia.Core.Diagnostics;

namespace Themia.Core.Mcp;

/// <summary>[S-9967] One tool as <c>tools/list</c> names it: its schema is a JSON Schema object, as text.</summary>
public sealed record McpToolInfo(string Name, string Title, string Description, string InputSchema);

/// <summary>[S-9968] What a tool call gave: JSON text for the model, or why it could not (<see cref="IsError"/>).</summary>
public sealed record McpToolResult(string Text, bool IsError);

/// <summary>A call's arguments were not what the tool's schema says: a JSON-RPC invalid-params error.</summary>
public sealed class McpArgumentException(string message) : Exception(message);

/// <summary>
/// [S-9967] [S-9968] The tools: read-only, each over <see cref="IMcpData"/>, each answering one JSON
/// object as text. No tool writes, opens, signs in or reaches the network on its own.
/// </summary>
public sealed class McpTools(IMcpData data, Func<DateTimeOffset> now)
{
    private const string Scope = "mcp";

    /// <summary>[S-9973] The longest note body returned; a longer one is cut and says so.</summary>
    public const int MaxNoteChars = 32 * 1024;

    /// <summary>[S-9974] [S-9976] The most rows of one list or calendar returned.</summary>
    public const int MaxRows = 500;

    /// <summary>[S-9975] The most recent period keys returned of one habit.</summary>
    public const int MaxHabitLog = 120;

    /// <summary>[S-9976] The longest calendar window, in days.</summary>
    public const int MaxDays = 31;

    private const string NoArguments = """{"type":"object","properties":{},"additionalProperties":false}""";

    /// <summary>[S-9967] Every tool, in the order <c>tools/list</c> gives them.</summary>
    public static IReadOnlyList<McpToolInfo> All { get; } =
    [
        new("list_displays", "List displays",
            "The displays Themia draws on: number, name, size in pixels, whether Themia is on there, and the id of the view each shows.",
            NoArguments),
        new("list_views", "List views",
            "Themia's views (desktop layouts): id, name, whether it is the active one, and how many widgets it holds.",
            NoArguments),
        new("list_widgets", "List widgets",
            "The widgets on one view (or on every view): id, kind, label, position and size as fractions of the display (0..1), and the group a widget is in; each view lists its groups (id, name).",
            """{"type":"object","properties":{"view_id":{"type":"string","description":"A view id from list_views; leave out for every view."}},"additionalProperties":false}"""),
        new("list_notes", "List notes",
            "The notes kept in Themia's Notes widgets: id, title and when each was last changed.",
            NoArguments),
        new("read_note", "Read a note",
            "One note's text (Markdown), by its id from list_notes.",
            """{"type":"object","properties":{"note_id":{"type":"string","description":"A note id from list_notes."}},"required":["note_id"],"additionalProperties":false}"""),
        new("read_tasks", "Read tasks",
            "Task lists: those kept on this PC, and the account lists a Tasks widget shows now, as last synced.",
            NoArguments),
        new("read_habits", "Read habits",
            "The habit lists of the Habit tracker: each habit's name and the periods it was done in, most recent first.",
            NoArguments),
        new("read_calendar", "Read calendar",
            "Events of each signed-in calendar account's default calendar from today on, as Themia last synced them.",
            """{"type":"object","properties":{"days":{"type":"integer","minimum":1,"maximum":31,"description":"How many days from today (default 7)."}},"additionalProperties":false}"""),
    ];

    /// <summary>
    /// [S-9968] Call a tool. An unknown tool or arguments outside its schema throw
    /// <see cref="McpArgumentException"/>; a read that failed is a result with <see cref="McpToolResult.IsError"/>.
    /// </summary>
    public async Task<McpToolResult> CallAsync(string name, JsonElement? arguments, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(name);
        var args = Arguments(arguments);

        try
        {
            switch (name)
            {
                case "list_displays":
                    NoneBut(args);
                    return Json(DisplaysJson(await data.DeskAsync(cancellation).ConfigureAwait(false)));
                case "list_views":
                    NoneBut(args);
                    return Json(ViewsJson(await data.DeskAsync(cancellation).ConfigureAwait(false)));
                case "list_widgets":
                    NoneBut(args, "view_id");
                    return WidgetsJson(await data.DeskAsync(cancellation).ConfigureAwait(false), OptionalString(args, "view_id"));
                case "list_notes":
                    NoneBut(args);
                    return Json(NotesJson(await data.NotesAsync(cancellation).ConfigureAwait(false)));
                case "read_note":
                    NoneBut(args, "note_id");
                    var id = OptionalString(args, "note_id") ?? throw new McpArgumentException("note_id is required.");
                    return NoteJson(await data.NoteAsync(id, cancellation).ConfigureAwait(false));
                case "read_tasks":
                    NoneBut(args);
                    return Json(TasksJson(await data.TasksAsync(cancellation).ConfigureAwait(false)));
                case "read_habits":
                    NoneBut(args);
                    return Json(HabitsJson(await data.HabitsAsync(cancellation).ConfigureAwait(false)));
                case "read_calendar":
                    NoneBut(args, "days");
                    var days = OptionalInt(args, "days") ?? 7;
                    if (days is < 1 or > MaxDays)
                    {
                        throw new McpArgumentException($"days must be 1 to {MaxDays}.");
                    }

                    var today = now();
                    var from = new DateTimeOffset(today.Date, today.Offset);
                    return Json(CalendarJson(await data.CalendarAsync(from, from.AddDays(days), cancellation).ConfigureAwait(false), from, days));
                default:
                    throw new McpArgumentException($"There is no tool named {name}.");
            }
        }
        catch (Exception ex) when (ex is not (McpArgumentException or OperationCanceledException))
        {
            Log.Error(Scope, $"the tool {name} failed", ex);
            return new McpToolResult("Themia could not read that just now.", IsError: true);
        }
    }

    // ---- arguments ------------------------------------------------------------------------------

    private static JsonElement? Arguments(JsonElement? arguments)
    {
        if (arguments is not { } given || given.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return given.ValueKind == JsonValueKind.Object ? given : throw new McpArgumentException("arguments must be an object.");
    }

    private static void NoneBut(JsonElement? args, params string[] known)
    {
        if (args is not { } given)
        {
            return;
        }

        foreach (var property in given.EnumerateObject())
        {
            if (!known.Contains(property.Name, StringComparer.Ordinal))
            {
                throw new McpArgumentException($"Unknown argument {property.Name}.");
            }
        }
    }

    private static string? OptionalString(JsonElement? args, string name)
    {
        if (args is not { } given || !given.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String ? value.GetString() : throw new McpArgumentException($"{name} must be a string.");
    }

    private static int? OptionalInt(JsonElement? args, string name)
    {
        if (args is not { } given || !given.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)
            ? number
            : throw new McpArgumentException($"{name} must be a whole number.");
    }

    // ---- answers --------------------------------------------------------------------------------

    private static McpToolResult Json(byte[] json) => new(Encoding.UTF8.GetString(json), IsError: false);

    private static byte[] DisplaysJson(McpDesk desk) => JsonRpc.Write(w =>
    {
        w.WriteStartArray("displays");
        foreach (var display in desk.Displays)
        {
            w.WriteStartObject();
            w.WriteNumber("number", display.Number);
            w.WriteString("name", display.Name);
            w.WriteNumber("width_px", display.WidthPx);
            w.WriteNumber("height_px", display.HeightPx);
            w.WriteBoolean("primary", display.Primary);
            w.WriteBoolean("themia_on", display.Enabled);
            w.WriteString("view_id", display.ViewId);
            w.WriteEndObject();
        }

        w.WriteEndArray();
    });

    private static byte[] ViewsJson(McpDesk desk) => JsonRpc.Write(w =>
    {
        w.WriteStartArray("views");
        foreach (var view in desk.Views)
        {
            w.WriteStartObject();
            w.WriteString("id", view.Id);
            w.WriteString("name", view.Name);
            w.WriteBoolean("active", view.Active);
            w.WriteNumber("widgets", view.Widgets.Count);
            w.WriteStartArray("shown_on_displays");
            foreach (var display in desk.Displays.Where(d => d.ViewId == view.Id))
            {
                w.WriteNumberValue(display.Number);
            }

            w.WriteEndArray();
            w.WriteEndObject();
        }

        w.WriteEndArray();
    });

    private static McpToolResult WidgetsJson(McpDesk desk, string? viewId)
    {
        var views = viewId is null ? desk.Views : desk.Views.Where(v => v.Id == viewId).ToList();
        if (viewId is not null && views.Count == 0)
        {
            return new McpToolResult($"There is no view with the id {viewId}; list_views gives the ids.", IsError: true);
        }

        return Json(JsonRpc.Write(w =>
        {
            w.WriteStartArray("views");
            foreach (var view in views)
            {
                w.WriteStartObject();
                w.WriteString("id", view.Id);
                w.WriteString("name", view.Name);
                w.WriteStartArray("widgets");
                foreach (var widget in view.Widgets)
                {
                    w.WriteStartObject();
                    w.WriteString("id", widget.Id);
                    w.WriteString("kind", widget.Kind);
                    w.WriteString("label", widget.Label);
                    w.WriteNumber("x", Round(widget.X));
                    w.WriteNumber("y", Round(widget.Y));
                    w.WriteNumber("width", Round(widget.W));
                    w.WriteNumber("height", Round(widget.H));
                    if (widget.Group is { } group)
                    {
                        w.WriteString("group", group);
                    }

                    w.WriteEndObject();
                }

                w.WriteEndArray();

                // [S-2450] The view's groups: a widget's "group" names one of these.
                w.WriteStartArray("groups");
                foreach (var group in view.Groups)
                {
                    w.WriteStartObject();
                    w.WriteString("id", group.Id);
                    if (group.Name is { } name)
                    {
                        w.WriteString("name", name);
                    }

                    w.WriteEndObject();
                }

                w.WriteEndArray();
                w.WriteEndObject();
            }

            w.WriteEndArray();
        }));
    }

    private static byte[] NotesJson(IReadOnlyList<Notes.NoteMeta> notes) => JsonRpc.Write(w =>
    {
        w.WriteStartArray("notes");
        foreach (var note in notes)
        {
            w.WriteStartObject();
            w.WriteString("id", note.Id);
            w.WriteString("title", note.Title);
            w.WriteString("updated", Instant(DateTimeOffset.FromUnixTimeMilliseconds(note.UpdatedAt)));
            w.WriteEndObject();
        }

        w.WriteEndArray();
    });

    private static McpToolResult NoteJson(McpNote? note)
    {
        if (note is null || note.Status == Notes.NoteReadStatus.Missing)
        {
            return new McpToolResult("There is no note with that id; list_notes gives the ids.", IsError: true);
        }

        if (note.Status != Notes.NoteReadStatus.Found)
        {
            return new McpToolResult("That note's file could not be read just now.", IsError: true);
        }

        var cut = note.Text.Length > MaxNoteChars;
        return Json(JsonRpc.Write(w =>
        {
            w.WriteString("id", note.Meta.Id);
            w.WriteString("title", note.Meta.Title);
            w.WriteString("updated", Instant(DateTimeOffset.FromUnixTimeMilliseconds(note.Meta.UpdatedAt)));
            w.WriteString("text", cut ? note.Text[..MaxNoteChars] : note.Text);
            w.WriteBoolean("truncated", cut);
        }));
    }

    private static byte[] TasksJson(IReadOnlyList<McpTaskList> lists) => JsonRpc.Write(w =>
    {
        w.WriteStartArray("lists");
        foreach (var list in lists)
        {
            w.WriteStartObject();
            w.WriteString("source", list.Source);
            w.WriteString("name", list.Name);
            if (list.FetchedAt is { } at)
            {
                w.WriteString("synced", Instant(at));
            }

            w.WriteBoolean("truncated", list.Tasks.Count > MaxRows);
            w.WriteStartArray("tasks");
            foreach (var task in list.Tasks.Take(MaxRows))
            {
                w.WriteStartObject();
                w.WriteString("title", task.Title);
                w.WriteBoolean("done", task.Done);
                if (task.Due is { Length: > 0 } due)
                {
                    w.WriteString("due", due);
                }

                w.WriteEndObject();
            }

            w.WriteEndArray();
            w.WriteEndObject();
        }

        w.WriteEndArray();
    });

    private static byte[] HabitsJson(IReadOnlyList<Habits.HabitList> lists) => JsonRpc.Write(w =>
    {
        w.WriteStartArray("lists");
        foreach (var list in lists)
        {
            w.WriteStartObject();
            w.WriteString("name", list.Name);
            w.WriteStartArray("habits");
            foreach (var habit in list.Habits.Take(MaxRows))
            {
                w.WriteStartObject();
                w.WriteString("name", habit.Name);
                w.WriteStartArray("done_in");
                foreach (var period in habit.Log.OrderDescending(StringComparer.Ordinal).Take(MaxHabitLog))
                {
                    w.WriteStringValue(period);
                }

                w.WriteEndArray();
                w.WriteEndObject();
            }

            w.WriteEndArray();
            w.WriteEndObject();
        }

        w.WriteEndArray();
    });

    private static byte[] CalendarJson(IReadOnlyList<McpCalendar> calendars, DateTimeOffset from, int days) => JsonRpc.Write(w =>
    {
        w.WriteString("from", Day(DateOnly.FromDateTime(from.DateTime)));
        w.WriteNumber("days", days);
        w.WriteStartArray("calendars");
        foreach (var calendar in calendars)
        {
            w.WriteStartObject();
            w.WriteString("account", calendar.Account);
            if (calendar.FetchedAt is { } at)
            {
                w.WriteString("synced", Instant(at));
            }

            if (calendar.Problem is { } problem)
            {
                w.WriteString("problem", problem);
            }

            w.WriteBoolean("truncated", calendar.Events.Count > MaxRows);
            w.WriteStartArray("events");
            foreach (var item in calendar.Events.OrderBy(e => e.Start).Take(MaxRows))
            {
                w.WriteStartObject();
                w.WriteString("subject", item.Subject);
                w.WriteBoolean("all_day", item.AllDay);
                if (item.AllDay)
                {
                    w.WriteString("first_day", Day(item.FirstDay));
                    w.WriteString("end_day_exclusive", Day(item.EndDay));
                }
                else
                {
                    w.WriteString("start", Instant(item.Start));
                    w.WriteString("end", Instant(item.End));
                }

                if (item.Location is { Length: > 0 } location)
                {
                    w.WriteString("location", location);
                }

                if (item.Organizer is { Length: > 0 } organizer)
                {
                    w.WriteString("organizer", organizer);
                }

                if (item.Declined)
                {
                    w.WriteBoolean("declined", true);
                }

                w.WriteEndObject();
            }

            w.WriteEndArray();
            w.WriteEndObject();
        }

        w.WriteEndArray();
    });

    private static double Round(double fraction) => Math.Round(fraction, 4);

    private static string Instant(DateTimeOffset at) => at.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture);

    private static string Day(DateOnly day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
