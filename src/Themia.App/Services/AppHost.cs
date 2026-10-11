using Microsoft.UI.Dispatching;
using Themia.App.Interop;
using Themia.App.Shell;
using Themia.App.Shell.Config;
using Themia.Core.Diagnostics;
using Themia.Core.Model;
using Themia.Core.Storage;

namespace Themia.App.Services;

/// <summary>
/// Owns everything with a lifetime: the persisted state, the widget windows, the shared ticker and
/// the hidden command window. There is exactly one, created on the UI thread, and it is the only writer of
/// <see cref="State"/> — widgets and windows mutate through the methods here so every change goes
/// through one save path.
/// </summary>
internal sealed partial class AppHost : IDisposable
{
    private const string Scope = "host";

    private readonly DispatcherQueue _dispatcher;
    private readonly AppStateStore _store;
    private readonly List<DesktopWindow> _windows = [];
    private readonly CommandWindow _command = new();

    /// <summary>[S-458] The screen-time tracker, from <see cref="Start"/> to <see cref="Dispose"/>.</summary>
    private ScreenTimeService? _screenTime;

    private ConfigWindow? _config;
    private readonly Mail.MailService _mail;
    private readonly Office.AccountOfficeSource _office;
    private readonly SystemData.SystemHub _system;
    private bool _editMode;
    private bool _disposed;

    public AppHost(DispatcherQueue dispatcher, AppStateStore store, AppState state)
    {
        _dispatcher = dispatcher;
        _store = store;
        State = state;
        Ticker = new Ticker(dispatcher);

        // [S-2500] One hub for every system widget, for the app's lifetime, on the shared ticker.
        _system = new SystemData.SystemHub(Ticker, dispatcher);
        AppServices.System = _system;

        // [S-1109] The language before anything with text is built, and before the surfaces subscribe.
        StartLanguage();

        // [S-820] The appearance engine runs before anything is built from a brush, and first on
        // every state change — subscribed before any surface — so the surfaces relayout against the
        // new theme, accent, font and text size. A Windows theme or accent change re-runs the same
        // path, which is how "use system setting" and the Windows accent follow Windows live.
        Shell.DesignApplier.Apply(state.Design);
        StateChanged += () => Shell.DesignApplier.Apply(State.Design);
        Shell.DesignApplier.Watch(dispatcher, () => StateChanged?.Invoke());

        // Beside the state file, so a portable or test state path takes its accounts with it.
        Accounts = new Accounts.AccountService(dispatcher, Themia.Core.Accounts.AccountStore.PathBeside(store.Path));
        AppServices.Accounts = Accounts;

        // [S-2600] The offline store every sync service reads and writes, and what every view's
        // widgets need from it ([S-2608]). A detached state (a debug render) gets a detached store.
        Sync = Services.Sync.SyncHost.Open(dispatcher, store.Persists ? Services.Sync.SyncHost.DirectoryFor(store.Path) : null);
        AppServices.Sync = Sync;
        Sync.SetInterests(Themia.Core.Sync.SyncInterests.From(state));
        StateChanged += () => Sync.SetInterests(Themia.Core.Sync.SyncInterests.From(State));

        // [S-2607] Signing out takes the account's synced data with it.
        Accounts.SignedOut += Sync.Vault.Purge;

        // [S-546] One mail service for every Mail widget, on the shared ticker.
        _mail = new Mail.MailService(Accounts, Ticker, Sync);
        AppServices.Mail = _mail;

        // [S-2661] The feeds of every Rss widget, kept fresh in the background.
        AppServices.Feeds = new Web.FeedSync(Sync, Ticker);

        // [S-588] [S-2630] One calendar and contacts source for every widget, synced through the store.
        _office = new Office.AccountOfficeSource(Accounts, Sync, Ticker);
        Office.OfficeSources.Current = _office;
        AppServices.ShowAccounts = () => ShowConfig(new ConfigTarget.App(AppPage.Accounts));
        AppServices.EditAppLayout = ShowLayoutEditor;

        // [S-591] One task service for every widget. The task lists kept on this PC live beside the
        // state file, as the accounts do.
        AppServices.Tasks = new Tasks.TaskService(
            new Tasks.AccountTaskRemote(Accounts),
            new Tasks.LocalTaskPool(new Themia.Core.Tasks.LocalTaskStore(Themia.Core.Tasks.LocalTaskStore.PathBeside(store.Path))),
            Ticker,
            Sync);

        // [S-1420] The habit lists every Habits widget shares, beside the state file like the tasks.
        AppServices.Habits = new Habits.HabitPool(new Themia.Core.Habits.HabitListStore(Themia.Core.Habits.HabitListStore.PathBeside(store.Path)));

        // [S-1340] The notes every Notes widget shares, in notes\ beside the state file.
        AppServices.Notes = new Notes.NotePool(new Themia.Core.Notes.NoteStore(Themia.Core.Notes.NoteStore.DirectoryBeside(store.Path)));
    }

    public AppState State { get; }

    public Ticker Ticker { get; }

    /// <summary>Themia's own accounts, shared by every widget that reads from one.</summary>
    public Accounts.AccountService Accounts { get; }

    /// <summary>[S-2600] The offline sync store and what the widgets need from it.</summary>
    public Services.Sync.SyncHost Sync { get; }

    /// <summary>Raised when anything that affects rendering changed. Windows re-read <see cref="State"/>.</summary>
    public event Action? StateChanged;

    /// <summary>Raised when edit mode is entered or left.</summary>
    public event Action<bool>? EditModeChanged;

    public bool EditMode => _editMode;

    /// <summary>The live widget surfaces, one per display. Used by Settings to offer a per-display view.</summary>
    public IReadOnlyList<DesktopWindow> Surfaces => _windows;

    /// <summary>Copy / cut / paste buffer, shared by every surface so widgets move between displays.</summary>
    public WidgetClipboard Clipboard { get; } = new();

    /// <summary>[S-1580] The widgets the edit commands act on: one or many, from one view.</summary>
    public WidgetSelection Selection { get; } = new();

    /// <summary>The most recently picked selected widget, for commands about one widget; null when none.</summary>
    public string? SelectedWidgetId => Selection.Primary;

    public event Action? SelectionChanged;

    /// <summary>[S-1580] Select exactly this widget, or nothing for null.</summary>
    public void Select(string? widgetId)
        => ChangeSelection(Selection.Set(widgetId is null ? null : Find(widgetId)?.View.Id, widgetId));

    /// <summary>[S-1580] Ctrl+click: add the widget to the selection, or take it out.</summary>
    public void ToggleSelect(string widgetId)
    {
        if (Find(widgetId) is var (view, _))
        {
            ChangeSelection(Selection.Toggle(view.Id, widgetId));
        }
    }

    /// <summary>[S-1581]/[S-1582] Select exactly these widgets of one view (a lasso, Ctrl+A).</summary>
    public void SelectMany(string viewId, IEnumerable<string> ids)
        => ChangeSelection(Selection.Replace(viewId, ids));

    private void ChangeSelection(bool changed)
    {
        if (changed)
        {
            SelectionChanged?.Invoke();
        }
    }

    /// <summary>Locate a widget by id across every view. Returns null when it no longer exists.</summary>
    public (ViewState View, WidgetInstance Widget)? Find(string? widgetId)
    {
        if (string.IsNullOrEmpty(widgetId))
        {
            return null;
        }

        foreach (var view in State.Views)
        {
            foreach (var widget in view.Widgets)
            {
                if (widget.Id == widgetId)
                {
                    return (view, widget);
                }
            }
        }

        return null;
    }

    // -----------------------------------------------------------------------------------------
    // Lifetime
    // -----------------------------------------------------------------------------------------

    public void Start()
    {
        // [S-1063] Ask the Store (packaged only) while the surfaces are built.
        License.Start();

        _command.EditModeToggleRequested += () => SetEditMode(!_editMode);

        // [S-848] Listening before the first surface is built, so a taskbar or display change that
        // lands during startup (autostart at logon, before Explorer has docked the taskbar) is heard.
        _command.DisplaysChanged += RequestDisplayPass;

        // [S-1011] The session unlock replays the unlock intro on every display.
        StartMotion();

        if (!_command.Create())
        {
            // Without it the hotkey and the second-launch route are both gone, and edit mode is
            // only reachable by double-clicking a widget the user may not have.
            Log.Error(Scope, "no command window; edit mode can only be reached by double-clicking a widget");
        }

        // Show Desktop raises the desktop window above everything, including anything pinned below
        // it, and hides the rest by cloaking them. Without the controller the widgets vanish at the
        // one moment the user asked to see their desktop.
        DesktopZOrder.Start(_dispatcher);

        // [S-927] Before the first surface is built, so an autostart under a running game holds the
        // surfaces hidden instead of flashing every widget up and straight back down.
        StartGameMode();

        // [S-459] Screen time is recorded whether or not a screen-time widget is on the desktop, so
        // the history is there the day one is added. Only the app starts it — never a debug render.
        _screenTime = ScreenTimeService.Start(
            _dispatcher,
            Path.Combine(Path.GetDirectoryName(_store.Path) ?? string.Empty, Themia.Core.Storage.ScreenTimeStore.FileName));
        AppServices.ScreenTime = _screenTime;

        // [S-2609] Syncs that waited for the network go as soon as it is back.
        Sync.Start();

        // [S-1301] The request-forgery guard reads the user's "local network access" switch on
        // every check, so flipping it applies at once without a restart.
        Services.Web.NetGuard.AllowPrivate = () => State.Preferences.AllowPrivateNetwork;

        // [S-1300] [S-729] [S-2660] The feed, quote and weather widgets' last answers, from the sealed
        // store, drawn at once on a cold launch instead of "Loading". Before the windows, so the
        // first render finds them.
        Services.Web.NetworkCache.Persist(Sync.Vault);
        Services.Web.WeatherService.Persist(Sync.Vault);

        // [S-890] Counted before the surfaces exist: a start that dies building them leaves its count.
        BeginStartGuard();

        ReconcileDisplays();

        // [S-884]/[S-890] Undo starts from the desktop as built; the start counts once it is laid out.
        AfterSurfacesBuilt();

        // Seed the file on a fresh profile so the starting layout survives a restart even if the
        // user never touches anything.
        _store.SaveSoon(State);
    }

    // -----------------------------------------------------------------------------------------
    // Lifetime, continued
    // -----------------------------------------------------------------------------------------

    /// <summary>
    /// Close everything down and quit. Reachable from the edit toolbar, which is the only place it
    /// is offered: an app with no tray entry has no other chrome to hang it from.
    /// </summary>
    /// <param name="afterShutdown">
    /// Run once everything is closed and saved, just before the process exits — the uninstall's
    /// last steps ([S-1552]), which must not race the final save. Its failures are logged; the app
    /// exits regardless.
    /// </param>
    public void RequestExit(Func<Task>? afterShutdown = null)
    {
        _ = _dispatcher.TryEnqueue(async () =>
        {
            // Close the surfaces first, then flush. A widget that edits its own config — a note —
            // writes its last, unflushed keystrokes into the config on detach, so flushing before
            // tearing down persisted everything except the sentence the user had just typed.
            CloseSurfaces();

            try
            {
                await _store.FlushAsync().ConfigureAwait(true);

                // [S-603] A task ticked on a list kept on this PC a moment before exit.
                if (AppServices.Tasks is { } tasks)
                {
                    await tasks.Local.FlushAsync().ConfigureAwait(true);
                }

                // [S-1420] A habit ticked a moment before exit.
                if (AppServices.Habits is { } habits)
                {
                    await habits.FlushAsync().ConfigureAwait(true);
                }

                // [S-1348] The last sentence typed, written into the pool by the widget's detach.
                if (AppServices.Notes is { } notes)
                {
                    await notes.FlushAsync().ConfigureAwait(true);
                }

                // [S-2606] A change queued a moment before exit, and the last sync's shelves.
                await Sync.FlushAsync().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                Log.Error(Scope, "final save failed", ex);
            }

            Dispose();

            if (afterShutdown is not null)
            {
                try
                {
                    await afterShutdown().ConfigureAwait(true);
                }
                catch (Exception ex)
                {
                    Log.Error(Scope, "the last step before exit failed", ex);
                }
            }

            Microsoft.UI.Xaml.Application.Current.Exit();
        });
    }

    /// <summary>
    /// Detach every widget, so anything a widget was holding is written into the state before the
    /// state is written to disk. Safe to call twice: <see cref="Dispose"/> calls it again.
    /// </summary>
    private void CloseSurfaces()
    {
        foreach (var window in _windows)
        {
            window.CloseSurface();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Notices.Disconnect();
        StopGameMode();
        DesktopZOrder.Stop();
        Clipboard.Clear();
        AppServices.Mail = null;
        AppServices.ShowAccounts = null;
        AppServices.EditAppLayout = null;
        CloseLayoutEditor();
        _mail.Dispose();
        Office.OfficeSources.Current = null;
        _office.Dispose();
        AppServices.Feeds?.Dispose();
        AppServices.Feeds = null;
        AppServices.Tasks?.Dispose();
        AppServices.Tasks = null;
        AppServices.Habits = null;
        AppServices.Notes = null;
        AppServices.Accounts = null;
        Accounts.SignedOut -= Sync.Vault.Purge;
        Accounts.Dispose();
        AppServices.Sync = null;
        Sync.Dispose();
        License.Dispose();

        // [S-459] Accrues up to now and writes the history synchronously: the process is ending.
        AppServices.ScreenTime = null;
        _screenTime?.Dispose();
        _screenTime = null;

        CloseSurfaces();

        _windows.Clear();

        // After the surfaces: their views have let go, so this closes whatever is still lingering.
        AppServices.System = null;
        _system.Dispose();

        _config?.Close();
        Ticker.Dispose();
        _command.Dispose();
    }
}
