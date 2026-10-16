namespace Themia.Core.Widgets;

/// <summary>[S-814] What a press inside a widget landed on.</summary>
public enum PressTarget
{
    /// <summary>Nothing of the widget's own: the gap between rows, the padding, the header text.</summary>
    Background,

    /// <summary>One of the widget's items (a row, a tile, a cell): its own handlers decide the selection.</summary>
    Item,

    /// <summary>A control (a button, a box, a slider) or a press a handler already took.</summary>
    Control,
}

/// <summary>[S-816] Which of Themia's open light-dismiss popups an event closes.</summary>
public enum PopupDismissal
{
    None,

    /// <summary>Every surface's but the one pressed: that one's own light dismiss already saw the press.</summary>
    OtherSurfaces,

    /// <summary>Every surface's: the click was outside all of them, where no light dismiss looks.</summary>
    Everywhere,
}

/// <summary>
/// What one event did to the interaction session: the widget it left (its view drops its selection
/// and the shell stops watching it), the widget that stays but drops its selection, whether the
/// outside-click watcher starts or stops, and which popups close. All empty means nothing changed.
/// </summary>
public readonly record struct SessionChange<T>(T? Left, T? Reset, bool Arm, bool Disarm, PopupDismissal Popups)
    where T : class
{
    public bool IsNone => Left is null && Reset is null && !Arm && !Disarm && Popups == PopupDismissal.None;
}

/// <summary>
/// [S-813] The interaction session: from a press on one of Themia's surfaces until the user is done
/// with it. It names the widget the user is working in (the one whose selection or cursor shows),
/// and while it is open the shell watches for a press anywhere else on screen.
/// <para>
/// The keyboard lease ([S-800]) cannot be this on its own: only a widget with keys holds it, Windows
/// may refuse it the foreground, and a press on the holder keeps it. A selection is the user's
/// "I am working here", so it ends on every way of being done: a press on the widget's own background,
/// on another widget or the shell's chrome, outside every surface (the desktop, the taskbar, another
/// app, another display), another app taking the foreground, Escape, edit mode, the widget going away.
/// </para>
/// <para>
/// Pure: the shell feeds it events and carries out the <see cref="SessionChange{T}"/>.
/// </para>
/// </summary>
/// <typeparam name="T">The widget, compared by reference.</typeparam>
public sealed class WidgetSession<T>
    where T : class
{
    /// <summary>Whether the session is open (the shell is watching for a press outside).</summary>
    public bool Open { get; private set; }

    /// <summary>The widget the user is working in, or null (a press on the chrome, or it went away).</summary>
    public T? Active { get; private set; }

    /// <summary>
    /// [S-814] A press on a surface: in <paramref name="widget"/>, or on the shell's own chrome (null:
    /// the view bar, edit mode). A press on the active widget's background clears its selection unless
    /// it extends one (Ctrl / Shift) or the user is typing in it; a press on an item or a control is
    /// the widget's to decide. A press anywhere else leaves the active widget.
    /// </summary>
    public SessionChange<T> Press(T? widget, PressTarget target, bool extending, bool typing)
    {
        var arm = !Open;
        Open = true;

        T? left = null;
        if (!ReferenceEquals(Active, widget))
        {
            left = Active;
            Active = widget;
        }

        var reset = widget is not null && target == PressTarget.Background && !extending && !typing ? widget : null;
        return new SessionChange<T>(left, reset, arm, Disarm: false, PopupDismissal.OtherSurfaces);
    }

    /// <summary>
    /// [S-815]/[S-816] A press outside every one of Themia's surfaces, or another app took the
    /// foreground: the session ends and every popup closes.
    /// </summary>
    public SessionChange<T> Outside() => End(PopupDismissal.Everywhere);

    /// <summary>[S-815] Escape, edit mode beginning, or the active widget's surface closing.</summary>
    public SessionChange<T> Finish() => End(PopupDismissal.None);

    /// <summary>
    /// [S-815] The active widget's surface lost the keyboard to one of Themia's own windows (the
    /// settings): the widget drops its selection, but the watch goes on for popups still open.
    /// </summary>
    public SessionChange<T> Leave()
    {
        if (Active is not { } left)
        {
            return default;
        }

        Active = null;
        return new SessionChange<T>(left, null, Arm: false, Disarm: false, PopupDismissal.None);
    }

    /// <summary>[S-815] The widget went away: nothing is left to clear, but the watch goes on for open popups.</summary>
    public void Remove(T widget)
    {
        if (ReferenceEquals(Active, widget))
        {
            Active = null;
        }
    }

    private SessionChange<T> End(PopupDismissal popups)
    {
        if (!Open)
        {
            return default;
        }

        var left = Active;
        Active = null;
        Open = false;
        return new SessionChange<T>(left, null, Arm: false, Disarm: true, popups);
    }
}

/// <summary>[S-814]/[S-815] The pieces of the session's rules that the shell's input reduces to.</summary>
public static class SessionInput
{
    private const uint WmLButtonDown = 0x0201;
    private const uint WmRButtonDown = 0x0204;
    private const uint WmMButtonDown = 0x0207;
    private const uint WmXButtonDown = 0x020B;

    /// <summary>
    /// [S-814] What a press landed on, from the walk up from the element under the pointer: a marked
    /// item wins (a row with a button inside it is still the row), then a control or a press some
    /// handler already took, else the widget's background.
    /// </summary>
    public static PressTarget Classify(bool onItem, bool onControl, bool handled)
        => onItem ? PressTarget.Item
            : onControl || handled ? PressTarget.Control
            : PressTarget.Background;

    /// <summary>[S-815] Whether a low-level mouse message is a button going down (any button).</summary>
    public static bool IsButtonDown(uint message)
        => message is WmLButtonDown or WmRButtonDown or WmMButtonDown or WmXButtonDown;

    /// <summary>
    /// [S-815] Whether a press is outside the session: on a window of another process. A press on one
    /// of Themia's own windows (a surface, a popup, the settings) is heard by that window itself.
    /// No window under the pointer (0) is not ours either.
    /// </summary>
    public static bool IsOutside(uint pressedProcess, uint ownProcess) => pressedProcess != ownProcess;

    /// <summary>
    /// [S-815] Whether another app taking the foreground ends the session. Not when the window that
    /// had it was Start or Search (a <c>CoreWindow</c>): a press on a widget closes those ([S-938]),
    /// and the foreground going back to the previous app is that press's doing, not the user leaving.
    /// </summary>
    public static bool ForegroundEndsSession(string? previousForegroundClass)
        => previousForegroundClass != Shell.ShellClickRules.CoreWindowClass;
}
