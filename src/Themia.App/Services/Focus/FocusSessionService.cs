using Microsoft.UI.Dispatching;
using Themia.App.Services.Toggles;
using Themia.Core.Diagnostics;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;

namespace Themia.App.Services.Focus;

/// <summary>
/// [S-4421] The one focus session of the app, shared by every focus widget as Windows has one focus
/// session: started from any of them, shown by all. It is Themia's own countdown, because starting a
/// Windows focus session is closed to Themia ([S-4424]); it does what Windows' Focus does with it —
/// holds Windows' do not disturb while it runs ([S-4423], Karpo's <c>FocusQuiet</c>), through the same
/// source as the do not disturb toggle ([S-3913]), so the toggle shows what the session did.
/// <para>
/// UI thread only (the first widget's): the end is a one-shot timer at the session's end, not a poll;
/// widgets tick their own seconds only while it runs ([S-4422]). In memory: Themia quitting ends the
/// session and hands do not disturb back on the way out.
/// </para>
/// </summary>
internal static class FocusSessionService
{
    private const string Scope = "focus";

    private static readonly List<FocusLease> Leases = [];

    private static readonly FocusQuietHold Hold = new();
    private static FocusSessionState _session = FocusSessionState.Idle;
    private static bool _wantsQuiet;
    private static ToggleLease? _quiet;
    private static WindowsFocusWatch? _windows;
    private static DispatcherQueue? _queue;
    private static DispatcherQueueTimer? _end;
    private static bool _exitHooked;

    public static FocusSnapshot Snapshot => new(
        _session,
        _quiet?.Reading ?? ToggleReading.Pending,
        _session.Running && _wantsQuiet,
        _windows?.Active);

    /// <summary>Follow the session; UI thread.</summary>
    public static FocusLease Acquire(Action changed)
    {
        var queue = DispatcherQueue.GetForCurrentThread()
            ?? throw new InvalidOperationException("acquire the focus session from a thread with a dispatcher");
        _queue ??= queue;

        var lease = new FocusLease(changed);
        Leases.Add(lease);
        if (_windows is null)
        {
            _windows = new WindowsFocusWatch(() => Post(Raise));
            _windows.Start();
        }

        return lease;
    }

    internal static void Release(FocusLease lease)
    {
        if (!Leases.Remove(lease) || Leases.Count > 0)
        {
            return;
        }

        // The last widget went (a view switch, say): the session runs on to its end, do not disturb
        // held and handed back as ever; only Windows' own focus stops being followed.
        _windows?.Dispose();
        _windows = null;
    }

    /// <summary>Windows' own focus, which announces itself, read once more (a widget's pointer).</summary>
    public static void Refresh()
    {
        _windows?.Read();
        _quiet?.Refresh();
    }

    /// <summary>[S-4421] Start a session of <paramref name="minutes"/>; one already running is replaced.</summary>
    public static void Start(int minutes, bool doNotDisturb)
    {
        var now = DateTimeOffset.Now;
        _session = FocusSessionRules.Start(now, minutes);
        _wantsQuiet = doNotDisturb;

        if (doNotDisturb)
        {
            _quiet ??= ToggleHub.Acquire(QuickToggle.DoNotDisturb, OnQuiet);
        }

        HookExit();
        ArmEnd(_session.Remaining(now));
        Review();
        Raise();
    }

    /// <summary>[S-4421] End it now (or because its time came).</summary>
    public static void End()
    {
        _session = FocusSessionState.Idle;
        _end?.Stop();
        Review();
        Raise();
    }

    /// <summary>The session or do not disturb moved: what the hold asks of Windows.</summary>
    private static void Review()
    {
        var reading = _quiet?.Reading ?? ToggleReading.Pending;
        var command = Hold.Update(_session.Running && _wantsQuiet, reading);

        switch (command)
        {
            case QuietCommand.TurnOn:
                _ = SwitchAsync(true);
                break;

            case QuietCommand.TurnOff:
                _ = SwitchAsync(false);
                break;
        }

        // Nothing holds it any more: let go of the source, unless a switch is on its way.
        if (!Hold.Holding && command == QuietCommand.None && !_session.Running)
        {
            _quiet?.Dispose();
            _quiet = null;
        }
    }

    private static async Task SwitchAsync(bool on)
    {
        var lease = _quiet;
        if (lease is null)
        {
            return;
        }

        bool done;
        try
        {
            done = await lease.SetAsync(on).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            Log.Error(Scope, $"do not disturb could not be switched {(on ? "on" : "off")}", ex);
            done = false;
        }

        if (on)
        {
            Hold.Settled(done);
            if (!done)
            {
                Log.Warn(Scope, "Windows did not turn do not disturb on for the focus session; it runs without it");
            }
        }
        else if (!_session.Running && ReferenceEquals(lease, _quiet))
        {
            _quiet?.Dispose();
            _quiet = null;
        }

        Raise();
    }

    private static void OnQuiet()
    {
        Review();
        Raise();
    }

    /// <summary>The session's end, once, at its time: a one-shot timer, not a poll.</summary>
    private static void ArmEnd(TimeSpan after)
    {
        if (_queue is null)
        {
            return;
        }

        if (_end is null)
        {
            _end = _queue.CreateTimer();
            _end.IsRepeating = false;
            _end.Tick += (_, _) =>
            {
                try
                {
                    if (_session.Over(DateTimeOffset.Now))
                    {
                        End();
                    }
                    else if (_session.Running)
                    {
                        // Woken a little early (the clock moved): once more at the new end.
                        ArmEnd(_session.Remaining(DateTimeOffset.Now));
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(Scope, "the focus session could not end", ex);
                }
            };
        }

        _end.Stop();
        _end.Interval = after < TimeSpan.Zero ? TimeSpan.Zero : after;
        _end.Start();
    }

    /// <summary>
    /// Themia quitting during a session ends it: do not disturb the session turned on is handed back,
    /// as Windows' Focus does when it ends. A bounded wait on the way out, the one place this waits.
    /// </summary>
    private static void HookExit()
    {
        if (_exitHooked)
        {
            return;
        }

        _exitHooked = true;
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            try
            {
                if (Hold.TurnedOn && _quiet is { } lease)
                {
                    _ = lease.SetAsync(false).Wait(TimeSpan.FromSeconds(2));
                }
            }
            catch (Exception ex)
            {
                Log.Warn(Scope, "do not disturb could not be handed back as Themia quit", ex);
            }
        };
    }

    private static void Post(Action action)
    {
        if (_queue is null || !_queue.TryEnqueue(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Log.Error(Scope, "a focus change could not be passed on", ex);
            }
        }))
        {
            Log.Debug(Scope, "a focus change arrived with no thread to take it");
        }
    }

    private static void Raise()
    {
        foreach (var lease in Leases.ToArray())
        {
            lease.Notify();
        }
    }
}

/// <summary>One widget's hold on the focus session.</summary>
internal sealed class FocusLease : IDisposable
{
    private readonly Action _changed;
    private bool _disposed;

    internal FocusLease(Action changed) => _changed = changed;

    internal void Notify()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            _changed();
        }
        catch (Exception ex)
        {
            Log.Error("focus", "a focus widget could not take a change", ex);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        FocusSessionService.Release(this);
    }
}
