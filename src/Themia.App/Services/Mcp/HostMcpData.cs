using Microsoft.UI.Dispatching;
using Themia.Core.Habits;
using Themia.Core.Localization;
using Themia.Core.Mcp;
using Themia.Core.Notes;
using Themia.Core.Office;
using Themia.Core.Tasks;
using Themia.Sdk;

namespace Themia.App.Services.Mcp;

/// <summary>
/// [S-9969] What the assistants' server reads of the running app: every read made on the UI thread
/// (the only thread the state and the pools are read on), from what the host already holds. The
/// server's threads only wait for the answer.
/// </summary>
internal sealed class HostMcpData(DispatcherQueue dispatcher, AppHost host) : IMcpData
{
    public Task<McpDesk> DeskAsync(CancellationToken cancellation) => OnUi(() =>
    {
        var screens = host.Displays
            .Select(d => new McpScreen(d.Id, d.ShortName, d.Bounds.Width, d.Bounds.Height, d.IsPrimary))
            .ToList();
        return McpDesk.Of(host.State, screens);
    }, cancellation);

    public Task<IReadOnlyList<NoteMeta>> NotesAsync(CancellationToken cancellation)
        => OnUi(() => AppServices.Notes is { } notes ? (IReadOnlyList<NoteMeta>)[.. notes.Notes] : [], cancellation);

    public Task<McpNote?> NoteAsync(string id, CancellationToken cancellation) => OnUi(() =>
    {
        if (AppServices.Notes is not { } notes || notes.Find(id) is not { } meta)
        {
            return null;
        }

        var read = notes.Read(id);
        return new McpNote(meta, read.Status, read.Text ?? string.Empty);
    }, cancellation);

    public Task<IReadOnlyList<McpTaskList>> TasksAsync(CancellationToken cancellation) => OnUi(() =>
    {
        var lists = new List<McpTaskList>();
        if (AppServices.Tasks is not { } tasks)
        {
            return (IReadOnlyList<McpTaskList>)lists;
        }

        var thisPc = Str.Mcp_ThisPc.Text();
        foreach (var local in tasks.Local.Lists)
        {
            lists.Add(new McpTaskList(thisPc, local.Name, [.. local.Tasks.Select(t => new McpTask(t.Title, t.Done, null))], null));
        }

        // [S-9974] An account's lists only as a widget holds them now: the server never starts a sync.
        foreach (var feed in tasks.HeldFeeds.Where(f => f.Loaded))
        {
            var account = tasks.Find(feed.AccountId)?.Label ?? feed.AccountId;
            var name = feed.IsToday
                ? Str.Common_Today.Text()
                : feed.Lists?.FirstOrDefault(l => l.Id == feed.ListId)?.Name ?? feed.ListId;
            lists.Add(new McpTaskList(account, name, [.. feed.Tasks.Select(t => new McpTask(t.DisplayTitle, t.Completed, t.DueRaw))], feed.FetchedAt));
        }

        return lists;
    }, cancellation);

    public Task<IReadOnlyList<HabitList>> HabitsAsync(CancellationToken cancellation)
        => OnUi(() => AppServices.Habits is { } habits ? (IReadOnlyList<HabitList>)[.. habits.Lists] : [], cancellation);

    public async Task<IReadOnlyList<McpCalendar>> CalendarAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellation)
    {
        // The source is the UI thread's: started there, awaited there, the answer handed back.
        var reads = await OnUi(() =>
        {
            if (Office.OfficeSources.Current is not { } office)
            {
                return new List<(string Account, Task<Office.Synced<IReadOnlyList<CalendarEvent>>> Read)>();
            }

            return office.Accounts(HostFeatures.Calendar)
                .Where(account => !account.NeedsSignIn)
                .Select(account => (Account: account.Label, Read: office.EventsAsync(new CalendarTarget(account.Id, null), from, to, force: false, cancellation)))
                .ToList();
        }, cancellation).ConfigureAwait(false);

        var calendars = new List<McpCalendar>(reads.Count);
        foreach (var (account, read) in reads)
        {
            var synced = await read.ConfigureAwait(false);
            calendars.Add(new McpCalendar(account, synced.Value ?? [],
                synced.Problem == SyncProblem.None ? null : synced.Problem.ToString(), synced.At));
        }

        return calendars;
    }

    /// <summary>Run <paramref name="read"/> on the UI thread and hand its answer (or its exception) back.</summary>
    private Task<T> OnUi<T>(Func<T> read, CancellationToken cancellation)
    {
        var answer = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var registration = cancellation.Register(() => answer.TrySetCanceled(cancellation));
        if (!dispatcher.TryEnqueue(() =>
        {
            try
            {
                answer.TrySetResult(read());
            }
            catch (Exception ex)
            {
                // A callback on the UI thread: nothing escapes; the tool says it could not read.
                answer.TrySetException(ex);
            }
            finally
            {
                registration.Dispose();
            }
        }))
        {
            registration.Dispose();
            answer.TrySetException(new InvalidOperationException("the UI thread is shutting down"));
        }

        return answer.Task;
    }
}
