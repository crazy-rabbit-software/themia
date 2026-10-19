namespace Themia.Sdk.WinUI;

/// <summary>
/// [S-6913] What a widget's properties menu lines ([S-3719]) read of the host: its local stores,
/// the accounts and the launcher. A module's <c>MenuLines</c> is handed no <see cref="IWidgetHost"/>
/// (the menu is the host's, not the card's), so the host installs the same ones here once at start,
/// as it installs <see cref="DriveList"/>. A store is null while the host keeps none (a debug render).
/// </summary>
public static class SettingsHost
{
    private static Func<INotes?>? _notes;
    private static Func<IHabitLists?>? _habits;
    private static Func<Themia.App.Services.IScreenTimeSource?>? _screenTime;
    private static IShellLauncher? _launcher;
    private static Action<object>? _ownPicker;
    private static IAppLayouts? _appLayouts;
    private static Func<Themia.App.Services.Office.IOfficeSource?>? _office;
    private static Func<Themia.App.Services.Tasks.ITaskSource?>? _tasks;
    private static Func<FeatureId, IReadOnlyList<Themia.App.Services.Accounts.AccountInfo>>? _accounts;

    private const string NotInstalled = "no settings host installed: the host installs it at start, before any menu is built";

    /// <summary>[S-1348] The notes kept on this PC.</summary>
    public static INotes? Notes => Volatile.Read(ref _notes)?.Invoke();

    /// <summary>[S-1420] The habit lists kept on this PC.</summary>
    public static IHabitLists? Habits => Volatile.Read(ref _habits)?.Invoke();

    /// <summary>[S-3322] The screen-time history, whose hidden apps and limits every screen-time widget's menu shares.</summary>
    public static Themia.App.Services.IScreenTimeSource? ScreenTime => Volatile.Read(ref _screenTime)?.Invoke();

    /// <summary>[S-561] The accounts, calendars and contacts the host's office source offers: what the account widgets' menus list. Null before the host has started.</summary>
    public static Themia.App.Services.Office.IOfficeSource? Office => Volatile.Read(ref _office)?.Invoke();

    /// <summary>[S-590] The task accounts and lists the host's task service offers: what the task widgets' menus list. Null before the host has started.</summary>
    public static Themia.App.Services.Tasks.ITaskSource? Tasks => Volatile.Read(ref _tasks)?.Invoke();

    /// <summary>[S-529] The signed-in accounts offering <paramref name="feature"/>, in the order they were added: what an account widget's menu lists. Empty before the host has started.</summary>
    public static IReadOnlyList<Themia.App.Services.Accounts.AccountInfo> AccountsWith(FeatureId feature) => Volatile.Read(ref _accounts)?.Invoke(feature) ?? [];

    /// <summary>The one place anything is opened (a menu's "Open in File Explorer").</summary>
    public static IShellLauncher Launcher
        => Volatile.Read(ref _launcher) ?? throw new InvalidOperationException(NotInstalled);

    /// <summary>[S-1411] The app-layout editor Shortcuts' menu opens on a new layout item.</summary>
    public static IAppLayouts AppLayouts
        => Volatile.Read(ref _appLayouts) ?? throw new InvalidOperationException(NotInstalled);

    /// <summary>
    /// A Windows file or folder picker a menu line opens is owned by the window the click that opened
    /// it came from (WinRT pickers need an owner window to show at all).
    /// </summary>
    public static void OwnPicker(object picker)
        => (Volatile.Read(ref _ownPicker) ?? throw new InvalidOperationException(NotInstalled))(picker);

    /// <summary>The host's stores, launcher, window and layout editor. Once per process.</summary>
    internal static void Install(Func<INotes?> notes, Func<IHabitLists?> habits, Func<Themia.App.Services.IScreenTimeSource?> screenTime, IShellLauncher launcher, Action<object> ownPicker, IAppLayouts appLayouts,
        Func<Themia.App.Services.Office.IOfficeSource?> office, Func<Themia.App.Services.Tasks.ITaskSource?> tasks,
        Func<FeatureId, IReadOnlyList<Themia.App.Services.Accounts.AccountInfo>> accounts)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(office);
        ArgumentNullException.ThrowIfNull(tasks);
        ArgumentNullException.ThrowIfNull(notes);
        ArgumentNullException.ThrowIfNull(habits);
        ArgumentNullException.ThrowIfNull(screenTime);
        ArgumentNullException.ThrowIfNull(launcher);
        ArgumentNullException.ThrowIfNull(ownPicker);
        ArgumentNullException.ThrowIfNull(appLayouts);
        if (Interlocked.CompareExchange(ref _launcher, launcher, null) is not null)
        {
            throw new InvalidOperationException("the settings host is installed once per process");
        }

        Volatile.Write(ref _notes, notes);
        Volatile.Write(ref _habits, habits);
        Volatile.Write(ref _screenTime, screenTime);
        Volatile.Write(ref _ownPicker, ownPicker);
        Volatile.Write(ref _appLayouts, appLayouts);
        Volatile.Write(ref _office, office);
        Volatile.Write(ref _tasks, tasks);
        Volatile.Write(ref _accounts, accounts);
    }
}
