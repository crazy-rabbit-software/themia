namespace Themia.Sdk.WinUI;

/// <summary>
/// [S-6913] What a widget's settings page reads of the host: its local stores and the launcher. A
/// settings page has no card and so no <see cref="IWidgetHost"/>, so the host installs the same ones
/// here once at start, as it installs <see cref="DriveList"/>. A store is null while the host keeps
/// none (a debug render).
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

    /// <summary>[S-1348] The notes kept on this PC.</summary>
    public static INotes? Notes => Volatile.Read(ref _notes)?.Invoke();

    /// <summary>[S-1420] The habit lists kept on this PC.</summary>
    public static IHabitLists? Habits => Volatile.Read(ref _habits)?.Invoke();

    /// <summary>[S-3322] The screen-time history, whose hidden apps and limits every screen-time page shares.</summary>
    public static Themia.App.Services.IScreenTimeSource? ScreenTime => Volatile.Read(ref _screenTime)?.Invoke();

    /// <summary>The one place anything is opened (a page's "Open in File Explorer").</summary>
    /// <summary>[S-561] The accounts, calendars and contacts the host's office source offers: what the account widgets' pages list. Null before the host has started.</summary>
    public static Themia.App.Services.Office.IOfficeSource? Office => Volatile.Read(ref _office)?.Invoke();

    public static IShellLauncher Launcher
        => Volatile.Read(ref _launcher) ?? throw new InvalidOperationException("no settings host installed: the host installs it before any settings page is shown");

    /// <summary>[S-1411] The app-layout editor a Shortcuts page opens on a layout item.</summary>
    public static IAppLayouts AppLayouts
        => Volatile.Read(ref _appLayouts) ?? throw new InvalidOperationException("no settings host installed: the host installs it before any settings page is shown");

    /// <summary>
    /// A Windows file or folder picker a page opens is owned by the settings window, which the click that
    /// opened it came from (WinRT pickers need an owner window to show at all).
    /// </summary>
    public static void OwnPicker(object picker)
        => (Volatile.Read(ref _ownPicker) ?? throw new InvalidOperationException("no settings host installed: the host installs it before any settings page is shown"))(picker);

    /// <summary>The host's stores, launcher, window and layout editor. Once per process.</summary>
    internal static void Install(Func<INotes?> notes, Func<IHabitLists?> habits, Func<Themia.App.Services.IScreenTimeSource?> screenTime, IShellLauncher launcher, Action<object> ownPicker, IAppLayouts appLayouts,
        Func<Themia.App.Services.Office.IOfficeSource?> office)
    {
        ArgumentNullException.ThrowIfNull(office);
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
    }
}
