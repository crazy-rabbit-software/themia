namespace Themia.Sdk.WinUI;

/// <summary>
/// [S-6913] What a widget's settings page reads of the host's local stores: a settings page has no card
/// and so no <see cref="IWidgetHost"/>, so the host installs the same stores here once at start, as it
/// installs <see cref="DriveList"/>. Each is null while the host keeps none (a debug render).
/// </summary>
public static class SettingsStores
{
    private static Func<INotes?>? _notes;

    /// <summary>[S-1348] The notes kept on this PC.</summary>
    public static INotes? Notes => Volatile.Read(ref _notes)?.Invoke();

    /// <summary>The host's stores. Once per process.</summary>
    internal static void Install(Func<INotes?> notes)
    {
        ArgumentNullException.ThrowIfNull(notes);
        if (Interlocked.CompareExchange(ref _notes, notes, null) is not null)
        {
            throw new InvalidOperationException("the settings stores are installed once per process");
        }
    }
}
