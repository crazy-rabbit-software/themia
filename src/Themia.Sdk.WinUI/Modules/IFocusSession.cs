using Themia.Core.Widgets;

namespace Themia.Sdk.WinUI;

/// <summary>What a focus widget shows: the session, do not disturb as Windows has it, and Windows' own focus.</summary>
/// <param name="Session">Running or idle.</param>
/// <param name="Quiet">Windows' do not disturb while a session that wants it runs; pending otherwise.</param>
/// <param name="WantsQuiet">The running session asked for do not disturb.</param>
/// <param name="WindowsFocus">Windows' own focus session: on, off, or null where it cannot be read.</param>
public readonly record struct FocusSnapshot(FocusSessionState Session, ToggleReading Quiet, bool WantsQuiet, bool? WindowsFocus);

/// <summary>
/// [S-4421] The one focus session of the app as a focus widget reaches it (<see cref="IWidgetHost.Focus"/>):
/// started from any focus widget, shown by all. The host holds Windows' do not disturb while it runs
/// ([S-4423], through its own quick-setting hub, never the widget's) and follows Windows' own focus.
/// </summary>
public interface IFocusSession
{
    /// <summary>What there is to show now.</summary>
    FocusSnapshot Snapshot { get; }

    /// <summary>Follow the session; <paramref name="changed"/> runs on the UI thread. Dispose to let go. UI thread.</summary>
    IDisposable Acquire(Action changed);

    /// <summary>Windows' own focus and do not disturb read once more (a widget's pointer).</summary>
    void Refresh();

    /// <summary>[S-4421] Start a session of <paramref name="minutes"/>; one already running is replaced.</summary>
    void Start(int minutes, bool doNotDisturb);

    /// <summary>[S-4421] End it now.</summary>
    void End();
}
