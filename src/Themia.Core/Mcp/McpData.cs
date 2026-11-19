using Themia.Core.Habits;
using Themia.Core.Model;
using Themia.Core.Notes;
using Themia.Core.Office;
using Themia.Sdk;

namespace Themia.Core.Mcp;

/// <summary>[S-9970] One display as the host knows it: its key, its name and its size in physical pixels.</summary>
public sealed record McpScreen(string Id, string Name, int WidthPx, int HeightPx, bool Primary);

/// <summary>[S-9970] A display as an assistant reads it: numbered from 1, never its device path.</summary>
public sealed record McpDisplay(int Number, string Name, int WidthPx, int HeightPx, bool Primary, bool Enabled, string? ViewId);

/// <summary>[S-9971] One widget on a view: its kind, its label and where it is, as fractions of its display.</summary>
public sealed record McpWidget(string Id, string Kind, string Label, double X, double Y, double W, double H);

/// <summary>[S-9971] A view and its widgets.</summary>
public sealed record McpView(string Id, string Name, bool Active, IReadOnlyList<McpWidget> Widgets);

/// <summary>[S-9970] [S-9971] The desk at one moment: the displays and the views, read on the UI thread.</summary>
public sealed record McpDesk(IReadOnlyList<McpDisplay> Displays, IReadOnlyList<McpView> Views)
{
    /// <summary>
    /// [S-9970] [S-9971] The desk as <paramref name="state"/> holds it, without changing it (a
    /// dangling display assignment is read as the active view, never repaired from here). A widget's
    /// label is the one the user gave it, else its kind's name.
    /// </summary>
    public static McpDesk Of(AppState state, IReadOnlyList<McpScreen> screens)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(screens);

        var active = state.FindView(state.ActiveViewId) ?? state.Views.FirstOrDefault();
        var displays = new List<McpDisplay>(screens.Count);
        for (var i = 0; i < screens.Count; i++)
        {
            var screen = screens[i];
            var shown = state.MonitorViews.TryGetValue(screen.Id, out var viewId) && state.FindView(viewId) is { } assigned
                ? assigned
                : active;
            displays.Add(new McpDisplay(i + 1, screen.Name, screen.WidthPx, screen.HeightPx, screen.Primary,
                !state.DisabledMonitors.Contains(screen.Id), shown?.Id));
        }

        var views = state.Views
            .Select(view => new McpView(view.Id, view.Name, ReferenceEquals(view, active), [.. view.Widgets.Select(Widget)]))
            .ToList();

        return new McpDesk(displays, views);
    }

    private static McpWidget Widget(WidgetInstance widget)
    {
        var kind = widget.Config.Kind;
        var label = !string.IsNullOrWhiteSpace(widget.Label)
            ? widget.Label
            : ModuleCatalog.TryGet(kind, out var descriptor) ? descriptor.Label.Text() : kind.Value ?? string.Empty;
        return new McpWidget(widget.Id, kind.Value ?? string.Empty, label, widget.X, widget.Y, widget.W, widget.H);
    }
}

/// <summary>[S-9973] A note's body as the Notes widget would show it.</summary>
public sealed record McpNote(NoteMeta Meta, NoteReadStatus Status, string Text);

/// <summary>[S-9974] One task.</summary>
public sealed record McpTask(string Title, bool Done, string? Due);

/// <summary>[S-9974] A task list: kept on this PC, or an account's that a widget holds now.</summary>
/// <param name="Source">"This PC", or the account's label.</param>
public sealed record McpTaskList(string Source, string Name, IReadOnlyList<McpTask> Tasks, DateTimeOffset? FetchedAt);

/// <summary>[S-9976] One account's default calendar over the window asked for, as its last sync holds it.</summary>
/// <param name="Problem">Why the last sync failed, in a word ("offline"), or null.</param>
public sealed record McpCalendar(string Account, IReadOnlyList<CalendarEvent> Events, string? Problem, DateTimeOffset? FetchedAt);

/// <summary>
/// [S-9969] What the server reads of the host. Each call is answered on the host's UI thread from
/// what it already holds; nothing here writes, and nothing returns a token, an address book, a mail
/// or a file outside what a widget shows.
/// </summary>
public interface IMcpData
{
    Task<McpDesk> DeskAsync(CancellationToken cancellation);

    Task<IReadOnlyList<NoteMeta>> NotesAsync(CancellationToken cancellation);

    /// <summary>A note's body; null for an id there is no note of.</summary>
    Task<McpNote?> NoteAsync(string id, CancellationToken cancellation);

    Task<IReadOnlyList<McpTaskList>> TasksAsync(CancellationToken cancellation);

    Task<IReadOnlyList<HabitList>> HabitsAsync(CancellationToken cancellation);

    /// <summary>Events overlapping [<paramref name="from"/>, <paramref name="to"/>) of every calendar account's default calendar.</summary>
    Task<IReadOnlyList<McpCalendar>> CalendarAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellation);
}
