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
    private static IShellLauncher? _launcher;

    /// <summary>[S-1348] The notes kept on this PC.</summary>
    public static INotes? Notes => Volatile.Read(ref _notes)?.Invoke();

    /// <summary>[S-1420] The habit lists kept on this PC.</summary>
    public static IHabitLists? Habits => Volatile.Read(ref _habits)?.Invoke();

    /// <summary>The one place anything is opened (a page's "Open in File Explorer").</summary>
    public static IShellLauncher Launcher
        => Volatile.Read(ref _launcher) ?? throw new InvalidOperationException("no settings host installed: the host installs it before any settings page is shown");

    /// <summary>The host's stores and launcher. Once per process.</summary>
    internal static void Install(Func<INotes?> notes, Func<IHabitLists?> habits, IShellLauncher launcher)
    {
        ArgumentNullException.ThrowIfNull(notes);
        ArgumentNullException.ThrowIfNull(habits);
        ArgumentNullException.ThrowIfNull(launcher);
        if (Interlocked.CompareExchange(ref _launcher, launcher, null) is not null)
        {
            throw new InvalidOperationException("the settings host is installed once per process");
        }

        Volatile.Write(ref _notes, notes);
        Volatile.Write(ref _habits, habits);
    }
}
