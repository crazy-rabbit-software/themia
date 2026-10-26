namespace Themia.Core.Shell;

/// <summary>The window-manager events the z-order controller listens to ([S-34a]).</summary>
public enum ShellEvent
{
    /// <summary><c>EVENT_SYSTEM_FOREGROUND</c>: another window took the foreground.</summary>
    Foreground = 0,

    /// <summary><c>EVENT_SYSTEM_MINIMIZESTART</c> / <c>MINIMIZEEND</c>: a window was minimised or restored.</summary>
    Minimize = 1,

    /// <summary><c>EVENT_OBJECT_REORDER</c>: a window's children changed z-order.</summary>
    Reorder = 2,

    /// <summary><c>EVENT_OBJECT_SHOW</c>: an object was shown.</summary>
    Show = 3,

    /// <summary><c>EVENT_OBJECT_CLOAKED</c> / <c>UNCLOAKED</c>: DWM stopped or started compositing a window.</summary>
    Cloak = 4,
}

/// <summary>Which window an event names.</summary>
public enum EventSource
{
    /// <summary>The desktop window itself (<c>GetDesktopWindow</c>), the parent of every top-level window.</summary>
    DesktopWindow = 0,

    /// <summary>A top-level window.</summary>
    TopLevelWindow = 1,

    /// <summary>A child window, or no window at all.</summary>
    Other = 2,
}

/// <summary>What one event asks of the controller.</summary>
public enum ZTrigger
{
    /// <summary>Says nothing about where the surfaces belong.</summary>
    Ignore = 0,

    /// <summary>Queue a check.</summary>
    Check = 1,

    /// <summary>
    /// Queue a check and give the repairs a fresh allowance ([S-34d]): the user did something, so a
    /// repair after it is a new answer, not another round of a fight.
    /// </summary>
    CheckAfresh = 2,
}

/// <summary>
/// [S-34a] Which window-manager events queue a z-order check. Pure, so the filter that decides what
/// wakes the controller is tested without a window manager.
/// </summary>
public static class ZOrderEvents
{
    /// <param name="kind">The event.</param>
    /// <param name="wholeWindow">
    /// Whether it is about the window itself (<c>OBJID_WINDOW</c>, <c>CHILDID_SELF</c>) rather than a
    /// part of one: a caret, a cursor, a list item.
    /// </param>
    /// <param name="source">Which window the event names.</param>
    public static ZTrigger Classify(ShellEvent kind, bool wholeWindow, EventSource source) => kind switch
    {
        // Always a top-level window, and always the user's own doing: the one event that refills.
        ShellEvent.Foreground => ZTrigger.CheckAfresh,

        // A top-level window's z-order change is announced by its parent, the desktop window. The
        // same event from a list view or a toolbar reordering its items says nothing about windows.
        ShellEvent.Reorder => source == EventSource.DesktopWindow ? ZTrigger.Check : ZTrigger.Ignore,

        // A window going away, coming back or appearing changes whether anything is on screen
        // ([S-33a], [S-33e]); a child window, a caret or a cursor doing it does not.
        ShellEvent.Minimize or ShellEvent.Show or ShellEvent.Cloak
            => wholeWindow && source == EventSource.TopLevelWindow ? ZTrigger.Check : ZTrigger.Ignore,

        _ => ZTrigger.Ignore,
    };
}

/// <summary>Whether a repair may write.</summary>
public enum RepairGrant
{
    /// <summary>Write.</summary>
    Granted = 0,

    /// <summary>The allowance is spent: do not write, and say so once.</summary>
    Refused = 1,

    /// <summary>Still spent, already said: do not write.</summary>
    RefusedAgain = 2,
}

/// <summary>
/// [S-34d] A bound on z-order repairs between two user actions.
/// <para>
/// A repair is a write that the controller then hears back as a window-manager event, and another
/// program that also insists on a place (a second desktop-widget app anchoring to the desktop, the
/// shell re-raising its own layer) answers each of our writes with one of its own. Each side is
/// right by its own rule, so nothing would ever end the exchange: the controller would burn a core
/// one event at a time. Counting the writes ends it without a clock. The allowance is generous for
/// anything a real transition needs (the lift, a few late desktop raises, an uncloak per surface) and
/// is given back on the next foreground change, display change, unlock or resume.
/// </para>
/// </summary>
public sealed class ZOrderRepairs
{
    /// <summary>Repairs allowed between two user actions.</summary>
    public const int Allowance = 16;

    private int _left = Allowance;
    private bool _reported;

    /// <summary>Repairs left before the next refill.</summary>
    public int Left => _left;

    /// <summary>A user action: the allowance is whole again.</summary>
    public void Refill()
    {
        _left = Allowance;
        _reported = false;
    }

    /// <summary>Ask to write one repair.</summary>
    public RepairGrant TrySpend()
    {
        if (_left > 0)
        {
            _left--;
            return RepairGrant.Granted;
        }

        if (_reported)
        {
            return RepairGrant.RefusedAgain;
        }

        _reported = true;
        return RepairGrant.Refused;
    }
}
