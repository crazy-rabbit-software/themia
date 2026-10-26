using Themia.Core.Diagnostics;
using Themia.Core.Model;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-1243] The seam between a power widget and the machine. The widget decides <i>whether</i> to run
/// an action (<see cref="PowerButton"/>); this only runs it. The app has two: the Win32 one, and an
/// inert one for previews ([S-1251]). Tests use a fake, so no test can shut anything down.
/// </summary>
public interface IPowerBackend
{
    /// <summary>
    /// Run <paramref name="action"/> and say how it went. Blocking (sleep returns only after the
    /// machine resumes), so <see cref="PowerButton"/> calls it on a worker thread. Never throws.
    /// </summary>
    PowerOutcome Run(PowerAction action);

    /// <summary>[S-1914] Whether this machine can hibernate right now. A cheap, read-only probe.</summary>
    bool CanHibernate { get; }
}

/// <summary>What a press on a power widget did.</summary>
public enum PowerPress
{
    /// <summary>The action ran (or was handed to the backend and has finished).</summary>
    Ran = 0,

    /// <summary>[S-1913] The widget is now armed and asks for a second click; nothing ran.</summary>
    Armed = 1,

    /// <summary>
    /// [S-1913] The press came too soon after arming to be a second, deliberate click (a double-click
    /// is one intent, not two). Still armed; nothing ran.
    /// </summary>
    Ignored = 2,

    /// <summary>[S-1914] The action cannot run on this machine; nothing ran.</summary>
    Unavailable = 3,

    /// <summary>[S-1916] The action is still running from an earlier press; nothing new ran.</summary>
    Busy = 4,
}

/// <summary>
/// [S-1913] One power widget's state: idle, armed, running, or showing why the last run failed.
/// <para>
/// Safety lives here, not in the view's timer. An armed widget is armed only for
/// <see cref="ArmWindow"/> after the press that armed it, whatever the repaint timer did; a second
/// press sooner than <see cref="MinimumDelay"/> is ignored, so an accidental double-click cannot
/// shut the PC down; a clock that went backwards reads as "too long ago". The machine is touched
/// only through <see cref="IPowerBackend"/>, one run at a time.
/// </para>
/// <para>
/// Single-threaded: call it from the UI thread. <see cref="PressAsync"/> hands the blocking call to
/// a worker and resumes on the caller's context.
/// </para>
/// </summary>
public sealed class PowerButton
{
    /// <summary>[S-1913] How long an armed widget waits for the second click: time to read it and click, no longer.</summary>
    public static readonly TimeSpan ArmWindow = TimeSpan.FromSeconds(5);

    /// <summary>[S-1913] A second press sooner than this after arming is the tail of a double-click.</summary>
    public static readonly TimeSpan MinimumDelay = TimeSpan.FromMilliseconds(400);

    private readonly IPowerBackend _backend;
    private DateTimeOffset? _armedAt;

    public PowerButton(PowerAction action, IPowerBackend backend)
    {
        ArgumentNullException.ThrowIfNull(backend);
        Action = action;
        _backend = backend;
        Refresh();
    }

    public PowerAction Action { get; }

    /// <summary>[S-1914] Whether the action can run here; read by <see cref="Refresh"/>.</summary>
    public bool Available { get; private set; }

    /// <summary>[S-1916] A run is in flight (sleep: until the machine resumes).</summary>
    public bool Running { get; private set; }

    /// <summary>[S-1915] Why the last run failed, in words, or null. Cleared by the next press.</summary>
    public string? Failure { get; private set; }

    /// <summary>
    /// [S-1914] Read the machine's capability again. Cheap; the view calls it when it attaches and
    /// when the pointer comes onto the widget, so a change made with <c>powercfg</c> shows up without
    /// polling.
    /// </summary>
    public void Refresh()
        => Available = PowerActionRules.Available(Action, Action != PowerAction.Hibernate || _backend.CanHibernate);

    /// <summary>Whether the widget is armed at <paramref name="now"/>.</summary>
    public bool IsArmed(DateTimeOffset now) => _armedAt is { } at && Live(at, now);

    /// <summary>
    /// [S-1913] How much of the arm window is left at <paramref name="now"/>, from 1 (just armed) to
    /// 0 (expired or not armed). The view's ring drains with it.
    /// </summary>
    public double ArmRemaining(DateTimeOffset now)
    {
        if (_armedAt is not { } at || !Live(at, now))
        {
            return 0;
        }

        return Math.Clamp(1 - ((now - at).TotalMilliseconds / ArmWindow.TotalMilliseconds), 0, 1);
    }

    /// <summary>
    /// [S-1913] How long the arm window still has at <paramref name="now"/>: what the ring drains over
    /// and when the view repaints the widget disarmed. Zero when it is not armed.
    /// </summary>
    public TimeSpan ArmLeft(DateTimeOffset now)
        => _armedAt is { } at && Live(at, now) ? ArmWindow - (now - at) : TimeSpan.Zero;

    /// <summary>
    /// One press at <paramref name="now"/>. Decides synchronously — the state (armed, running) is
    /// already updated when this returns its task — and, when the action runs, completes after the
    /// backend has answered.
    /// </summary>
    public async Task<PowerPress> PressAsync(DateTimeOffset now)
    {
        if (Running)
        {
            return PowerPress.Busy;
        }

        if (!Available)
        {
            Disarm();
            return PowerPress.Unavailable;
        }

        if (PowerActionRules.NeedsArming(Action))
        {
            if (_armedAt is not { } at || !Live(at, now))
            {
                _armedAt = now;
                Failure = null;
                return PowerPress.Armed;
            }

            if (now - at < MinimumDelay)
            {
                return PowerPress.Ignored;
            }
        }

        Disarm();
        Failure = null;
        Running = true;

        PowerOutcome outcome;

        try
        {
            // The continuation comes back to the caller's (UI) context: the view repaints from it.
            outcome = await Task.Run(() => _backend.Run(Action)).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            // The contract says never; if a backend breaks it, the widget still says it failed.
            Log.Error("power", $"{Action} threw", ex);
            outcome = PowerOutcome.Error(0);
        }
        finally
        {
            Running = false;
        }

        Failure = PowerActionRules.FailureMessage(Action, outcome);
        return PowerPress.Ran;
    }

    /// <summary>[S-1913] The pointer left the widget, or the widget went away.</summary>
    public void Disarm() => _armedAt = null;

    /// <summary>
    /// Disarm if the window has passed. Returns true when that changed anything, so the view only
    /// repaints when there is something to repaint.
    /// </summary>
    public bool Expire(DateTimeOffset now)
    {
        if (_armedAt is not { } at || Live(at, now))
        {
            return false;
        }

        Disarm();
        return true;
    }

    /// <summary>
    /// Inside the window. A clock that went backwards (a time-zone or NTP jump) is outside it: the
    /// safe reading of "I cannot tell how long ago" is "too long".
    /// </summary>
    private static bool Live(DateTimeOffset armedAt, DateTimeOffset now) => now >= armedAt && now - armedAt < ArmWindow;
}
